#!/bin/bash
# Author/review outside runtime Assets; CPU previews only. Final GPU render is a separate explicit phase.
set -euo pipefail
P="$(cd "$(dirname "$0")/../../../.." && pwd)"
BLENDER=${BLENDER:-$HOME/Data/Blender/current/blender}
mkdir -p "$P/ArtSource/FullCinematics/Opening/Review"
"$BLENDER" -b --factory-startup -t 2 --python-exit-code 1 --python "$P/Tools/blender/cinematics/full_story/opening_scene.py" -- --cpu-previews > "$P/ArtSource/FullCinematics/Opening/authoring.log" 2>&1
"$BLENDER" -b --factory-startup -t 2 --python-exit-code 1 --python "$P/Tools/blender/cinematics/full_story/audit_opening.py" > "$P/ArtSource/FullCinematics/Opening/audit.log" 2>&1
