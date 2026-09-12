# Maintainer signing setup

This is a developer/AI runbook. End users should use the [short installation guide](INSTALL.zh-TW.md).

1. Review the prepared [application](SIGNPATH-APPLICATION.md) and apply through [SignPath Foundation](https://signpath.org/apply.html). The maintainer must supply an account email and accept the actual terms/privacy choices. The account is not registered merely by creating this workflow.
2. If accepted, complete account MFA and verify source-control MFA. Install the official [SignPath GitHub App](https://docs.signpath.io/trusted-build-systems/github) for this repository only after reviewing its requested permissions.
3. Create project slug `chrome-remote-desktop-image-paste`, a ZIP-root artifact configuration for exactly `ChromeRemoteDesktopImagePaste-Setup.exe`, and a `release-signing` policy requiring manual maintainer approval. Enforce product name and product/file version restrictions. Use a production Foundation certificate only after it is granted.
4. Create the GitHub `code-signing` environment with the maintainer as required reviewer and main as its allowed branch. Provider approval remains mandatory even if the GitHub account's plan cannot enforce an environment gate.
5. Store the signing API token as the environment secret `SIGNPATH_API_TOKEN`; set repository variable `SIGNPATH_ORGANIZATION_ID` to the approved organization ID. Only then set `SIGNPATH_ENABLED=true`.
6. Dispatch **Build and request approved code signing** on main. The provider verifies GitHub artifact provenance. Approve the exact requested build in SignPath, then download the `universal-installer-signed` artifact after the workflow validates its trusted signature and timestamp.
7. Review the signed executable, version and source commit before publishing a signed release. Do not replace an unsigned RC asset in place and leave old hashes: use a new release/version and recompute its checksum.

The action is pinned to an official SignPath commit. [Official GitHub integration](https://docs.signpath.io/trusted-build-systems/github) · [Foundation conditions](https://signpath.org/terms.html).

Current enrollment, organization ID, production certificate, environment reviewer gate, source-control MFA and API token are **not yet verified/configured**. The signing workflow is prepared but disabled.
