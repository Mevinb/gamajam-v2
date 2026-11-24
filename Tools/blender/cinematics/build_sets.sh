#!/bin/bash
# Compact, deterministic original film sets. Run without Unity open/importing.
set -euo pipefail
P="$(cd "$(dirname "$0")/../../.." && pwd)"
BLENDER=${BLENDER:-$HOME/Data/Blender/current/blender}
mkdir -p "$P/Logs" "$P/ArtSource/Cinematics" "$P/Assets/Parasite/Resources/Cinematics/Textures"
"$BLENDER" -b --python-exit-code 1 --python "$P/Tools/blender/cinematics/build_sets.py" -- "$P/Assets/Parasite/Resources/Cinematics" "$P/ArtSource/Cinematics" > "$P/Logs/blender-cinematics.log" 2>&1
grep '^\[cinematics\]' "$P/Logs/blender-cinematics.log"
