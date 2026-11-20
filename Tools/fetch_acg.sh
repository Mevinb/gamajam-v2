#!/bin/bash
# Fetch ambientCG (CC0) materials/decals/atlases: Tools/fetch_acg.sh <ID> <RES e.g. 2K|1K> [ID RES ...]
# Extracts colour/normalGL/roughness/metalness/AO/displacement/emission/opacity maps into Assets/Parasite/Art/City/acg/<ID>/
set -u
P="$(cd "$(dirname "$0")/.." && pwd)"; T="$HOME/Data/tmp_dl"; mkdir -p "$T"
while [ $# -ge 2 ]; do
  id=$1; res=$2; shift 2
  dst="$P/Assets/Parasite/Art/City/acg/$id"
  if ls "$dst"/*_Color.* >/dev/null 2>&1; then echo "have $id"; continue; fi
  z="$T/${id}_${res}.zip"
  curl -sSL -A "Mozilla/5.0" --retry 3 -m 300 -o "$z" "https://ambientcg.com/get?file=${id}_${res}-JPG.zip" || { echo "FAIL $id"; continue; }
  sz=$(du -k "$z" | cut -f1)
  mkdir -p "$dst"
  for m in Color NormalGL Roughness Metalness AmbientOcclusion Displacement Emission Opacity; do
    f=$(unzip -Z1 "$z" 2>/dev/null | grep -E "_${m}\.(jpg|png)$" | head -1)
    [ -n "$f" ] && unzip -p "$z" "$f" > "$dst/${id}_${m}.${f##*.}"
  done
  rm -f "$z"
  echo "got $id ${sz}K -> $(du -sk "$dst" | cut -f1)K: $(ls "$dst" | tr '\n' ' ')"
done
