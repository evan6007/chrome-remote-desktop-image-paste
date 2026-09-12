using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

internal sealed class BridgeSettings {
    internal string Key, Endpoint;
    internal bool Receiver, AutoStart;
    internal static string NewKey() {
        byte[] bytes=new byte[32];
        using(RandomNumberGenerator rng=RandomNumberGenerator.Create()) rng.GetBytes(bytes);
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
    internal void Validate() {
        if(Key==null || !Regex.IsMatch(Key,"\\A[0-9a-f]{64}\\z")) throw new InvalidDataException("配對資料格式不正確。");
        if(!Receiver && !SecureImage.ValidEndpoint(Endpoint)) throw new InvalidDataException("接收端網址不正確，請重新複製配對碼。");
        if(Receiver && !string.IsNullOrEmpty(Endpoint)) throw new InvalidDataException("Receiver configuration must not contain a cached endpoint.");
    }
    internal string Encode() {
        Validate();
        return "2\n"+(Receiver?"receiver":"sender")+"\n"+Key+"\n"+(Endpoint??"")+"\n"+(AutoStart?"1":"0");
    }
    internal static BridgeSettings Decode(string text) {
        string[] parts=text.Split('\n');
        if(parts.Length!=5 || parts[0]!="2" || (parts[1]!="receiver" && parts[1]!="sender") || (parts[4]!="0" && parts[4]!="1")) throw new InvalidDataException("設定檔版本或格式不正確。");
        BridgeSettings value=new BridgeSettings { Receiver=parts[1]=="receiver",Key=parts[2],Endpoint=parts[3],AutoStart=parts[4]=="1" };
        value.Validate();return value;
    }
    // The invitation is a secret, not a public device ID. It grants access until the receiver resets its key.
    internal string Invitation(string endpoint) {
        if(!Receiver || !SecureImage.ValidEndpoint(endpoint)) throw new InvalidOperationException("請先等接收端連線完成。");
        return "CRDIP2:"+Convert.ToBase64String(Encoding.UTF8.GetBytes("2\n"+Key+"\n"+endpoint));
    }
    internal static BridgeSettings FromInvitation(string text) {
        text=(text??"").Trim();
        if(text.Length>2048 || !text.StartsWith("CRDIP2:",StringComparison.Ordinal)) throw new InvalidDataException("請貼上接收端產生的完整配對碼（CRDIP2: 開頭）。");
        try {
            string[] parts=new UTF8Encoding(false,true).GetString(Convert.FromBase64String(text.Substring(7))).Split('\n');
            if(parts.Length!=3 || parts[0]!="2") throw new InvalidDataException("配對碼格式不正確。");
            BridgeSettings result=new BridgeSettings { Receiver=false,Key=parts[1],Endpoint=parts[2],AutoStart=false };
            result.Validate();return result;
        } catch(FormatException) { throw new InvalidDataException("配對碼不完整，請重新複製。"); }
    }
}

internal static class RuntimeSettings {
    internal const string Version="0.2.0";
    internal const string Product="Chrome Remote Desktop Image Paste";
    internal static BridgeSettings Current;
    internal static string TestRoot;
    internal static string Root { get { return TestRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RemoteImageBridge"); } }
    static readonly byte[] Entropy=Encoding.UTF8.GetBytes("ChromeRemoteDesktopImagePaste.settings.v2");
    internal static string ConfigPath { get { return Path.Combine(Root,"settings.dpapi"); } }
    internal static BridgeSettings Load() {
        if(!File.Exists(ConfigPath)) return null;
        byte[] encrypted=File.ReadAllBytes(ConfigPath);
        if(encrypted.Length>8192) throw new InvalidDataException("設定檔大小不正確。");
        byte[] plain=ProtectedData.Unprotect(encrypted,Entropy,DataProtectionScope.CurrentUser);
        try { return BridgeSettings.Decode(new UTF8Encoding(false,true).GetString(plain)); }
        finally { Array.Clear(plain,0,plain.Length); }
    }
    internal static void Save(BridgeSettings value) {
        byte[] plain=Encoding.UTF8.GetBytes(value.Encode()),encrypted;
        try { encrypted=ProtectedData.Protect(plain,Entropy,DataProtectionScope.CurrentUser); }
        finally { Array.Clear(plain,0,plain.Length); }
        Directory.CreateDirectory(Root);
        string temporary=Path.Combine(Root,"settings-"+Guid.NewGuid().ToString("N")+".tmp");
        try {
            File.WriteAllBytes(temporary,encrypted);
            if(File.Exists(ConfigPath)) File.Replace(temporary,ConfigPath,null); else File.Move(temporary,ConfigPath);
            foreach(string name in new string[]{"remote-endpoint.txt","endpoint.txt"}) {
                string old=Path.Combine(Root,name);if(File.Exists(old)) File.Delete(old);
            }
        } finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
}

internal static class Pair {
    internal static string Key { get { if(RuntimeSettings.Current==null) throw new InvalidOperationException("Pairing is not configured.");return RuntimeSettings.Current.Key; } }
    internal static bool IsReceiver { get { return RuntimeSettings.Current.Receiver; } }
}
internal static class EndpointConfig {
    internal static string Default { get { return RuntimeSettings.Current.Endpoint??""; } }
}
