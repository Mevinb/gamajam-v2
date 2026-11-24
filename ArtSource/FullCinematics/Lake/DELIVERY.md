# Full lake source delivery

Four fully packed editable Blender sources, authored at 1920×1080 and 24 fps:

| Source | Duration | Content |
|---|---:|---|
| `Lake_Arrival.blend` | 15 s / 360 frames | Three shots from a shore arrival to a human glance and the neutral choice framing. |
| `Lake_Hold.blend` | 6 s / 144 frames | Periodic breathing, subtle gaze/hands, water and breeze while explicit choice waits. |
| `Lake_Peace.blend` | 7 s / 168 frames | Exhale, relaxed hands, ordinary eyes, warm quiet and shore pullback. |
| `Lake_Dominion.blend` | 7 s / 168 frames | Deliberate gaze, curled fingers, green eyes, then a crane toward 42 distant human heartbeats. |

Arrival and hold are neutral. Waiting, reaching a clip endpoint or skipping
never chooses an outcome. Peace and Dominion require their explicit choices.
The original summary follows the selected resolution; later Continue remains
separate.

Rebuild, preview and audit instructions are in
`Tools/blender/cinematics/full_story/lake_story/README.md`. `manifest.json`
records shot ranges and triggers. `audit.json` records per-file SHA256, packed
image checks, exact frame settings, branch-only signal isolation, and measured
bone animation ranges. The virtual loop endpoint at frame 145 matches frame 1
for both the full rig and the water/wind (zero measured delta). Render only
frames 1–144 for the loop.

The packed sources are approximately 32.3 MB each, 129.4 MB total. All retain
the future Cycles GPU / 48 sample quality profile. The authoring tool has no GPU
render mode. Its CPU still previews use two threads, eight samples and 480×270.
Per-clip `previews/*_evidence.json` records the source hash, render configuration,
frame hashes and measured CPU preview times. The PNGs validate the authored
composition and pose; they do not certify the completed motion or final movie.

All four films total 840 unique frames / 35 seconds. The final Linux codec remains
VP8/WebM, matching the verified current player. At approximately 7 Mbps these
clips would total about 31 MB, split into individual files well below GitHub's
100 MB limit. Final quality/bitrate still need visual and decoder verification.

GPU time is unmeasured for these complete scenes. The earlier 720p/32-sample
inserts took roughly 4–8 seconds/frame; pixel/sample scaling suggests a broad
10–27 seconds/frame at 1080p/48 samples, about 2.3–6.3 hours for these 840 frames.
Adaptive sampling, foliage, physical water and CPU/GPU denoising overhead can
change that considerably. Replace this estimate with measured final-quality RTX
keyframes before a long render. Reserve roughly 2–3 GB for lake PNG working frames.
No external billed rendering service was used.

Remaining integration work: final RTX frame render, audio mix, movie encode and
decode verification, and explicit runtime trigger wiring. The old source,
runtime assets, generated Unity scenes and frozen build candidate were not
changed. No Unity process, GPU render or commit was started by this lake task.

See `PROVENANCE.md` for existing project and CC0 asset reuse.
