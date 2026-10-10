#!/bin/sh
# Assembles the download page (site/) with the brand files, fonts and screenshots it uses.
#   tools/site/build.sh [out-dir]   (default: artifacts/site)
# Run by .github/workflows/pages.yml once the repository is public.
set -eu
cd "$(dirname "$0")/../.."
out=${1:-artifacts/site}
rm -rf "$out"
mkdir -p "$out/fonts" "$out/images"
cp site/index.html site/site.css "$out/"
cp assets/brand/mark.svg assets/brand/mark-flat.svg assets/brand/icon-32.png assets/brand/social-preview.png "$out/"
cp assets/fonts/Rubik-Bold.ttf assets/fonts/Lexend-Regular.ttf assets/fonts/Lexend-SemiBold.ttf assets/fonts/*OFL.txt "$out/fonts/"
cp docs/images/*.png "$out/images/"
echo "Download page: $out/index.html"
