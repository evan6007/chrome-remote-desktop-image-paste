// Image clipboard bridge with encrypted HTTPS transport and an explicit endpoint-pairing channel.
// Windows desktop helper. Does not move the mouse, activate other windows, or record keystrokes.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Native {
    [DllImport("user32.dll")] internal static extern bool OpenClipboard(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool CloseClipboard();
    [DllImport("user32.dll")] internal static extern bool EmptyClipboard();
    [DllImport("user32.dll")] internal static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll")] internal static extern IntPtr SetClipboardData(uint format, IntPtr data);
    [DllImport("user32.dll")] internal static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern uint RegisterClipboardFormat(string name);
    [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] internal static extern bool AddClipboardFormatListener(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool RemoveClipboardFormatListener(IntPtr window);
    [DllImport("kernel32.dll")] internal static extern IntPtr GlobalAlloc(uint flags, UIntPtr size);
    [DllImport("kernel32.dll")] internal static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll")] internal static extern bool GlobalUnlock(IntPtr handle);
    [DllImport("kernel32.dll")] internal static extern UIntPtr GlobalSize(IntPtr handle);
    [DllImport("kernel32.dll")] internal static extern IntPtr GlobalFree(IntPtr handle);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll", SetLastError=true)] internal static extern bool PostMessage(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    internal delegate IntPtr Hook(int code, IntPtr message, IntPtr info);
    [DllImport("user32.dll")] internal static extern IntPtr SetWindowsHookEx(int id, Hook hook, IntPtr module, uint thread);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr info);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] internal static extern IntPtr CreateWindowStation(string name,uint flags,uint access,IntPtr security);
    [DllImport("user32.dll", SetLastError=true)] internal static extern bool SetProcessWindowStation(IntPtr station);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] internal static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr devmode,uint flags,uint access,IntPtr security);
    [DllImport("user32.dll", SetLastError=true)] internal static extern bool SetThreadDesktop(IntPtr desktop);
}

