# Third-party components

The source in this repository is provided under the MIT license in `LICENSE`.

The network tunnel is **cloudflared**, copyright Cloudflare, Inc. and its contributors, licensed under Apache License 2.0. Its binary is not committed to or bundled in the app installer. The receiver setup downloads the official Windows amd64 release and verifies its pinned SHA-256 and Windows publisher signature. `scripts/Install-Tunnel.ps1` is an optional developer download helper.

- Upstream source and notices: https://github.com/cloudflare/cloudflared
- Upstream license: https://github.com/cloudflare/cloudflared/blob/master/LICENSE
- Download source: https://github.com/cloudflare/cloudflared/releases
- Pinned version in this release: `2026.9.1`

Windows and .NET Framework are provided by Microsoft under their own terms. This repository does not redistribute them. Chrome Remote Desktop, Chrome, Google, Cloudflare and Windows are their respective owners' names and marks; this project is independently developed.
