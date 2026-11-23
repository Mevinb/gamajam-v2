#!/bin/bash
# Copy finished characters from the staging dir into the Unity project (atomic renames on the same filesystem).
#   Tools/blender/characters/install_characters.sh [Id ...]   (default: every <Id>.fbx in the staging dir)
set -euo pipefail
HERE=$(cd "$(dirname "$0")" && pwd)
P=$(cd "$HERE/../../.." && pwd)
WORK=${PARASITE_CHAR_WORK:-$HOME/Data/parasite-work/characters}
OUT="$WORK/out"
if [ $# -gt 0 ]; then ids=("$@"); else ids=(); for f in "$OUT"/Models/*.fbx; do ids+=("$(basename "$f" .fbx)"); done; fi
# install: same filesystem -> mv is an atomic rename, so a concurrent Unity import never sees half-written files
A="$P/Assets/Parasite/Art/Characters"
mkdir -p "$A/Models" "$A/Textures/Skins" "$A/Textures/Eyes"
install() { cp "$1" "$(dirname "$2")/.$(basename "$2").tmp~" && mv "$(dirname "$2")/.$(basename "$2").tmp~" "$2"; }
for f in "$OUT"/Textures/Skins/* "$OUT"/Textures/Eyes/*; do
  [ -f "$f" ] || continue
  rel=${f#$OUT/Textures/}
  if ! cmp -s "$f" "$A/Textures/$rel" 2>/dev/null; then install "$f" "$A/Textures/$rel"; fi
done
install "$OUT/Textures/Skin_VeinMask.png" "$A/Textures/Skin_VeinMask.png"
for id in "${ids[@]}"; do
  [ -f "$OUT/Models/$id.fbx" ] || continue
  for t in "$OUT/Textures/${id}_"*; do install "$t" "$A/Textures/$(basename "$t")"; done
  install "$OUT/Models/$id.json" "$A/Models/$id.json"
  install "$OUT/Models/$id.fbx" "$A/Models/$id.fbx"
done
python3 "$HERE/write_credits.py" "$WORK" "$A/Models" > "$A/CREDITS.md" || true
echo "installed into $A"
