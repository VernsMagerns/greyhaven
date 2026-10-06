#!/bin/sh
# Fails if anything about to ship still contains a placeholder. Run by CI before uploading the
# greyhaven-windows build, and before every release.
#   tools/release-check.sh [published-folder]   (default: artifacts/windows)
set -eu
cd "$(dirname "$0")/.."
out=${1:-artifacts/windows}
fail=0

repo=$(sed -n 's/.*GitHubRepository = "\([^"]*\)".*/\1/p' src/GreyHaven.Core/GreyHavenInfo.cs)
case "$repo" in
  OWNER/* | "") echo "FAIL: GreyHavenInfo.GitHubRepository is still a placeholder ('$repo')."; fail=1 ;;
  *) echo "ok: repository is $repo" ;;
esac

# Placeholder links in files people read or that ship with the app.
if grep -rIn --exclude=release-check.sh "github\.com/OWNER\|OWNER/greyhaven" README.md docs data samples "$out" 2>/dev/null; then
  echo "FAIL: placeholder links found above."; fail=1
else
  echo "ok: no placeholder links in README, docs, data, samples or $out"
fi

# Reports generated for every sample, in every format the CLI produces.
cli="dotnet run --project src/GreyHaven.Cli -c Release --"
reports_ok=1
for s in $($cli samples | awk 'NF {print $1}'); do
  for fmt in "" "--json"; do
    if $cli rate --sample "$s" --offline $fmt 2>/dev/null | grep -n "OWNER/"; then
      echo "FAIL: placeholder in the $s report ${fmt:-(text)}"; fail=1; reports_ok=0
    fi
  done
done
[ $reports_ok -eq 1 ] && echo "ok: sample reports have no placeholders"
exit $fail
