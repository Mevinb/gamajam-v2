# Full cinematic render driver

The packed Blender source owns its camera cuts. The driver reads camera-bound timeline markers from that source,
selects the correct camera for every frame, verifies it before and after rendering, and records the active camera,
marker, transform, lens and focus settings. There is no opening-only manifest override.

All working PNGs, temporary files and checkpoints go into a named job directory under
`Logs/full-cinematic-renders/`, which the repository ignores. The driver rejects output under `ArtSource` or `Assets`.
Small intentional review images are still produced separately by the authoring tools in the source review folders.

Run from the project root, only after root explicitly releases the GPU:

```bash
~/Data/Blender/current/blender -b ArtSource/FullCinematics/Opening/OpeningFull.blend -t 2 --python-exit-code 1 \
  --python Tools/blender/cinematics/full_story/render_story.py -- \
  --gpu-released --start 1 --end 24 --samples 48 \
  --out Logs/full-cinematic-renders/opening-pilot
```

The same driver handles every lake source, using its packed markers and authored frame range. The hold source ends
at frame 144; frame 145 is its virtual repeated endpoint and is rejected as outside the render range. Requested
bounds are inclusive. Samples must be integers from 1 through 4096. Adaptive overrides are checked against the
sample limit. A source must preserve its 24 fps timing and pack its file images.

A small CPU probe is available after the isolated benchmark window ends:

```bash
~/Data/Blender/current/blender -b ArtSource/FullCinematics/Opening/OpeningFull.blend -t 2 --python-exit-code 1 \
  --python Tools/blender/cinematics/full_story/render_story.py -- \
  --cpu-preview --start 120 --end 121 --samples 8 \
  --out Logs/full-cinematic-renders/camera-cut-cpu-probe
```

This renders the first exact camera boundary at 480×270 using two CPU render threads. It neither enables GPU
rendering nor replaces the intentional source review images.

## Stop and resume

Create `<job>/STOP` while a job runs. The current frame finishes and is published atomically; the renderer then
records `status: stopped` and exits between frames. Remove that file to continue, and rerun the same command with
`--resume`. A custom `--stop-file` path may also be supplied. A stop request never interrupts an image write.

Each job directory has `job-provenance.json`; each requested chunk has its own `render-evidence-START-END.json`.
The provenance binds source SHA256, scene, driver and policy SHA256, Blender version/build, selected device ID and
backend, camera schedule, frame range, sampling/adaptive/denoise/bounce/seed settings, image/color management,
resolution and relevant render settings. Resume refuses changed provenance. Recorded images must match their
size, SHA256 and packed camera binding. Untracked overlapping frames are refused rather than overwritten.
Use non-overlapping chunks in one job directory, or a new directory for another configuration.

A job lock prevents concurrent writers. Every completed image is renamed from `working/`; checkpoints are flushed
and replaced atomically after each frame. Failures record `status: failed`; only a fully verified requested range
records `status: complete`. The source bytes must remain unchanged throughout the job.

Policy checks are fast and do not load Blender:

```bash
python3 -m unittest discover -s Tools/blender/cinematics/full_story -p test_render_policy.py -v
```

These checks cover input bounds, ignored-cache confinement, exact camera cuts, invalid bindings, resume provenance
changes and image record validity. The bounded actual-Blender CPU smoke also passed: it rendered only frames 120/121,
requested STOP during the first render, resumed across the exact packed cut, preserved hashes and rejected corrupted
images, changed sampling and stale driver fingerprints. Reproduce it with `python3 Tools/blender/cinematics/full_story/smoke_driver.py`
only when root permits the small CPU window. All raw logs/images stay ignored; the small intentional summary is
`ArtSource/FullCinematics/driver-smoke-evidence.json`. This verifies the driver lifecycle, not final RTX movie quality.
