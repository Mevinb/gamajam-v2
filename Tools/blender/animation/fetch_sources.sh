#!/bin/bash
# Downloads the animation sources into $ANIM_SRC (default ~/Data/parasite-work/animation/src):
#  - Quaternius "Universal Animation Library" 1 and 2, [Standard] editions (CC0, free "name your own price" downloads
#    from itch.io via the public "No thanks, just take me to the downloads" flow; no login).
#  - CMU Graphics Lab Motion Capture Database trials (ASF/AMC) from mocap.cs.cmu.edu.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
SRC="${ANIM_SRC:-$HOME/Data/parasite-work/animation/src}"
mkdir -p "$SRC/cmu"
fetch_ual() { # <slug> <dir> <zipname>
  local slug=$1 dir=$2 zip=$3
  if [ ! -d "$SRC/$dir" ]; then
    local tmp="$SRC/_dl_$dir"; mkdir -p "$tmp"
    ONLY="[Standard]" bash "$HERE/itch_dl.sh" quaternius "$slug" "$tmp"
    unzip -q -o "$tmp/$zip" -d "$SRC/$dir"
    rm -rf "$tmp"
  fi
}
fetch_ual universal-animation-library ual1 "Universal Animation Library[Standard].zip"
fetch_ual universal-animation-library-2 ual2 "Universal Animation Library 2[Standard].zip"
for t in 07_12; do
  s=${t%_*}
  [ -s "$SRC/cmu/$s.asf" ] || curl -sf -m 120 -A "Mozilla/5.0" -o "$SRC/cmu/$s.asf" "http://mocap.cs.cmu.edu/subjects/$s/$s.asf"
  [ -s "$SRC/cmu/$t.amc" ] || curl -sf -m 120 -A "Mozilla/5.0" -o "$SRC/cmu/$t.amc" "http://mocap.cs.cmu.edu/subjects/$s/$t.amc"
done
echo "sources ready in $SRC"
