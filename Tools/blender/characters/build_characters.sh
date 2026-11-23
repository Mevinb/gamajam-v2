#!/bin/bash
# Regenerate PARASITE's skinned human characters (MPFB 2 + Blender 4.5, headless) and install them into Unity.
#   Tools/blender/characters/build_characters.sh            # all characters in specs.py
#   Tools/blender/characters/build_characters.sh CivMaya    # just some
#   PREVIEW=1 ...                                           # also Cycles preview sheets in $WORK/previews
# Then import in Unity:  Tools/unity.sh chars-import -executeMethod Parasite.EditorTools.CharacterModelImporter.RebuildBatch
set -euo pipefail
HERE=$(cd "$(dirname "$0")" && pwd)
P=$(cd "$HERE/../../.." && pwd)
WORK=${PARASITE_CHAR_WORK:-$HOME/Data/parasite-work/characters}
BLENDER=${BLENDER:-$HOME/Data/Blender/current/blender}
FABRIC="$P/Assets/Parasite/Resources/Textures/Clothes"
"$HERE/setup_mpfb.sh"
export BLENDER_USER_RESOURCES="$WORK/blender_user"
OUT="$WORK/out"
LOGS="$WORK/logs"
mkdir -p "$OUT/Models" "$OUT/Textures" "$LOGS"
if [ $# -gt 0 ]; then ids=("$@"); else mapfile -t ids < <(cd "$HERE" && python3 -c "import specs; print('\n'.join(c['id'] for c in specs.CHARACTERS))"); fi
if [ ! -f "$OUT/Textures/Skin_VeinMask.png" ]; then
  "$BLENDER" -b --python-exit-code 1 --python "$HERE/bake_veins.py" -- "$OUT" > "$LOGS/veins.log" 2>&1
fi
fail=0
for id in "${ids[@]}"; do
  if "$BLENDER" -b --python-exit-code 1 --python "$HERE/make_character.py" -- "$id" "$OUT" "$FABRIC" ${PREVIEW:+--blend} > "$LOGS/$id.log" 2>&1; then
    grep "^\[chars\] DONE" "$LOGS/$id.log"
    if [ -n "${PREVIEW:-}" ]; then
      mkdir -p "$WORK/previews"
      "$BLENDER" -b "$OUT/$id.blend" --python "$HERE/preview.py" -- "$OUT" "$id" "$WORK/previews/$id" > "$LOGS/$id.preview.log" 2>&1 || true
    fi
  else
    echo "FAILED $id (see $LOGS/$id.log)"; grep -A12 Traceback "$LOGS/$id.log" | head -20; fail=1
  fi
done
"$HERE/install_characters.sh" "${ids[@]}" || fail=1
exit $fail
