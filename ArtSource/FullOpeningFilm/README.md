# RTX-rendered full opening

The complete 80-second, eleven-shot opening was rendered from the editable packed source `ArtSource/FullCinematics/Opening/OpeningFull.blend`. All 1,920 real production frames use 1920×1080 at 24 fps, 48 samples, the RTX 4050 OptiX backend and GPU OpenImageDenoise. The silent runtime movie is `Assets/Parasite/Resources/Cinematics/FullStory/OpeningFull.webm`.

`production-manifest.json` pins the final movie, source, full frame gate, encoder and atomic five-film publication. `render-provenance.json` preserves the actual renderer/device/quality/color/camera profile. `source-frame-hashes.csv` contains the byte count, SHA256 and packed camera marker for every production PNG. Raw render frames remain local ignored outputs rather than source assets.

`opening-11shot-contact.jpg` shows actual production midpoints; `review-samples.json` pins their frame numbers and hashes. The contact/collapse sequence was also reviewed at 34, 35, 36, 37, 38.5, 40 and 42 seconds, including full-resolution frame 925. It depicts Petrov kneeling to assist the worker. No definite torso penetration was observed; an occluded boot near the vessel base could not be classified from the images alone.

The movie passes exact frame-count/format checks and a complete FFmpeg decode. It is silent: captions, narration, taps and
music come from the game. Actual RTX4050/Vulkan Editor verification records12/13 in the first collection and2/2/native0 in a
focused normal80s plus score-pause baseline rerun. The original audio assertion failure is preserved. All eleven normal-speed
shot checkpoints, three real voice-source progress checks, actual image readbacks, natural handoff and zero owned media pass.
[Portable runtime proof](../../Docs/verification/full-opening-runtime-20261125.json) pins exact sources, XML/log/capture hashes
and limits. Native graphics warnings remain recorded; their allocator cause and final-player impact are unproven.

Strict rebuilt-player full-five media/story acceptance remains pending. These Editor results do not establish all continuous
speech, physical speaker output or human keyboard/gamepad behavior. Reproducible render/encode/validation/publication tools
are in `Tools/blender/cinematics/full_story/publishing/README.md`. Offline RTX path tracing remains separate from Linux URP rendering.
