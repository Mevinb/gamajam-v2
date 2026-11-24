#!/bin/bash
# Three-second Cycles/OptiX camera inserts, then Linux-native VP8 WebM encoding.
# Each film keeps its complete editable scene in ArtSource/Cinematics.
set -euo pipefail
P="$(cd "$(dirname "$0")/../../.." && pwd)"
BLENDER=${BLENDER:-$HOME/Data/Blender/current/blender}
mkdir -p "$P/Logs" "$P/Assets/Parasite/Resources/Cinematics/Films"
for film in opening ending; do
    "$BLENDER" -b --python-exit-code 1 --python "$P/Tools/blender/cinematics/render_films.py" -- "$P" "$film" render > "$P/Logs/blender-film-$film-render.log" 2>&1
    python3 "$P/Tools/blender/cinematics/encode_films.py" "$film"
done
