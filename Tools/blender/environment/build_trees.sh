#!/bin/bash
# Full tree pipeline: sources -> leaf-cluster atlases (Cycles) -> tree FBX (3 species x 3 LODs) -> Assets.
# Work files live in ~/Data/parasite-work/graphics/trees; results are moved into Assets atomically at the end.
# usage: Tools/blender/environment/build_trees.sh [--skip-clusters]
set -euo pipefail
P="$(cd "$(dirname "$0")/../../.." && pwd)"
H="$(dirname "$0")"
B="$HOME/Data/Blender/current/blender"
W="$HOME/Data/parasite-work/graphics/trees"; S="$HOME/Data/parasite-work/graphics/src"
DST="$P/Assets/Parasite/Art/City/Foliage/Trees"
mkdir -p "$W" "$S"
"$H/fetch_sources.sh" "$S" >/dev/null
python3.12 "$H/leaf_rects.py" "$S" "$W/leaf_rects.json"
if [ "${1:-}" != "--skip-clusters" ]; then
  "$B" -b --python "$H/leaf_clusters.py" -- "$S" "$W" 1024 > "$W/clusters.log" 2>&1
  "$B" -b --python "$H/exr_to_npy.py" -- "$W/clusters" > "$W/npy.log" 2>&1
  python3.12 "$H/post_atlas.py" "$W/clusters" "$W/atlas"
fi
"$B" -b --python "$H/trees.py" -- "$W/fbx" 2>&1 | grep '\[trees\]'
"$B" -b --python "$H/preview_trees.py" -- "$W/fbx" "$W/atlas" "$S" "$W/preview.png" > "$W/preview.log" 2>&1 || true
STAGE="$W/stage"; rm -rf "$STAGE"; mkdir -p "$STAGE"
cp "$W"/fbx/Tree*.fbx "$STAGE/"
for n in Green Light Yellow Maple; do
  cp "$W/atlas/Leaves${n}_albedo.png" "$STAGE/TreeLeaves${n}_albedo.png"
  cp "$W/atlas/Leaves${n}_normal.png" "$STAGE/TreeLeaves${n}_normal.png"
done
for pair in Plane:bark_platanus Linden:bark_brown_02 Maple:japanese_zelkova_bark; do
  n=${pair%%:*}; s=${pair#*:}
  cp "$S/${s}_diff_1k.jpg" "$STAGE/Bark${n}_albedo.jpg"
  cp "$S/${s}_nor_gl_1k.jpg" "$STAGE/Bark${n}_normal.jpg"
done
mkdir -p "$DST"
for f in "$STAGE"/*; do mv -f "$f" "$DST/"; done
echo "trees -> $DST"; ls -la "$DST" | grep -v meta
