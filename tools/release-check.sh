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

# Going public: no placeholders may remain in the public-facing documents.
public=$(sed -n 's/.*IsRepositoryPublic = \(true\|false\);.*/\1/p' src/GreyHaven.Core/GreyHavenInfo.cs)
if [ "$public" = "true" ]; then
  if grep -n "CONTACT METHOD\|Fill in at application time" CODE_OF_CONDUCT.md docs/*.md 2>/dev/null; then
    echo "FAIL: placeholders above must be filled in before the repository is public."; fail=1
  else
    echo "ok: public-facing documents have no placeholders"
  fi
fi

# The throwaway design preview (tools/ui-preview) must never ship.
if [ -d "$out" ] && grep -rIl "Design preview" "$out" 2>/dev/null | grep -q .; then
  echo "FAIL: the design preview (tools/ui-preview) is in $out."; fail=1
else
  echo "ok: no design preview in $out"
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