internal static class Wire {
    internal const int ChunkBytes = 144 * 1024;
    internal const int MaxBytes = 24 * 1024 * 1024;
    internal const int MaxPixels = 36 * 1000 * 1000;
    internal static string Prefix { get { return "REMOTEIMAGE2:" + SecureImage.ControlId + ":"; } }
    internal static string Hash(byte[] bytes) {
        using (SHA256 h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    internal static string Packet(byte[] bytes, string id, int index) {
        int count = (bytes.Length + ChunkBytes - 1) / ChunkBytes;
        int offset = index * ChunkBytes;
        return Prefix + "DATA:" + id + ":" + index + ":" + count + ":" + bytes.Length + ":" + Hash(bytes) + ":" +
            Convert.ToBase64String(bytes, offset, Math.Min(ChunkBytes, bytes.Length - offset));
    }
    internal static string Ack(string id, int index) { return Prefix + "ACK:" + id + ":" + index; }
    internal static string Ready(string id) { return Prefix + "READY:" + id; }
    internal static string[] Parse(string text) {
        if (text == null || !text.StartsWith(Prefix, StringComparison.Ordinal) || text.Length > 210000) return null;
        return text.Substring(Prefix.Length).Split(':');
    }
    internal static void ValidatePng(byte[] bytes) {
        byte[] sig = {137,80,78,71,13,10,26,10};
        if (bytes.Length < 33 || bytes.Length > MaxBytes) throw new InvalidDataException("Image size exceeds limit");
        for (int i=0;i<8;i++) if(bytes[i]!=sig[i]) throw new InvalidDataException("PNG signature required");
        long w = ReadBig(bytes, 16), h = ReadBig(bytes, 20);
        if(w<1 || h<1 || w>32768 || h>32768 || w*h>MaxPixels) throw new InvalidDataException("Image dimensions exceed limit");
        using (MemoryStream s = new MemoryStream(bytes)) using (Image img=Image.FromStream(s,true,true)) {
            if(img.Width != w || img.Height != h) throw new InvalidDataException("Invalid PNG dimensions");
        }
    }
    internal static long ReadBig(byte[] b,int p) { return ((long)b[p]<<24) | ((long)b[p+1]<<16) | ((long)b[p+2]<<8) | b[p+3]; }
}

internal sealed class Assembler {
    internal string Id, Digest;
    internal int Count, Length, Next;
    internal MemoryStream Buffer = new MemoryStream();
    internal DateTime Updated = DateTime.UtcNow;
    internal byte[] Accept(string[] p) {
        if(p.Length!=7 || p[0]!="DATA") throw new InvalidDataException("Bad packet");
        Guid parsed;
        int n,c,l;
        if(!Guid.TryParseExact(p[1],"N",out parsed) || !int.TryParse(p[2],out n) || !int.TryParse(p[3],out c) ||
           !int.TryParse(p[4],out l) || l<1 || l>Wire.MaxBytes || c!=(l+Wire.ChunkBytes-1)/Wire.ChunkBytes || n<0 || n>=c || p[5].Length!=64)
            throw new InvalidDataException("Packet bounds rejected");
        byte[] chunk=Convert.FromBase64String(p[6]);
        if(chunk.Length!=Math.Min(Wire.ChunkBytes,l-n*Wire.ChunkBytes)) throw new InvalidDataException("Chunk length mismatch");
        if(Id==null) { if(n!=0) throw new InvalidDataException("First chunk missing"); Id=p[1];Digest=p[5];Count=c;Length=l; }
        if(Id!=p[1] || Digest!=p[5] || Count!=c || Length!=l || n>Next) throw new InvalidDataException("Packet order mismatch");
        if(n<Next) { Updated=DateTime.UtcNow; return null; }
        Buffer.Write(chunk,0,chunk.Length); Next++; Updated=DateTime.UtcNow;
        if(Next!=Count) return null;
        byte[] result=Buffer.ToArray();
        if(result.Length!=Length || Wire.Hash(result)!=Digest) throw new InvalidDataException("Image checksum mismatch");
        Wire.ValidatePng(result);
        return result;
    }
}

internal static class Clip {
    internal static bool ReadBusy;
    internal static readonly uint Png=Native.RegisterClipboardFormat("PNG");
    internal static bool HasImage { get { return Native.IsClipboardFormatAvailable(Png) || Native.IsClipboardFormatAvailable(8) || Native.IsClipboardFormatAvailable(2); } }
    internal static bool HasText { get { return Native.IsClipboardFormatAvailable(13); } }
    internal static string OwnerName() {
        uint pid; Native.GetWindowThreadProcessId(Native.GetClipboardOwner(),out pid);
        try { return Process.GetProcessById((int)pid).ProcessName; } catch { return "unknown"; }
    }
    internal static bool OwnedByThisProcess() { uint pid;Native.GetWindowThreadProcessId(Native.GetClipboardOwner(),out pid);return pid==(uint)Process.GetCurrentProcess().Id; }
    internal static bool IsEmptyText(IntPtr owner) {
        if(HasImage || !HasText || !Native.OpenClipboard(owner)) return false;
        try {
            IntPtr h=Native.GetClipboardData(13); if(h==IntPtr.Zero || Native.GlobalSize(h).ToUInt64()<2) return false;
            IntPtr p=Native.GlobalLock(h);if(p==IntPtr.Zero) return false;
            try { return Marshal.ReadInt16(p)==0; } finally { Native.GlobalUnlock(h); }
        } finally { Native.CloseClipboard(); }
    }
    internal static bool ShouldRestore(bool armed,bool remoteOwner,bool emptyText,bool hasImage) {
        return armed && remoteOwner && emptyText && !hasImage;
    }
    internal static byte[] Read(uint format,int limit) {
        IntPtr h=Native.GetClipboardData(format); if(h==IntPtr.Zero) return null;
        ulong length=Native.GlobalSize(h).ToUInt64(); if(length==0 || length>(ulong)limit) return null;
        IntPtr p=Native.GlobalLock(h); if(p==IntPtr.Zero) return null;
        try { byte[] b=new byte[(int)length]; Marshal.Copy(p,b,0,b.Length); return b; }
        finally { Native.GlobalUnlock(h); }
    }
    // Only read protocol text. Ordinary copied text is never collected or logged.
    internal static string ReadProtocol(IntPtr owner) {
        ReadBusy=false;
        if(!HasText) return null;
        if(!Native.OpenClipboard(owner)) { ReadBusy=true;return null; }
        try {
            IntPtr h=Native.GetClipboardData(13); if(h==IntPtr.Zero) return null;
            ulong size=Native.GlobalSize(h).ToUInt64();
            if(size<(ulong)(Wire.Prefix.Length*2) || size>420004) return null;
            IntPtr p=Native.GlobalLock(h); if(p==IntPtr.Zero) return null;
            try {
                if(Marshal.PtrToStringUni(p,Wire.Prefix.Length)!=Wire.Prefix) return null;
                return Marshal.PtrToStringUni(p,(int)size/2).TrimEnd('\0');
            } finally { Native.GlobalUnlock(h); }
        } finally { Native.CloseClipboard(); }
    }
    internal static byte[] ReadPng(IntPtr owner) {
        byte[] raw=null;
        if(Native.IsClipboardFormatAvailable(Png) && Native.OpenClipboard(owner)) {
            try { raw=Read(Png,Wire.MaxBytes); } finally { Native.CloseClipboard(); }
        }
        if(raw!=null) { Wire.ValidatePng(raw); return raw; }
        if(!Clipboard.ContainsImage()) return null;
        using(Image img=Clipboard.GetImage()) {
            if(img==null) return null;
            if((long)img.Width*img.Height>Wire.MaxPixels) throw new InvalidDataException("Image too large");
            using(MemoryStream s=new MemoryStream()) { img.Save(s,ImageFormat.Png); raw=s.ToArray(); }
        }
        Wire.ValidatePng(raw); return raw;
    }
    internal static byte[] Dib(byte[] png) {
        using(MemoryStream input=new MemoryStream(png)) using(Image image=Image.FromStream(input))
        using(Bitmap bitmap=new Bitmap(image.Width,image.Height,PixelFormat.Format24bppRgb)) {
            using(Graphics g=Graphics.FromImage(bitmap)) { g.Clear(Color.White); g.DrawImageUnscaled(image,0,0); }
            using(MemoryStream output=new MemoryStream()) {
                bitmap.Save(output,ImageFormat.Bmp); byte[] bmp=output.ToArray(), dib=new byte[bmp.Length-14];
                Buffer.BlockCopy(bmp,14,dib,0,dib.Length); return dib;
            }
        }
    }
    private static IntPtr Allocate(byte[] data) {
        IntPtr h=Native.GlobalAlloc(2,(UIntPtr)data.Length); if(h==IntPtr.Zero) throw new OutOfMemoryException();
        IntPtr p=Native.GlobalLock(h); if(p==IntPtr.Zero) { Native.GlobalFree(h); throw new OutOfMemoryException(); }
        try { Marshal.Copy(data,0,p,data.Length); } finally { Native.GlobalUnlock(h); }
        return h;
    }
    internal static bool Write(IntPtr owner,string text,byte[] png,byte[] dib) {
        List<uint> formats=new List<uint>(); List<IntPtr> blocks=new List<IntPtr>();
        try {
            // Images come first so local image-aware applications prefer them.
            if(png!=null) { formats.Add(Png); blocks.Add(Allocate(png)); formats.Add(8); blocks.Add(Allocate(dib)); }
            if(text!=null) { formats.Add(13); blocks.Add(Allocate(Encoding.Unicode.GetBytes(text+"\0"))); }
            if(!Native.OpenClipboard(owner)) return false;
            try {
                if(!Native.EmptyClipboard()) return false;
                for(int i=0;i<formats.Count;i++) {
                    if(Native.SetClipboardData(formats[i],blocks[i])==IntPtr.Zero) throw new InvalidOperationException("Clipboard write failed");
                    blocks[i]=IntPtr.Zero;
                }
                return true;
            } finally { Native.CloseClipboard(); }
        } finally { foreach(IntPtr h in blocks) if(h!=IntPtr.Zero) Native.GlobalFree(h); }
    }
}

internal sealed class ProgressToast : Form {
    internal event Action<int,string,string,bool,bool> Updated;
    readonly System.Windows.Forms.Timer expiry=new System.Windows.Forms.Timer();
    int percent; string heading="",detail="";bool failed;
    internal ProgressToast() {
        FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;DoubleBuffered=true;
        BackColor=Color.FromArgb(26,30,38);Size=new Size(370,108);
        expiry.Tick+=delegate { expiry.Stop();Hide(); };
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams {
        get { CreateParams p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x20;return p; }
    }
    internal void Report(int value,string title,string subtitle,bool done,bool error) {
        percent=Math.Max(0,Math.Min(100,value));heading=title;detail=subtitle;failed=error;
        if(Updated!=null) Updated(percent,title,subtitle,done,error);
        Rectangle area=Screen.PrimaryScreen.WorkingArea;Location=new Point(area.Right-Width-18,area.Bottom-Height-18);
        expiry.Stop();Invalidate();if(!Visible) Show();
        if(done || error) { expiry.Interval=error?8000:5500;expiry.Start(); }
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);Graphics g=e.Graphics;
        using(Pen border=new Pen(Color.FromArgb(67,75,89))) g.DrawRectangle(border,0,0,Width-1,Height-1);
        using(Font title=new Font("Microsoft JhengHei UI",11,FontStyle.Bold))
        using(Font small=new Font("Microsoft JhengHei UI",9))
        using(SolidBrush light=new SolidBrush(Color.FromArgb(236,239,245)))
        using(SolidBrush muted=new SolidBrush(Color.FromArgb(177,188,205)))
        using(SolidBrush track=new SolidBrush(Color.FromArgb(55,63,77)))
        using(SolidBrush fill=new SolidBrush(failed?Color.FromArgb(255,170,114):Color.FromArgb(92,214,177))) {
            g.DrawString(heading,title,light,16,13);
            string value=failed?"!":percent+"%";SizeF size=g.MeasureString(value,title);g.DrawString(value,title,fill,Width-16-size.Width,13);
            g.FillRectangle(track,17,48,Width-34,7);g.FillRectangle(fill,17,48,(Width-34)*percent/100,7);
            g.DrawString(detail,small,muted,16,69);
        }
    }
    protected override void Dispose(bool disposing) { if(disposing) expiry.Dispose();base.Dispose(disposing); }
}

internal sealed class ProgressMeter : Control {
    internal int Value;
    internal bool Failed;
    internal ProgressMeter() { DoubleBuffered=true; }
    protected override void OnPaint(PaintEventArgs e) {
        using(SolidBrush track=new SolidBrush(Color.FromArgb(54,63,77)))
        using(SolidBrush fill=new SolidBrush(Failed?Color.FromArgb(244,158,107):Color.FromArgb(92,214,177))) {
            e.Graphics.FillRectangle(track,ClientRectangle);
            e.Graphics.FillRectangle(fill,0,0,Width*Math.Max(0,Math.Min(100,Value))/100,Height);
        }
    }
}

internal sealed class Dashboard : Form {
    readonly Label state=new Label(),detail=new Label(),signals=new Label(),lastResult=new Label(),percent=new Label();
    readonly ProgressMeter meter=new ProgressMeter();
    readonly Button pause=new Button();
    readonly bool receiver;
    bool disposeRequested;
    internal bool HasConfirmedTransfer;
    internal Dashboard(bool isReceiver,Action togglePause,Action quit,Action reconnect=null) {
        receiver=isReceiver;
        Text="遠端圖片橋接 v0.2.0 — "+(receiver?"接收端":"本機傳送端");
        BackColor=Color.FromArgb(26,30,38);ForeColor=Color.FromArgb(236,239,245);
        Font=new Font("Microsoft JhengHei UI",10);ClientSize=new Size(508,416);
        FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;ShowInTaskbar=true;
        StartPosition=FormStartPosition.Manual;
        Rectangle area=Screen.PrimaryScreen.WorkingArea;Location=new Point(area.Right-Width-24,area.Top+55);
        AddLabel("圖片剪貼簿橋接",22,20,455,33,17,true,ForeColor);
        AddLabel(Environment.MachineName+"  ·  "+(receiver?"接收傳送端的圖片":"傳送圖片到遠端")+"  ·  v0.2.0",24,61,455,25,9,false,Color.FromArgb(166,179,199));
        state.SetBounds(24,106,365,29);state.Font=new Font(Font.FontFamily,12,FontStyle.Bold);state.Text="程式已啟動，等待圖片";Controls.Add(state);
        percent.SetBounds(411,106,72,29);percent.TextAlign=ContentAlignment.TopRight;percent.ForeColor=Color.FromArgb(92,214,177);percent.Font=new Font(Font.FontFamily,12,FontStyle.Bold);percent.Text="0%";Controls.Add(percent);
        meter.SetBounds(24,148,460,9);Controls.Add(meter);
        detail.SetBounds(24,174,460,32);detail.Font=new Font(Font.FontFamily,9);detail.ForeColor=Color.FromArgb(181,194,213);detail.Text="傳送端截圖即自動傳送，不必點 Google Remote。";Controls.Add(detail);
        signals.SetBounds(24,216,460,48);signals.Font=new Font(Font.FontFamily,9);signals.ForeColor=Color.FromArgb(166,179,199);Controls.Add(signals);
        lastResult.SetBounds(24,273,460,32);lastResult.Font=new Font(Font.FontFamily,9);lastResult.ForeColor=Color.FromArgb(181,194,213);lastResult.Text="本次啟動尚未確認圖片傳輸。";Controls.Add(lastResult);
        AddLabel("關閉視窗會收至背景；再開工具就會顯示此視窗。",24,321,460,25,9,false,Color.FromArgb(149,162,182));
        ConfigureButton(pause,"暫停",24,359,102);pause.Click+=delegate { if(togglePause!=null) togglePause(); };Controls.Add(pause);
        Button pair=new Button();ConfigureButton(pair,"配對電腦",140,359,110);pair.Click+=delegate { if(reconnect!=null) reconnect(); };Controls.Add(pair);
        Button hide=new Button();ConfigureButton(hide,"收至背景",264,359,104);hide.Click+=delegate { Hide(); };Controls.Add(hide);
        Button stop=new Button();ConfigureButton(stop,"結束",382,359,102);stop.Click+=delegate { if(quit!=null) quit(); };Controls.Add(stop);
        VisibleChanged+=delegate { WriteVisibility(); };
    }
    void AddLabel(string text,int x,int y,int width,int height,float size,bool bold,Color color) {
        Label label=new Label();label.SetBounds(x,y,width,height);label.Text=text;label.Font=new Font(Font.FontFamily,size,bold?FontStyle.Bold:FontStyle.Regular);label.ForeColor=color;Controls.Add(label);
    }
    static void ConfigureButton(Button button,string text,int x,int y,int width) {
        button.SetBounds(x,y,width,36);button.Text=text;button.FlatStyle=FlatStyle.Flat;
        button.FlatAppearance.BorderColor=Color.FromArgb(75,86,102);button.BackColor=Color.FromArgb(40,47,59);button.ForeColor=Color.FromArgb(236,239,245);
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    internal void ShowPanel() { if(WindowState==FormWindowState.Minimized) WindowState=FormWindowState.Normal;Show();WriteVisibility(); }
    internal void SetState(string value,bool isPaused) { state.Text=value=="待命"?"程式已啟動，等待圖片":value;pause.Text=isPaused?"繼續":"暫停"; }
    internal void SetSignals(bool remoteActive,bool hasImage) {
        string text="工具狀態：執行中　｜　剪貼簿："+(hasImage?"有圖片":"目前沒有圖片");
        text+="\r\n圖片連線："+(remoteActive?"已確認，可自動傳圖":"正在連線；需要時按「重新配對」");
        signals.Text=text;
    }
    internal void OnProgress(int value,string title,string subtitle,bool done,bool error) {
        meter.Value=value;meter.Failed=error;meter.Invalidate();percent.Text=value+"%";
        percent.ForeColor=error?Color.FromArgb(244,158,107):Color.FromArgb(92,214,177);
        state.Text=title;detail.Text=subtitle;
        if(done && !error) { HasConfirmedTransfer=true;lastResult.Text="最近完成："+DateTime.Now.ToString("HH:mm:ss")+(receiver?" · 已收到圖片，可按 Ctrl+V":" · 遠端已確認收到圖片"); }
        if(error) lastResult.Text="尚未完成，請檢查上方狀態再重試。";
    }
    void WriteVisibility() {
        try { Directory.CreateDirectory(Bridge.Root);File.WriteAllText(Path.Combine(Bridge.Root,(receiver?"receiver":"sender")+"-window.txt"),"VERSION=0.2.0\r\nVISIBLE="+Visible+"\r\nPID="+Process.GetCurrentProcess().Id); } catch {}
    }
    protected override void OnFormClosing(FormClosingEventArgs e) {
        if(!disposeRequested && e.CloseReason==CloseReason.UserClosing) { e.Cancel=true;Hide();return; }
        base.OnFormClosing(e);
    }
    internal void Shutdown() { disposeRequested=true;Close();Dispose(); }
}

internal sealed class Bridge : Form {
    readonly bool receiver;
    readonly NotifyIcon tray=new NotifyIcon();
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    readonly string role;
    readonly ProgressToast progress=new ProgressToast();
    readonly Dashboard dashboard;
    readonly EventWaitHandle showSignal;
    uint sequence;
    string readyId;
    byte[] readyPng,readyDib;
    DateTime lastNotice=DateTime.MinValue;
    Assembler assembler;
    bool paused,busy,wasRemote,readyArmed;
    int restoredCount;
    DateTime readyAt;
    DateTime lastSignals=DateTime.MinValue;
    NetworkHost network;
    string endpoint;
    bool networkBusy,networkConnected,checkingNetwork;
    byte[] queuedPng;
    uint lastCapturedSequence;
    DateTime lastNetworkImage=DateTime.MinValue;
    DateTime nextNetworkCheck=DateTime.MinValue;
    Native.Hook hookCallback;
    IntPtr hookHandle;
    internal static string Root { get { return RuntimeSettings.Root; } }
    internal BridgeSettings PendingSettings;
    internal Bridge(bool isReceiver,EventWaitHandle signal,bool showWindow) {
        receiver=isReceiver; role=receiver?"receiver":"sender";
        showSignal=signal;
        dashboard=new Dashboard(receiver,delegate { TogglePause(); },delegate { Close(); },delegate { PairNetwork(); });
        progress.Updated+=dashboard.OnProgress;
        ShowInTaskbar=false; FormBorderStyle=FormBorderStyle.FixedToolWindow;
        CreateHandle();
        sequence=Native.GetClipboardSequenceNumber();
        Native.AddClipboardFormatListener(Handle);
        tray.Icon=SystemIcons.Information; tray.Visible=true;
        ContextMenuStrip menu=new ContextMenuStrip();
        ToolStripMenuItem pause=new ToolStripMenuItem("暫停圖片橋接");
        pause.Click+=delegate { TogglePause();pause.Checked=paused; };
        menu.Items.Add(pause);
        menu.Items.Add("開啟狀態視窗",null,delegate { dashboard.ShowPanel(); });
        menu.Items.Add("設定角色與登入啟動",null,delegate { Configure(); });
        tray.DoubleClick+=delegate { dashboard.ShowPanel(); };
        menu.Items.Add("顯示目前進度",null,delegate {
            if(receiver && readyArmed) progress.Report(100,"圖片已收到","已放入遠端剪貼簿，可以按 Ctrl+V",true,false);
            else if(assembler!=null) ShowReceiveProgress();
            else progress.Report(0,"圖片橋接待命","傳送端截圖後會自動傳送",true,false);
        });
        menu.Items.Add("停止並取消開機啟動",null,delegate { Installer.SetStartup(false);RuntimeSettings.Current.AutoStart=false;RuntimeSettings.Save(RuntimeSettings.Current);Close(); });
        menu.Items.Add("結束",null,delegate { Close(); }); tray.ContextMenuStrip=menu;
        hookCallback=OnKey;
        hookHandle=Native.SetWindowsHookEx(13,hookCallback,Native.GetModuleHandle(null),0);
        Log("started-v0.2.0 hook="+(hookHandle!=IntPtr.Zero));
        timer.Interval=80; timer.Tick+=delegate { Tick(); }; timer.Start();
        Status("待命"); Notice(receiver?"遠端接收端已啟動":"本機傳送端已啟動",receiver?"收到橋接圖片後即可貼上。":"連線確認後，新的截圖會自動傳送，不必切換視窗。");
        if(showWindow) dashboard.ShowPanel();
        if(receiver) {
            network=new NetworkHost(AcceptNetworkImage,ReportNetwork,delegate(string address) {
                Post(delegate { endpoint=address;nextNetworkCheck=DateTime.UtcNow.AddSeconds(3);Status("正在建立圖片連線"); });
            },Log);
            try { network.Start(); } catch(Exception e) { Status("連線啟動失敗");Log("network-start-error "+e.GetType().Name); }
        } else {
            string cached=Path.Combine(Root,"remote-endpoint.txt");
            endpoint=File.Exists(cached)?File.ReadAllText(cached).Trim():EndpointConfig.Default;
            if(!SecureImage.ValidEndpoint(endpoint)) endpoint=EndpointConfig.Default;
            CheckNetwork();
        }
    }
    protected override void SetVisibleCore(bool value) { base.SetVisibleCore(false); }
    protected override void WndProc(ref Message m) { base.WndProc(ref m); if(m.Msg==0x031D && !busy) Tick(); }
    internal static bool RemoteActive() {
        IntPtr w=Native.GetForegroundWindow(); StringBuilder title=new StringBuilder(768); Native.GetWindowText(w,title,title.Capacity);
        string text=title.ToString();
        if(text.IndexOf("遠端",StringComparison.OrdinalIgnoreCase)<0 && text.IndexOf("Chrome Remote Desktop",StringComparison.OrdinalIgnoreCase)<0 && text.IndexOf("Chrome 遠端桌面",StringComparison.Ordinal)<0) return false;
        uint pid; Native.GetWindowThreadProcessId(w,out pid);
        try { string name=Process.GetProcessById((int)pid).ProcessName; return name.Equals("chrome",StringComparison.OrdinalIgnoreCase) || name.Equals("msedge",StringComparison.OrdinalIgnoreCase); } catch { return false; }
    }
    void Status(string state) {
        tray.Text="圖片橋接 " +(receiver?"遠端":"本機")+"："+state;
        dashboard.SetState(state,paused);
        try { Directory.CreateDirectory(Root); File.WriteAllText(Path.Combine(Root,role+"-status.txt"),DateTime.Now.ToString("s")+" "+state+"\r\nPID="+Process.GetCurrentProcess().Id+"\r\nVERSION=0.2.0\r\nRESTORED="+restoredCount+"\r\nNETWORK="+networkConnected,Encoding.UTF8); } catch {}
    }
    void Post(Action action) { try { if(!IsDisposed && IsHandleCreated) BeginInvoke(action); } catch {} }
    void ReportNetwork(int value,string title,string description,bool done,bool error) {
        Post(delegate { progress.Report(value,title,description,done,error);if(done || error) Status(title); });
    }
    void CheckNetwork() {
        if(!SecureImage.ValidEndpoint(endpoint)) { Status("等待圖片連線配對");return; }
        if(checkingNetwork) return;checkingNetwork=true;
        string address=endpoint;
        ThreadPool.QueueUserWorkItem(delegate {
            bool ok=false;
            for(int i=0;i<3 && !IsDisposed;i++) { try { NetworkSender.Health(address);ok=true;break; } catch { Thread.Sleep(1000+i*500); } }
            Post(delegate {
                checkingNetwork=false;nextNetworkCheck=DateTime.UtcNow.AddSeconds(5);
                if(address!=endpoint) return;networkConnected=ok;Status(ok?"連線已確認，等待圖片":"正在重試圖片連線，可按重新配對");
                if(ok) { Log("encrypted-peer-verified");TryUpload(); }
            });
        });
    }
    void PairNetwork() {
        if(receiver) {
            if(!SecureImage.ValidEndpoint(endpoint)) { Status("正在建立圖片連線，請稍候");return; }
            using(PairingDialog dialog=new PairingDialog(endpoint,delegate {
                PendingSettings=new BridgeSettings { Receiver=true,Key=BridgeSettings.NewKey(),Endpoint="",AutoStart=RuntimeSettings.Current.AutoStart };
            })) dialog.ShowDialog(dashboard);
            if(PendingSettings!=null) Close();
        } else Configure();
    }
    void Configure() {
        using(SetupWizard dialog=new SetupWizard(RuntimeSettings.Current,true)) {
            if(dialog.ShowDialog(dashboard)==DialogResult.OK) { PendingSettings=dialog.Result;Close(); }
        }
    }
    bool AcceptNetworkImage(byte[] png,string id,DateTime created) {
        if(IsDisposed) return false;
        try { return (bool)Invoke(new Func<bool>(delegate {
            if(paused || created<lastNetworkImage) return false;
            byte[] dib=Clip.Dib(png);bool written=false;
            for(int i=0;i<5;i++) { if(Set(null,png,dib)) { written=true;break; } Thread.Sleep(10); }
            if(!written) return false;
            readyPng=png;readyDib=dib;readyId=id;readyAt=DateTime.UtcNow;readyArmed=true;lastNetworkImage=created;
            Status("圖片已就緒，可貼上");Log("network-image-ready bytes="+png.Length);return true;
        })); } catch { return false; }
    }
    void TryUpload() {
        if(receiver || paused || networkBusy || queuedPng==null || !SecureImage.ValidEndpoint(endpoint)) return;
        byte[] png=queuedPng;queuedPng=null;networkBusy=true;string address=endpoint;
        ThreadPool.QueueUserWorkItem(delegate {
            try {
                long elapsed=NetworkSender.Upload(address,png,false,ReportNetwork);
                Post(delegate { networkBusy=false;networkConnected=true;Log("network-image-acked bytes="+png.Length+" milliseconds="+elapsed);TryUpload(); });
            } catch(Exception e) {
                Post(delegate { networkBusy=false;networkConnected=false;if(queuedPng==null) queuedPng=png;Status("圖片傳送失敗，請重新配對");Log("network-upload-error "+e.GetType().Name);progress.Report(0,"圖片尚未送達","檢查圖片連線，或按「重新配對」後重試",false,true); });
            }
        });
    }
    void TogglePause() { paused=!paused;if(paused) CancelSend();Status(paused?"已暫停":"待命"); }
    void Log(string state) {
        try { string path=Path.Combine(Root,role+"-events.log");if(File.Exists(path) && new FileInfo(path).Length>131072) File.WriteAllText(path,"");File.AppendAllText(path,DateTime.Now.ToString("s")+" "+state+"\r\n",Encoding.UTF8); } catch {}
    }
    void ShowReceiveProgress() {
        if(assembler==null || assembler.Count<1) return;
        int value=(int)(100L*assembler.Buffer.Length/assembler.Length);
        progress.Report(value,"正在接收傳送端的圖片",assembler.Next+" / "+assembler.Count+" 包 · "+(assembler.Buffer.Length/1048576.0).ToString("0.0")+" / "+(assembler.Length/1048576.0).ToString("0.0")+" MB",false,false);
    }
    bool RestoreRemoteEmpty() {
        bool armed=readyArmed && readyPng!=null && (DateTime.UtcNow-readyAt).TotalMinutes<30;
        if(!armed || Clip.HasImage) return false;
        if(!Clip.ShouldRestore(armed,Clip.OwnerName()=="remoting_desktop",Clip.IsEmptyText(Handle),Clip.HasImage)) return false;
        // Do not resend READY here: returning only native image formats avoids an echo loop.
        if(!Set(null,readyPng,readyDib)) return true;
        restoredCount++;Status("圖片已就緒，可貼上");Log("restored-image-after-remote-empty");return true;
    }
    void Notice(string title,string text) { if((DateTime.UtcNow-lastNotice).TotalSeconds<3) return; lastNotice=DateTime.UtcNow; tray.ShowBalloonTip(2500,title,text,ToolTipIcon.Info); }
    bool Set(string text,byte[] png,byte[] dib) {
        bool ok=Clip.Write(Handle,text,png,dib); sequence=ok?Native.GetClipboardSequenceNumber():0; return ok;
    }
    void CancelSend() {
        queuedPng=null;
    }
    void Receive(string text) {
        string[] p=Wire.Parse(text); if(p==null || p.Length<2) return;
        if(p[0]=="LINK" && p.Length==2) { return; }
        if(p[0]=="READY" && p.Length==2 && p[1]==readyId && readyPng!=null) {
            if(!Clip.HasImage) Set(null,readyPng,readyDib);
            return;
        }
        if(p[0]!="DATA" || p.Length!=7) return;
        if(p[1]==readyId && readyPng!=null) { Set(Wire.Ready(readyId),readyPng,readyDib); return; }
        if(assembler==null || assembler.Id!=p[1]) {
            if(p[2]!="0") return;
            readyArmed=false;
            if(assembler!=null) assembler.Buffer.Dispose(); assembler=new Assembler();
        }
        byte[] result=assembler.Accept(p);
        if(result!=null) {
            readyPng=result; readyDib=Clip.Dib(result); readyId=p[1];
            readyArmed=true;readyAt=DateTime.UtcNow;
            if(Set(Wire.Ready(readyId),readyPng,readyDib)) {
                Status("圖片已就緒，可貼上"); assembler.Buffer.Dispose(); assembler=null;
                Log("received-image bytes="+result.Length+" sha256="+Wire.Hash(result));
                progress.Report(100,"圖片已收到","已放入遠端剪貼簿，可以按 Ctrl+V",true,false);
            }
        } else {
            Set(Wire.Ack(p[1],int.Parse(p[2])),null,null);
            Status("接收 "+assembler.Next+"/"+assembler.Count);
            Log("received-packet "+assembler.Next+"/"+assembler.Count);ShowReceiveProgress();
        }
    }
    void Tick() {
        if(busy || IsDisposed) return;
        if(showSignal!=null && showSignal.WaitOne(0)) dashboard.ShowPanel();
        if(!networkConnected && !checkingNetwork && SecureImage.ValidEndpoint(endpoint) && DateTime.UtcNow>=nextNetworkCheck) CheckNetwork();
        if((DateTime.UtcNow-lastSignals).TotalMilliseconds>700) { lastSignals=DateTime.UtcNow;dashboard.SetSignals(networkConnected,Clip.HasImage); }
        if(paused) return;
        busy=true;
        try {
            uint current=Native.GetClipboardSequenceNumber();
            bool remote=!receiver && RemoteActive();
            bool entered=remote&&!wasRemote;wasRemote=remote;
            if(current!=sequence || entered) {
                sequence=current;
                string text=Clip.ReadProtocol(Handle);
                if(Clip.ReadBusy) { sequence=0;return; }
                if(receiver) {
                    if(text!=null) Receive(text);
                    else if(!RestoreRemoteEmpty() && !Clip.ReadBusy && !Clip.OwnedByThisProcess()) {
                        if(readyArmed) Log("clipboard-replaced owner="+Clip.OwnerName()+" image="+Clip.HasImage+" empty="+Clip.IsEmptyText(Handle));
                        readyArmed=false;
                    }
                }
                else {
                    string[] p=Wire.Parse(text);
                    if(p!=null && p.Length==2 && p[0]=="ENDPOINT") {
                        string address=Encoding.UTF8.GetString(Convert.FromBase64String(p[1]));
                        if(SecureImage.ValidEndpoint(address)) { endpoint=address;File.WriteAllText(Path.Combine(Root,"remote-endpoint.txt"),address);CheckNetwork(); }
                    } else if(Clip.HasImage && text==null && current!=lastCapturedSequence && !Clip.OwnedByThisProcess()) {
                        byte[] png=Clip.ReadPng(Handle);if(png!=null) { lastCapturedSequence=current;queuedPng=png;TryUpload(); }
                    }
                }
            }
            if(receiver && assembler!=null && (DateTime.UtcNow-assembler.Updated).TotalSeconds>60) { assembler.Buffer.Dispose();assembler=null;Status("接收逾時");progress.Report(0,"圖片接收逾時","請在傳送端重新截圖，再回到遠端畫面",false,true); }
        } catch(Exception e) {
            if(receiver && assembler!=null) { assembler.Buffer.Dispose();assembler=null; }
            if(!receiver) CancelSend();
            // Log only exception class; never clipboard payloads or image bytes.
            Status("錯誤 "+e.GetType().Name);
            Log("error "+e.GetType().Name);progress.Report(0,"圖片橋接未完成","請重新截圖；可從托盤檢查狀態",false,true);
            Notice("圖片橋接未完成","圖片可能太大或剪貼簿正忙碌，請重新截圖。原本的文字複製不受影響。");
        } finally { busy=false; }
    }
    IntPtr OnKey(int code,IntPtr message,IntPtr info) {
        // Look only at Ctrl+V / Shift+Insert to prevent encoded packets being pasted.
        // No keys are recorded; this never moves the mouse or changes focus.
        if(code>=0 && (message==(IntPtr)0x100 || message==(IntPtr)0x104) && !paused) {
            int key=Marshal.ReadInt32(info);
            bool paste=(key==0x56 && Native.GetAsyncKeyState(0x11)<0) || (key==0x2D && Native.GetAsyncKeyState(0x10)<0);
            if(paste) {
                try {
                    if(receiver) RestoreRemoteEmpty();
                    string text=Clip.ReadProtocol(Handle);
                    if(text!=null) {
                        if(receiver) {
                            // Keep the keyboard hook short; decode PNG packets on the timer.
                            // Restoring an already-decoded READY image avoids a clipboard echo race.
                            if(text==Wire.Ready(readyId) && readyPng!=null) {
                                if(!Set(null,readyPng,readyDib)) { Log("paste-deferred-clipboard-busy");return (IntPtr)1; }
                                Log("paste-image-ready");
                            }
                            if(!Clip.HasImage) { Log("paste-deferred-transfer-pending");Notice("圖片還在傳送","傳送完成後再按一次 Ctrl+V，即可貼上圖片。"); return (IntPtr)1; }
                        } else if(!RemoteActive()) {
                            if(readyPng!=null) Set(null,readyPng,readyDib);
                        }
                    }
                } catch(Exception e) { Log("paste-error "+e.GetType().Name);return (IntPtr)1; }
            }
        }
        return Native.CallNextHookEx(hookHandle,code,message,info);
    }
    protected override void OnFormClosed(FormClosedEventArgs e) {
        timer.Stop(); if(!receiver) CancelSend();
        if(network!=null) network.Dispose();
        Native.RemoveClipboardFormatListener(Handle);if(hookHandle!=IntPtr.Zero) Native.UnhookWindowsHookEx(hookHandle);
        tray.Visible=false;tray.Dispose();StatusFileStopped();
        progress.Dispose();
        dashboard.Shutdown();
        base.OnFormClosed(e);Application.ExitThread();
    }
    void StatusFileStopped() { try { File.WriteAllText(Path.Combine(Root,role+"-status.txt"),DateTime.Now.ToString("s")+" stopped"); } catch {} }
}

internal static class Program {
    static Mutex singleton;
    [STAThread] static int Main(string[] args) {
        try {
            if(args.Length>1 && (args[0]=="--self-test" || args[0]=="--clipboard-test" || args[0]=="--setup-test" || args[0]=="--tunnel-test" || args[0]=="--transport-test")) {
                RuntimeSettings.TestRoot=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1])),"isolated-"+Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RuntimeSettings.Root);
                RuntimeSettings.Current=new BridgeSettings { Receiver=true,Key=BridgeSettings.NewKey(),Endpoint="" };
                if(args[0]=="--self-test") { SelfTest.Run(args[1]);ConfigurationTests.Run(args[1]); }
                if(args[0]=="--clipboard-test") SelfTest.ClipboardRun(args[1]);
                if(args[0]=="--setup-test") ConfigurationTests.Render(args[1]);
                if(args[0]=="--transport-test") ConfigurationTests.Transport(args[1]);
                if(args[0]=="--tunnel-test") { Installer.EnsureTunnel(delegate {});File.WriteAllText(args[1],"PASS official tunnel download, SHA-256 and Windows signature"); }
                return 0;
            }
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length>0 && args[0]=="--uninstall") { Installer.Uninstall();return 0; }
            if(args.Length>0 && args[0]=="--remove-installed") { Installer.RemoveInstalled();return 0; }
            bool background=args.Length>0 && args[0]=="--background";
            BridgeSettings settings=RuntimeSettings.Load();
            if(!background && (!Installer.Installed || settings==null)) {
                using(SetupWizard wizard=new SetupWizard(settings)) {
                    if(wizard.ShowDialog()!=DialogResult.OK) return 0;
                    settings=wizard.Result;
                }
                if(!Installer.Installed) { Installer.Launch();return 0; }
            }
            if(settings==null) return 0; // Unconfigured startup must never collect clipboard images.
            RuntimeSettings.Current=settings;
            bool created;singleton=new Mutex(true,"Local\\RemoteImageBridge-v2",out created);
            string signalName="Local\\RemoteImageBridge-show-v2";
            if(!created) {
                if(!background) using(EventWaitHandle signal=new EventWaitHandle(false,EventResetMode.AutoReset,signalName)) signal.Set();
                singleton.Dispose();
                return 0;
            }
            BridgeSettings pending=null;
            try {
                using(EventWaitHandle signal=new EventWaitHandle(false,EventResetMode.AutoReset,signalName))
                using(Bridge app=new Bridge(settings.Receiver,signal,!background)) { Application.Run(app);pending=app.PendingSettings; }
            } finally { singleton.ReleaseMutex();singleton.Dispose(); }
            if(pending!=null) { Installer.Install(pending);Installer.Launch(); }
            return 0;
        } catch(Exception e) {
            Directory.CreateDirectory(Bridge.Root);File.WriteAllText(Path.Combine(Bridge.Root,"startup-error.txt"),RuntimeSettings.TestRoot!=null?e.ToString():e.GetType().Name);
            if(args.Length==0) MessageBox.Show("啟動未完成："+e.Message,RuntimeSettings.Product,MessageBoxButtons.OK,MessageBoxIcon.Error);
            return 1;
        }
    }
}

