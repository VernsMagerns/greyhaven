# Signing keys

Public keys GreyHaven uses to check that a distro's list of installer fingerprints is genuine.
Fetched from keyserver.ubuntu.com on 2026-10-07 and exported with only the key's own signatures
(`gpg --export-options export-minimal,export-clean`). Each fingerprint matches the one the
distro publishes, and each key verified that day's real signature ("Good signature").
`data/distros.yaml` pins the fingerprint, so a changed key file is refused.

| File | Key | Fingerprint | Published at |
|---|---|---|---|
| ubuntu-cdimage.asc | Ubuntu CD Image Automatic Signing Key (2012) | 8439 38DF 228D 22F7 B374 2BC0 D94A A3F0 EFE2 1092 | https://ubuntu.com/tutorials/how-to-verify-ubuntu |
| linuxmint-iso.asc | Linux Mint ISO Signing Key | 27DE B156 44C6 B3CF 3BD7 D291 300F 846B A25B AE09 | https://linuxmint-installation-guide.readthedocs.io/en/latest/verify.html |

Pop!_OS publishes no signature; GreyHaven checks the SHA-256 that System76's build API returns over HTTPS.
