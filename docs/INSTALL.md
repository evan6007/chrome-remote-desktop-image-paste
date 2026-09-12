# Install and use

[Download the Windows release candidate](https://github.com/evan6007/chrome-remote-desktop-image-paste/releases/tag/v0.2.0-rc.1) and choose **ChromeRemoteDesktopImagePaste-Setup.exe**.

Install the same EXE on both PCs. No source build or Chrome extension is needed. This build is unsigned and may trigger a Windows warning. The current setup interface is in Traditional Chinese.

![Three setup steps](media/installation-en.png)

1. **Remote PC:** choose 接收圖片 (Receiver), then 安裝並啟動 (Install and start). Wait for connection verified. Choose 配對電腦 (Pair computer) → 複製配對碼 (Copy pairing code). First installation downloads about 53 MB of network components.
2. **Local PC:** choose 傳送圖片 (Sender), paste the complete pairing code, then install. Use your trusted transfer method if CRD text clipboard sync is unavailable.
3. Take a **new Win+Shift+S screenshot** locally. Wait for **100%**, then **Ctrl+V** into an image-capable app on the remote PC.

Keep pairing codes private. If the receiver restarts, copy its new code into the sender again.

Login startup is optional. Closing the window minimizes to the tray; use the app menu to pause, configure or quit.

[Home](../README.md) · [Help](TROUBLESHOOTING.md) · [Privacy and uninstall](PRIVACY.md)
