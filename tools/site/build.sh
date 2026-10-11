#!/bin/sh
# Assembles the landing page (site/) into a folder ready to deploy:
#   tools/site/build.sh [out-dir]   (default: artifacts/site)
# Adds the brand images and the game list for the game checker, generated from the same
# ProtonDB data and anti-cheat list the app ships ("greyhaven game-index").
# Deployed to Cloudflare Pages by .github/workflows/site.yml.
set -eu
cd "$(dirname "$0")/../.."
out=${1:-artifacts/site}
rm -rf "$out"
mkdir -p "$out/data"
cp -R site/. "$out/"
cp assets/brand/mark.svg assets/brand/mark-flat.svg assets/brand/icon-32.png assets/brand/icon-256.png assets/brand/og-image.png "$out/"
dotnet run --project src/GreyHaven.Cli -c Release -- game-index --out "$out/data/games.json" >/dev/null
# The one inline script (it marks the page as having JavaScript) is allowed by its hash.
cat > "$out/_headers" <<'HEADERS'
/*
  Content-Security-Policy: default-src 'self'; script-src 'self' 'sha256-qOhFsq0QMV2REqNwk5hGH5bZE9SkX6gt6yeyKdYPv+U='; style-src 'self'; img-src 'self' data: https://shared.akamai.steamstatic.com; font-src 'self'; connect-src 'self' https://api.github.com; frame-ancestors 'none'; base-uri 'self'; form-action 'none'
  Referrer-Policy: strict-origin-when-cross-origin
  X-Content-Type-Options: nosniff
  Permissions-Policy: camera=(), microphone=(), geolocation=()
/fonts/*
  Cache-Control: public, max-age=31536000, immutable
/data/*
  Cache-Control: public, max-age=86400
HEADERS
echo "Landing page: $out/index.html ($(du -sh "$out" | cut -f1))"
