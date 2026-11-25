# PARASITE expansion and realism upgrade

The current request reopens feature development and authorizes frequent commits and pushes to `Mevinb/gamajam-v2`.
Older handoff statements freezing the eight-mission release, or prohibiting pushes for its capture session, are historical.
The full upgrade remains in progress; a passing compile or a small acceptance suite does not establish that every bug is fixed.

## Requested outcome and evidence

| Requirement | Evidence needed before calling it complete |
|---|---|
| Fix game bugs | Reproduce reported defects, fix them, exercise affected behavior and run campaign regressions on the rebuilt player. Keep remaining known defects visible. |
| More realistic graphics, textures, models and shaders | Integrated assets, fresh gameplay/cinematic renders reviewed at their actual resolution, and a fresh RTX 4050 benchmark at declared settings. |
| Blender-authored redesign | Reproducible Blender scripts, editable `.blend` source, exported assets actually used in the game, documented ownership/licences. |
| Redesign opening and ending | Fresh renders of every opening shot and both lakeside resolutions; verify skip, explicit choice, simulation freeze and restoration. |
| Continue beyond the lake | Reach the existing lake by playing the campaign, choose either future, enter the new chapter, complete its objectives and verify checkpoint recovery for both branches. |
| Keep expanding missions and improve play | Playable missions with distinct mechanics, clear objectives, fair access to needed hosts, and acceptance covering success, cancellation and recovery. |
| Use the RTX 4050 and add ray tracing | Confirm the rendering GPU and supported API. Document measured performance and distinguish hardware ray tracing, offline path tracing and screen-space reflections. |
| Multiple agents | Parallel campaign, Blender cinematic and graphics implementation, followed by integration review. |
| Push important working steps | Feature commits authored as Mevinb, verified push to `origin/main`, no generated build/cache/log directories committed. |

## Rendering direction

The current delivery platform is Linux x86-64, Unity 6000.3.29f1, URP 17.3.0, NVIDIA RTX 4050 Laptop GPU (6 GB).
URP's screen-space reflection shader traces the camera depth buffer. It does not establish RT-core or DXR use.
Unity's built-in HDRP hardware ray tracing requires a different pipeline and a supported platform/API; do not advertise the
Linux URP player as hardware ray traced. The user explicitly selected **retain Linux and add RTX-rendered Blender cinematics**,
alongside improved real-time shaders. Do not add a Windows/DX12 target for this upgrade.

Primary references: [Unity render-pipeline feature comparison](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/choose-a-render-pipeline/feature-comparison),
[HDRP 17.3 ray-tracing requirements](https://docs.unity.cn/Packages/com.unity.render-pipelines.high-definition@17.3/manual/Ray-Tracing-Getting-Started.html).

## Current source and delivery boundary — 25 November 2026

The source now contains the original 14 missions, branch-sensitive riverside missions M15–M17 and Far Shore missions M18–M20.
The island adds tidal culvert and maintenance-gate entry, physical Peace escorts or Dominion couriers, carried signal custody,
individual boarding and an all-free protected departure. Both complete rescue branches and the original 14 recovery/boarding
fixtures pass actual Editor execution. A new focused physical sidestep regression passes, and native scenario 23 passes both
branches on the latest private candidate. Its native scenario 24 exits 1 at an exposure setup that does not sustain the ordinary
sentry's sight cone. Facing that fixture sentry between Holding and the westward lead passes the focused Editor case with
unchanged exposure/vision/time rules. Native 24 and the full campaign regression remain open on the final rebuilt player;
[the physical-walker proof](verification/island-walker-20261125.json) preserves the exact pass/failure and source scopes.

Blender-authored quarantine walls and 2K PBR atlases are integrated with unchanged physical colliders/navigation. Actual import,
entry routes, camera, raster aperture and HDR staged visual review pass. Reflection ownership/fallback/cleanup passes actual GPU
checks; the rendered Vulkan standalone water case passes. Eight island voice clips pass technical gates and actual Editor source-tail
checks. See [HANDOFF.md](HANDOFF.md) and the portable proofs it links for exact artifacts and limits.

All 2,760 RTX/OptiX frames have completed, including the full 80-second opening and all four lake sequences at 1080p24.
All five films are now published in live Resources after exact frame/source provenance, visual sample review, encoding and
complete FFmpeg decode. Actual Editor story gates pass across named methods in the original12/13 collection and focused2/2
normal80s/pause-baseline rerun. The original failure remains recorded; this is not a single13casegreen result. Normal playback
reaches all eleven shots and the final half-second, observes three real voice-source progress checks, and restores gameplay
with zero owned media. Final-player verification remains required; [runtime proof](verification/full-opening-runtime-20261125.json)
retains exact sources/results and native warnings. The scoped native movie lifetime fix passes default stress
and actual pending-seek quit codes 0 and 1; OS/window-close pending-media routing remains separate.

The immutable `Builds/Linux` 0.5.0 preview was built and launched for the user, then exited normally. It contains M18 and the
four full lake films, not M19/M20 or subsequent fixes. New private candidates preserve that preview. The latest is
`Builds/Diagnostics/native-lifetime-20261125T061935708762Z-c08646f2`; it is a diagnostic build rather than a final release.
The retained 0.4.2 archive and older test rows describe historical artifacts only.

Measured desktop island performance on a profiled private candidate is High 265.6 FPS / p99 9.44 ms and Ultra 179.0 FPS /
p99 11.28 ms at 1920×1080/Vulkan, AI active, all 20 waypoints, all sampled frames focused and native exit 0. Private Xvfb results
around 25 FPS were presentation-confounded. Neither set is a measurement of a future final binary or every camera view.

## Required next verification

- Verify strict full-five media and the final title/story/audio/skip/choice/handoff flow in the rebuilt player. These named
  Editor gates now pass, including normal80s playback; their scope does not establish the final native binary.
- Run current native rescue recovery and a complete rebuilt-player campaign/settings/media/queued-shortcut regression. Keep the
  rendered water check outside standalone batch mode. Automated movement/queued keys do not establish physical input or a human playthrough.
- Review gameplay graphics at actual resolution, listen to the voices and measure declared desktop presets on the final player.
- Prepare a fresh release artifact, current guides/issues/test report, checksums and an isolated extracted-player verification
  while retaining the user's preview. The current package shell script targets `Builds/Linux` and needs a safe explicit-candidate path.
- Continue mission and realism work from observed gameplay findings. All-bugs-fixed and the open-ended expansion request remain
  unproven; compilation, completed renders and narrow passing suites do not close the full objective.

`TEST_REPORT.md` retains the dated test history. `KNOWN_ISSUES.md` records validation limits; reconcile its older status paragraphs
with the final artifact before shipping. Build output and captures remain ignored under `Builds/` and `Logs/`.
