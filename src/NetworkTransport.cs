using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

internal static class SecureImage {
    static byte[] Key(string purpose) { using(SHA256 s=SHA256.Create()) return s.ComputeHash(Encoding.UTF8.GetBytes(purpose+":"+Pair.Key)); }
    internal static string Auth { get { return Convert.ToBase64String(Key("image-http-auth-v4")); } }
    internal static string Proof(string value) { using(HMACSHA256 h=new HMACSHA256(Key("image-proof-v4"))) return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(value))); }
    static bool Equal(byte[] a,byte[] b) { if(a.Length!=b.Length) return false;int d=0;for(int i=0;i<a.Length;i++) d|=a[i]^b[i];return d==0; }
    internal static bool EqualText(string a,string b) { return Equal(Encoding.UTF8.GetBytes(a??""),Encoding.UTF8.GetBytes(b??"")); }
    internal static byte[] Encrypt(byte[] png,string id,bool probe) {
        byte[] plain;
        using(MemoryStream m=new MemoryStream()) using(BinaryWriter w=new BinaryWriter(m)) {
            w.Write(DateTime.UtcNow.Ticks);w.Write(new Guid(id).ToByteArray());w.Write(probe);w.Write(png.Length);w.Write(png);w.Flush();plain=m.ToArray();
        }
        using(Aes aes=Aes.Create()) {
            aes.Key=Key("image-encryption-v4");aes.GenerateIV();aes.Mode=CipherMode.CBC;aes.Padding=PaddingMode.PKCS7;
            byte[] cipher;using(ICryptoTransform enc=aes.CreateEncryptor()) cipher=enc.TransformFinalBlock(plain,0,plain.Length);
            byte[] result=new byte[16+cipher.Length+32];Buffer.BlockCopy(aes.IV,0,result,0,16);Buffer.BlockCopy(cipher,0,result,16,cipher.Length);
            using(HMACSHA256 mac=new HMACSHA256(Key("image-mac-v4"))) Buffer.BlockCopy(mac.ComputeHash(result,0,result.Length-32),0,result,result.Length-32,32);
            return result;
        }
    }
    internal static byte[] Decrypt(byte[] body,out string id,out bool probe,out DateTime created) {
        if(body.Length<80 || body.Length>Wire.MaxBytes+256) throw new InvalidDataException("Envelope size invalid");
        byte[] actual=new byte[32];Buffer.BlockCopy(body,body.Length-32,actual,0,32);
        using(HMACSHA256 mac=new HMACSHA256(Key("image-mac-v4"))) if(!Equal(actual,mac.ComputeHash(body,0,body.Length-32))) throw new InvalidDataException("Envelope authentication failed");
        using(Aes aes=Aes.Create()) {
            aes.Key=Key("image-encryption-v4");byte[] iv=new byte[16];Buffer.BlockCopy(body,0,iv,0,16);aes.IV=iv;
            aes.Mode=CipherMode.CBC;aes.Padding=PaddingMode.PKCS7;
            byte[] plain;using(ICryptoTransform dec=aes.CreateDecryptor()) plain=dec.TransformFinalBlock(body,16,body.Length-48);
            using(MemoryStream m=new MemoryStream(plain)) using(BinaryReader r=new BinaryReader(m)) {
                created=new DateTime(r.ReadInt64(),DateTimeKind.Utc);id=new Guid(r.ReadBytes(16)).ToString("N");probe=r.ReadBoolean();int length=r.ReadInt32();
                if(Math.Abs((DateTime.UtcNow-created).TotalMinutes)>5 || length<1 || length>Wire.MaxBytes || m.Length-m.Position!=length) throw new InvalidDataException("Envelope metadata invalid");
                byte[] png=r.ReadBytes(length);Wire.ValidatePng(png);return png;
            }
        }
    }
    internal static bool ValidEndpoint(string url) {
        Uri parsed;return Uri.TryCreate(url,UriKind.Absolute,out parsed) && parsed.Scheme=="https" && parsed.Host.EndsWith(".trycloudflare.com",StringComparison.OrdinalIgnoreCase) && parsed.Port==443 && parsed.AbsolutePath=="/" && parsed.UserInfo=="" && parsed.Query=="" && parsed.Fragment=="";
    }
}

