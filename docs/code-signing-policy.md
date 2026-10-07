# Code signing policy

*Draft. Becomes active when GreyHaven's first signed release is published.*

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/).

## What is signed

Only files built by this repository's GitHub Actions release workflow, from a tagged commit on
`main`:

- `GreyHaven-Setup.exe` (the installer people download)
- `GreyHaven.msi` (the same installer for IT admins)
- the programs inside them: `GreyHaven.exe` and `greyhaven.exe`, and GreyHaven's own `.dll` files

Nothing built on a personal computer is ever signed. Third-party files (the .NET runtime, fonts)
keep their makers' own signatures, or none.

## Team and roles

| Role | Who | What they do |
|---|---|---|
| Committers and reviewers | [@VernsMagerns](https://github.com/VernsMagerns) | Write code and review every pull request from people outside the team before it's merged. |
| Approvers | [@VernsMagerns](https://github.com/VernsMagerns) | Approve each signing request, for each release. |

Everyone in these roles uses multi-factor authentication on GitHub and SignPath. The roles will
grow as the project gets more maintainers.

## How a release is signed

1. A maintainer tags a release on `main`.
2. GitHub Actions builds and tests it on Linux and Windows, and runs `tools/release-check.sh`.
3. The build is sent to SignPath. An approver checks that the request matches the tagged
   commit and approves it.
4. The signed files, their SHA-256 checksums and GitHub's build attestation are published
   together on the GitHub release.

## Privacy

GreyHaven only sends data when the user asks for something that needs it, and never sends
anything about the user to the GreyHaven project. See [PRIVACY.md](../PRIVACY.md) for exactly
what is sent, to whom, and when.
