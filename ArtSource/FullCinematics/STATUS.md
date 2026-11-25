# Current full-story authoring status

The frozen 0.4 runtime candidate, generated Unity scenes and shipped inserts are untouched by this work.
All full-source files below are authored at 1080p/24 fps. Full production began on 25 November 2026 at 00:37 UTC.
The serial job renders LakeArrival, LakeHold, LakePeace and LakeDominion before OpeningFull.

- Opening: 80 seconds, 1,920 frames, 11 cameras/story beats, 19 caption cue events (14 nonempty intervals).
- Lake: 15-second neutral arrival, 6-second periodic choice hold, 7-second Peace and 7-second Dominion.
- Combined: 115 seconds, 2,760 unique frames. Every packed source is below 100 MB.
- Motion: all 34 existing body/finger actions retargeted and independently verified on the four real actor rigs.
- Speech: all eight opening actor lines have exact catalogue/source matches and measured 100 Hz RMS envelopes.

The frozen opening authoring candidate is `Opening/OpeningFull.blend` (57,040,716 bytes), SHA-256:
`23af37e4b53dd56b1ad36571d8a8b88c81de990a33c9dfff5dc4247810f8aba8`.
Thirteen CPU views cover all eleven story beats. The speaking Vale shot now has a camera-only revision at unchanged
19–26 second timing; actual ray-cast first hits `Dr Vale Hero` and the 21-second head projection is safely framed.
`camera04-revision.json` records the scope, and retained view provenance identifies the unchanged base source rather
than claiming those images were rendered again.

The initial RTX pilot is retained as an explicitly unapproved baseline. The revised 1080p/48-sample macro and
natural LakeArrival frame passed root visual review. GPU OpenImageDenoise preserved the CPU OIDN image (about
61.4 dB PSNR) while reducing the same frame from ~28 s to ~11 s. OptiX denoising was only ~1.5% faster and changed
fine gel detail more. The active production strategy is 48 samples with GPU OIDN. Separate per-job source/driver/denoiser provenance, frame hashes and GPU-wide telemetry are retained.

Fresh source audit passes: physical 4 mm glass thickness and radial corner normals, 12 max/transmission bounces,
actual grounded walking soles over a full retimed cycle, care contact, physical escape squeeze, and all speaking
face framing. The natural lake pose uses actual deformed thigh contact and preserves neutral/Peace branch
isolation and exact hold endpoints.

The approved blends are frozen. Active checkpoints, per-frame hashes and GPU-wide telemetry are under
`Logs/full-cinematic-renders/full-production-20261125T003711Z`. The supervisor uses the tested between-frame STOP/resume
protocol and a 4 GiB free-space reserve. Initial lake frames are completing in roughly 3–4 seconds each; the complete
115-second film set is not yet rendered, encoded or integrated. Production continues alongside staged mission work.
A source rebuild/review is reproducible with:

```bash
Tools/blender/cinematics/full_story/build_opening.sh
```

Lake sources are final for this authoring review. `Lake/Lake_Storyboard_CPU.png` contains 13 inspected stills whose
hashes match the four packed sources; branch isolation, packed images and exact loop endpoints pass their CPU audit.

The GPU render driver is deliberately gated by `--gpu-released`. Its frame chunks, atomic images and provenance-bound checkpoints stay under ignored `Logs/full-cinematic-renders`; camera selection comes from the packed timeline, and a stop file pauses between complete frames. GPU quality/timing calibration is complete. Full movie rendering is active; silent encoding, live-audio synchronization and
actual-player full-film integration remain required.


The actual-Blender CPU driver smoke passed all 15 checks using only two rendered low-resolution frames, with
GPU unused. Exact cut120/121, graceful stop, hash-preserving resume and rejection of changed image/sampling/driver
provenance are recorded in `driver-smoke-evidence.json`. Long working frames remain outside source under ignored
`Logs/full-cinematic-renders`.

`source-publication-manifest.json` and `source-publication.paths` explicitly list the intentional authoring bundle.
Author logs, temporary/reference images and all render caches are excluded. The manifest is checked by:

```bash
python3 Tools/blender/cinematics/full_story/publication_manifest.py --check
```
