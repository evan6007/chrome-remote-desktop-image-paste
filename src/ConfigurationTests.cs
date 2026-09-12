using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

internal static class ConfigurationTests {
    static void Check(bool value,string name,List<string> results) { if(!value) throw new Exception("Test failed: "+name);results.Add("PASS "+name); }
    static bool Reject(Action action) { try { action();return false; } catch(InvalidDataException) { return true; } catch(FormatException) { return true; } catch(CryptographicException) { return true; } }
    internal static void Run(string output) {
        List<string> results=new List<string>();
        string binaryHash=Wire.Hash(File.ReadAllBytes(Application.ExecutablePath));
        BridgeSettings receiver=new BridgeSettings { Receiver=true,Key=BridgeSettings.NewKey(),Endpoint="",AutoStart=true };
        Check(receiver.Key!=BridgeSettings.NewKey(),"independent installations generate distinct random pairing secrets",results);
        string invitation=receiver.Invitation("https://example-tunnel.trycloudflare.com");
        BridgeSettings sender=BridgeSettings.FromInvitation(invitation);
        Check(!sender.Receiver && sender.Key==receiver.Key && sender.Endpoint=="https://example-tunnel.trycloudflare.com","same universal binary accepts receiver invitation at runtime",results);
        Check(!sender.AutoStart,"login startup is opt-in for a new installation",results);
        Check(BridgeSettings.FromInvitation(" \r\n"+invitation+"\r\n").Key==receiver.Key,"copied invitation tolerates surrounding whitespace",results);
        Check(Reject(delegate { BridgeSettings.FromInvitation("CRDIP2:invalid"); }),"truncated invitation rejected",results);
        Check(Reject(delegate { BridgeSettings.FromInvitation(new string('a',3000)); }),"oversized invitation rejected",results);
        foreach(string endpoint in new string[]{"http://example-tunnel.trycloudflare.com","https://example.com","https://example.trycloudflare.com.attacker.test","https://example.trycloudflare.com/a","https://user:pass@example.trycloudflare.com","https://example.trycloudflare.com:444","https://example.trycloudflare.com/?x=1","https://example.trycloudflare.com/#x"}) {
            string code="CRDIP2:"+Convert.ToBase64String(Encoding.UTF8.GetBytes("2\n"+receiver.Key+"\n"+endpoint));
            Check(Reject(delegate { BridgeSettings.FromInvitation(code); }),"invitation rejects untrusted endpoint shape "+results.Count,results);
        }
        Check(Reject(delegate { BridgeSettings.Decode("2\nreceiver\nnot-a-key\n\n0"); }),"short pairing secret rejected",results);
        RuntimeSettings.Save(receiver);BridgeSettings loaded=RuntimeSettings.Load();
        Check(loaded.Key==receiver.Key && loaded.Receiver && loaded.AutoStart,"receiver settings survive protected save and load",results);
        Check(!Encoding.UTF8.GetString(File.ReadAllBytes(RuntimeSettings.ConfigPath)).Contains(receiver.Key),"saved configuration does not contain the plaintext pairing key",results);
        File.WriteAllText(Path.Combine(RuntimeSettings.Root,"remote-endpoint.txt"),"old address");
        RuntimeSettings.Save(sender);loaded=RuntimeSettings.Load();
        Check(!loaded.Receiver && loaded.Key==receiver.Key && loaded.Endpoint==sender.Endpoint,"sender role and endpoint persist without rebuilding",results);
        Check(!File.Exists(Path.Combine(RuntimeSettings.Root,"remote-endpoint.txt")),"new pairing clears the old endpoint cache",results);
        byte[] encrypted=File.ReadAllBytes(RuntimeSettings.ConfigPath);encrypted[encrypted.Length-1]^=1;File.WriteAllBytes(RuntimeSettings.ConfigPath,encrypted);
        Check(Reject(delegate { RuntimeSettings.Load(); }),"tampered DPAPI configuration fails closed",results);
        RuntimeSettings.Save(receiver);RuntimeSettings.Current=receiver;
        Check(!Wire.Prefix.Contains(receiver.Key),"clipboard control prefix does not expose the raw pairing secret",results);
        string previousAuth=SecureImage.Auth;receiver.Key=BridgeSettings.NewKey();
        Check(SecureImage.Auth!=previousAuth && receiver.Key!=sender.Key,"reset pairing revokes previous transport credentials",results);
        Check(binaryHash==Wire.Hash(File.ReadAllBytes(Application.ExecutablePath)),"pairing and role changes never modify the executable",results);
        Check(System.Diagnostics.FileVersionInfo.GetVersionInfo(Application.ExecutablePath).FileVersion=="0.2.0.0","release binary has consistent publisher product and version metadata",results);
        // Exercise the actual installer with private test paths/registry keys,
        // never the user's app directory, Start Menu or Windows Run key.
        receiver.AutoStart=false;Installer.Install(receiver);
        Check(Wire.Hash(File.ReadAllBytes(Installer.Executable))==binaryHash,"installation preserves executable bytes for Authenticode",results);
        Check(File.Exists(Installer.Shortcut),"installer creates a usable shortcut in the isolated fixture",results);
        using(Microsoft.Win32.RegistryKey key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Installer.StartupKey)) Check(key.GetValue("RemoteImageBridge")==null,"installation leaves login startup off unless selected",results);
        using(Microsoft.Win32.RegistryKey key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Installer.UninstallKey)) Check((string)key.GetValue("DisplayVersion")==RuntimeSettings.Version,"Windows uninstall entry records the installed version",results);
        Installer.SetStartup(true);
        using(Microsoft.Win32.RegistryKey key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Installer.StartupKey)) Check(((string)key.GetValue("RemoteImageBridge")).Contains("--background"),"startup opt-in records the installed app path",results);
        string unrelated=Path.Combine(RuntimeSettings.Root,"unrelated-user-file.txt");File.WriteAllText(unrelated,"preserve");
        Installer.RemoveInstalled(false);
        Check(!File.Exists(Installer.Executable) && !File.Exists(RuntimeSettings.ConfigPath) && !File.Exists(Installer.Shortcut),"uninstaller removes executable settings and shortcut",results);
        Check(File.Exists(unrelated),"uninstaller preserves unrelated files in its directory",results);
        using(Microsoft.Win32.RegistryKey key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Installer.UninstallKey)) Check(key==null,"uninstaller removes its registration",results);
        File.AppendAllLines(output,results,Encoding.UTF8);
    }
    internal static void Transport(string output) {
        List<string> results=new List<string>();Installer.EnsureTunnel(delegate {});
        byte[] received=null;
        using(NetworkHost host=new NetworkHost(delegate(byte[] image,string id,DateTime created) { received=image;return true; },delegate {},delegate {},delegate {})) {
            host.Start();DateTime deadline=DateTime.UtcNow.AddSeconds(90);BridgeSettings sender=null;
            while(DateTime.UtcNow<deadline) {
                if(host.Endpoint!=null) {
                    sender=BridgeSettings.FromInvitation(RuntimeSettings.Current.Invitation(host.Endpoint));
                    try { NetworkSender.VerifyPair(sender);break; } catch(System.Net.WebException) { sender=null; }
                }
                System.Threading.Thread.Sleep(1000);
            }
            Check(sender!=null,"runtime invitation verifies the remote endpoint over real HTTPS",results);
            BridgeSettings wrong=new BridgeSettings { Receiver=false,Key=BridgeSettings.NewKey(),Endpoint=sender.Endpoint };
            bool rejected=false;try { NetworkSender.VerifyPair(wrong); } catch(System.Net.WebException) { rejected=true; }
            Check(rejected,"wrong pairing key is rejected by the live transport",results);
            byte[] png;using(Bitmap bitmap=new Bitmap(48,32)) {
                using(Graphics graphics=Graphics.FromImage(bitmap)) graphics.Clear(Color.CornflowerBlue);
                using(MemoryStream stream=new MemoryStream()) { bitmap.Save(stream,ImageFormat.Png);png=stream.ToArray(); }
            }
            bool completed=false;NetworkSender.Upload(sender.Endpoint,png,false,delegate(int p,string a,string b,bool done,bool error) { if(p==100 && done && !error) completed=true; });
            Check(received!=null && Wire.Hash(received)==Wire.Hash(png) && completed,"real HTTPS image round trip and authenticated receipt preserve bytes",results);
        }
        File.WriteAllLines(output,results,Encoding.UTF8);
    }
    internal static void Render(string output) {
        Exception failure=null;
        System.Threading.Thread worker=new System.Threading.Thread(delegate() { try { RenderOnDesktop(output); } catch(Exception e) { failure=e; } });
        worker.SetApartmentState(System.Threading.ApartmentState.STA);worker.Start();worker.Join();
        if(failure!=null) throw new Exception("UI render failed",failure);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void RenderOnDesktop(string output) {
        // A separate, never activated desktop lets WinForms render its real controls
        // without displaying any window on the interactive desktop. No clipboard calls.
        IntPtr desktop=Native.CreateDesktop("CrdUi-"+Guid.NewGuid().ToString("N"),IntPtr.Zero,IntPtr.Zero,0,0x000F01FF,IntPtr.Zero);
        if(desktop==IntPtr.Zero || !Native.SetThreadDesktop(desktop)) throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string root=Path.GetDirectoryName(Path.GetFullPath(output));Directory.CreateDirectory(root);
        RenderForm(new SetupWizard(null),Path.Combine(root,"setup-receiver.png"));
        BridgeSettings sender=new BridgeSettings { Receiver=false,Key=BridgeSettings.NewKey(),Endpoint="https://example-tunnel.trycloudflare.com" };
        RenderForm(new SetupWizard(sender,true),Path.Combine(root,"setup-sender.png"));
        RenderForm(new PairingDialog(sender.Endpoint,delegate {}),Path.Combine(root,"pairing-dialog.png"));
        File.WriteAllText(output,"PASS setup and pairing forms render without displaying windows or changing the clipboard");
    }
    static void RenderForm(Form form,string path) {
        using(form) {
            form.Show();Application.DoEvents();
            using(Bitmap bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(path,ImageFormat.Png); }
            form.Close();
        }
    }
}
