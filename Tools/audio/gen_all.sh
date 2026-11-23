#!/usr/bin/env bash
# Full audio regeneration (deterministic): voice-over (missing lines only) + vocal sets, SFX, ambience, music, QA.
# Source packs for SFX/ambience must exist in $PARASITE_AUDIO_WORK/{kenney,oga} (see gen_sfx.py / gen_ambience.py).
set -euo pipefail
cd "$(dirname "$0")/../.."
Tools/audio/fetch_sources.sh
Tools/audio/gen_voice.sh --vocals
Tools/audio/gen_sfx.sh
Tools/audio/gen_ambience.sh
Tools/audio/gen_music.sh
Tools/audio/analyze.sh Assets/Parasite/Resources/Audio | tail -1
