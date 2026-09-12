using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Installer {
    internal const string TunnelVersion="2026.9.1";
    internal const string TunnelSha256="2837888cc0f5d58f15b6dc478376de90b4d3ba5241c7947455d1e0a0df429712";
    const string RegistryName="RemoteImageBridge";
    static string TestRegistryRoot { get { return @"Software\RemoteImageBridgeTests\"+Path.GetFileName(RuntimeSettings.Root); } }
    static string UninstallParent { get { return RuntimeSettings.TestRoot==null?@"Software\Microsoft\Windows\CurrentVersion\Uninstall":TestRegistryRoot+@"\Uninstall"; } }
    internal static string UninstallKey { get { return UninstallParent+@"\ChromeRemoteDesktopImagePaste"; } }
    internal static string StartupKey { get { return RuntimeSettings.TestRoot==null?@"Software\Microsoft\Windows\CurrentVersion\Run":TestRegistryRoot+@"\Run"; } }
    internal static string Executable { get { return Path.Combine(RuntimeSettings.Root,"RemoteImageBridge.exe"); } }
    internal static string Shortcut { get { return Path.Combine(RuntimeSettings.TestRoot??Environment.GetFolderPath(Environment.SpecialFolder.Programs),RuntimeSettings.Product+".lnk"); } }
    internal static bool Installed { get { return string.Equals(Application.ExecutablePath,Executable,StringComparison.OrdinalIgnoreCase); } }
    internal static void EnsureTunnel(Action<int,string> report) {
        Directory.CreateDirectory(RuntimeSettings.Root);
        string target=Path.Combine(RuntimeSettings.Root,"cloudflared.exe");
        if(File.Exists(target) && Wire.Hash(File.ReadAllBytes(target))==TunnelSha256) { report(100,"網路元件已驗證");return; }
        string temporary=Path.Combine(RuntimeSettings.Root,"cloudflared-"+Guid.NewGuid().ToString("N")+".tmp");
        try {
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            string url="https://github.com/cloudflare/cloudflared/releases/download/"+TunnelVersion+"/cloudflared-windows-amd64.exe";
            HttpWebRequest request=(HttpWebRequest)WebRequest.Create(url);
            request.UserAgent="ChromeRemoteDesktopImagePaste/"+RuntimeSettings.Version;
            request.Timeout=120000;request.ReadWriteTimeout=30000;
            using(HttpWebResponse response=(HttpWebResponse)request.GetResponse()) {
                if(response.ResponseUri.Scheme!="https" || response.ContentLength>80*1024*1024) throw new InvalidDataException("下載來源或大小不正確。");
                using(Stream input=response.GetResponseStream()) using(FileStream output=File.Create(temporary)) {
                    byte[] buffer=new byte[65536];long total=0;int n,last=-1;
                    while((n=input.Read(buffer,0,buffer.Length))>0) {
                        total+=n;if(total>80*1024*1024) throw new InvalidDataException("下載檔案超過大小限制。");
                        output.Write(buffer,0,n);int percent=response.ContentLength>0?(int)(95*total/response.ContentLength):0;
                        if(percent!=last) { last=percent;report(percent,"下載網路元件 "+(total/1048576.0).ToString("0.0")+" MB / 約 53 MB"); }
                    }
                }
            }
            if(Wire.Hash(File.ReadAllBytes(temporary))!=TunnelSha256) throw new InvalidDataException("下載驗證失敗，請稍後重試。");
            VerifyCloudflareSignature(temporary);
            File.Copy(temporary,target,true);report(100,"官方網路元件驗證完成");
        } finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct TrustFile { public uint Size;public IntPtr Path;public IntPtr Handle;public IntPtr Subject; }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct TrustData {
        public uint Size;public IntPtr Policy;public IntPtr Sip;public uint UIChoice;public uint Revocation;
        public uint UnionChoice;public IntPtr File;public uint StateAction;public IntPtr State;public IntPtr Url;
        public uint Flags;public uint UIContext;
    }
    [DllImport("wintrust.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]
    static extern int WinVerifyTrust(IntPtr window,[In] ref Guid action,[In] ref TrustData data);
    internal static void VerifyCloudflareSignature(string path) {
        IntPtr fileName=Marshal.StringToCoTaskMemUni(path),filePointer=IntPtr.Zero;
        try {
            TrustFile file=new TrustFile { Size=(uint)Marshal.SizeOf(typeof(TrustFile)),Path=fileName };
            filePointer=Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(TrustFile)));Marshal.StructureToPtr(file,filePointer,false);
            TrustData data=new TrustData { Size=(uint)Marshal.SizeOf(typeof(TrustData)),UIChoice=2,UnionChoice=1,File=filePointer };
            Guid action=new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            if(WinVerifyTrust(new IntPtr(-1),ref action,ref data)!=0) throw new InvalidDataException("Cloudflare 簽章驗證失敗。");
            using(X509Certificate2 certificate=new X509Certificate2(X509Certificate.CreateFromSignedFile(path))) {
                if(certificate.Subject.IndexOf("Cloudflare",StringComparison.OrdinalIgnoreCase)<0) throw new InvalidDataException("下載檔案的發布者不正確。");
            }
        } finally { if(filePointer!=IntPtr.Zero) Marshal.FreeCoTaskMem(filePointer);Marshal.FreeCoTaskMem(fileName); }
    }
    internal static void StopInstalledProcesses() {
        foreach(string name in new string[]{"RemoteImageBridge","cloudflared"}) foreach(Process old in Process.GetProcessesByName(name)) {
            using(old) try {
                string expected=name=="cloudflared"?Path.Combine(RuntimeSettings.Root,"cloudflared.exe"):Executable;
                if(old.Id!=Process.GetCurrentProcess().Id && old.SessionId==Process.GetCurrentProcess().SessionId && string.Equals(old.MainModule.FileName,expected,StringComparison.OrdinalIgnoreCase)) {
                    old.Kill();if(!old.WaitForExit(10000)) throw new IOException("請先結束舊版工具，再重試。");
                }
            } catch(System.ComponentModel.Win32Exception) { /* An unrelated inaccessible process must not be touched. */ }
        }
    }
    internal static void Install(BridgeSettings settings) {
        settings.Validate();Directory.CreateDirectory(RuntimeSettings.Root);
        if(!Installed) { StopInstalledProcesses();File.Copy(Application.ExecutablePath,Executable,true); }
        RuntimeSettings.Save(settings);SetStartup(settings.AutoStart);
        using(RegistryKey key=Registry.CurrentUser.CreateSubKey(UninstallKey)) {
            key.SetValue("DisplayName",RuntimeSettings.Product);key.SetValue("DisplayVersion",RuntimeSettings.Version);
            key.SetValue("Publisher","evan6007");key.SetValue("InstallLocation",RuntimeSettings.Root);
            key.SetValue("DisplayIcon",Executable);key.SetValue("UninstallString","\""+Executable+"\" --uninstall");
            key.SetValue("URLInfoAbout","https://github.com/evan6007/chrome-remote-desktop-image-paste");
            key.SetValue("NoModify",1);key.SetValue("NoRepair",1);
        }
        CreateShortcut();
    }
    internal static void SetStartup(bool enabled) {
        using(RegistryKey key=Registry.CurrentUser.CreateSubKey(StartupKey)) {
            if(enabled) key.SetValue(RegistryName,"\""+Executable+"\" --background"); else key.DeleteValue(RegistryName,false);
        }
    }
    static void CreateShortcut() {
        object shell=null,link=null;
        try {
            Type type=Type.GetTypeFromProgID("WScript.Shell");shell=Activator.CreateInstance(type);
            link=type.InvokeMember("CreateShortcut",System.Reflection.BindingFlags.InvokeMethod,null,shell,new object[]{Shortcut});
            Type linkType=link.GetType();
            linkType.InvokeMember("TargetPath",System.Reflection.BindingFlags.SetProperty,null,link,new object[]{Executable});
            linkType.InvokeMember("Arguments",System.Reflection.BindingFlags.SetProperty,null,link,new object[]{"--show"});
            linkType.InvokeMember("Save",System.Reflection.BindingFlags.InvokeMethod,null,link,null);
        } finally { if(link!=null) Marshal.FinalReleaseComObject(link);if(shell!=null) Marshal.FinalReleaseComObject(shell); }
    }
    internal static void Launch() { Process.Start(new ProcessStartInfo(Executable,"--show") { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden }); }
    internal static void Uninstall() {
        if(MessageBox.Show("移除圖片貼上工具、配對資料與登入啟動設定？",RuntimeSettings.Product,MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK) return;
        string helper=Path.Combine(Path.GetTempPath(),"ImagePaste-Uninstall-"+Guid.NewGuid().ToString("N")+".exe");
        File.Copy(Application.ExecutablePath,helper);
        Process.Start(new ProcessStartInfo(helper,"--remove-installed") { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden });
    }
    internal static void RemoveInstalled(bool notify=true) {
        if(Installed) throw new InvalidOperationException("Uninstall helper must run outside the installation directory.");
        StopInstalledProcesses();SetStartup(false);
        using(RegistryKey key=Registry.CurrentUser.OpenSubKey(UninstallParent,true)) if(key!=null) key.DeleteSubKeyTree("ChromeRemoteDesktopImagePaste",false);
        if(File.Exists(Shortcut)) File.Delete(Shortcut);
        // Delete only files owned by this application. Never recursively delete a computed directory.
        foreach(string name in new string[]{"RemoteImageBridge.exe","cloudflared.exe","settings.dpapi","endpoint.txt","remote-endpoint.txt","sender-events.log","receiver-events.log","sender-status.txt","receiver-status.txt","sender-window.txt","receiver-window.txt","startup-error.txt"}) {
            string file=Path.Combine(RuntimeSettings.Root,name);if(File.Exists(file)) File.Delete(file);
        }
        if(Directory.Exists(RuntimeSettings.Root) && Directory.GetFileSystemEntries(RuntimeSettings.Root).Length==0) Directory.Delete(RuntimeSettings.Root);
        if(notify) MessageBox.Show("已移除圖片貼上工具。",RuntimeSettings.Product,MessageBoxButtons.OK,MessageBoxIcon.Information);
        if(RuntimeSettings.TestRoot!=null) Registry.CurrentUser.DeleteSubKeyTree(TestRegistryRoot,false);
    }
}
