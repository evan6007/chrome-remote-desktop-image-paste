# Security

This is an experimental personal-computer tool, not an audited security product.

## Pairing secrets

The v0.2 receiver generates 32 random bytes at runtime using the OS cryptographic random-number generator. Settings are protected with current-user Windows DPAPI in `settings.dpapi`. Software running as the same Windows user is outside this protection boundary.

The universal installer contains **no pairing credentials**. Its pairing code deliberately includes the key and receiver endpoint; anyone with that code can derive the pair's transport credentials. Codes do not automatically expire. Do not publish settings, test fixtures, pairing codes or the entire `.private`/`dist` directories. Only the checked generic installer and checksum belong in a release.

Use the GitHub source archive to share the project. Before publishing, stage only source/documentation files and run `scripts/Check-PublicTree.ps1`. The scan is a guardrail, not a guarantee against every possible secret format.

If a pair key leaks, use **Reset pairing** on the receiver and pair the sender again. A new tunnel URL alone does not rotate the cryptographic key. Do not paste keys into issues, public chat or screenshots.

## Transport and clipboard boundaries

- Separate SHA-256-derived keys are used for HTTP authentication, AES-256-CBC encryption, HMAC-SHA256 envelope authentication and signed health/receipt messages. Encryption uses a fresh IV and encrypt-then-MAC; the MAC is checked before decryption.
- The receiver listens only on loopback. An explicitly installed Cloudflare tunnel exposes the authenticated image/health endpoints over HTTPS. Redirects are disabled by the sender, and only constrained `*.trycloudflare.com` HTTPS endpoints are accepted.
- Requests have size, pixel-count and socket-timeout limits. The receiver checks freshness and keeps a bounded in-memory cache of transfer IDs; this is not a persistent replay ledger.
- Ordinary protocol messages use a derived control identifier, not the raw pairing key. Runtime pairing uses an explicit secret invitation which must be transferred only to the selected computer. Sender setup verifies a secret-bound health challenge before saving it.
- While active, the sending helper sends every newly copied image. The receiving helper replaces the current clipboard when an image arrives. Ordinary text is not uploaded by this helper.
- The tool checks paste shortcuts with a Windows hook. It does not retain a typing history or synthesize mouse/keyboard input.
- Images and ordinary clipboard text are not written to application logs. Local metadata and old image hashes can still be sensitive; sanitize diagnostics before sharing.

## Reporting

For a suspected vulnerability, use [GitHub private reporting](https://github.com/evan6007/chrome-remote-desktop-image-paste/security/advisories/new). Do not put pairing codes, settings, personal screenshots or a working exploit against someone else's endpoint in a public issue. General, non-sensitive reliability bugs can use normal issues. [Privacy and uninstall](docs/PRIVACY.md) · [Developer / AI documentation](docs/DEVELOPERS.md).
