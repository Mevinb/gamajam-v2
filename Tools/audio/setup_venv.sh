#!/usr/bin/env bash
# Creates the offline audio-generation venv (Kokoro-82M TTS + numpy/scipy/numba DSP).
# Location: $PARASITE_AUDIO_WORK (default ~/Data/parasite-work/audio). Re-running is a no-op when complete.
# Reuses the user-site torch (python3.12 --user packages) via --system-site-packages to avoid a 2.5 GB download.
set -euo pipefail
WORK="${PARASITE_AUDIO_WORK:-$HOME/Data/parasite-work/audio}"
VENV="$WORK/venv"
mkdir -p "$WORK"
if [ ! -x "$VENV/bin/python" ]; then
  uv venv --seed --python 3.12 --system-site-packages "$VENV"
fi
export VIRTUAL_ENV="$VENV"
if ! "$VENV/bin/python" -c "import kokoro, misaki, soundfile, numba, pyloudnorm, en_core_web_sm" 2>/dev/null; then
  "$VENV/bin/python" -c "import torch" 2>/dev/null || uv pip install torch --index-url https://download.pytorch.org/whl/cpu
  uv pip install --no-deps kokoro==0.9.4 misaki==0.9.4
  uv pip install loguru addict regex espeakng-loader num2words phonemizer-fork spacy soundfile pyloudnorm numba scipy transformers huggingface_hub
  uv pip install https://github.com/explosion/spacy-models/releases/download/en_core_web_sm-3.8.0/en_core_web_sm-3.8.0-py3-none-any.whl
fi
echo "$VENV/bin/python"
