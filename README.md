# Google Chrome Remote Desktop Image Paste

**Google Chrome 遠端桌面圖片貼上工具 — 本機截圖，遠端直接 Ctrl+V 貼上。**

[繁體中文使用說明](README.zh-TW.md)

Take a screenshot with Win+Shift+S on your local Windows computer, then press Ctrl+V to paste it on the computer you control through Google Chrome Remote Desktop.

This is an experimental **Windows desktop helper**, released as source under the MIT license. It is not a Chrome extension and is not affiliated with Google or Cloudflare. No store account is needed to build or use it.

## What it does

- Watches new image clipboard changes on the sending computer, including Win+Shift+S captures.
- Transfers encrypted PNG data over HTTPS without requiring the remote-desktop tab to be focused for each transfer.
- Restores native PNG and DIB clipboard formats on the receiver so compatible desktop apps can paste with Ctrl+V.
- Shows progress, connection status and a completion receipt. Sender progress reaches 100% only after the receiver confirms that it wrote the clipboard.
- Keeps ordinary text on Chrome Remote Desktop's existing clipboard channel.
- Provides pause/resume, a tray menu and a status window that does not activate other windows or move the mouse.

```mermaid
flowchart LR
    A[Sender: new screenshot] --> B[Encrypt on sender]
    B --> C[HTTPS via Cloudflare Tunnel]
    C --> D[Receiver: verify and decrypt]
    D --> E[Windows image clipboard]
    E --> F[Ctrl+V in a compatible app]
```

## Before you start

