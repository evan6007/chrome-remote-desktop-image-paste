# 開發者／AI 文件

這一頁給要修改、測試、協助開發本專案的人與 AI。一般使用者請看 [三步安裝](INSTALL.zh-TW.md)。

## Architecture

Version 0.2.0 is one generic C# / .NET Framework 4.8 executable. Its installer UI chooses Receiver or Sender at runtime. No pairing key, endpoint, machine name or personal account is compiled into a release. `src/RuntimeSettings.cs` generates a random 256-bit receiver key and protects per-user settings with DPAPI. A copied `CRDIP2:` invitation carries that secret plus an allowlisted HTTPS endpoint; possession grants access until the receiver explicitly resets pairing. Invitations are not short PINs and do not expire automatically.

The receiver uses a verified, pinned upstream Cloudflare executable to expose a loopback HTTP listener through Quick Tunnel. Images are AES-256-CBC encrypted and HMAC-SHA256 authenticated before HTTPS transport. The sender verifies a secret-bound health challenge before accepting a pairing. A 100% status requires an authenticated receipt after the receiver writes native PNG/DIB clipboard formats. Ordinary clipboard text is neither logged nor uploaded. The keyboard hook only handles paste timing; it does not log keystrokes.

Tunnel addresses change after receiver restarts. Re-copy the invitation into the sender. Pairing updates clear cached addresses. Revoke a leaked key with Reset pairing; merely restarting does not revoke it. This version is not compatible with the private-key-embedded v0.1 installer; install v0.2 on both machines and create a fresh pair.

## Build and verification

On 64-bit Windows with .NET Framework 4.8:

```powershell
.\scripts\Build.ps1 -SelfTest
.\scripts\Check-Release.ps1
```

Or run `Build-From-Source.cmd`. No Git installation, Visual Studio or Node runtime is needed for the Windows app itself. Node/Playwright/Skechu are only used to regenerate documentation diagrams.

The public output is `dist/unsigned/ChromeRemoteDesktopImagePaste-Setup.exe` and `SHA256SUMS.txt`. The filename is stable across roles; the byte hash remains unchanged during runtime pairing. Never publish the entire `dist`, `.private`, runtime settings, pair codes or test fixtures.

Tests cover image reconstruction and limits, authenticated encryption, invitation validation, protected settings round trips and tamper rejection, credential reset, executable immutability, version metadata, and setup rendering on a separate desktop. Native clipboard tests require a newly created private window station; an unavailable station is reported as **SKIP**, never as a pass. UI render fixtures use synthetic private test pair codes and must not be published. Tests do not access the interactive user's clipboard or mouse.

An optional `--tunnel-test <report-path>` downloads/verifies the upstream component into an isolated test directory. No helper is installed or started in the user's actual app directory. Windows 10 is a target platform; current local development is Windows 11. Two independent physical PCs and every target app/network are not automatically covered by CI.

## Installation and lifecycle

`src/Installer.cs` copies the byte-identical executable into the current user's app directory, creates a Start Menu shortcut and Windows uninstall entry, and enables login startup only when selected. It never elevates, creates a service, changes firewall rules or imports a certificate. Only processes whose executable path matches this app's directory are stopped for update/uninstall. Uninstall deletes an explicit list of owned files rather than recursively deleting a computed path; its temporary removal helper may remain in Windows Temp for normal cleanup.

Settings dialogs verify the proposed sender without changing active transport keys. The bridge shuts down before installing new settings and starting a fresh process, so requests cannot race a key change in the same process.

## Release and signing

[Code signing policy](CODE-SIGNING.md) · [Maintainer signing setup](SIGNING-SETUP.md) · [Application draft](SIGNPATH-APPLICATION.md) · [Privacy](PRIVACY.md) · [Security](../SECURITY.md) · [Editable Skechu assets](media/README.md)

All uploaded app artifacts must come from a recorded GitHub-hosted build of public source. The build workflow uploads only the generic installer and checksum, plus explicitly listed text test reports. Signing is a separate manual workflow, disabled until enrollment is completed. It never runs on pull requests and checks the repository, branch, signing environment, provider credentials, Authenticode result, timestamp and expected product identity.
