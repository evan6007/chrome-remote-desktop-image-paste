# 安裝與使用

[下載 Windows 測試版](https://github.com/evan6007/chrome-remote-desktop-image-paste/releases/tag/v0.2.0-rc.1) → 選 **ChromeRemoteDesktopImagePaste-Setup.exe**。

**兩台都下載同一個 EXE，不用下載原始碼或裝外掛。** 目前未簽章，Windows 可能顯示未知發行者。

![三步設定](media/installation-zh.png)

1. **先在遠端電腦**開啟安裝檔，選「這台是遠端：接收圖片」→「安裝並啟動」。等「連線已確認」後，按「配對電腦」→「複製配對碼」。首次會下載約 53 MB 網路元件。
2. **回到手邊的電腦**開啟相同安裝檔，選「這台是本機：傳送圖片」，貼上完整配對碼 →「安裝並啟動」。若本機剪貼簿沒同步，可以用自己信任的方式傳送配對碼。
3. 手邊按 **Win+Shift+S** 重新截圖。等 **100%** 後，在遠端能接收圖片的程式按 **Ctrl+V**。

配對碼只交給自己的另一台電腦。**遠端工具重啟後，重新複製配對碼到本機工具即可。**

開機啟動可在安裝時勾選；平常按 X 會收至通知區。暫停、設定與結束都在工具選單。

[回首頁](../README.md) · [遇到問題](TROUBLESHOOTING.md) · [隱私與移除](PRIVACY.md)
