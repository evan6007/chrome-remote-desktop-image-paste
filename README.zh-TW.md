# Remote Image Bridge：遠端圖片剪貼簿橋接

[English](README.md)

在一台 Windows 電腦截圖，透過 Chrome 遠端桌面操作另一台電腦時，直接在另一台按 Ctrl+V 貼上圖片。

這是 **MIT 授權的 Windows 桌面工具原始碼**，目前為實驗版，介面為繁體中文。不是 Chrome 擴充功能，也不是 Google 或 Cloudflare 官方產品；自行下載原始碼、建置與使用不需要商店帳號。

## 可以做什麼

- Win+Shift+S 截圖或複製新圖片後，自動傳送到配對的電腦。
- 圖片先加密，再透過獨立 HTTPS 連線傳送，每張圖不必反覆點 Google Remote 才繼續。
- 接收端放入 Windows PNG／DIB 剪貼簿，讓相容軟體以 Ctrl+V 貼上。
- 有固定狀態視窗、進度條及暫停功能；傳送端 100% 代表接收端已確認放入剪貼簿。
- 不控制滑鼠、不搶其他視窗焦點、不更動注音或鍵盤設定；一般文字沿用 Google Remote 原本的同步功能。

## 安裝自己的兩台電腦

目前先提供原始碼，每組電腦自行建置兩個配對安裝檔。**請在接收端電腦，用同一份專案完成兩端的建置。** 需要 Windows、PowerShell 及 .NET Framework 4.8；不需要 Visual Studio 或 NuGet。開發環境為 Windows 11，其他 Windows 版本尚未驗證。

### 1. 接收端建置

下載原始碼 ZIP 或 git clone，解壓縮後在專案資料夾開 PowerShell：

```powershell
.\scripts\Build.ps1 -Role Receiver -SelfTest
.\scripts\Install-Tunnel.ps1
```

第二行會從 Cloudflare 官方 GitHub 下載 tunnel 程式，確認 SHA-256 與 Windows 簽章。接著雙擊 `dist\RemoteImageBridge-Receiver.exe`，等視窗顯示「連線已確認」。

工具只安裝到目前使用者的 `%LOCALAPPDATA%\RemoteImageBridge`，並加入使用者登入自動啟動，不建立 Windows 系統服務、不更動防火牆。若組織限制 PowerShell 腳本執行，請依組織規定處理；此專案不要求更動全機執行原則。

### 2. 仍在接收端，建置傳送端

在同一份專案執行：

```powershell
.\scripts\Build.ps1 -Role Sender -UseRunningReceiver -SelfTest
```

它會讀取目前接收端的網址，並使用第一次建置時產生的私人配對密鑰。

### 3. 傳送端安裝

透過 Google Remote 的「下載檔案」，把 `dist\RemoteImageBridge-Sender.exe` 下載到你自己的傳送端電腦，再在那台雙擊。等「連線已確認」，重新截一張圖；看到「圖片已到遠端」與 100% 後，就能在接收端貼上。

**每組建置的 EXE 都包含私人配對密鑰，只能交給自己的配對電腦。** 分享給朋友時請分享這個 GitHub 專案，讓朋友自行產生另一組；不要公開你的 `dist/`、`.private/` 或整個工作資料夾 ZIP。

## 接收端重開後

目前使用的 Cloudflare Quick Tunnel 每次重啟會更換網址。在傳送端按「重新配對」，切回遠端畫面讓短控制訊息同步；必要時在接收端也按「重新配對」，再切回傳送端。

若仍無法配對，可以在原專案重跑 `Build.ps1 -Role Sender -UseRunningReceiver`，再更新傳送端。如果傳送端保留了舊配對網址，先結束工具，只移除傳送端 `%LOCALAPPDATA%\RemoteImageBridge\remote-endpoint.txt`，再開工具。

配對階段可能需要切換視窗；**圖片傳輸本身不再走 Google Remote 的文字分段通道**。

## 目前限制

- 實驗版，正式公開分支的跨機截圖與貼上仍需要實機驗證，不能把單機測試當成已完成。
- 單向傳圖，每個 Windows 使用者目前只支援一组配對。單張最多 24 MiB／3600 萬像素。
- 工具啟用期間，新複製的圖片都會自動傳送；不想傳時可暫停。正在傳的一張可能會完成，完全停止可結束工具。
- 不會自動傳送啟動前已存在的圖片；請重新截圖或複製。
- 關閉主視窗會收至通知區；通知區可「停止並取消開機啟動」。
- 圖片不存入檔案，僅在記憶體及 Windows 剪貼簿處理；診斷紀錄不包含圖片或一般文字內容，但有時間、大小、狀態等資訊。
- 兩台時鐘需要相差少於五分鐘。
- Quick Tunnel 官方只定位為測試與開發用途，沒有穩定性保證；本專案沒有提供代管服務。[Cloudflare 說明](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/do-more-with-tunnels/trycloudflare/)

完整技術與驗證界線見 [英文 README](README.md)。私人資訊處理見 [SECURITY.md](SECURITY.md)。歡迎回報問題與送 PR；請勿附上私人配對 EXE、密鑰或未清理的日誌。
