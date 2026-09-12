# Code signing policy

## Current status

**The v0.2.0 release candidate is unsigned. SignPath enrollment and trusted signing are not yet complete.** It now uses one generic installer: pairing credentials are generated and stored after installation, never embedded in release or signing artifacts. Passing CI, a checksum, MIT licensing or HTTPS delivery does not establish a trusted Windows publisher signature.

**Application status (2026-09-12): submission confirmation observed in the maintainer's browser; awaiting SignPath review.** A submitted application is not provider approval or a signed release.

The separately downloaded Cloudflare binary is signed by its publisher. That signature does not sign or endorse this project's own executable.

## Intended download experience

The v0.2.0 flow is one reusable installer for everyone, with sender/receiver selection and private pairing performed after installation. End users do not need a compiler or a developer certificate. Installation is per-user, login startup is opt-in, and Windows Settings provides uninstallation.

This architecture is implemented in the release candidate. The executable is byte-identical across roles and carries consistent product/version metadata. **Trusted signing still requires provider approval and a verified signed output.** See the [maintainer runbook](SIGNING-SETUP.md).

## Routes being evaluated

| Route | Status and constraints |
| --- | --- |
| [SignPath Foundation](https://signpath.org/) | Application submission confirmed on 2026-09-12; review pending. The project must meet the [eligibility and provenance requirements](https://signpath.org/terms.html), including already releasing the artifact form to be signed |
| Microsoft Store **MSIX** distribution | Microsoft can sign the package after Store certification. This requires packaging, policy compatibility and approval; it is not achieved by uploading an arbitrary EXE |
| Public CA / signing service | Requires a qualifying account, validation and potentially fees. No purchase or enrollment has been made |

Microsoft's [signing-options documentation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options) distinguishes Store MSIX signing from the MSI/EXE submission route. Its [SmartScreen documentation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation) explains why even valid new signatures, including EV certificates, do not guarantee immediate removal of reputation prompts. We do not advertise zero warnings for this unsigned source release.

## Release controls

- Maintainer, committer and proposed reviewer/signing approver: [evan6007](https://github.com/evan6007). Provider membership, MFA and production approval policy remain to be verified. External pull requests require maintainer review; every production signing request requires maintainer approval in SignPath.
- A reusable artifact must be built from a recorded public commit. Product/version metadata, the installer and the main executable must agree.
- Pairing secrets must remain runtime-local and must never enter shared build artifacts or signing jobs.
- Third-party components retain their original licenses and signatures.
- No signing key or provider token will be stored in source. Fork pull requests must not be able to invoke privileged release signing.
- An enabled signing provider and a verified timestamped output are both required before any release is labeled signed.
- `.github/workflows/sign-release.yml` is a manual workflow restricted to this repository's main branch and the `code-signing` environment. It remains disabled until `SIGNPATH_ENABLED` is explicitly enabled after enrollment. It never signs pull-request builds. The release check rejects unsigned, invalid, incorrectly identified or non-timestamped outputs.

## Privacy and system changes

While enabled, the current sender automatically transmits newly copied images to the receiver chosen by the operator, using Cloudflare as a transport provider. The helper exposes pause/quit controls, keeps image bytes in memory, and records metadata rather than image contents. Installation is per-user and login startup is optional. See the [privacy policy](PRIVACY.md) for storage, system changes, startup and removal, and [SECURITY.md](../SECURITY.md) for transport boundaries.

Users are not instructed to disable Defender/SmartScreen or install a project root certificate. A self-signed certificate does not meet the public-trust goal.
