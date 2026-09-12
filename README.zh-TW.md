# Google Chrome 遠端桌面圖片貼上工具

**本機截圖，遠端直接 Ctrl+V 貼上。**

**[下載最新原始碼 ZIP](https://github.com/evan6007/chrome-remote-desktop-image-paste/archive/refs/heads/main.zip)** · **[完整圖解安裝教學](docs/INSTALL.zh-TW.md)** · [English](README.md)

在本機按 Win+Shift+S 截圖，透過 Google Chrome 遠端桌面操作另一台電腦時，直接在遠端按 Ctrl+V 貼上圖片，不用先存檔再傳檔。

![本機截圖，遠端接收與貼上](docs/media/overview-zh.png)

圖由 [Skechu](https://evan6007.github.io/skechu-ppt/) 繪製，附上 [可編輯 SKC 原稿](docs/media/image-paste-guide.skc)；可自行修改文字、方塊與箭頭。

## 先看兩台各要做什麼

| 電腦 | 身分 | 操作 |
| --- | --- | --- |
| 你人坐在前面的家裡電腦／筆電 | 本機／傳送端 | 截圖，安裝產生的 Sender EXE |
| Google Remote 畫面裡被你控制的電腦 | 遠端／接收端 | 先建置、安裝接收端，再建立 Sender；之後在這裡貼上 |

## 第一次安裝

1. **先在遠端電腦**下載本專案 ZIP，按右鍵「全部解壓縮」。
2. 雙擊 **`1-Install-Receiver.cmd`**，等接收端視窗顯示「連線已確認」。
3. **仍在遠端、同一個資料夾**，雙擊 **`2-Create-Sender.cmd`**。
4. Google Remote 側邊欄選「下載檔案」，把 `dist\RemoteImageBridge-Sender.exe` 下載到本機。
5. **回到本機電腦**雙擊 Sender EXE，等「連線已確認」。
6. 本機重新按 **Win+Shift+S** 截圖，等 **100%** 後，在遠端目標程式按 **Ctrl+V**。

**[第一次使用請看完整教學 →](docs/INSTALL.zh-TW.md)** 每一步都有在哪台操作、會出現什麼、下一步按哪裡，並包含重新配對、排錯、更新與移除。

![第一次安裝的六個步驟](docs/media/installation-zh.png)

## 安裝前須知

- Windows 原始碼實驗版，開發環境為 Windows 11；需要 .NET Framework 4.8，腳本會自動建置，不需要 Visual Studio 或 Git。
- 目前不是 Chrome 擴充功能，也不是 Google 或 Cloudflare 官方產品。
- **目前沒有公開受信任的程式碼簽章，不能保證下載或執行時不出現 Windows 提示。** 兩個啟動腳本方便測試，不等同正式簽章安裝檔。[Code signing policy／簽章現況](docs/CODE-SIGNING.md)
- 每組配對的 EXE 都包含自己的密鑰。分享給朋友時分享 GitHub 原始碼，讓朋友建立另一組；不要公開自己的配對 EXE、`.private` 或工作資料夾 ZIP。
- 接收端第一次安裝需要下載官方 Cloudflare 網路元件。兩台都要能上網。

## 每次使用

**兩端工具執行中且未暫停 → 本機截圖 → 等 100% → 遠端 Ctrl+V。**

圖片不必先存成檔案，也不必每張手動傳檔。只能貼文字的輸入框不會因此支援圖片。工具啟用時新複製的圖片會自動傳送，不想傳時可以暫停；按 X 會收至通知區。

接收端工具重啟後，臨時網址會改變，按「重新配對」並依視窗提示切換一次遠端畫面；[完整配對步驟](docs/INSTALL.zh-TW.md#遠端重開機或工具重啟後如何重新配對)。

## 目前限制與驗證

- 目前為單向傳圖，每個 Windows 使用者只支援一組配對；圖片最多 24 MiB／3600 萬像素。
- 圖片在記憶體處理，未寫入圖片檔或一般文字日誌；詳見 [隱私與安全說明](SECURITY.md)。
- 兩台時鐘需要相差少於五分鐘。Cloudflare Quick Tunnel 是開發測試通道，沒有長期穩定性保證。
- 建置與自我測試通過不等於所有兩台實機、網路與貼上目標都已驗證。[完整技術與測試範圍](README.md#validation-and-current-status)

## 開源授權

MIT 授權，歡迎使用與改進。[LICENSE](LICENSE) · [第三方元件](THIRD_PARTY_NOTICES.md) · [回報問題](https://github.com/evan6007/chrome-remote-desktop-image-paste/issues)