- Two Windows desktop computers. Windows 11 has been the development environment; other versions are not verified.
- On the computer used to build the pair: Windows PowerShell 5.1 or PowerShell 7, with .NET Framework 4.8 and the bundled C# compiler. No Visual Studio, NuGet or paid API is required.
- Internet access on both computers. The current transport uses Cloudflare Quick Tunnels. They are intended for testing/development, have no uptime guarantee, and get a new URL when the receiver restarts. See [Cloudflare's documentation](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/do-more-with-tunnels/trycloudflare/).
- One sender/receiver pair per Windows user profile. The current UI is Traditional Chinese.

**Each build pair contains its own secret. Never publish your generated EXEs, the `.private` directory, or a ZIP of your whole working directory.** Share source using GitHub's source archive; only send a generated Sender EXE to your own trusted sending computer. See [SECURITY.md](SECURITY.md).

## Set up your own pair

Perform steps 1–3 on the **receiving computer**, in a clean checkout or extracted source ZIP. Build both roles in the same checkout so they share the newly generated key.

### 1. Build and install the receiver

Open Windows PowerShell in this project folder:

```powershell
.\scripts\Build.ps1 -Role Receiver -SelfTest
.\scripts\Install-Tunnel.ps1
```

The second command explicitly downloads the official Cloudflare Windows binary, verifies its GitHub SHA-256 digest and Windows signature, and places it under `%LOCALAPPDATA%\RemoteImageBridge`. It does not install a Windows service or modify firewall rules.

Double-click `dist\RemoteImageBridge-Receiver.exe`. It installs for the current user, registers login startup and opens its status window. Wait until it reports that the connection has been verified.

If local PowerShell policy blocks downloaded scripts, inspect the source and follow your organization's script policy. No machine-wide execution-policy change is required by this project.

### 2. Build the sender for this receiver

Back in the **same checkout**:

```powershell
.\scripts\Build.ps1 -Role Sender -UseRunningReceiver -SelfTest
```

This reads the current receiver endpoint and uses the key generated in step 1. It does not contact the endpoint during its logic tests.

### 3. Install on the sending computer

Transfer only `dist\RemoteImageBridge-Sender.exe` to your trusted sending computer using Chrome Remote Desktop's file-download function or another trusted transfer method. Double-click it **on that computer**.

Wait for `連線已確認` (connection verified), then make a new screenshot. When the sender shows 100% / `圖片已到遠端`, paste in a compatible app on the receiver. Images present before startup are not automatically sent; make a new capture or copy the image again.

### After the receiver restarts

The Quick Tunnel URL changes. Click `重新配對` (re-pair) on the sender, then switch into the remote-desktop view to deliver the short pairing message through Chrome Remote Desktop's text clipboard channel. If the reply does not arrive, click re-pair on the receiver and return to the sender.

For an explicit endpoint update, rebuild the Sender in the original checkout with `-UseRunningReceiver` and reinstall it on the sender. Exit the sender helper and remove only its `%LOCALAPPDATA%\RemoteImageBridge\remote-endpoint.txt` cache if it retains an older re-paired URL, then reopen it.

Re-pairing can require switching windows. Individual **image transfers** use the independent HTTPS connection and do not require those focus switches.

## Controls and removal

- `暫停 / 繼續`: pause/resume clipboard bridging. An upload already in flight may finish; quit the helper to stop it completely.
- Close the status window to hide it; double-click the tray icon or installed EXE to show it again.
- Tray `停止並取消開機啟動`: stop and remove this tool's current-user login startup entry.
- The installed executable, state files and optional tunnel binary are in `%LOCALAPPDATA%\RemoteImageBridge`.
- Startup uses `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `RemoteImageBridge`. No administrator privilege is needed.

## Limits and privacy

- Single-direction image sync, up to 24 MiB and 36 million pixels per image. PNG is not converted to JPEG or resized.
- If several images arrive during a transfer, the latest queued image replaces earlier queued images.
- While enabled, **newly copied images are automatically transmitted**. Pause it when you do not want images shared.
- Image bytes are processed in memory; the helper does not save screenshots to disk or log image/ordinary-text contents. Metadata logs include timestamps, state, sizes and diagnostic process names; the legacy decoder can log image hashes.
- The receiver briefly restores its last image when Chrome Remote Desktop overwrites it with empty text. Newly copied ordinary text/another image invalidates that cached image. Clipboard handling differs between applications and requires real-device testing.
- A low-level keyboard hook checks Ctrl+V / Shift+Insert to strip the helper's control markers and handle empty-text overwrites. It does not record typed text, synthesize keys, move the mouse, or change input-method settings.
- Cloudflare receives the HTTPS request and forwards application-encrypted image bytes. The shared pairing secret and separate image encryption key are not transmitted as HTTP fields. Metadata such as traffic size and timing remains visible to the transport provider.
- Both computers' clocks must be reasonably synchronized (within five minutes).

## Validation and current status

The included `--self-test` suite checks chunk assembly compatibility, limits, hashes, image validation, native memory conversion, clipboard protection rules, authenticated encryption and endpoint validation. It does not change the user's live clipboard or prove cross-machine paste success.

Run:

```powershell
.\scripts\Build.ps1 -Role Receiver -SelfTest
.\scripts\Build.ps1 -Role Sender -Endpoint https://example-tunnel.trycloudflare.com -SelfTest
.\scripts\Check-PublicTree.ps1
```

The last command checks the Git-tracked file list; run `git add` before using it for a release. CI compiles both roles, runs logic tests and checks tracked source. CI deliberately does **not** upload generated binaries, which contain pairing credentials.

The predecessor prototype passed an encrypted Internet loopback probe with approximately 1.77 MB in 0.46 seconds and 7.09 MB in 1.25 seconds. Those were receiver-to-Cloudflare-to-receiver synthetic probes, not measurements from a second physical computer and not a performance promise for this release. The public fork requires independent two-computer validation.

## Contributing

Issues and pull requests are welcome. Useful next steps are a reusable installer with runtime pairing, eliminating the remaining focus-dependent re-pairing step, English UI, and a durable self-hosted transport option. Never include screenshots, pairing keys, private builds or unsanitized logs in public issues.

## License

MIT; see [LICENSE](LICENSE). Cloudflare's separately downloaded `cloudflared` has its own Apache-2.0 license; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