internal sealed class NetworkHost : IDisposable {
    readonly Func<byte[],string,DateTime,bool> accept;
    readonly Action<int,string,string,bool,bool> progress;
    readonly Action<string> endpointReady;
    readonly Action<string> log;
    readonly Dictionary<string,string> completed=new Dictionary<string,string>();
    readonly object gate=new object();
    TcpListener listener;Process tunnel;bool stopped;int active;
    internal string Endpoint;
    internal NetworkHost(Func<byte[],string,DateTime,bool> acceptImage,Action<int,string,string,bool,bool> report,Action<string> ready,Action<string> logger) {
        accept=acceptImage;progress=report;endpointReady=ready;log=logger;
    }
    internal void Start() {
        listener=new TcpListener(IPAddress.Loopback,0);listener.Start(8);int port=((IPEndPoint)listener.LocalEndpoint).Port;
        ThreadPool.QueueUserWorkItem(delegate { while(!stopped) { try { TcpClient client=listener.AcceptTcpClient();if(Interlocked.Increment(ref active)>4) { Interlocked.Decrement(ref active);client.Close();continue; } ThreadPool.QueueUserWorkItem(delegate { try { Handle(client); } catch(Exception e) { log("http-error "+e.GetType().Name); } finally { client.Close();Interlocked.Decrement(ref active); } }); } catch { if(!stopped) Thread.Sleep(100); } } });
        string executable=Path.Combine(Bridge.Root,"cloudflared.exe");if(!File.Exists(executable)) throw new FileNotFoundException("遠端transport component missing");
        ProcessStartInfo start=new ProcessStartInfo(executable,"tunnel --no-autoupdate --protocol http2 --url http://127.0.0.1:"+port);
        start.UseShellExecute=false;start.CreateNoWindow=true;start.WindowStyle=ProcessWindowStyle.Hidden;start.RedirectStandardOutput=true;start.RedirectStandardError=true;
        tunnel=new Process();tunnel.StartInfo=start;tunnel.EnableRaisingEvents=true;
        DataReceivedEventHandler output=delegate(object sender,DataReceivedEventArgs e) {
            if(e.Data==null) return;
            Match match=Regex.Match(e.Data,@"https://[a-z0-9-]+\.trycloudflare\.com");
            if(match.Success && Endpoint==null) { Endpoint=match.Value;File.WriteAllText(Path.Combine(Bridge.Root,"endpoint.txt"),Endpoint);endpointReady(Endpoint); }
            if(e.Data.IndexOf("Registered tunnel connection",StringComparison.Ordinal)>=0) log("encrypted-tunnel-connected");
        };
        tunnel.OutputDataReceived+=output;tunnel.ErrorDataReceived+=output;
        tunnel.Exited+=delegate { if(!stopped) { Endpoint=null;progress(0,"圖片連線已中斷","請重新啟動 遠端圖片橋接工具",false,true); } };
        tunnel.Start();tunnel.BeginOutputReadLine();tunnel.BeginErrorReadLine();log("encrypted-http-listener-ready");
    }
    void Handle(TcpClient client) {
        client.ReceiveTimeout=15000;client.SendTimeout=15000;
        using(NetworkStream stream=client.GetStream()) {
            List<byte> header=new List<byte>();
            while(header.Count<16384) { int b=stream.ReadByte();if(b<0) return;header.Add((byte)b);int n=header.Count;if(n>=4 && header[n-4]==13 && header[n-3]==10 && header[n-2]==13 && header[n-1]==10) break; }
            string[] lines=Encoding.ASCII.GetString(header.ToArray()).Split(new string[]{"\r\n"},StringSplitOptions.None);
            string[] first=lines[0].Split(' ');if(first.Length!=3) { Reply(stream,400,"Bad request");return; }
            string auth=null;int length=-1;bool quietProbe=false;
            foreach(string line in lines) { int p=line.IndexOf(':');if(p<0) continue;string key=line.Substring(0,p).Trim(),value=line.Substring(p+1).Trim();if(key.Equals("Authorization",StringComparison.OrdinalIgnoreCase)) auth=value;if(key.Equals("Content-Length",StringComparison.OrdinalIgnoreCase)) int.TryParse(value,out length);if(key.Equals("X-Image-Probe",StringComparison.OrdinalIgnoreCase)) quietProbe=value=="1"; }
            if(!SecureImage.EqualText(auth,"Bearer "+SecureImage.Auth)) { Reply(stream,403,"Forbidden");return; }
            if(first[0]=="GET" && first[1].StartsWith("/health/",StringComparison.Ordinal)) {
                string nonce=first[1].Substring(8);Guid unused;if(!Guid.TryParseExact(nonce,"N",out unused)) { Reply(stream,400,"Bad nonce");return; }
                Reply(stream,200,"RemoteImageBridge:"+SecureImage.Proof(nonce));return;
            }
            if(first[0]!="POST" || first[1]!="/image" || length<80 || length>Wire.MaxBytes+256) { Reply(stream,400,"Bad image request");return; }
            byte[] body=new byte[length];int offset=0,shown=-1;
            while(offset<length) { int n=stream.Read(body,offset,Math.Min(65536,length-offset));if(n==0) throw new EndOfStreamException();offset+=n;int percent=(int)(85L*offset/length);if(percent!=shown) { shown=percent;if(!quietProbe) progress(percent,"正在接收加密圖片",(offset/1048576.0).ToString("0.0")+" / "+(length/1048576.0).ToString("0.0")+" MB",false,false); } }
            string id;bool probe;DateTime created;byte[] png;
            try { png=SecureImage.Decrypt(body,out id,out probe,out created); } catch { Reply(stream,400,"Image verification failed");return; }
            if(probe!=quietProbe) { Reply(stream,400,"Operation mismatch");return; }
            string digest=Wire.Hash(png),ack=id+":"+digest+":"+SecureImage.Proof(id+":"+digest);
            lock(gate) {
                string old;if(completed.TryGetValue(id,out old)) { Reply(stream,200,old);return; }
                if(!probe && !accept(png,id,created)) { Reply(stream,409,"Clipboard unavailable or superseded");return; }
                if(completed.Count>=64) completed.Clear();completed[id]=ack;
            }
            log((probe?"probe":"image")+"-received bytes="+png.Length);
            if(!probe) progress(100,"圖片已收到","已放入遠端剪貼簿，可以按 Ctrl+V",true,false);
            Reply(stream,200,ack);
        }
    }
    static void Reply(Stream stream,int code,string text) {
        byte[] body=Encoding.UTF8.GetBytes(text);byte[] header=Encoding.ASCII.GetBytes("HTTP/1.1 "+code+" "+(code==200?"OK":"Error")+"\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: "+body.Length+"\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");stream.Write(header,0,header.Length);stream.Write(body,0,body.Length);
    }
    public void Dispose() { stopped=true;if(listener!=null) listener.Stop();if(tunnel!=null) { try { if(!tunnel.HasExited) tunnel.Kill(); } catch {} tunnel.Dispose(); } }
}

