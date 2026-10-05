# Index builder

Builds `data/indexes/*.idx.gz` from official distro packages, so GreyHaven can check
offline whether each distro supports a device ID.

```sh
dotnet run --project tools/GreyHaven.IndexBuilder -- all         # everything
dotnet run --project tools/GreyHaven.IndexBuilder -- kernels     # kernel driver ID tables
dotnet run --project tools/GreyHaven.IndexBuilder -- packages    # driver package Modaliases
dotnet run --project tools/GreyHaven.IndexBuilder -- fingerprint # libfprint supported/unsupported lists
# options: --only <id>  --data <dir> (default: data)  --cache <dir> (default: ~/.cache/greyhaven-index)
```

It downloads about 1 GB of kernel packages the first time; later runs use the cache.

## What it does

- **Kernels:** reads each kernel `.deb` listed in `data/distros.yaml` (an `ar` archive
  containing a tar of `.ko.zst` modules). It extracts every `alias=` from each module's ELF
  `.modinfo` section, plus `modules.builtin.modinfo` for built-in drivers. Kernels before
  about 7.0 don't record aliases for built-in drivers; for those, `builtinClassAliasesFrom`
  borrows the class-wide patterns (never device-specific IDs) of the kernel's built-in
  modules from a newer index.
- **Packages:** reads the `Modaliases:` field from the distro's package indexes. This is
  how NVIDIA driver packages declare which GPUs they support.
- **Fingerprint:** reads libfprint's udev hwdb, which lists readers by driver plus known
  unsupported readers.

Every index records its source URLs and their SHA-256 in its header, so results can be
audited and reproduced. Output is sorted and de-duplicated, so regenerating gives a clean diff.

## Updating to a new distro release

1. Find the kernel on the new official ISO. The ISO's package manifest lists it, e.g.
   `releases.ubuntu.com/<ver>/*.manifest`. For ISOs without a published manifest, read
   `casper/filesystem.manifest` from the ISO.
2. Update `data/distros.yaml` with the kernel version, series and package URLs, and set
   `verifiedFrom` to where you checked.
3. Run `dotnet run --project tools/GreyHaven.IndexBuilder -- all`, then `dotnet test`.
4. Review the rating changes in `tests/GreyHaven.Tests/Ratings/RealDataTests.cs` failures,
   if any, before updating expectations.
