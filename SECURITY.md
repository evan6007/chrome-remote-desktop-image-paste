# Security and private build material

This is an experimental personal-computer tool, not an audited security product.

## Pairing secrets

`scripts/Build.ps1` generates 32 random bytes using the OS cryptographic random-number generator. The hex key is stored in `.private/pair-key.txt`; generated configuration sources are in the same ignored directory. Both roles embed that key in their private executables.

Compiled binaries are **not generic distributable installers**. Anyone with a pair's binary or key can derive its HTTP authentication and image-encryption credentials. Keep both `.private/` and `dist/` private. Each independent user pair must build from its own checkout. This initial source release intentionally has no public EXE assets.

Use the GitHub source archive to share the project. Before publishing, stage only source/documentation files and run `scripts/Check-PublicTree.ps1`. The scan is a guardrail, not a guarantee against every possible secret format.

If a pair key leaks, stop both helpers, generate a fresh pair in a new checkout and reinstall both roles. A new tunnel URL alone does not rotate the cryptographic key. Do not paste keys into issues, chat or screenshots.

## Transport and clipboard boundaries

- Separate SHA-256-derived keys are used for HTTP authentication, AES-256-CBC encryption, HMAC-SHA256 envelope authentication and signed health/receipt messages. Encryption uses a fresh IV and encrypt-then-MAC; the MAC is checked before decryption.
- The receiver listens only on loopback. An explicitly installed Cloudflare tunnel exposes the authenticated image/health endpoints over HTTPS. Redirects are disabled by the sender, and only constrained `*.trycloudflare.com` HTTPS endpoints are accepted.
- Requests have size, pixel-count and socket-timeout limits. The receiver checks freshness and keeps a bounded in-memory cache of transfer IDs; this is not a persistent replay ledger.
- The clipboard-based re-pairing protocol uses a key-bearing control prefix. Only use it inside your trusted remote-desktop session. It shares endpoint information, not user images, and is a compatibility feature pending replacement with runtime pairing.
- While active, the sending helper sends every newly copied image. The receiving helper replaces the current clipboard when an image arrives. Ordinary text is not uploaded by this helper.
- The tool checks paste shortcuts with a Windows hook. It does not retain a typing history or synthesize mouse/keyboard input.
- Images and ordinary clipboard text are not written to application logs. Local metadata and old image hashes can still be sensitive; sanitize diagnostics before sharing.

## Reporting

For a suspected vulnerability, please use GitHub's private vulnerability reporting feature when available. Do not put pairing keys, private EXEs, personal screenshots or a working exploit against someone else's endpoint in a public issue. General, non-sensitive reliability bugs can use normal issues.
