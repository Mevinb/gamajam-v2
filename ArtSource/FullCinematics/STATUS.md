# Current full-story authoring status

The frozen 0.4 runtime candidate, generated Unity scenes and shipped inserts are untouched by this work.
All full-source files below are authored at 1080p/24 fps. No full-story GPU render has started.

- Opening: 80 seconds, 1,920 frames, 11 cameras/story beats, 19 caption cue events (14 nonempty intervals).
- Lake: 15-second neutral arrival, 6-second periodic choice hold, 7-second Peace and 7-second Dominion.
- Combined: 115 seconds, 2,760 unique frames. Every packed source is below 100 MB.
- Motion: all 34 existing body/finger actions retargeted and independently verified on the four real actor rigs.
- Speech: all eight opening actor lines have exact catalogue/source matches and measured 100 Hz RMS envelopes.

The final opening authoring candidate is `Opening/OpeningFull.blend` (57,026,794 bytes), SHA-256:
`8b8531ce2c48197bd1543841e1d3d156b0f2254b61a158f595115412ede093d1`.
Its nine fresh CPU preview hashes are in `Opening/preview-evidence.json`. The source includes dressed actors,
physical tap/contact, collapse and care, actuated mechanisms, a lit duct, detailed city skyline/sky/haze,
varied wet pavement and bounded interior gel scattering.

The latest fresh-process audit passes all authoring checks. Petrov's wrist target is exactly 0.10 m above the
patient's chest bone, allowing the hand/finger surface contact instead of the previous unreachable pose.
The culture now aligns with and squeezes through the real 0.17 m release port: measured radial extent is
0.155–0.162 m over the limiting samples. Actual shipped speech RMS envelopes drive all eight actor dialogue
lines; no text-length or generic sine approximation determines speech aperture.

All current Blender authoring/render processes have finished. Heavy CPU and all GPU rendering are held for
root's isolated player benchmarks. The complete source is ready for a measured RTX pilot after profiling.
A source rebuild/review is reproducible with:

```bash
Tools/blender/cinematics/full_story/build_opening.sh
```

Lake sources are final for this authoring review. `Lake/Lake_Storyboard_CPU.png` contains 13 inspected stills whose
hashes match the four packed sources; branch isolation, packed images and exact loop endpoints pass their CPU audit.

The GPU render driver is deliberately gated by `--gpu-released`. Its frame chunks, atomic images and provenance-bound checkpoints stay under ignored `Logs/full-cinematic-renders`; camera selection comes from the packed timeline, and a stop file pauses between complete frames. GPU quality/timing calibration, complete movie render,
audio/encode and actual-player full-film integration remain required after authoring approval.


The actual-Blender CPU driver smoke passed all 15 checks using only two rendered low-resolution frames, with
GPU unused. Exact cut120/121, graceful stop, hash-preserving resume and rejection of changed image/sampling/driver
provenance are recorded in `driver-smoke-evidence.json`. Long working frames remain outside source under ignored
`Logs/full-cinematic-renders`.

`source-publication-manifest.json` and `source-publication.paths` explicitly list the intentional authoring bundle.
Author logs, temporary/reference images and all render caches are excluded. The manifest is checked by:

```bash
python3 Tools/blender/cinematics/full_story/publication_manifest.py --check
```
