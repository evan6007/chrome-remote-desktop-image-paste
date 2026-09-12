# Privacy and installation / 隱私與安裝

- **傳什麼：**只有工具啟用後新複製的圖片，自動加密傳給你指定的接收端；不分析圖片內容，不蒐集一般文字，不記錄按鍵內容。按暫停可停止新的傳送；正在傳的一張可能完成，完全停止請結束工具。
- **經過哪裡：**網路通道使用 Cloudflare Quick Tunnel。Cloudflare 看得到連線中繼資訊，圖片內容另以兩端共有的密鑰加密。服務本身仍受 [Cloudflare 隱私政策](https://www.cloudflare.com/privacypolicy/) 約束。
- **存在哪裡：**圖片只放在記憶體和接收端 Windows 剪貼簿。Windows 剪貼簿歷程、目標程式或備份軟體可能另外保存；本工具不控制它們。配對資料以 Windows DPAPI 保護，保存在目前使用者的 `%LOCALAPPDATA%\RemoteImageBridge\settings.dpapi`。同一 Windows 帳號下的程式不在這個保護邊界之外。
- **配對碼：**含私人密鑰與接收端網址，僅交給自己要配對的電腦。它沒有自動到期功能；外洩時應在接收端「重設配對」，不要只重開程式。
- **系統變更：**只安裝在目前使用者的 `%LOCALAPPDATA%\RemoteImageBridge`，建立開始選單捷徑及「已安裝的應用程式」項目。登入啟動預設不勾選；不需要管理員，不修改防火牆、不安裝服務或根憑證。
- **移除：**Windows 設定 → 應用程式 → 已安裝的應用程式 → **Chrome Remote Desktop Image Paste** → 解除安裝。會停止工具並移除配對資料、工具檔案與登入啟動；最初下載的安裝檔可自行刪除。

English: while enabled, this helper sends newly copied images to the receiver selected by its operator. Payloads are encrypted before passing through Cloudflare. It does not collect ordinary copied text, log keystrokes, retain image files or send analytics. Metadata logs stay local. Pairing credentials are protected with current-user Windows DPAPI; software running as the same Windows user is outside that protection boundary. Windows clipboard history and target apps can independently retain pasted content. Installation is per-user, login startup is opt-in, and uninstall is available in Windows Settings.

[回首頁](../README.md) · [Technical security details](../SECURITY.md)
