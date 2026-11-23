#!/bin/bash
# Downloads the CC0 source textures used by the Blender environment scripts (tree bark + leaf atlases).
# Sources stay outside Assets/ (default ~/Data/parasite-work/graphics/src); only rendered/exported results go into the project.
# usage: Tools/blender/environment/fetch_sources.sh [dest]
set -eu
D="${1:-$HOME/Data/parasite-work/graphics/src}"; mkdir -p "$D"; cd "$D"
UA="Mozilla/5.0"
# Poly Haven bark (CC0): diffuse, OpenGL normal, roughness at 1k
for id in bark_platanus bark_brown_02 japanese_zelkova_bark; do
  [ -f "${id}_diff_1k.jpg" ] && continue
  json=$(curl -s -A "$UA" "https://api.polyhaven.com/files/$id")
  for key in Diffuse nor_gl Rough; do
    url=$(python3 -c "import json,sys; d=json.loads(sys.argv[1]); print(d['$key']['1k']['jpg']['url'])" "$json")
    curl -s -A "$UA" -o "$(basename "$url")" "$url"
  done
done
# ambientCG leaf atlases (CC0): colour, opacity, normal at 2K
for id in LeafSet024 LeafSet014 LeafSet007 LeafSet027; do
  [ -f "${id}_Color.png" ] || [ -f "${id}_Color.jpg" ] && continue
  z="$D/${id}.zip"
  curl -sSL -A "$UA" -o "$z" "https://ambientcg.com/get?file=${id}_2K-PNG.zip"
  for m in Color Opacity NormalGL Roughness; do
    f=$(unzip -Z1 "$z" | grep -E "_${m}\.(jpg|png)$" | head -1 || true)
    [ -n "$f" ] && unzip -p "$z" "$f" > "${id}_${m}.${f##*.}"
  done
  rm -f "$z"
done
ls -la "$D"
