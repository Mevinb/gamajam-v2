#!/usr/bin/env bash
# Regenerate PARASITE voice-over. Only missing clips are synthesised (re-runnable after new dialogue lands).
#   Tools/audio/gen_voice.sh            # dialogue lines (C# sources + Logs/voice_lines_seen.tsv)
#   Tools/audio/gen_voice.sh --list     # show collected lines + casting, no synthesis
#   Tools/audio/gen_voice.sh --vocals   # also gameplay barks/radio/civilian/scientist sets + crowd walla stems
#   Tools/audio/gen_voice.sh --prune    # delete clips whose line no longer exists
#   Tools/audio/gen_voice.sh --force    # re-render everything
set -euo pipefail
cd "$(dirname "$0")/../.."
PY="$(Tools/audio/setup_venv.sh | tail -1)"
export PARASITE_AUDIO_WORK="${PARASITE_AUDIO_WORK:-$HOME/Data/parasite-work/audio}"
export PYTHONPYCACHEPREFIX="$PARASITE_AUDIO_WORK/pycache"
export NUMBA_CACHE_DIR="$PARASITE_AUDIO_WORK/numba-cache"
export HF_HOME="${HF_HOME:-$PARASITE_AUDIO_WORK/hf}"
exec "$PY" Tools/audio/gen_voice.py "$@"
