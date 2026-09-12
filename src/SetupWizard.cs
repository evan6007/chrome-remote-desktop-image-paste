using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class SetupWizard : Form {
    readonly RadioButton receiver=new RadioButton(),sender=new RadioButton();
    readonly TextBox invitation=new TextBox();
    readonly CheckBox startup=new CheckBox();
    readonly Button install=new Button();
    readonly Label status=new Label();
    readonly ProgressBar progress=new ProgressBar();
    readonly BridgeSettings existing;
    readonly bool configureOnly;
    bool working;
    internal BridgeSettings Result;
    internal SetupWizard(BridgeSettings current,bool settingsOnly=false) {
        existing=current;configureOnly=settingsOnly;
        AutoScaleMode=AutoScaleMode.None;Font=new Font("Microsoft JhengHei UI",10);
        Text=RuntimeSettings.Product+" — 設定";ClientSize=new Size(610,620);
        BackColor=Color.FromArgb(26,30,38);ForeColor=Color.FromArgb(236,239,245);
        StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
        Label title=LabelAt("讓本機截圖，直接貼到遠端",26,22,560,38);title.Font=new Font(Font.FontFamily,18,FontStyle.Bold);
        LabelAt("兩台下載同一個程式，先設定被控制的電腦。",28,69,550,30);
        receiver.Text="這台是遠端：接收圖片（辦公室／實驗室）";receiver.SetBounds(28,121,555,35);
        sender.Text="這台是本機：傳送圖片（家裡／手邊的電腦）";sender.SetBounds(28,164,555,35);
        Controls.Add(receiver);Controls.Add(sender);receiver.Checked=current==null || current.Receiver;sender.Checked=current!=null && !current.Receiver;
        LabelAt("本機傳送端：貼上遠端的配對碼",28,216,550,26);
        invitation.SetBounds(28,249,554,72);invitation.Multiline=true;invitation.ScrollBars=ScrollBars.Vertical;
        invitation.BackColor=Color.FromArgb(39,47,60);invitation.ForeColor=ForeColor;Controls.Add(invitation);
        receiver.CheckedChanged+=delegate { invitation.Enabled=sender.Checked; };invitation.Enabled=sender.Checked;
        if(current!=null && !current.Receiver) invitation.Text=new BridgeSettings { Receiver=true,Key=current.Key,Endpoint="" }.Invitation(current.Endpoint);
        Label note=LabelAt("配對碼只交給自己的另一台電腦。接收端重啟後，需重新複製配對碼。",28,331,554,45);note.ForeColor=Color.FromArgb(177,190,209);
        startup.Text="登入 Windows 時自動啟動";startup.SetBounds(28,384,550,31);startup.Checked=current!=null && current.AutoStart;Controls.Add(startup);
        Label privacy=LabelAt("啟用後會自動傳送新複製的圖片，可隨時暫停。\n圖片加密經 Cloudflare 傳給你配對的電腦；不需管理員權限。",28,423,554,51);privacy.Font=new Font(Font.FontFamily,9);
        LinkLabel policy=new LinkLabel { Text="隱私、安裝位置與移除方式",LinkColor=Color.FromArgb(121,199,255) };policy.SetBounds(28,480,450,25);
        policy.LinkClicked+=delegate { System.Diagnostics.Process.Start("https://github.com/evan6007/chrome-remote-desktop-image-paste/blob/main/docs/PRIVACY.md"); };Controls.Add(policy);
        progress.SetBounds(28,515,554,9);Controls.Add(progress);
        status.SetBounds(28,537,345,64);status.Text="版本 "+RuntimeSettings.Version+" · 個人使用者安裝";status.Font=new Font(Font.FontFamily,9);Controls.Add(status);
        install.Text=settingsOnly?"儲存並啟動":"安裝並啟動";install.SetBounds(390,544,192,46);
        install.BackColor=Color.FromArgb(40,115,155);install.FlatStyle=FlatStyle.Flat;install.ForeColor=Color.White;Controls.Add(install);
        install.Click+=async delegate { await Complete(); };AcceptButton=install;
        FormClosing+=delegate(object source,FormClosingEventArgs e) { if(working) e.Cancel=true; };
    }
    Label LabelAt(string text,int x,int y,int w,int h) { Label label=new Label { Text=text };label.SetBounds(x,y,w,h);Controls.Add(label);return label; }
    void Report(int percent,string text) { if(IsDisposed) return;BeginInvoke(new Action(delegate { progress.Value=Math.Max(0,Math.Min(100,percent));status.Text=text; })); }
    async Task Complete() {
        BridgeSettings proposed;
        try {
            proposed=receiver.Checked?new BridgeSettings { Receiver=true,Key=existing!=null&&existing.Receiver?existing.Key:BridgeSettings.NewKey(),Endpoint="" }:BridgeSettings.FromInvitation(invitation.Text);
        } catch(Exception e) { status.Text=e.Message;return; }
        proposed.AutoStart=startup.Checked;working=true;install.Enabled=false;receiver.Enabled=false;sender.Enabled=false;invitation.Enabled=false;startup.Enabled=false;
        try {
            status.Text=proposed.Receiver?"準備接收端網路元件…":"正在確認配對的接收端…";
            await Task.Run(delegate {
                if(proposed.Receiver) Installer.EnsureTunnel(Report); else NetworkSender.VerifyPair(proposed);
            });
            if(!configureOnly) Installer.Install(proposed);
            Result=proposed;working=false;DialogResult=DialogResult.OK;Close();
        } catch(Exception e) {
            working=false;install.Enabled=true;receiver.Enabled=true;sender.Enabled=true;invitation.Enabled=sender.Checked;startup.Enabled=true;
            status.Text="未完成："+(e is System.Net.WebException?"無法連線。確認接收端仍開著，再重試。":e.Message);
        }
    }
}

internal sealed class PairingDialog : Form {
    internal PairingDialog(string endpoint,Action reset) {
        AutoScaleMode=AutoScaleMode.None;Font=new Font("Microsoft JhengHei UI",10);Text="配對另一台電腦";ClientSize=new Size(580,305);
        StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
        Label title=new Label { Text="複製配對碼 → 在本機工具選「傳送圖片」後貼上。" };title.SetBounds(22,20,535,35);Controls.Add(title);
        TextBox text=new TextBox { Multiline=true,ReadOnly=true,Text=RuntimeSettings.Current.Invitation(endpoint),ScrollBars=ScrollBars.Vertical };text.SetBounds(22,67,535,84);Controls.Add(text);
        Label note=new Label { Text="配對碼含私人密鑰，不要公開。只要接收端未重啟，網址即可使用。\n若配對碼外洩，按「重設配對」讓舊碼失效。" };note.SetBounds(22,164,535,53);Controls.Add(note);
        Button copy=new Button { Text="複製配對碼" };copy.SetBounds(352,237,205,42);copy.Click+=delegate { try { Clipboard.SetText(text.Text);copy.Text="已複製，貼到本機工具"; } catch { copy.Text="剪貼簿忙碌，請重試"; } };Controls.Add(copy);
        Button revoke=new Button { Text="重設配對" };revoke.SetBounds(22,237,150,42);revoke.Click+=delegate {
            if(MessageBox.Show(this,"舊配對碼與舊傳送端將失效，需要重新配對。",Text,MessageBoxButtons.OKCancel,MessageBoxIcon.Question)==DialogResult.OK) { Close();reset(); }
        };Controls.Add(revoke);
    }
}
