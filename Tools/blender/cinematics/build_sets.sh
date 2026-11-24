#!/bin/bash
# Generate/verify outside Assets, then publish complete files atomically. Run before the frozen Unity verification phase.
set -euo pipefail
P="$(cd "$(dirname "$0")/../../.." && pwd)"
BLENDER=${BLENDER:-$HOME/Data/Blender/current/blender}
mkdir -p "$P/Logs" "$P/ArtSource/Cinematics" "$P/Assets/Parasite/Resources/Cinematics/Textures"
STAGE=$(mktemp -d "$P/Logs/cinematic-set-stage.XXXXXX")
trap 'rm -rf "$STAGE"' EXIT
"$BLENDER" -b --python-exit-code 1 --python "$P/Tools/blender/cinematics/build_sets.py" -- "$STAGE/Cinematics" "$STAGE/Source" > "$P/Logs/blender-cinematics.log" 2>&1
"$BLENDER" -b --python-exit-code 1 --python "$P/Tools/blender/cinematics/verify_sets.py" -- "$STAGE/Cinematics" > "$P/Logs/blender-cinematic-axis-verify.log" 2>&1
python3 - "$P" "$STAGE" <<'PY'
from pathlib import Path
import os,sys
project,stage=map(Path,sys.argv[1:])
for group,target in [('Cinematics',project/'Assets/Parasite/Resources/Cinematics'),('Cinematics/Textures',project/'Assets/Parasite/Resources/Cinematics/Textures'),('Source',project/'ArtSource/Cinematics')]:
    for source in (stage/group).iterdir():
        if source.is_file() and source.suffix in {'.fbx','.png','.blend','.json'}:
            os.replace(source,target/source.name)
PY
grep '^\[cinematics\]' "$P/Logs/blender-cinematics.log"
grep '^\[cinematic-verify\]' "$P/Logs/blender-cinematic-axis-verify.log"
