# Full lake ending authoring

`author_lake.py` opens `ArtSource/Cinematics/EndingFilm.blend` read-only and creates
four new packed sources under `ArtSource/FullCinematics/Lake/`. It never writes the
old source, game assets, Unity scenes, or the frozen build candidate.

```bash
~/Data/Blender/current/blender -b --python Tools/blender/cinematics/full_story/lake_story/author_lake.py -- build
~/Data/Blender/current/blender -b --python Tools/blender/cinematics/full_story/lake_story/author_lake.py -- preview all
~/Data/Blender/current/blender -b --python Tools/blender/cinematics/full_story/lake_story/author_lake.py -- audit
python3 Tools/blender/cinematics/full_story/lake_story/make_storyboard.py
```

The saved profile is 1920×1080, 24 fps, Cycles 48 samples, adaptive sampling and
denoising, with a future GPU device setting. **The script does not implement GPU
rendering.** `preview` always switches to Cycles CPU, two threads, eight samples,
480×270, and does not save these overrides into the source.

| Source | Frames | Duration | Purpose |
|---|---:|---:|---|
| `Lake_Arrival.blend` | 1–360 | 15 s | Shore dolly, a neutral glance and resting hand adjustment, settle into choice framing. |
| `Lake_Hold.blend` | 1–144 | 6 s | Periodic human breathing, subtle eyes and hands, water and shore breeze while the choice UI waits. |
| `Lake_Peace.blend` | 1–168 | 7 s | Explicit Peace only: exhale, hands relax, ordinary eyes, warm human quiet and a shore pullback. |
| `Lake_Dominion.blend` | 1–168 | 7 s | Explicit Dominion only: deliberate gaze, finger curl, green iris activation, crane reveal of 42 distant human heartbeats. |

Camera cuts use timeline markers. The first camera is also assigned as the scene
camera. Standard humanoid pose bones retain their original names. All animation
is ordinary keyed Blender data, without a runtime driver dependency.

The choice loop has a virtual endpoint at frame 145 matching frame 1. Render
frames 1–144; do not encode the duplicate frame 145. The audit checks every rig
bone's endpoint matrix. The water and wind have a 144-frame period as well.
Waiting, skipping or reaching the end of arrival must lead to the choice UI;
they never authorize Peace or Dominion. Play either branch only after its
explicit outcome. The original summary follows the branch. Continue into the
riverside campaign is a separate action.

`manifest.json` records shot ranges, trigger boundaries, provenance and estimates.
`audit.json` records packed-image checks, per-file SHA256, source size, frame
counts, branch signal isolation and the loop endpoint matrix delta. The source
character, textures, shore, bench, jetty, water and sun come from the project's
original packed film source. Park planting reuses the project's detailed
procedural city trees, existing CC0 bark/leaf atlases and existing CC0 Shrub 04
model, opened read-only and packed into the new files. The park layout, ground
extension, distant silhouettes, bank, performance, camera blocking, wind and
heartbeat shader animation are original authoring. See
`ArtSource/FullCinematics/Lake/PROVENANCE.md` and the existing
`Docs/THIRD_PARTY_ASSETS.md`. No new download or external licence is introduced.

Preview PNGs in `previews/` are CPU composition evidence, not final quality movie
frames. The full motion, RTX rendering speed, final encode/decode, audio and
runtime integration still require their own verification. Full delivery uses
840 unique frames (35 seconds across all four clips); a play with one loop and
one outcome uses 672 frames. Estimated H.264 video at 12–20 Mbps is 52.5–87.5 MB
for all four clips. The 1–5 seconds/frame RTX estimate is unmeasured (14–70 minutes
for all 840 frames); benchmark before committing to a long render. Reserve
roughly 1–3 GB for intermediate PNG frames.
