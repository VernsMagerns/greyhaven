# Security policy

## Supported versions

GreyHaven is in early development. Only the newest release (or the newest test build) gets
security fixes.

## Reporting a problem

**Please don't open a public issue for security problems.** Report them privately through
GitHub instead:

1. Go to the repository's **Security** tab.
2. Click **Report a vulnerability**.
3. Describe what you found and how to reproduce it.

You'll get a reply within 7 days. Once the problem is fixed and a release is out, we'll credit
you in the release notes if you'd like.

## What matters most

GreyHaven is built to change as little as possible on someone's PC. Problems we especially want
to hear about:

- Anything that **changes the PC without asking**, or does more than the step says.
- **Writing a USB stick:** anything that could pick or write to the wrong drive (planned,
  milestone 6).
- **Downloads:** anything that would let a changed or fake Linux installer file pass the
  checksum and signature checks (planned, milestone 6).
- **The elevated helper:** anything that lets other programs use it, or that keeps
  administrator rights longer than one action (planned, milestone 5).
- **Privacy:** GreyHaven reading or sending anything that [PRIVACY.md](PRIVACY.md) says it
  doesn't.
- **Links and data:** a way for GreyHaven's data files to open something other than an https
  link or a file shipped with the app.

## How GreyHaven is built and signed

Releases are built only by GitHub Actions from this repository. See
[docs/code-signing-policy.md](docs/code-signing-policy.md).
