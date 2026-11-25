# Far Shore dialogue sources

Eight original island dialogue lines were synthesized locally with cached Kokoro-82M v1.0 weights on CPU, using two threads and disabled network access. The production manifest retains the actual voice profiles, tool/source hashes, clip keys and full-stream decode/level measurements. These are synthesized voices, not human actor recordings. Existing 59 voice catalogue entries were preserved; the eight clips extend the catalogue to 67.

The actual OGG files live under `Assets/Parasite/Resources/Audio/Voice`; exact text, cast and source ownership live in `Assets/Parasite/Audio/voice_manifest.json`. Technical audio/key checks pass for all eight. Subjective listening and new island arrival/departure playback are still unverified. Arrival/departure source now allows finite audible clip durations with silent fallbacks; that source is newer than the retained 0.5.0 preview and does not change its binary.
