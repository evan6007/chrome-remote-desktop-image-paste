# Installation and everyday use

**Google Chrome Remote Desktop Image Paste: screenshot locally, Ctrl+V remotely.**

[Project home](../README.md) · [繁體中文完整教學](INSTALL.zh-TW.md) · **[Download latest source ZIP](https://github.com/evan6007/chrome-remote-desktop-image-paste/archive/refs/heads/main.zip)**

This guide starts from a source download. You do not need Git, Visual Studio, Node.js or a Chrome extension. This is an experimental Windows source release: two double-click launchers compile a private pair using Windows' included compiler. It is **not yet a signed, general-purpose installer**; see [Code signing policy and current status](CODE-SIGNING.md).

## Which computer is which?

| Name | Meaning | Job |
| --- | --- | --- |
| **Local / Sender** | The computer at your desk, whose physical keyboard you are using; e.g. your home PC or laptop | Capture with Win+Shift+S and run the Sender EXE |
| **Remote / Receiver** | The other computer shown inside Chrome Remote Desktop; e.g. your office/lab PC | Build both roles, run the Receiver, then paste images with Ctrl+V |

If you are at home controlling an office PC, complete **steps 1–3 inside the remote office desktop**, then download the generated Sender to your home computer.

![Local screenshot, encrypted transfer, remote paste](media/overview-en.png)

Drawn in [Skechu](https://evan6007.github.io/skechu-ppt/). [Editable SKC project](media/image-paste-guide.skc) · [SVG](media/overview-en.svg). These are explanatory diagrams, not screenshots proving a live connection.

## Requirements

- Two Windows computers with Internet access and an existing Chrome Remote Desktop connection.
- Windows 11 is the development environment. Other OS versions have not been verified.
- The remote/build computer needs Windows PowerShell and .NET Framework 4.8. Missing compiler support produces an explicit error.
- The first remote installation downloads about 55 MB for the official Cloudflare tunnel component and verifies its digest and Windows signature.
- Keep the extracted source folder. Its private pairing material is needed when building the Sender or updating your pair.

![Six-step installation order](media/installation-en.png)

## 1. Download and extract on the REMOTE computer

1. Inside the remote desktop, open a browser and visit [this repository](https://github.com/evan6007/chrome-remote-desktop-image-paste).
2. Choose **Code → Download ZIP**, or use the source-ZIP link above.
3. On the remote computer, right-click the downloaded ZIP and choose **Extract All**.
4. Open the extracted project folder. You should see:

```text
chrome-remote-desktop-image-paste-main/
├── 1-Install-Receiver.cmd
├── 2-Create-Sender.cmd
├── scripts/
├── src/
└── README.md
```

Do not run directly from the ZIP preview. If you see another project folder, open it. If the two CMD files are missing, download the latest `main` archive linked above.

## 2. Install the Receiver, still on the REMOTE computer

1. Double-click **`1-Install-Receiver.cmd`**.
2. A console reports three stages: compile and self-test, download/verify Cloudflare, launch the receiver installer.
3. Wait for the helper's status window. Its role should be `接收端` (Receiver).
4. Wait for **`連線已確認，等待圖片`** (connection verified, waiting for image). A visible window or passing compiler tests alone do not prove connectivity.
5. When the console finishes, you may close it. The receiver continues in the notification area.

Installation is per-user under `%LOCALAPPDATA%\RemoteImageBridge`, with login startup enabled. The launcher's PowerShell execution setting applies only to that process, not the machine-wide execution policy. Follow your organization's policy on managed machines.

## 3. Create the Sender, still on the REMOTE computer

1. Return to the **same extracted project folder**.
2. Double-click **`2-Create-Sender.cmd`**.
3. Wait for **`READY TO TRANSFER`**.
4. Open the project's `dist` folder and find **`RemoteImageBridge-Sender.exe`**.

Do not launch the Sender on this remote PC: doing so would replace its installed receiver with the sending role. Do not build from a second freshly downloaded checkout, because that would use a different key. Both roles must use the `.private` key from the same checkout.

## 4. Download the Sender to the LOCAL computer

1. Open Chrome Remote Desktop's side options panel.
2. Under **File transfer**, choose **Download file**. This means download from the remote computer to the local one.
3. In the remote file picker, select the project's `dist\RemoteImageBridge-Sender.exe`.
4. Wait for your **local browser** to finish downloading it, then open the local Downloads folder.

The next step happens on your local desktop, outside the remote-desktop view. Another trusted file-transfer method is also fine.

## 5. Install the Sender on the LOCAL computer

1. Double-click the downloaded **`RemoteImageBridge-Sender.exe`** locally.
2. Its role should be `本機傳送端` (Local Sender).
3. Wait for **`連線已確認，等待圖片`**. This means the Sender has verified the receiver.
4. Keep both helpers running.

The local computer needs only this EXE. It does not need the source checkout, Cloudflare component or the two CMD launchers.

## Everyday use

1. Ensure both helpers are running and not paused.
2. On the **local computer**, press **Win+Shift+S** and capture an area. Use a new capture made after the helper started.
3. Wait for **100% / `圖片已到遠端`** (image reached remote).
4. Inside Chrome Remote Desktop, click the destination app's image-capable input area, then press **Ctrl+V**.

No manual PNG save or per-image file download is needed. Image transfer does not require repeated clicks to refocus Chrome Remote Desktop. A text-only input still cannot accept images.

While enabled, the helper sends newly copied images automatically. Pause it when you do not want that. An in-flight upload may finish; quit to stop completely.

## Reading the status

| State | Meaning |
| --- | --- |
| `正在建立圖片連線` | Receiver transport is still starting |
| `連線已確認，等待圖片` | Connection and pair were verified; take a new screenshot |
| 0–90% | Image transmission is in progress |
| 95% / waiting for remote confirmation | Do not treat this as completed yet |
| 100% / image reached remote | Receiver confirmed it placed the image in the clipboard; paste now |
| Not delivered / re-pair | Delivery is unconfirmed; follow reconnection/troubleshooting below |

Sender and receiver allocate their progress percentages differently; the values need not match at every instant.

## Re-pairing after the receiver restarts

The current temporary tunnel gets a new address on restart.

1. Start the remote Receiver and wait for its transport to initialize.
2. Click **`重新配對`** (Re-pair) on the **local Sender**.
3. Switch into the remote-desktop view once, allowing its existing text clipboard channel to carry the short pairing message.
4. If no reply arrives, click Re-pair on the **remote Receiver**, then return to the local Sender.
5. Wait for connection verified, then capture again.

This pairing step may require switching windows; individual images still use the independent HTTPS connection.

If clipboard pairing does not work, return to the original source folder on the remote computer, run `2-Create-Sender.cmd` again, download the new Sender and reinstall locally. If an old endpoint cache persists, quit the local helper, open `%LOCALAPPDATA%\RemoteImageBridge`, delete **only `remote-endpoint.txt`**, and reopen the updated helper.

## Troubleshooting

| Symptom | Action |
| --- | --- |
| No EXE in the downloaded ZIP | Expected: this is source. Run `1-Install-Receiver.cmd` remotely to produce the EXE in `dist` |
| CMD fails immediately / scripts not found | Extract All first; keep the CMD files beside both `scripts` and `src` |
| `.NET Framework C# compiler not found` | Check 64-bit Windows and .NET Framework 4.8 on the build computer; ask your administrator if needed |
| Cloudflare download fails, 403 or timeout | Check remote Internet/GitHub access and organization restrictions; disabling the firewall is not required |
| `Start the Receiver and wait...` | Complete step 2 and wait for its connection before step 3; use the same Windows user |
| `Build the Receiver first...` | This checkout has no pairing key. Use the original folder rather than a new download |
| Sender accidentally installed on the remote PC | Run its original `1-Install-Receiver.cmd` again, wait for connection, then repeat steps 3–5 |
| Connected but an old screenshot is not sent | Capture/copy again after the helper starts |
| 100% but no paste | Check remote focus, image support in the destination, and whether another copy replaced the clipboard |
| Connection lost after a restart | Re-pair; ensure the two clocks differ by less than five minutes |
| Windows or an organization blocks the executable | These private builds are unsigned. Check the source and organization policy; do not disable security software or install a self-signed root as a workaround |
| Status window disappeared after X | X hides it; use the tray icon or open `%LOCALAPPDATA%\RemoteImageBridge\RemoteImageBridge.exe` |

When reporting a problem, provide the role, version and error text. Metadata logs are `sender-events.log` / `receiver-events.log` in the installed folder. Inspect them before public sharing. Never attach paired EXEs, `.private`, working-folder ZIPs or personal images.

## Update, remove and share

- **Update:** update public source files in the original remote checkout while preserving `.private`, then repeat launchers 1 and 2. A new checkout creates a new pair and requires reinstalling both roles.
- **Stop and remove login startup:** choose `停止並取消開機啟動` from the tray menu on each computer.
- **Remove files:** after stopping, you may delete that computer's `%LOCALAPPDATA%\RemoteImageBridge`. The original build folder retains the pair until you separately remove it.
- **Share with friends:** share the [repository URL](https://github.com/evan6007/chrome-remote-desktop-image-paste); let each friend build a different pair, rather than distributing your private EXE.

## Optional PowerShell route

In the extracted **remote** project folder, type `powershell` in File Explorer's address bar and press Enter. Run:

```powershell
.\scripts\Build.ps1 -Role Receiver -SelfTest
.\scripts\Install-Tunnel.ps1
```

Double-click `dist\RemoteImageBridge-Receiver.exe`, wait for connection verified, then in the same project run:

```powershell
.\scripts\Build.ps1 -Role Sender -UseRunningReceiver -SelfTest
```

Transfer the Sender locally and continue at step 5.

## Validation boundary

The source package, compile/self-tests and diagram exports can be checked in isolation. That is not a substitute for two-physical-computer testing across Windows versions, networks and destination apps. Image limits are 24 MiB / 36 million pixels. The current Cloudflare Quick Tunnel is a development/testing transport without an uptime guarantee. [Full technical limitations](../README.md#limits-and-privacy).
