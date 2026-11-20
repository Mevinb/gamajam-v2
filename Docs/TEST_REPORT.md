# Verification report — 21 November 2026

Runtime/build source: `31a3e25` on `main`. Unity 6000.3.29f1, Ubuntu 26.04, Intel i5-12500H (16 threads),
16 GB RAM, NVIDIA RTX 4050 Laptop GPU (6 GB), OpenGLCore.

The demo targets **Linux x86-64 only**. Windows packaging was cancelled at the user's request.

## Verified checks

| Check | Result | Evidence |
|---|---|---|
| CPU Roslyn compilation | 60 runtime + 11 editor source files pass | `Tools/compile_check.sh all` |
| Final regenerated-scene PlayMode suite | 2 test cases pass, 0 failures; 13 gameplay scenarios pass | `Logs/playmode-results.xml`, `Logs/playmode.log` |
| EditMode rules/audio | 22 pass, 0 failures | `Logs/editmode-results.xml` |
| Focused settings UI | Pass, including real queued Escape and paused input isolation | `Logs/ui-playmode-results.xml`, `Logs/ui-playmode.log` |
| Generated scenes | Both generated successfully; warm run about 30 seconds | `Logs/buildscenes.log` |
| Environment photos | 15 RTX renders; zero missing scripts | `Logs/photos-final.log`, `Logs/photos/` |
| Linux build | Succeeded, 425 MB, 11.33 seconds cached, 0 errors/warnings | `Logs/build-linux.log` |
| Standalone Linux gameplay acceptance | 13/13 pass; process exit 0 | `Logs/standalone-acceptance.txt`, `Logs/standalone-acceptance-player.log` |

Gameplay scenarios exercise movement, aiming/possession, unconscious recovery, split/switch, access denial,
all three complete infiltration routes, both research terminals, two-person authorization, the ending,
observed suspicion/decay, early missions, CCTV wall occlusion/power cut/cooldown, scientist reassurance,
pursuit refusal, guard capture/ejection, pause and restart. Traffic remains active.

UI coverage uses the actual title and pause canvases: live master/ambience gain, graphics/sensitivity controls,
saved preference reloads including mute, menu focus, panel bounds, Back/Resume and gameplay isolation while paused.
Three 1600×900 captures were inspected under `Logs/ui/`. Batch tests temporarily route keyboard input to the Game View
and restore input settings/devices/preferences afterward. They do not prove physical gamepad operation.

## Static geometry and visual comparison

| Saved-scene mesh geometry | Baseline | Final | Reduction |
|---|---:|---:|---:|
| Total triangles | 965,633 | 482,691 | 50.01% |
| Imported prop triangles | 760,253 | 277,497 | 63.50% |
| Other triangles | 205,380 | 205,194 | 0.09% |
| Merged mesh records | 1,393 | 1,391 | — |

Exact counts and scene hashes are in `Logs/geometry-before.json` and `Logs/geometry-after.json`.
PhotoTool additionally counts gameplay meshes, so its totals exceed static-world counts.
The handoff's old 5.8-million figure predates an earlier LOD pass and is not this comparison's baseline.

Compared alley, market, plaza/shop, gate, staff entrance and street-detail views before/after. Facade textures and
fixture silhouettes show no obvious regression at these views. This is not a claim of identical detail at every angle.
Environment lighting was refreshed and procedural dressing varies between generation revisions.
Final gate/staff photos confirm text no longer appears through opaque walls while intended labels stay readable.
Crates match their collision dimensions and the starting alley remains navigable.

Removing unused `ContributeGI` flags from a fully realtime world reduced the recorded light-baker input from
6,134.55 MB to 1.50 MB and bootstrap precompute from 64.07 seconds to 0.28 seconds. Environment/reflection baking remains enabled.

## Standalone RTX performance

Measured the actual Linux executable, with no Unity build running concurrently:

```bash
./play.sh -benchmark -screen-width 1920 -screen-height 1080 -screen-fullscreen 1 \
  -logFile /home/mevlec/Data/Parasite/Logs/benchmark-1080p-player.log \
  -autotestOut /home/mevlec/Data/Parasite/Logs/benchmark-1080p/report.txt
```

| Setting/measurement | Observed |
|---|---|
| Actual resolution | 1920×1080, FullScreenWindow |
| Preset / render scale | High / 1.00 |
| vSync / frame limit | Off / uncapped |
| Live simulation | 16 NPCs, traffic active, 2,292 renderers, 16 lights |
| Sample count / duration | 19,635 frames / 109.1 seconds |
| Mean | 180.0 FPS / 5.55 ms |
| 99th-percentile frame time | 17.11 ms, equivalent to 58.5 FPS |
| Worst sampled frame | 42.1 ms |
| Unity allocated / reserved memory | 186 MB / 348 MB |

The tour starts in a legal scientist host and reaches the main-gate, staff-terminal and service-tunnel approaches.
Its final return-to-spawn leg **times out after 35 seconds** at approximately (-26.9, -32.3), so this is not a completed
round trip. No capture/checkpoint reset interrupted sampling. Startup and screenshot encoding are excluded;
the percentile is a frame-time percentile, not an average of the slowest 1% of frames.
Occasional hitches remain despite the high average; this is not a guarantee of constant 60 FPS throughout the game.
The graphics-driver memory profiler returned zero, so no graphics-memory allocation claim is derived from that counter.

A preliminary windowed run averaged 181.1 FPS at **1920×1042** because desktop window borders reduced the requested height.
It is retained in `Logs/benchmark/`, but is not the 1080p measurement above.

## Remaining verification

Standalone Linux acceptance also passed while concurrent shader compilation used the CPU; this is a gameplay check, separate from
the uncontended performance tour above. Physical controller operation and an extended human playthrough remain unverified.
See [known issues](KNOWN_ISSUES.md) for the remaining visual/simulation limitations.