internal static class NetworkSender {
    static HttpWebRequest Request(string url,string method) {
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
        HttpWebRequest request=(HttpWebRequest)WebRequest.Create(url);request.Method=method;request.Timeout=20000;request.ReadWriteTimeout=15000;
        request.AllowAutoRedirect=false;request.Headers["Authorization"]="Bearer "+SecureImage.Auth;request.ServicePoint.Expect100Continue=false;request.KeepAlive=true;return request;
    }
    internal static void Health(string endpoint) {
        if(!SecureImage.ValidEndpoint(endpoint)) throw new InvalidDataException("Invalid endpoint");string nonce=Guid.NewGuid().ToString("N");
        HttpWebRequest request=Request(endpoint+"/health/"+nonce,"GET");
        using(WebResponse response=request.GetResponse()) using(StreamReader r=new StreamReader(response.GetResponseStream())) {
            if(!SecureImage.EqualText(r.ReadToEnd(),"RemoteImageBridge:"+SecureImage.Proof(nonce))) throw new InvalidDataException("Peer verification failed");
        }
    }
    internal static long Upload(string endpoint,byte[] png,bool probe,Action<int,string,string,bool,bool> progress) {
        if(!SecureImage.ValidEndpoint(endpoint)) throw new InvalidDataException("Invalid endpoint");
        string id=Guid.NewGuid().ToString("N");Stopwatch watch=Stopwatch.StartNew();byte[] encrypted=SecureImage.Encrypt(png,id,probe);
        HttpWebRequest request=Request(endpoint+"/image","POST");request.ContentType="application/octet-stream";request.ContentLength=encrypted.Length;request.AllowWriteStreamBuffering=false;
        if(probe) request.Headers["X-Image-Probe"]="1";
        progress(0,"正在傳送加密圖片","正在連線到遠端",false,false);
        using(Stream stream=request.GetRequestStream()) { for(int offset=0;offset<encrypted.Length;) { int n=Math.Min(65536,encrypted.Length-offset);stream.Write(encrypted,offset,n);offset+=n;progress((int)(90L*offset/encrypted.Length),"正在傳送圖片到遠端",(offset/1048576.0).ToString("0.0")+" / "+(encrypted.Length/1048576.0).ToString("0.0")+" MB",false,false); } }
        progress(95,"等待遠端確認","圖片已送出，正在驗證並放入剪貼簿",false,false);
        using(WebResponse response=request.GetResponse()) using(StreamReader reader=new StreamReader(response.GetResponseStream())) {
            string proof=id+":"+Wire.Hash(png);if(!SecureImage.EqualText(reader.ReadToEnd(),proof+":"+SecureImage.Proof(proof))) throw new InvalidDataException("Receipt verification failed");
        }
        watch.Stop();progress(100,"圖片已到遠端","可直接 Ctrl+V · "+(watch.ElapsedMilliseconds/1000.0).ToString("0.00")+" 秒",true,false);return watch.ElapsedMilliseconds;
    }
}
