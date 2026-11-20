# Verification report — PARASITE 0.2.0 — 21 November 2026

Linux x86-64 is the sole delivery target. Unity 6000.3.29f1, Ubuntu 26.04,
Intel i5-12500H, 16 GB RAM, NVIDIA RTX 4050 Laptop GPU (6 GB), OpenGLCore.
Runtime/build source: `3f71476`; generated city: `8e86791`.
The source includes the Ctrl+M judges presentation shortcut and corrected release-shot camera placement.

## Release checks

| Check | Result | Evidence |
|---|---|---|
| CPU Roslyn compilation | 66 runtime + 11 editor files pass for the final source | `Tools/compile_check.sh all` |
| Eight-mission campaign acceptance | All 13 gameplay scenarios pass, including three complete routes and Peace/Dominion outcomes | `Logs/final-playmode.log` |
| Combined PlayMode suite | Initial run: 5 passed, 1 opening capture-timing failure; affected opening cases subsequently both passed | `Logs/final-playmode-results.xml`, `Logs/opening-final-results.xml` |
| Opening focused rerun | 2/2 pass; 11 rendered shots, full unscaled timeline, real queued Space/Escape/Enter, no replay on restart/checkpoint | `Logs/opening-final.log`, `Logs/ui/10-opening-*.png` |
| Settings, journal and lakeside UI | 3/3 pass; explicit choice, immutable outcome, both resolutions and temporary-scene cleanup | `Logs/epilogue-ui-results.xml`; also passed in the combined run |
| Pure rules/audio EditMode tests | Earlier 22/22 pass; relevant rules/audio sources unchanged | `Logs/editmode-results.xml` |
| Generated scenes | Bootstrap and eight-mission City regenerated successfully | `Logs/campaign-scenes.log` |
| Environment render audit | 15 RTX captures; 1,449 mesh instances, 490,641 triangles including gameplay meshes, zero missing scripts | `Logs/campaign-photos.log`, `Logs/photos/` |
| First 0.2.0 Linux build | Succeeded: 425 MB, 62.52 seconds, zero errors/warnings | `Logs/final-linux.log` |
| Final opening + presentation shortcut | 3/3 pass: both opening cases and left/right Ctrl+M; M alone does nothing; no duplicate/automatic ending choice | `Logs/presentation-final-results.xml`, `Logs/presentation-final.log` |
| Final Linux rebuild | Succeeded: 425 MB, 6.56 seconds cached, zero errors/warnings | `Logs/presentation-linux.log` |
| Final standalone Linux acceptance | 13/13 pass on the rebuilt executable; all three eight-mission routes, Peace and Dominion | `Logs/presentation-standalone.txt`, `Logs/presentation-standalone-player.log` |

The initial combined failure was confined to the capture test's accelerated clock: 20× playback
overshot the first five-second shot before its assertion. Runtime code was unchanged for that fix;
the test was slowed to 2× and both affected opening cases passed. All six unique original PlayMode
cases have therefore passed across the combined run and targeted rerun. This does not claim that
the initial combined XML was wholly successful. The final three-case focused run also passed
after the presentation shortcut and release-shot camera correction. The corrected shot was rendered
and inspected; the operator no longer occludes the interlock view.

Campaign coverage includes movement, possession/release, split/switch, access denial, all three
entry routes, research terminals, two-person authorization, Vale's protected reveal, coolant/archive
cooperation and interruption, dispatch/relay cooperation, broadcast pursuit refusal/interruption,
drain extraction, both explicit futures, suspicion, CCTV, abilities, capture, pause and restart.
World simulation stays frozen during story sequences; a high-suspicion nearby-guard check exercises
the dialogue protection. `Escaped` commits only after the chosen lakeside resolution.

UI tests exercise actual canvases and queued keyboard input. They temporarily route input to the
Editor Game View and restore devices, preferences and routing afterward. The cinematic uses
captions and sound effects. These checks do not establish physical gamepad operation or an
extended human playthrough. Lakeside captures show the seated girl, lake, sunset, two choices,
and distinct Peace/Dominion resolutions and summaries.

The rebuilt standalone title screen was rendered and inspected. A separate desktop smoke attempt
could not reliably deliver synthetic keyboard/mouse input through Wayland/Xwayland, so it does
not establish a standalone Ctrl+M keypress or lakeside screenshot. The queued-input PlayMode
shortcut test is the verified key-binding evidence; standalone campaign acceptance exercises
the actual player's ending systems using simulated game input.

## Geometry and visual scope

| Saved generated-city geometry | Before reduction | Release city |
|---|---:|---:|
| Total triangles | 965,633 | 482,781 |
| Imported prop triangles | 760,253 | 277,497 |
| Other triangles | 205,380 | 205,284 |
| Merged mesh records | 1,393 | 1,391 |

Evidence: `Logs/geometry-before.json`, `Logs/geometry-release.json`. The current city retains the
approximately 50% triangle reduction while adding campaign terminals. Prop triangles are down
63.50%. PhotoTool includes gameplay meshes and therefore reports a larger total than this static-city count.
Reviewed alley, market, plaza, gates, staff access and tunnel views. Puddle shore alpha includes
reflections/specular so rectangular water-mesh edges do not remain opaque. This is a sampled visual
review, not a claim of identical detail at every angle. Character rigs remain stylized and procedural.

## Performance evidence and limits

The earlier five-mission build was benchmarked at actual 1920×1080, High preset, render scale 1.00,
vSync off and uncapped on the RTX 4050: 180.0 FPS mean over 19,635 frames/109.1 seconds,
99th-percentile frame time 17.11 ms and worst sampled frame 42.1 ms. Its final return leg timed out
after 35 seconds, so the route was incomplete. Evidence: `Logs/benchmark-1080p/report.txt` and
`Logs/benchmark-1080p-player.log`.

Those are historical baseline measurements, **not a fresh 0.2.0 benchmark**. Startup and screenshot
encoding were excluded. They neither guarantee constant 60 FPS nor establish performance on
other hardware. No additional benchmark run delays this deadline-bound release.
See [known issues](KNOWN_ISSUES.md) for remaining simulation and verification limits.
