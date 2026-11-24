#!/bin/bash
# CPU mesh/texture authoring and verification. Publish only complete staged artifacts.
set -euo pipefail
P="$(cd "$(dirname "$0")/../../.." && pwd)"
BLENDER=${BLENDER:-$HOME/Data/Blender/current/blender}
mkdir -p "$P/Logs"
riverside_stage=$(mktemp -d "$P/Logs/riverside-stage-XXXXXX")
trap 'rm -rf "$riverside_stage"' EXIT
"$BLENDER" -b -t 2 --python-exit-code 1 --python "$P/Tools/blender/riverside/build_props.py" -- "$P" "$riverside_stage" > "$P/Logs/blender-riverside.log" 2>&1
grep '^\[riverside\]' "$P/Logs/blender-riverside.log"
"$BLENDER" -b -t 2 --python-exit-code 1 --python "$P/Tools/blender/riverside/verify_assets.py" -- "$P" "$riverside_stage" > "$P/Logs/blender-riverside-verify.log" 2>&1
grep '^\[riverside-verify\]' "$P/Logs/blender-riverside-verify.log"
python3 "$P/Tools/blender/riverside/publish_assets.py" "$P" "$riverside_stage"
