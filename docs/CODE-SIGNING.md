# Code signing policy

## Current status

**The project currently has no publicly trusted code-signing certificate and no signed general-purpose installer.** The public distribution is source; generated per-pair EXEs are unsigned and contain private pairing credentials. Passing CI, a checksum, MIT licensing or HTTPS delivery does not establish a trusted Windows publisher signature.

The separately downloaded Cloudflare binary is signed by its publisher. That signature does not sign or endorse this project's own executable.

## Intended download experience

The intended future flow is one reusable installer for everyone, with sender/receiver selection and private pairing performed after installation. End users should not need a compiler, a developer certificate, a root-certificate import or a security-policy change.

This is a planned architecture change, **not implemented in the current source release**. Per-pair compilation changes the executable, so a signature on a generic release cannot simply be carried over to private rebuilt EXEs.

## Routes being evaluated

| Route | Status and constraints |
| --- | --- |
| [SignPath Foundation](https://signpath.org/) | Free signing is available to accepted open-source projects; no application or approval is claimed for this project. The project must meet the [eligibility and provenance requirements](https://signpath.org/terms.html), including already releasing the artifact form to be signed |
| Microsoft Store **MSIX** distribution | Microsoft can sign the package after Store certification. This requires packaging, policy compatibility and approval; it is not achieved by uploading an arbitrary EXE |
| Public CA / signing service | Requires a qualifying account, validation and potentially fees. No purchase or enrollment has been made |

Microsoft's [signing-options documentation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options) distinguishes Store MSIX signing from the MSI/EXE submission route. Its [SmartScreen documentation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation) explains why even valid new signatures, including EV certificates, do not guarantee immediate removal of reputation prompts. We do not advertise zero warnings for this unsigned source release.

## Release controls

- Maintainer: [evan6007](https://github.com/evan6007). Signing responsibilities and approval rules will be finalized if a provider accepts the project.
- A reusable artifact must be built from a recorded public commit. Product/version metadata, the installer and the main executable must agree.
- Pairing secrets must remain runtime-local and must never enter shared build artifacts or signing jobs.
- Third-party components retain their original licenses and signatures.
- No signing key or provider token will be stored in source. Fork pull requests must not be able to invoke privileged release signing.
- An enabled signing provider and a verified timestamped output are both required before any release is labeled signed.

## Privacy and system changes

While enabled, the current sender automatically transmits newly copied images to the receiver chosen by the operator, using Cloudflare as a transport provider. The helper exposes pause/quit controls, keeps image bytes in memory, and records metadata rather than image contents. Installation is per-user and enables login startup. The [installation guide](INSTALL.md) documents stopping startup and removing the software. [SECURITY.md](../SECURITY.md) describes pairing and transport boundaries.

Users are not instructed to disable Defender/SmartScreen or install a project root certificate. A self-signed certificate does not meet the public-trust goal.
