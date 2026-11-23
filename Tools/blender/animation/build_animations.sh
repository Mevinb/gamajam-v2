#!/bin/bash
# Rebuilds every humanoid clip FBX into Assets/Parasite/Art/Characters/Animations (+ Preview mannequins).
# usage: Tools/blender/animation/build_animations.sh [--only Clip1,Clip2]
# Then (Unity, serialized): Tools/unity.sh anim-build -executeMethod Parasite.EditorTools.HumanAnimatorBuilder.BuildBatch
#      previews:            Tools/unity.sh anim-preview -executeMethod Parasite.EditorTools.AnimationPreviewTool.CaptureBatch
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
P="$(cd "$HERE/../../.." && pwd)"
BLENDER="${BLENDER:-$HOME/Data/Blender/current/blender}"
SRC="${ANIM_SRC:-$HOME/Data/parasite-work/animation/src}"
WORK="${ANIM_WORK:-$HOME/Data/parasite-work/animation}"
DEST="$P/Assets/Parasite/Art/Characters/Animations"
"$HERE/fetch_sources.sh"
STAGE="$WORK/stage-$$"
rm -rf "$STAGE"; mkdir -p "$STAGE"
"$BLENDER" -b --factory-startup --python "$HERE/build.py" -- --src "$SRC" --out "$STAGE" --preview-mannequins "$@" 2>&1 \
  | grep -E '^\[anim\]|Error|Traceback|File "' || true
ls "$STAGE"/*.fbx >/dev/null 2>&1 || { echo "build produced no FBX" >&2; exit 1; }
mkdir -p "$DEST/Preview"
cp "$SRC/ual2/Universal Animation Library 2[Standard]/Female Mannequin/Unity/Mannequin_F.fbx" "$STAGE/Preview/UAL_Mannequin_F.fbx"
# atomic per-file moves into the project
for f in "$STAGE"/*.fbx; do mv -f "$f" "$DEST/"; done
for f in "$STAGE"/Preview/*.fbx; do mv -f "$f" "$DEST/Preview/"; done
[ -f "$STAGE/manifest.json" ] && mv -f "$STAGE/manifest.json" "$HERE/manifest.json"
rm -rf "$STAGE"
echo "clips in $DEST:"; ls "$DEST" | grep -c '\.fbx$'
