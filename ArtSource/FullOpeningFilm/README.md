# RTX-rendered full opening

The complete 80-second, eleven-shot opening was rendered from the editable packed source `ArtSource/FullCinematics/Opening/OpeningFull.blend`. All 1,920 real production frames use 1920×1080 at 24 fps, 48 samples, the RTX 4050 OptiX backend and GPU OpenImageDenoise. The silent runtime movie is `Assets/Parasite/Resources/Cinematics/FullStory/OpeningFull.webm`.

`production-manifest.json` pins the final movie, source, full frame gate, encoder and atomic five-film publication. `render-provenance.json` preserves the actual renderer/device/quality/color/camera profile. `source-frame-hashes.csv` contains the byte count, SHA256 and packed camera marker for every production PNG. Raw render frames remain local ignored outputs rather than source assets.

`opening-11shot-contact.jpg` shows actual production midpoints; `review-samples.json` pins their frame numbers and hashes. The contact/collapse sequence was also reviewed at 34, 35, 36, 37, 38.5, 40 and 42 seconds, including full-resolution frame 925. It depicts Petrov kneeling to assist the worker. No definite torso penetration was observed; an occluded boot near the vessel base could not be classified from the images alone.

The movie passed exact frame-count/format checks and a full FFmpeg decode before publication. It contains no soundtrack: captions, narration, taps and music come from the running game. Actual imported Unity decode, normal-speed presentation, skip and natural gameplay handoff are recorded separately; the manifest keeps those gates pending until their actual results are available. A complete encoded stream alone does not certify the runtime flow or a human playtest.

The reproducible render/encode/validation/publication tools and commands are in `Tools/blender/cinematics/full_story/publishing/README.md`. These offline RTX path-traced frames are separate from the Linux URP player's real-time shaders.