internal static class SelfTest {
    static void Assert(bool ok,string name,List<string> results) { if(!ok) throw new Exception(name); results.Add("PASS "+name); }
    internal static void NetworkRun(string endpoint,string output) {
        List<string> results=new List<string>();NetworkSender.Health(endpoint);results.Add("PASS remote peer challenge verified");
        foreach(int width in new int[]{320,960,1920}) {
            int height=width*9/16;byte[] png;
            using(Bitmap b=new Bitmap(width,height)) {
                Random random=new Random(width);BitmapData data=b.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
                byte[] pixels=new byte[Math.Abs(data.Stride)*height];random.NextBytes(pixels);for(int i=3;i<pixels.Length;i+=4) pixels[i]=255;
                Marshal.Copy(pixels,0,data.Scan0,pixels.Length);b.UnlockBits(data);
                using(MemoryStream s=new MemoryStream()) { b.Save(s,ImageFormat.Png);png=s.ToArray(); }
            }
            long milliseconds=NetworkSender.Upload(endpoint,png,true,delegate {});
            results.Add("PASS HTTPS encrypted round-trip bytes="+png.Length+" milliseconds="+milliseconds);
            File.WriteAllLines(output,results,Encoding.UTF8);
        }
    }
    internal static void DashboardRun(string output) {
        List<string> results=new List<string>();
        using(Dashboard panel=new Dashboard(true,null,null)) {
            panel.SetState("待命",false);panel.SetSignals(false,false);
            Assert(!panel.HasConfirmedTransfer,"startup does not claim a confirmed remote transfer",results);
            IntPtr before=Native.GetForegroundWindow();panel.ShowPanel();Application.DoEvents();
            Assert(panel.Visible,"main status window is visible on manual launch",results);
            Assert(Native.GetForegroundWindow()==before,"status window opens without stealing focus",results);
            using(Bitmap img=new Bitmap(panel.Width,panel.Height)) { panel.DrawToBitmap(img,new Rectangle(0,0,img.Width,img.Height));img.Save(Path.ChangeExtension(output,"png"),ImageFormat.Png); }
            panel.Close();Application.DoEvents();Assert(!panel.Visible && !panel.IsDisposed,"closing status window keeps background bridge available",results);
            panel.ShowPanel();Application.DoEvents();Assert(panel.Visible,"reopening brings back the existing window",results);
            panel.OnProgress(58,"正在接收傳送端的圖片","7 / 12 包 · 1.0 / 1.7 MB",false,false);
            Assert(!panel.HasConfirmedTransfer,"partial progress is not reported as completion",results);
            using(Bitmap img=new Bitmap(panel.Width,panel.Height)) { panel.DrawToBitmap(img,new Rectangle(0,0,img.Width,img.Height));img.Save(Path.Combine(Path.GetDirectoryName(output),"dashboard-v3-progress.png"),ImageFormat.Png); }
            panel.OnProgress(100,"圖片已收到","已放入遠端剪貼簿，可以按 Ctrl+V",true,false);
            Assert(panel.HasConfirmedTransfer,"completed transfer updates the confirmed status",results);
            panel.Shutdown();Assert(panel.IsDisposed,"explicit shutdown closes the status window",results);
        }
        File.WriteAllLines(output,results,Encoding.UTF8);
    }
    internal static void Run(string output) {
        List<string> results=new List<string>();
        byte[] png;
        using(Bitmap b=new Bitmap(960,540)) {
            Random rand=new Random(817);System.Drawing.Imaging.BitmapData data=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            byte[] pixels=new byte[Math.Abs(data.Stride)*b.Height];rand.NextBytes(pixels);for(int i=3;i<pixels.Length;i+=4) pixels[i]=255;
            Marshal.Copy(pixels,0,data.Scan0,pixels.Length);b.UnlockBits(data);
            using(MemoryStream s=new MemoryStream()) { b.Save(s,ImageFormat.Png);png=s.ToArray(); }
        }
        Assert(png.Length>1024*1024,"large lossless PNG exceeds CRD single-message limit",results);
        string id=Guid.NewGuid().ToString("N");Assembler a=new Assembler();byte[] rebuilt=null;
        int count=(png.Length+Wire.ChunkBytes-1)/Wire.ChunkBytes;
        for(int n=0;n<count;n++) {
            string packet=Wire.Packet(png,id,n);Assert(Encoding.UTF8.GetByteCount(packet)<256*1024,"bounded packet "+n,results);
            rebuilt=a.Accept(Wire.Parse(packet));
            if(n==0) { a.Accept(Wire.Parse(packet));Assert(a.Next==1,"duplicate packet does not duplicate bytes",results); }
        }
        Assert(rebuilt!=null && Wire.Hash(rebuilt)==Wire.Hash(png),"multi-packet PNG preserves all source bytes",results);
        byte[] dib=Clip.Dib(png);Assert(BitConverter.ToInt32(dib,4)==960 && BitConverter.ToInt32(dib,8)==540,"Windows DIB dimensions",results);
        Assert(Wire.Parse("ordinary text")==null,"ordinary text ignored",results);
        Assert(Wire.Parse(Wire.Prefix.Replace(SecureImage.ControlId,new string('z',SecureImage.ControlId.Length))+"READY:"+id)==null,"other pairing key ignored",results);
        bool rejected=false;try { Assembler bad=new Assembler();bad.Accept(Wire.Parse(Wire.Packet(png,id,1))); } catch(InvalidDataException) { rejected=true; }
        Assert(rejected,"out-of-order initial packet rejected",results);
        rejected=false;try { string[] p=Wire.Parse(Wire.Packet(png,id,0));p[4]="999999999";new Assembler().Accept(p); } catch(InvalidDataException) { rejected=true; }
        Assert(rejected,"oversized packet rejected before allocation",results);
        rejected=false;try { byte[] bomb=(byte[])png.Clone();for(int i=16;i<24;i++) bomb[i]=255;Wire.ValidatePng(bomb); } catch(InvalidDataException) { rejected=true; }
        Assert(rejected,"oversized image dimensions rejected",results);
        rejected=false;try { Assembler bad=new Assembler();for(int n=0;n<count;n++) { string[] p=Wire.Parse(Wire.Packet(png,id,n));p[5]=new string('0',64);bad.Accept(p); } } catch(InvalidDataException) { rejected=true; }
        Assert(rejected,"corrupt transfer checksum rejected",results);
        string secureId=Guid.NewGuid().ToString("N"),decodedId;bool isProbe;DateTime created;
        byte[] envelope=SecureImage.Encrypt(png,secureId,true);
        byte[] decoded=SecureImage.Decrypt(envelope,out decodedId,out isProbe,out created);
        Assert(secureId==decodedId && isProbe && Wire.Hash(decoded)==Wire.Hash(png),"authenticated encryption preserves source image",results);
        rejected=false;envelope[35]^=1;try { SecureImage.Decrypt(envelope,out decodedId,out isProbe,out created); } catch(InvalidDataException) { rejected=true; }
        Assert(rejected,"modified encrypted image rejected before decryption",results);
        Assert(SecureImage.ValidEndpoint("https://example-tunnel.trycloudflare.com") && !SecureImage.ValidEndpoint("http://example-tunnel.trycloudflare.com") && !SecureImage.ValidEndpoint("https://example.com") && !SecureImage.ValidEndpoint("https://example.trycloudflare.com.attacker.test"),"encrypted endpoint scheme and host restriction",results);
        Assert(Clip.ShouldRestore(true,true,true,false),"remote empty clipboard restores armed image",results);
        Assert(!Clip.ShouldRestore(true,true,false,false),"ordinary remote text never replaced by stale image",results);
        Assert(!Clip.ShouldRestore(true,false,true,false),"local clipboard clear respected",results);
        Assert(!Clip.ShouldRestore(false,true,true,false),"unarmed clipboard not restored",results);
        Assert(!Clip.ShouldRestore(true,true,true,true),"existing image is not overwritten",results);
        // Native allocation round-trip without touching either user's clipboard.
        IntPtr h=Native.GlobalAlloc(2,(UIntPtr)png.Length);IntPtr ptr=Native.GlobalLock(h);
        try { Marshal.Copy(png,0,ptr,png.Length);byte[] copy=new byte[png.Length];Marshal.Copy(ptr,copy,0,copy.Length);Assert(Wire.Hash(copy)==Wire.Hash(png),"native memory round-trip",results); }
        finally { Native.GlobalUnlock(h);Native.GlobalFree(h); }
        File.WriteAllLines(output,results,Encoding.UTF8);
    }
    internal static void ProgressRun(string output) {
        List<string> results=new List<string>();
        using(ProgressToast progress=new ProgressToast()) {
            IntPtr before=Native.GetForegroundWindow();
            progress.Report(67,"正在接收傳送端的圖片","8 / 12 包 · 1.1 / 1.7 MB",false,false);
            Application.DoEvents();
            Assert(Native.GetForegroundWindow()==before,"progress window does not take keyboard focus",results);
            using(Bitmap preview=new Bitmap(progress.Width,progress.Height)) { progress.DrawToBitmap(preview,new Rectangle(0,0,preview.Width,preview.Height));preview.Save(Path.ChangeExtension(output,"png"),ImageFormat.Png); }
            progress.Hide();
            Assert(!progress.Visible,"progress window can hide without mouse control",results);
        }
        File.WriteAllLines(output,results,Encoding.UTF8);
    }
    internal static void NativeImageRun(string output) {
        List<string> results=new List<string>();NativeWindow owner=new NativeWindow();
        owner.CreateHandle(new CreateParams { Caption="遠端native image clipboard test" });
        uint written=0;
        try {
            // Run only on an already empty text clipboard, so no copied user content is discarded.
            if(!Clip.IsEmptyText(owner.Handle)) { File.WriteAllText(output,"SKIP: clipboard is not empty; preserved unchanged");return; }
            byte[] png;
            using(Bitmap b=new Bitmap(24,16)) { using(Graphics g=Graphics.FromImage(b)) g.Clear(Color.CornflowerBlue);using(MemoryStream s=new MemoryStream()) { b.Save(s,ImageFormat.Png);png=s.ToArray(); } }
            uint expected=Native.GetClipboardSequenceNumber();
            if(!Clip.IsEmptyText(owner.Handle) || Native.GetClipboardSequenceNumber()!=expected) { File.WriteAllText(output,"SKIP: clipboard changed; preserved unchanged");return; }
            Assert(Clip.Write(owner.Handle,null,png,Clip.Dib(png)),"real Windows PNG+DIB clipboard write",results);written=Native.GetClipboardSequenceNumber();
            Assert(Clip.HasImage && !Clip.HasText,"image-only clipboard prevents control text being pasted",results);
            Assert(Wire.Hash(Clip.ReadPng(owner.Handle))==Wire.Hash(png),"real native PNG clipboard bytes verified",results);
            using(Image bitmap=Clipboard.GetImage()) Assert(bitmap!=null && bitmap.Width==24 && bitmap.Height==16,"Windows image paste API reads the bitmap",results);
            File.WriteAllLines(output,results,Encoding.UTF8);
        } finally {
            // Restore the original empty state only if nobody made a newer copy.
            if(written!=0 && Native.GetClipboardSequenceNumber()==written) Clip.Write(owner.Handle,"",null,null);
            owner.DestroyHandle();
        }
    }
    internal static void ClipboardRun(string output) {
        Exception failure=null;
        Thread worker=new Thread(delegate() { try { ClipboardOnDesktop(output); } catch(Exception e) { failure=e; } });
        worker.SetApartmentState(ApartmentState.STA);worker.Start();worker.Join();
        if(failure!=null) throw new Exception("Isolated clipboard test failed",failure);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void ClipboardOnDesktop(string output) {
        List<string> results=new List<string>();
        // A separate window station owns a separate clipboard. The interactive user's
        // clipboard and the running Chrome Remote Desktop session are never touched.
        IntPtr station=Native.CreateWindowStation("CrdTest-"+Guid.NewGuid().ToString("N"),1,0x000F037F,IntPtr.Zero);
        // Non-admin Windows processes cannot choose a name. CREATE_ONLY prevents
        // reuse of an existing station, including any existing user's clipboard.
        if(station==IntPtr.Zero && Marshal.GetLastWin32Error()==5) station=Native.CreateWindowStation(null,1,0x000F037F,IntPtr.Zero);
        if(station==IntPtr.Zero) {
            File.WriteAllText(output,"SKIP isolated clipboard: cannot create a NEW private window station; Windows error "+Marshal.GetLastWin32Error()+". Interactive clipboard was not accessed.");return;
        }
        if(!Native.SetProcessWindowStation(station)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"SetProcessWindowStation failed");
        IntPtr desktop=Native.CreateDesktop("BridgeTest",IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);
        if(desktop==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"CreateDesktop failed");
        if(!Native.SetThreadDesktop(desktop)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"SetThreadDesktop failed");
        NativeWindow owner=new NativeWindow();owner.CreateHandle(new CreateParams { Caption="遠端Image Bridge isolated test" });
        try {
            byte[] png;
            using(Bitmap b=new Bitmap(240,100)) {
                using(Graphics g=Graphics.FromImage(b)) { g.Clear(Color.CornflowerBlue);g.DrawString("clipboard test",SystemFonts.DefaultFont,Brushes.White,8,8); }
                using(MemoryStream s=new MemoryStream()) { b.Save(s,ImageFormat.Png);png=s.ToArray(); }
            }
            byte[] dib=Clip.Dib(png);string id=Guid.NewGuid().ToString("N");string packet=Wire.Packet(png,id,0);
            Assert(Clip.Write(owner.Handle,packet,png,dib),"native sender clipboard write",results);
            Assert(Clip.HasImage && Clip.HasText,"native PNG+DIB+Unicode coexist",results);
            Assert(Wire.Hash(Clip.ReadPng(owner.Handle))==Wire.Hash(png),"local image preserved with transport text",results);
            string wire=Clip.ReadProtocol(owner.Handle);
            Assert(wire==packet,"Chrome-compatible Unicode transport bytes",results);
            Assert(Clip.Write(owner.Handle,wire,null,null),"simulate CRD text-only receive",results);
            Assert(!Clip.HasImage,"transport contains no native bitmap",results);
            byte[] decoded=new Assembler().Accept(Wire.Parse(Clip.ReadProtocol(owner.Handle)));
            Assert(Clip.Write(owner.Handle,Wire.Ready(id),decoded,Clip.Dib(decoded)),"native receiver clipboard write",results);
            Assert(Clip.HasImage && Wire.Hash(Clip.ReadPng(owner.Handle))==Wire.Hash(png),"native receiver image is byte-identical",results);
            Assert(Native.IsClipboardFormatAvailable(8),"DIB available for Windows paste targets",results);
            Assert(Clip.Write(owner.Handle,"ordinary text",null,null),"ordinary text copy",results);
            Assert(Clip.ReadProtocol(owner.Handle)==null && !Clip.HasImage,"ordinary copy replaces prior image cleanly",results);
            File.WriteAllLines(output,results,Encoding.UTF8);
        } finally { owner.DestroyHandle(); }
        // The test process exits immediately and Windows reclaims its private station.
    }
}
