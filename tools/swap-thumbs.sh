#!/bin/bash
# Replace stored letterboxed thumbnails with the 4:5 cuts from seed-media/thumbs.
# usage: swap-thumbs.sh <api dir with seed-media/> <storage media dir>
# Matches each media/<clip>/thumb.jpg to its seed poster by md5 (the seeder copies posters verbatim),
# or to an earlier thumbnail cut listed in seed-media/thumbs/PREV-MD5 ("<md5> <name>" per line).
# Works with macOS bash 3.2 (no associative arrays).
set -eu
API=$1; MEDIA=$2
h() { if command -v md5sum >/dev/null 2>&1; then md5sum "$1" | cut -d' ' -f1; else md5 -q "$1"; fi; }
LOOKUP=$(mktemp)
for p in "$API"/seed-media/*.jpg; do echo "$(h "$p") $(basename "$p")" >> "$LOOKUP"; done
[ -f "$API/seed-media/thumbs/PREV-MD5" ] && cat "$API/seed-media/thumbs/PREV-MD5" >> "$LOOKUP"
n=0; skip=0
for t in "$MEDIA"/*/thumb.jpg; do
  name=$(grep "^$(h "$t") " "$LOOKUP" | head -1 | cut -d' ' -f2 || true)
  if [ -n "$name" ] && [ -f "$API/seed-media/thumbs/$name" ]; then cp "$API/seed-media/thumbs/$name" "$t"; n=$((n+1)); else skip=$((skip+1)); fi
done
rm -f "$LOOKUP"
echo "swapped $n, left $skip"
