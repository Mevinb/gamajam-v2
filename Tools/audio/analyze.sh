#!/usr/bin/env bash
# Listen-proxy QA of generated audio: loudness/peaks/clipping/silence/loop seams (+ --bands, --spec DIR).
#   Tools/audio/analyze.sh Assets/Parasite/Resources/Audio
set -euo pipefail
cd "$(dirname "$0")/../.."
PY="$(Tools/audio/setup_venv.sh | tail -1)"
export PARASITE_AUDIO_WORK="${PARASITE_AUDIO_WORK:-$HOME/Data/parasite-work/audio}"
export PYTHONPYCACHEPREFIX="$PARASITE_AUDIO_WORK/pycache"
export NUMBA_CACHE_DIR="$PARASITE_AUDIO_WORK/numba-cache"
exec "$PY" Tools/audio/analyze.py "${@:-Assets/Parasite/Resources/Audio}"
