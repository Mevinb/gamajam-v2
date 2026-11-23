#!/usr/bin/env bash
# Regenerate PARASITE ambience assets (deterministic). Usage: Tools/audio/gen_ambience.sh [name ...]
set -euo pipefail
cd "$(dirname "$0")/../.."
PY="$(Tools/audio/setup_venv.sh | tail -1)"
export PARASITE_AUDIO_WORK="${PARASITE_AUDIO_WORK:-$HOME/Data/parasite-work/audio}"
export PYTHONPYCACHEPREFIX="$PARASITE_AUDIO_WORK/pycache"
export NUMBA_CACHE_DIR="$PARASITE_AUDIO_WORK/numba-cache"
exec "$PY" Tools/audio/gen_ambience.py "$@"
