# Current upgrade verification — 25 November 2026

The **0.4.0 upgrade remains in progress**. This section supersedes earlier current-state claims below; prior release
records are preserved as historical evidence, not reused to certify changed source or a new executable.

| Check | Result and scope | Evidence |
|---|---|---|
| Fast Roslyn compile | PASS: 106 runtime / 19 editor files | `Tools/compile_check.sh all` |
| First expanded EditMode run | 52/53; failed the new traffic intersection fixture | `Logs/upgrade-editmode.xml` |
| Corrected expanded EditMode | PASS 53/53, including traffic frame-hitch/stop-line safety and continuation state rules | `Logs/upgrade-editmode-fix.xml` |
| Shared continuation logic, no graphics | PASS both Peace and Dominion flows; actual choice/Continue buttons, E targeting, timed work, pursuit cancellation, checkpoint reload/rearm, navigation reachability and ferry extraction | `Logs/afterlake-logic2.xml`, `Logs/afterlake-logic2.log` |
| Expanded asset/stealth rules | PASS 63/63, including Blender PBR/scale, permanent CCTV outages, live-cover capture checks and ejection-grace body discovery | `Logs/upgrade-stealth-rules.xml` |
| Full shared campaign logic, no graphics | Incomplete: scenarios01–06 PASS, then Editor exited139 during RouteB. No final XML; this is not a full-suite pass. OS apport confirms SIGSEGV; initiating cause remains unproven. | `Logs/upgrade-campaign-logic.log` |
| RTX Blender probes | OptiX on NVIDIA RTX 4050, CPU rendering disabled; opening and revised sunset-lake frames rendered and visually reviewed | `Logs/cinematic-films/*/probe.png`, Blender render logs |
| Full path-traced film renders | PASS: both 72-frame, 3-second 720p/24fps VP8 clips rendered with RTX 4050 OptiX, atomically encoded and fully decoded by ffmpeg; final-player decode is a separate gate | `ArtSource/Cinematics/*.render.json`, `Logs/blender-film-*-render.log` |
| First GPU film/graphics focus |4/5 PASS: real lake-arrival composition and three graphics-policy cases pass. Decoder-image test hit an async fixture-update race; its real-tick fix is being rerun. | `Logs/upgrade-films-graphics.xml`, `Logs/ui/05a-lakeside-rtx-arrival.png` |
| First riverside import rules |72/75 PASS; three triangle-count mismatches exposed collapsed bevel/near-collinear export faces. Cleaning source geometry and rerunning, with existing bounds/maps passing. | `Logs/upgrade-art-rules.xml` |
| Corrected imported assets/rules | PASS 84/84, including cleaned FBX geometry, physical texture density, owned mesh disposal and asymmetric front/back anchors | `Logs/upgrade-frozen-rules-final.xml` |
| Full GPU PlayMode before final ferry safeguard | PASS 17/17, including all 18 shared campaign scenarios; complete test order checked against source, no native exit in the frozen private-display run | `Logs/playmode-results.xml`, `Logs/playmode.log`, `Logs/upgrade-full-playmode-signals.log` |
| Final ferry-host safeguard | PASS 2/2 Peace/Dominion continuations after requiring all hosts released, rejecting direct-call/transit bypasses and checking cleared ownership links | `Logs/upgrade-ferry-host-release.xml` |
| Generated scenes | PASS: CityBuilder regenerated Bootstrap/City and environment/reflections;594,213 city LOD0 triangles | `Logs/upgrade-buildscenes.log` |
| City shader/serialization audit | PASS: regenerated city render reviewed; zero missing scripts, NVIDIA RTX 4050, 714,291 LOD0 triangles | `Logs/upgrade-city-photo.log`, `Logs/photos/02_market_st.png` |
| Initial Linux build | PASS: 968 MB, zero errors, two GroundWater implicit-derivative warnings on GL/Vulkan; not the final release candidate | `Logs/upgrade-linux.log` |
| Initial actual-player cinematic backend | PASS 2/2: RTX 4050/OpenGLCore, both VP8 clips advance at timeScale=0, populated pixel readback, clean exit0; PNGs reviewed | `Logs/standalone-0.4/films-initial.txt`, `films-initial-player.log` |
| Reflection sampling correction | PASS: explicit LOD preserves URP stereo/dynamic-resolution clamping; both target APIs rebuilt with zero warnings | commit `5fc21fa`, `Logs/upgrade-reflection-photo.log`, `Logs/upgrade-linux-final.log` |
| Final Linux candidate | PASS: 968 MB, 0 errors/0 warnings, source 846643bb0055a242262e25f48dd0b335a705f470; runtime DLL SHA 6d5baaf9b2c1b92481cfca58a760b794a31ca5154d5610a9e53559e52f69ec9b | `Logs/upgrade-linux-verified.log`, `Logs/standalone-0.4/final-build-info.json` |
| Final actual-player media | PASS 2/2 on RTX 4050/OpenGLCore: advancing frozen-time playback and nonempty pixel readbacks | `Logs/standalone-0.4/films-final.txt` |
| Actual-player shortcut regression | PASS 14/14 queued-keyboard cases, exit 0; performed before the benchmark-only walker update, which does not run in shortcut mode. This is not physical key validation. | `Logs/standalone-0.4/shortcut.txt`, `shortcut-player.log` |
| Benchmark route correction | A 35 s budget was shorter than the 193.7 m final leg at 4.6 m/s. Complete paths are now required and budgets derive from path length; waypoint coordinates unchanged. Runtime movement regression pending actual acceptance. | commit `846643b` |
| Final standalone campaign acceptance | Running all 18 shared scenarios on the final 968 MB candidate, including all-host ferry ownership and the corrected time-normalized diagnostic walker | `Logs/standalone-0.4/campaign-final-player.log` |
| Performance benchmark and release archive | Pending standalone campaign result; no fresh FPS or archive-checksum claim yet | Do not apply historical release results below to this artifact |

The first continuation run used source before the headless film guard landed and failed on missing in-progress footage;
`afterlake-logic2` is the successful rerun with Null-device bypass. This validates gameplay logic only, not video decode,
image quality, physical controls or performance. Later asset and scene checks supersede these earlier partial runs. The stable imported-asset/rules run is 84/84. The full GPU suite finished 17/17 with 18/18 campaign scenarios. The final ferry safeguard then passed both updated branch scenarios. Final-player coverage remains pending the new build.

Pushed feature commits: traffic `0cf6edf`, Blender set assets `910a2b1`, post-lake missions `acced2a`, stealth corrections `4c4b757`. Graphics and film integration are still being verified; the final Linux player has not yet been rebuilt for this upgrade. Compiler failure propagation is fixed and pushed in `e3f8175`; an isolated malformed-source fixture correctly returned exit 1, and the real test sources compile.

---

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


## Extended QA procedure catalogue

This appendix adds detailed procedures for reproducing and extending coverage of the shipped game.
The original verification report above is preserved byte for byte.
These are designed QA procedures, not newly executed tests or additional passing results.
The recorded release results above remain the authority for what was actually run.
Each procedure identifies its own setup, actions, expected behavior and evidence to collect.
A designed expectation must be checked against the cited source and observed behavior before a tester marks it passed.
No executable, generated scene, gameplay feature or test result was changed to add this appendix.

## Designed campaign verification procedures

**DESIGNED MANUAL PROCEDURES NOT EXECUTED IN THIS UPDATE.**
This appendix is a source-grounded manual QA plan, not an execution log or a new pass-count claim.
The original release report records the existing aggregate 13 gameplay scenarios; the cases below were not run for this documentation expansion.
Every CAM case remains Status Designed not run until a tester supplies dated evidence against a named executable and source revision.
Expected results are designed acceptance oracles derived from current code; timing, boundaries and observable feedback still require execution.
Source links are written relative to Docs because this fragment will be appended to Docs/TEST_REPORT.md.
Use the complete game guide for normal mission navigation; isolate each QA case so prerequisite flags from another run cannot contaminate its result.

### CAM-001 Fresh campaign journal

**Status:** Designed not run.
**Goal:** Verify fresh campaign journal as an isolated campaign behavior.
**Primary source:** [MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
**Preconditions:** New Game; no existing chapter flags.
**Actor configuration:** Free amoeba.
**Interaction target:** Starting alley.
**Before starting:** Record the target state and relevant flag expectation: first_host remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Start Begin Infiltration from the title.
2. Watch or deliberately skip the opening and release its input.
3. Open the pause journal without possessing anyone.
4. Compare the current chapter with the on-screen objective.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Mission 1 is current; historical cinematic actors did not count as acquired hosts.
**Expected flag condition:** first_host remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-001 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-002 Alley exit before first possession

**Status:** Designed not run.
**Goal:** Verify alley exit before first possession as an isolated campaign behavior.
**Primary source:** [MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
**Preconditions:** Mission 1; first_host unset.
**Actor configuration:** Free amoeba.
**Interaction target:** Alley mouth.
**Before starting:** Record the target state and relevant flag expectation: alley_exit set; first_host unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Move north through the alley mouth.
2. Stop outside the alley without taking a host.
3. Read the first two objective check marks.
4. Inspect the mission journal before continuing.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Alley exit can complete independently; first-host completion still requires taking a host.
**Expected flag condition:** alley_exit set; first_host unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-002 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-003 First host advances opening mission

**Status:** Designed not run.
**Goal:** Verify first host advances opening mission as an isolated campaign behavior.
**Primary source:** [MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
**Preconditions:** Mission 1 with an awake accessible civilian.
**Actor configuration:** Free amoeba entering civilian.
**Interaction target:** Nearby pedestrian.
**Before starting:** Record the target state and relevant flag expectation: first_host set.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Aim at the awake pedestrian within possession reach.
2. Press E when possession is offered.
3. Wait for the leap to finish.
4. Open the journal and inspect the next chapter.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The first acquired host satisfies the first-host progression condition.
**Expected flag condition:** first_host set.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-003 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-004 Optional split does not replace first host

**Status:** Designed not run.
**Goal:** Verify optional split does not replace first host as an isolated campaign behavior.
**Primary source:** [MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
**Preconditions:** Mission 1 with one free amoeba.
**Actor configuration:** Two free amoebas.
**Interaction target:** Starting alley.
**Before starting:** Record the target state and relevant flag expectation: tried_split may set; first_host remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Press V once.
2. Switch with Tab.
3. Remain in free form without possessing a human.
4. Inspect optional split and first-host objectives separately.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The optional split objective can complete without falsely completing the first-host objective.
**Expected flag condition:** tried_split may set; first_host remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-004 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-005 Newsstand with a human host

**Status:** Designed not run.
**Goal:** Verify newsstand with a human host as an isolated campaign behavior.
**Primary source:** [MissionProp.cs](../Assets/Parasite/Scripts/Missions/MissionProp.cs).
**Preconditions:** Mission 2; news_read unset.
**Actor configuration:** Civilian host.
**Interaction target:** Plaza newsstand.
**Before starting:** Record the target state and relevant flag expectation: news_read set.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Approach the newsstand until the interaction is offered.
2. Press E to start reading.
3. Remain close for its configured 2.5-second work duration.
4. Inspect the objective and completion toast.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Reading as a host sets the news objective once work completes.
**Expected flag condition:** news_read set.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-005 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-006 Newsstand rejects free form

**Status:** Designed not run.
**Goal:** Verify newsstand rejects free form as an isolated campaign behavior.
**Primary source:** [MissionProp.cs](../Assets/Parasite/Scripts/Missions/MissionProp.cs).
**Preconditions:** Mission 2; news_read unset.
**Actor configuration:** Free amoeba.
**Interaction target:** Plaza newsstand.
**Before starting:** Record the target state and relevant flag expectation: news_read remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Leave any host away from the newsstand.
2. Approach the same interaction point.
3. Read the denied prompt.
4. Attempt E and inspect the news objective.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The host requirement rejects the blob rather than completing the read.
**Expected flag condition:** news_read remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-006 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-007 Cafe listening as a host

**Status:** Designed not run.
**Goal:** Verify cafe listening as a host as an isolated campaign behavior.
**Primary source:** [EavesdropZone.cs](../Assets/Parasite/Scripts/Missions/EavesdropZone.cs).
**Preconditions:** Eavesdrop prerequisite satisfied; not pursuing.
**Actor configuration:** Human host.
**Interaction target:** Cafe eavesdrop zone.
**Before starting:** Record the target state and relevant flag expectation: eavesdropped set.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Approach Dr. Petrov within the configured 4.5-metre zone.
2. Stay close as the conversation captions progress.
3. Remain eligible through the configured nine-second listening duration.
4. Inspect journal completion and the resulting briefing.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Eligible listening completes the conversation objective without an E terminal interaction.
**Expected flag condition:** eavesdropped set.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-007 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-008 Main Gate access using Civilian

**Status:** Designed not run.
**Goal:** Verify main gate access using civilian as an isolated campaign behavior.
**Primary source:** [AccessDoor.cs](../Assets/Parasite/Scripts/Missions/AccessDoor.cs).
**Preconditions:** Before laboratory entry; supply cafe keycard for Staff Terminal or restored substation power for Service Tunnel.
**Actor configuration:** Civilian host.
**Interaction target:** Main Gate.
**Before starting:** Record the target state and relevant flag expectation: gate_open remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire an awake civilian without using the presentation shortcut.
2. Approach Main Gate and aim toward its interaction point.
3. Read the access prompt before pressing E.
4. Attempt entry, then inspect the door and recorded route.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Civilian lacks this route permission; the access attempt is denied and the entrance stays closed.
**Expected flag condition:** gate_open remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-008 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Profession permission matrix; designed and not executed.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-009 Main Gate access using Guard

**Status:** Designed not run.
**Goal:** Verify main gate access using guard as an isolated campaign behavior.
**Primary source:** [AccessDoor.cs](../Assets/Parasite/Scripts/Missions/AccessDoor.cs).
**Preconditions:** Before laboratory entry; supply cafe keycard for Staff Terminal or restored substation power for Service Tunnel.
**Actor configuration:** Guard host.
**Interaction target:** Main Gate.
**Before starting:** Record the target state and relevant flag expectation: gate_open may set after accepted opening.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire an awake guard without using the presentation shortcut.
2. Approach Main Gate and aim toward its interaction point.
3. Read the access prompt before pressing E.
4. Attempt entry, then inspect the door and recorded route.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Guard is accepted by the SecurityGate permission gate.
**Expected flag condition:** gate_open may set after accepted opening.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-009 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Profession permission matrix; designed and not executed.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-010 Main Gate access using Scientist

**Status:** Designed not run.
**Goal:** Verify main gate access using scientist as an isolated campaign behavior.
**Primary source:** [AccessDoor.cs](../Assets/Parasite/Scripts/Missions/AccessDoor.cs).
**Preconditions:** Before laboratory entry; supply cafe keycard for Staff Terminal or restored substation power for Service Tunnel.
**Actor configuration:** Scientist host.
**Interaction target:** Main Gate.
**Before starting:** Record the target state and relevant flag expectation: gate_open remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire an awake scientist without using the presentation shortcut.
2. Approach Main Gate and aim toward its interaction point.
3. Read the access prompt before pressing E.
4. Attempt entry, then inspect the door and recorded route.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Scientist lacks this route permission; the access attempt is denied and the entrance stays closed.
**Expected flag condition:** gate_open remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-010 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Profession permission matrix; designed and not executed.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-011 Main Gate access using Maintenance

**Status:** Designed not run.
**Goal:** Verify main gate access using maintenance as an isolated campaign behavior.
**Primary source:** [AccessDoor.cs](../Assets/Parasite/Scripts/Missions/AccessDoor.cs).
**Preconditions:** Before laboratory entry; supply cafe keycard for Staff Terminal or restored substation power for Service Tunnel.
**Actor configuration:** Maintenance host.
**Interaction target:** Main Gate.
**Before starting:** Record the target state and relevant flag expectation: gate_open remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire an awake maintenance without using the presentation shortcut.
2. Approach Main Gate and aim toward its interaction point.
3. Read the access prompt before pressing E.
4. Attempt entry, then inspect the door and recorded route.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Maintenance lacks this route permission; the access attempt is denied and the entrance stays closed.
**Expected flag condition:** gate_open remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-011 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Profession permission matrix; designed and not executed.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-012 Staff Terminal access using Civilian

**Status:** Designed not run.
**Goal:** Verify staff terminal access using civilian as an isolated campaign behavior.
**Primary source:** [StaffTerminal.cs](../Assets/Parasite/Scripts/Missions/StaffTerminal.cs).
**Preconditions:** Before laboratory entry; supply cafe keycard for Staff Terminal or restored substation power for Service Tunnel.
**Actor configuration:** Civilian host.
**Interaction target:** Staff Terminal.
**Before starting:** Record the target state and relevant flag expectation: staff_open remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire an awake civilian without using the presentation shortcut.
2. Approach Staff Terminal and aim toward its interaction point.
3. Read the access prompt before pressing E.
4. Attempt entry, then inspect the door and recorded route.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Civilian lacks this route permission; the access attempt is denied and the entrance stays closed.
**Expected flag condition:** staff_open remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-012 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Profession permission matrix; designed and not executed.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-013 Staff Terminal access using Guard

**Status:** Designed not run.
**Goal:** Verify staff terminal access using guard as an isolated campaign behavior.
**Primary source:** [StaffTerminal.cs](../Assets/Parasite/Scripts/Missions/StaffTerminal.cs).
**Preconditions:** Before laboratory entry; supply cafe keycard for Staff Terminal or restored substation power for Service Tunnel.
**Actor configuration:** Guard host.
**Interaction target:** Staff Terminal.
**Before starting:** Record the target state and relevant flag expectation: staff_open remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire an awake guard without using the presentation shortcut.
2. Approach Staff Terminal and aim toward its interaction point.
3. Read the access prompt before pressing E.
4. Attempt entry, then inspect the door and recorded route.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Guard lacks this route permission; the access attempt is denied and the entrance stays closed.
**Expected flag condition:** staff_open remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-013 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Profession permission matrix; designed and not executed.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-014 Staff Terminal access using Scientist

**Status:** Designed not run.
**Goal:** Verify staff terminal access using scientist as an isolated campaign behavior.
**Primary source:** [StaffTerminal.cs](../Assets/Parasite/Scripts/Missions/StaffTerminal.cs).
**Preconditions:** Before laboratory entry; supply cafe keycard for Staff Terminal or restored substation power for Service Tunnel.
**Actor configuration:** Scientist host.
**Interaction target:** Staff Terminal.
**Before starting:** Record the target state and relevant flag expectation: staff_open may set after accepted opening.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire an awake scientist without using the presentation shortcut.
2. Approach Staff Terminal and aim toward its interaction point.
3. Read the access prompt before pressing E.
4. Attempt entry, then inspect the door and recorded route.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Scientist is accepted by the StaffTerminal permission gate.
**Expected flag condition:** staff_open may set after accepted opening.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-014 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Profession permission matrix; designed and not executed.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-015 Staff Terminal access using Maintenance

**Status:** Designed not run.
**Goal:** Verify staff terminal access using maintenance as an isolated campaign behavior.
**Primary source:** [StaffTerminal.cs](../Assets/Parasite/Scripts/Missions/StaffTerminal.cs).
**Preconditions:** Before laboratory entry; supply cafe keycard for Staff Terminal or restored substation power for Service Tunnel.
**Actor configuration:** Maintenance host.
**Interaction target:** Staff Terminal.
**Before starting:** Record the target state and relevant flag expectation: staff_open remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire an awake maintenance without using the presentation shortcut.
2. Approach Staff Terminal and aim toward its interaction point.
3. Read the access prompt before pressing E.
4. Attempt entry, then inspect the door and recorded route.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Maintenance lacks this route permission; the access attempt is denied and the entrance stays closed.
**Expected flag condition:** staff_open remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-015 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Profession permission matrix; designed and not executed.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-016 Service Tunnel access using Civilian

**Status:** Designed not run.
**Goal:** Verify service tunnel access using civilian as an isolated campaign behavior.
**Primary source:** [AccessDoor.cs](../Assets/Parasite/Scripts/Missions/AccessDoor.cs).
**Preconditions:** Before laboratory entry; supply cafe keycard for Staff Terminal or restored substation power for Service Tunnel.
**Actor configuration:** Civilian host.
**Interaction target:** Service Tunnel.
**Before starting:** Record the target state and relevant flag expectation: tunnel_open remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire an awake civilian without using the presentation shortcut.
2. Approach Service Tunnel and aim toward its interaction point.
3. Read the access prompt before pressing E.
4. Attempt entry, then inspect the door and recorded route.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Civilian lacks this route permission; the access attempt is denied and the entrance stays closed.
**Expected flag condition:** tunnel_open remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-016 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Profession permission matrix; designed and not executed.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-017 Service Tunnel access using Guard

**Status:** Designed not run.
**Goal:** Verify service tunnel access using guard as an isolated campaign behavior.
**Primary source:** [AccessDoor.cs](../Assets/Parasite/Scripts/Missions/AccessDoor.cs).
**Preconditions:** Before laboratory entry; supply cafe keycard for Staff Terminal or restored substation power for Service Tunnel.
**Actor configuration:** Guard host.
**Interaction target:** Service Tunnel.
**Before starting:** Record the target state and relevant flag expectation: tunnel_open remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire an awake guard without using the presentation shortcut.
2. Approach Service Tunnel and aim toward its interaction point.
3. Read the access prompt before pressing E.
4. Attempt entry, then inspect the door and recorded route.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Guard lacks this route permission; the access attempt is denied and the entrance stays closed.
**Expected flag condition:** tunnel_open remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-017 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Profession permission matrix; designed and not executed.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-018 Service Tunnel access using Scientist

**Status:** Designed not run.
**Goal:** Verify service tunnel access using scientist as an isolated campaign behavior.
**Primary source:** [AccessDoor.cs](../Assets/Parasite/Scripts/Missions/AccessDoor.cs).
**Preconditions:** Before laboratory entry; supply cafe keycard for Staff Terminal or restored substation power for Service Tunnel.
**Actor configuration:** Scientist host.
**Interaction target:** Service Tunnel.
**Before starting:** Record the target state and relevant flag expectation: tunnel_open remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire an awake scientist without using the presentation shortcut.
2. Approach Service Tunnel and aim toward its interaction point.
3. Read the access prompt before pressing E.
4. Attempt entry, then inspect the door and recorded route.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Scientist lacks this route permission; the access attempt is denied and the entrance stays closed.
**Expected flag condition:** tunnel_open remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-018 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Profession permission matrix; designed and not executed.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-019 Service Tunnel access using Maintenance

**Status:** Designed not run.
**Goal:** Verify service tunnel access using maintenance as an isolated campaign behavior.
**Primary source:** [AccessDoor.cs](../Assets/Parasite/Scripts/Missions/AccessDoor.cs).
**Preconditions:** Before laboratory entry; supply cafe keycard for Staff Terminal or restored substation power for Service Tunnel.
**Actor configuration:** Maintenance host.
**Interaction target:** Service Tunnel.
**Before starting:** Record the target state and relevant flag expectation: tunnel_open may set after accepted opening.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire an awake maintenance without using the presentation shortcut.
2. Approach Service Tunnel and aim toward its interaction point.
3. Read the access prompt before pressing E.
4. Attempt entry, then inspect the door and recorded route.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Maintenance is accepted by the ServiceTunnel permission gate.
**Expected flag condition:** tunnel_open may set after accepted opening.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-019 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Profession permission matrix; designed and not executed.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-020 Staff keycard missing

**Status:** Designed not run.
**Goal:** Verify staff keycard missing as an isolated campaign behavior.
**Primary source:** [StaffTerminal.cs](../Assets/Parasite/Scripts/Missions/StaffTerminal.cs).
**Preconditions:** No keycard flag; Staff Terminal unused.
**Actor configuration:** Scientist host.
**Interaction target:** Staff Terminal.
**Before starting:** Record the target state and relevant flag expectation: staff_open unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Reach the prerequisite-controlled object without completing its requirement.
2. Read the current prompt and displayed denial reason.
3. Press E once while the requirement remains unsatisfied.
4. Inspect the entrance state and mission objective after the denial.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Scientist credentials alone do not bypass the keycard prerequisite.
**Expected flag condition:** staff_open unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-020 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-021 Tunnel power missing

**Status:** Designed not run.
**Goal:** Verify tunnel power missing as an isolated campaign behavior.
**Primary source:** [AccessDoor.cs](../Assets/Parasite/Scripts/Missions/AccessDoor.cs).
**Preconditions:** No tunnel_power flag; tunnel closed.
**Actor configuration:** Maintenance host.
**Interaction target:** Service Tunnel.
**Before starting:** Record the target state and relevant flag expectation: tunnel_open unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Reach the prerequisite-controlled object without completing its requirement.
2. Read the current prompt and displayed denial reason.
3. Press E once while the requirement remains unsatisfied.
4. Inspect the entrance state and mission objective after the denial.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Maintenance credentials alone do not bypass the actuator power prerequisite.
**Expected flag condition:** tunnel_open unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-021 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-022 Cafe keycard wrong profession

**Status:** Designed not run.
**Goal:** Verify cafe keycard wrong profession as an isolated campaign behavior.
**Primary source:** [MissionProp.cs](../Assets/Parasite/Scripts/Missions/MissionProp.cs).
**Preconditions:** Keycard still available.
**Actor configuration:** Civilian host.
**Interaction target:** Cafe keycard.
**Before starting:** Record the target state and relevant flag expectation: keycard unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Reach the prerequisite-controlled object without completing its requirement.
2. Read the current prompt and displayed denial reason.
3. Press E once while the requirement remains unsatisfied.
4. Inspect the entrance state and mission objective after the denial.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The keycard interaction rejects a host without StaffTerminal permission.
**Expected flag condition:** keycard unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-022 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-023 Substation wrong profession

**Status:** Designed not run.
**Goal:** Verify substation wrong profession as an isolated campaign behavior.
**Primary source:** [MissionProp.cs](../Assets/Parasite/Scripts/Missions/MissionProp.cs).
**Preconditions:** Substation objective incomplete.
**Actor configuration:** Scientist host.
**Interaction target:** Substation 4.
**Before starting:** Record the target state and relevant flag expectation: tunnel_power unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Reach the prerequisite-controlled object without completing its requirement.
2. Read the current prompt and displayed denial reason.
3. Press E once while the requirement remains unsatisfied.
4. Inspect the entrance state and mission objective after the denial.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The power-reset interaction requires ServiceTunnel permission.
**Expected flag condition:** tunnel_power unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-023 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-024 Lab A first download

**Status:** Designed not run.
**Goal:** Verify lab a first download as an isolated campaign behavior.
**Primary source:** [MissionProp.cs](../Assets/Parasite/Scripts/Missions/MissionProp.cs).
**Preconditions:** Research incomplete; staff host available.
**Actor configuration:** Staff host or hosts.
**Interaction target:** Lab A terminal.
**Before starting:** Record the target state and relevant flag expectation: data_a set; research_done depends on data_b.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Enter Lab A as an authorized staff host.
2. Start its download with E.
3. Stay near the terminal through the configured four-second work.
4. Check the individual terminal and combined research objectives.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Only the completed terminal flag is earned until Lab B is also copied.
**Expected flag condition:** data_a set; research_done depends on data_b.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-024 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-025 Lab B first download

**Status:** Designed not run.
**Goal:** Verify lab b first download as an isolated campaign behavior.
**Primary source:** [MissionProp.cs](../Assets/Parasite/Scripts/Missions/MissionProp.cs).
**Preconditions:** Research incomplete; staff host available.
**Actor configuration:** Staff host or hosts.
**Interaction target:** Lab B terminal.
**Before starting:** Record the target state and relevant flag expectation: data_b set; data_a unchanged.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Enter Lab B before copying Lab A.
2. Start its download with E.
3. Remain close until completion.
4. Inspect research before visiting Lab A.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Download order does not falsely supply the missing Lab A file.
**Expected flag condition:** data_b set; data_a unchanged.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-025 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-026 Research aggregation after second terminal

**Status:** Designed not run.
**Goal:** Verify research aggregation after second terminal as an isolated campaign behavior.
**Primary source:** [MissionProp.cs](../Assets/Parasite/Scripts/Missions/MissionProp.cs).
**Preconditions:** Exactly one terminal flag set.
**Actor configuration:** Staff host or hosts.
**Interaction target:** Both research terminals.
**Before starting:** Record the target state and relevant flag expectation: data_a and data_b imply research_done.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Confirm which terminal was already completed.
2. Visit the other lab as a staff host.
3. Complete that download normally.
4. Read the office-reader requirement again.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Combined research is earned only once both terminal flags exist.
**Expected flag condition:** data_a and data_b imply research_done.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-026 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-027 Reader prerequisite before research

**Status:** Designed not run.
**Goal:** Verify reader prerequisite before research as an isolated campaign behavior.
**Primary source:** [DualAuthReader.cs](../Assets/Parasite/Scripts/Missions/DualAuthReader.cs).
**Preconditions:** Both data downloads incomplete.
**Actor configuration:** Staff host or hosts.
**Interaction target:** Office Reader A.
**Before starting:** Record the target state and relevant flag expectation: office_open unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Reach the corridor reader as a staff host.
2. Read its prerequisite explanation.
3. Attempt to badge before downloading both files.
4. Inspect office opening and research flags.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** A staff badge cannot bypass the research prerequisite.
**Expected flag condition:** office_open unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-027 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-028 Single amoeba tries both readers

**Status:** Designed not run.
**Goal:** Verify single amoeba tries both readers as an isolated campaign behavior.
**Primary source:** [DualAuthReader.cs](../Assets/Parasite/Scripts/Missions/DualAuthReader.cs).
**Preconditions:** Research complete; office closed.
**Actor configuration:** Staff host or hosts.
**Interaction target:** Office readers.
**Before starting:** Record the target state and relevant flag expectation: office_open unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Badge Reader A with one staff host.
2. Move that same amoeba to Reader B while A is armed.
3. Read the second prompt and attempt E.
4. Inspect the office door before the arm expires.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The same amoeba cannot satisfy both sides of the two-person rule.
**Expected flag condition:** office_open unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-028 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-029 Two independent staff readers

**Status:** Designed not run.
**Goal:** Verify two independent staff readers as an isolated campaign behavior.
**Primary source:** [DualAuthReader.cs](../Assets/Parasite/Scripts/Missions/DualAuthReader.cs).
**Preconditions:** Research complete; two distinct amoebas in staff hosts.
**Actor configuration:** Staff host or hosts.
**Interaction target:** Office readers.
**Before starting:** Record the target state and relevant flag expectation: office_open set.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Place one staff host at each reader.
2. Badge the first reader.
3. Switch bodies with Tab and badge the other within eight seconds.
4. Inspect the office door and the mission journal.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Distinct staff operators satisfy the window and open the office.
**Expected flag condition:** office_open set.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-029 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-030 Reader window expires

**Status:** Designed not run.
**Goal:** Verify reader window expires as an isolated campaign behavior.
**Primary source:** [DualAuthReader.cs](../Assets/Parasite/Scripts/Missions/DualAuthReader.cs).
**Preconditions:** Research complete; office closed.
**Actor configuration:** Staff host or hosts.
**Interaction target:** Office readers.
**Before starting:** Record the target state and relevant flag expectation: office_open unset until a fresh valid pair.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Badge one reader.
2. Wait more than its eight-second arm window without badging the other.
3. Badge only the second reader.
4. Inspect whether fresh authorization is still required on the first.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** An expired arm does not combine with a later badge as if it were current.
**Expected flag condition:** office_open unset until a fresh valid pair.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-030 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-031 Vale requires office and research

**Status:** Designed not run.
**Goal:** Verify vale requires office and research as an isolated campaign behavior.
**Primary source:** [DrValeInteraction.cs](../Assets/Parasite/Scripts/Missions/DrValeInteraction.cs).
**Preconditions:** Research or office prerequisite incomplete.
**Actor configuration:** Eligible active body.
**Interaction target:** Dr. Vale.
**Before starting:** Record the target state and relevant flag expectation: confronted unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Approach Vale without both prerequisites.
2. Read the confrontation denial.
3. Attempt E once.
4. Inspect mission progress without using a cheat.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** A partial prerequisite does not start the reveal.
**Expected flag condition:** confronted unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-031 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-032 Vale reveal resumes the campaign

**Status:** Designed not run.
**Goal:** Verify vale reveal resumes the campaign as an isolated campaign behavior.
**Primary source:** [MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
**Preconditions:** Office and research complete.
**Actor configuration:** Eligible active body.
**Interaction target:** Dr. Vale.
**Before starting:** Record the target state and relevant flag expectation: confronted set after reveal.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Start the confrontation.
2. Observe the protected dialogue.
3. Advance or finish its captions.
4. Confirm player control returns with the signed-archive chapter current.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The doctor encounter continues the campaign instead of publishing final statistics.
**Expected flag condition:** confronted set after reveal.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-032 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-033 Repeated Vale interaction after reveal

**Status:** Designed not run.
**Goal:** Verify repeated vale interaction after reveal as an isolated campaign behavior.
**Primary source:** [MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
**Preconditions:** Confronted already set.
**Actor configuration:** Eligible active body.
**Interaction target:** Dr. Vale.
**Before starting:** Record the target state and relevant flag expectation: confronted remains set.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Return to Vale after control resumes.
2. Aim at the previous interaction point.
3. Check whether the completed confrontation is offered.
4. Inspect the current post-Vale objective.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The reveal is retired after completion and does not repeatedly restart.
**Expected flag condition:** confronted remains set.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-033 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-034 Protected Vale encounter under pursuit

**Status:** Designed not run.
**Goal:** Verify protected vale encounter under pursuit as an isolated campaign behavior.
**Primary source:** [MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
**Preconditions:** Office and research complete; controlled nearby-guard setup.
**Actor configuration:** Eligible active body.
**Interaction target:** Dr. Vale.
**Before starting:** Record the target state and relevant flag expectation: confronted changes only on story completion.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Create a reproducible pursuing guard situation and record its position.
2. Initiate Vale before capture.
3. Observe world motion and capture counters during the locked story.
4. Finish the dialogue and compare counters with the initial record.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Story protection prevents world/capture progression during locked dialogue.
**Expected flag condition:** confronted changes only on story completion.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-034 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-035 Coolant bypass role gate using Civilian

**Status:** Designed not run.
**Goal:** Verify coolant bypass role gate using civilian as an isolated campaign behavior.
**Primary source:** [CooperativeConsole.cs](../Assets/Parasite/Scripts/Missions/CooperativeConsole.cs).
**Preconditions:** confronted set; signed_archive unset.
**Actor configuration:** Civilian host.
**Interaction target:** Coolant bypass.
**Before starting:** Record the target state and relevant flag expectation: signed_archive is not earned merely by arming support.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire a civilian body.
2. Approach the marked support console.
3. Read its profession requirement, then press E.
4. Inspect the status lamp and dependent operation prompt.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** This Civilian cannot arm the Maintenance-only support control.
**Expected flag condition:** signed_archive is not earned merely by arming support.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-035 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-036 Coolant bypass role gate using Guard

**Status:** Designed not run.
**Goal:** Verify coolant bypass role gate using guard as an isolated campaign behavior.
**Primary source:** [CooperativeConsole.cs](../Assets/Parasite/Scripts/Missions/CooperativeConsole.cs).
**Preconditions:** confronted set; signed_archive unset.
**Actor configuration:** Guard host.
**Interaction target:** Coolant bypass.
**Before starting:** Record the target state and relevant flag expectation: signed_archive is not earned merely by arming support.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire a guard body.
2. Approach the marked support console.
3. Read its profession requirement, then press E.
4. Inspect the status lamp and dependent operation prompt.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** This Guard cannot arm the Maintenance-only support control.
**Expected flag condition:** signed_archive is not earned merely by arming support.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-036 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-037 Coolant bypass role gate using Scientist

**Status:** Designed not run.
**Goal:** Verify coolant bypass role gate using scientist as an isolated campaign behavior.
**Primary source:** [CooperativeConsole.cs](../Assets/Parasite/Scripts/Missions/CooperativeConsole.cs).
**Preconditions:** confronted set; signed_archive unset.
**Actor configuration:** Scientist host.
**Interaction target:** Coolant bypass.
**Before starting:** Record the target state and relevant flag expectation: signed_archive is not earned merely by arming support.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire a scientist body.
2. Approach the marked support console.
3. Read its profession requirement, then press E.
4. Inspect the status lamp and dependent operation prompt.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** This Scientist cannot arm the Maintenance-only support control.
**Expected flag condition:** signed_archive is not earned merely by arming support.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-037 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-038 Coolant bypass role gate using Maintenance

**Status:** Designed not run.
**Goal:** Verify coolant bypass role gate using maintenance as an isolated campaign behavior.
**Primary source:** [CooperativeConsole.cs](../Assets/Parasite/Scripts/Missions/CooperativeConsole.cs).
**Preconditions:** confronted set; signed_archive unset.
**Actor configuration:** Maintenance host.
**Interaction target:** Coolant bypass.
**Before starting:** Record the target state and relevant flag expectation: signed_archive is not earned merely by arming support.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire a maintenance body.
2. Approach the marked support console.
3. Read its profession requirement, then press E.
4. Inspect the status lamp and dependent operation prompt.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** This Maintenance may hold coolant while remaining in range.
**Expected flag condition:** signed_archive is not earned merely by arming support.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-038 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-039 Dispatch override role gate using Civilian

**Status:** Designed not run.
**Goal:** Verify dispatch override role gate using civilian as an isolated campaign behavior.
**Primary source:** [CooperativeConsole.cs](../Assets/Parasite/Scripts/Missions/CooperativeConsole.cs).
**Preconditions:** signed_archive set; containment_offline unset.
**Actor configuration:** Civilian host.
**Interaction target:** Dispatch override.
**Before starting:** Record the target state and relevant flag expectation: containment_offline is not earned merely by arming support.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire a civilian body.
2. Approach the marked support console.
3. Read its profession requirement, then press E.
4. Inspect the status lamp and dependent operation prompt.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** This Civilian cannot arm the Guard-only support control.
**Expected flag condition:** containment_offline is not earned merely by arming support.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-039 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-040 Dispatch override role gate using Guard

**Status:** Designed not run.
**Goal:** Verify dispatch override role gate using guard as an isolated campaign behavior.
**Primary source:** [CooperativeConsole.cs](../Assets/Parasite/Scripts/Missions/CooperativeConsole.cs).
**Preconditions:** signed_archive set; containment_offline unset.
**Actor configuration:** Guard host.
**Interaction target:** Dispatch override.
**Before starting:** Record the target state and relevant flag expectation: containment_offline is not earned merely by arming support.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire a guard body.
2. Approach the marked support console.
3. Read its profession requirement, then press E.
4. Inspect the status lamp and dependent operation prompt.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** This Guard may hold dispatch while remaining in range.
**Expected flag condition:** containment_offline is not earned merely by arming support.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-040 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-041 Dispatch override role gate using Scientist

**Status:** Designed not run.
**Goal:** Verify dispatch override role gate using scientist as an isolated campaign behavior.
**Primary source:** [CooperativeConsole.cs](../Assets/Parasite/Scripts/Missions/CooperativeConsole.cs).
**Preconditions:** signed_archive set; containment_offline unset.
**Actor configuration:** Scientist host.
**Interaction target:** Dispatch override.
**Before starting:** Record the target state and relevant flag expectation: containment_offline is not earned merely by arming support.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire a scientist body.
2. Approach the marked support console.
3. Read its profession requirement, then press E.
4. Inspect the status lamp and dependent operation prompt.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** This Scientist cannot arm the Guard-only support control.
**Expected flag condition:** containment_offline is not earned merely by arming support.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-041 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-042 Dispatch override role gate using Maintenance

**Status:** Designed not run.
**Goal:** Verify dispatch override role gate using maintenance as an isolated campaign behavior.
**Primary source:** [CooperativeConsole.cs](../Assets/Parasite/Scripts/Missions/CooperativeConsole.cs).
**Preconditions:** signed_archive set; containment_offline unset.
**Actor configuration:** Maintenance host.
**Interaction target:** Dispatch override.
**Before starting:** Record the target state and relevant flag expectation: containment_offline is not earned merely by arming support.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Acquire a maintenance body.
2. Approach the marked support console.
3. Read its profession requirement, then press E.
4. Inspect the status lamp and dependent operation prompt.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** This Maintenance cannot arm the Guard-only support control.
**Expected flag condition:** containment_offline is not earned merely by arming support.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-042 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-043 Signed archive operator role using Civilian

**Status:** Designed not run.
**Goal:** Verify signed archive operator role using civilian as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** confronted set; other amoeba validly holds coolant; signed_archive unset.
**Actor configuration:** Civilian host.
**Interaction target:** Signed archive.
**Before starting:** Record the target state and relevant flag expectation: signed_archive remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Keep the independent support host at coolant.
2. Bring the active civilian to the operation terminal.
3. Read the operation prompt and press E.
4. If eligible, stay positioned for the configured 8-second duration; otherwise record the denial.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Civilian is rejected by this operation profession gate.
**Expected flag condition:** signed_archive remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-043 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-044 Signed archive operator role using Guard

**Status:** Designed not run.
**Goal:** Verify signed archive operator role using guard as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** confronted set; other amoeba validly holds coolant; signed_archive unset.
**Actor configuration:** Guard host.
**Interaction target:** Signed archive.
**Before starting:** Record the target state and relevant flag expectation: signed_archive remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Keep the independent support host at coolant.
2. Bring the active guard to the operation terminal.
3. Read the operation prompt and press E.
4. If eligible, stay positioned for the configured 8-second duration; otherwise record the denial.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Guard is rejected by this operation profession gate.
**Expected flag condition:** signed_archive remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-044 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-045 Signed archive operator role using Scientist

**Status:** Designed not run.
**Goal:** Verify signed archive operator role using scientist as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** confronted set; other amoeba validly holds coolant; signed_archive unset.
**Actor configuration:** Scientist host.
**Interaction target:** Signed archive.
**Before starting:** Record the target state and relevant flag expectation: signed_archive sets only after uninterrupted completion.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Keep the independent support host at coolant.
2. Bring the active scientist to the operation terminal.
3. Read the operation prompt and press E.
4. If eligible, stay positioned for the configured 8-second duration; otherwise record the denial.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Scientist can perform the operation while the other host maintains support.
**Expected flag condition:** signed_archive sets only after uninterrupted completion.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-045 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-046 Signed archive operator role using Maintenance

**Status:** Designed not run.
**Goal:** Verify signed archive operator role using maintenance as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** confronted set; other amoeba validly holds coolant; signed_archive unset.
**Actor configuration:** Maintenance host.
**Interaction target:** Signed archive.
**Before starting:** Record the target state and relevant flag expectation: signed_archive remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Keep the independent support host at coolant.
2. Bring the active maintenance to the operation terminal.
3. Read the operation prompt and press E.
4. If eligible, stay positioned for the configured 8-second duration; otherwise record the denial.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Maintenance is rejected by this operation profession gate.
**Expected flag condition:** signed_archive remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-046 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-047 Containment relay operator role using Civilian

**Status:** Designed not run.
**Goal:** Verify containment relay operator role using civilian as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** signed_archive set; other amoeba validly holds dispatch; containment_offline unset.
**Actor configuration:** Civilian host.
**Interaction target:** Containment relay.
**Before starting:** Record the target state and relevant flag expectation: containment_offline remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Keep the independent support host at dispatch.
2. Bring the active civilian to the operation terminal.
3. Read the operation prompt and press E.
4. If eligible, stay positioned for the configured 7-second duration; otherwise record the denial.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Civilian is rejected by this operation profession gate.
**Expected flag condition:** containment_offline remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-047 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-048 Containment relay operator role using Guard

**Status:** Designed not run.
**Goal:** Verify containment relay operator role using guard as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** signed_archive set; other amoeba validly holds dispatch; containment_offline unset.
**Actor configuration:** Guard host.
**Interaction target:** Containment relay.
**Before starting:** Record the target state and relevant flag expectation: containment_offline remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Keep the independent support host at dispatch.
2. Bring the active guard to the operation terminal.
3. Read the operation prompt and press E.
4. If eligible, stay positioned for the configured 7-second duration; otherwise record the denial.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Guard is rejected by this operation profession gate.
**Expected flag condition:** containment_offline remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-048 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-049 Containment relay operator role using Scientist

**Status:** Designed not run.
**Goal:** Verify containment relay operator role using scientist as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** signed_archive set; other amoeba validly holds dispatch; containment_offline unset.
**Actor configuration:** Scientist host.
**Interaction target:** Containment relay.
**Before starting:** Record the target state and relevant flag expectation: containment_offline remains unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Keep the independent support host at dispatch.
2. Bring the active scientist to the operation terminal.
3. Read the operation prompt and press E.
4. If eligible, stay positioned for the configured 7-second duration; otherwise record the denial.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Scientist is rejected by this operation profession gate.
**Expected flag condition:** containment_offline remains unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-049 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-050 Containment relay operator role using Maintenance

**Status:** Designed not run.
**Goal:** Verify containment relay operator role using maintenance as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** signed_archive set; other amoeba validly holds dispatch; containment_offline unset.
**Actor configuration:** Maintenance host.
**Interaction target:** Containment relay.
**Before starting:** Record the target state and relevant flag expectation: containment_offline sets only after uninterrupted completion.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Keep the independent support host at dispatch.
2. Bring the active maintenance to the operation terminal.
3. Read the operation prompt and press E.
4. If eligible, stay positioned for the configured 7-second duration; otherwise record the denial.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Maintenance can perform the operation while the other host maintains support.
**Expected flag condition:** containment_offline sets only after uninterrupted completion.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-050 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-051 Signed archive interruption when support leaves range

**Status:** Designed not run.
**Goal:** Verify signed archive interruption when support leaves range as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** confronted set; valid paired work can start.
**Actor configuration:** Scientist operator plus required support host.
**Interaction target:** Signed archive.
**Before starting:** Record the target state and relevant flag expectation: signed_archive remains unset after interrupted work.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Arm coolant with the other amoeba.
2. Start the operation and record a visible nonzero progress value.
3. Switch to the coolant operator and move clearly outside its 2.4-metre radius.
4. Inspect progress and completion flags before attempting any restart.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The dependent operation interrupts when HeldByOther becomes false.
**Expected flag condition:** signed_archive remains unset after interrupted work.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-051 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Designed interruption scenario; boundary behavior requires execution evidence.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-052 Signed archive interruption when operator leaves range

**Status:** Designed not run.
**Goal:** Verify signed archive interruption when operator leaves range as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** confronted set; valid paired work can start.
**Actor configuration:** Scientist operator plus required support host.
**Interaction target:** Signed archive.
**Before starting:** Record the target state and relevant flag expectation: signed_archive remains unset after interrupted work.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Arm coolant with the other amoeba.
2. Start the operation and record a visible nonzero progress value.
3. Move the operation host clearly outside the terminal 2.4-metre radius.
4. Inspect progress and completion flags before attempting any restart.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The operation interrupts when its operator moves out of range.
**Expected flag condition:** signed_archive remains unset after interrupted work.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-052 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Designed interruption scenario; boundary behavior requires execution evidence.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-053 Signed archive interruption when operator abandons host

**Status:** Designed not run.
**Goal:** Verify signed archive interruption when operator abandons host as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** confronted set; valid paired work can start.
**Actor configuration:** Scientist operator plus required support host.
**Interaction target:** Signed archive.
**Before starting:** Record the target state and relevant flag expectation: signed_archive remains unset after interrupted work.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Arm coolant with the other amoeba.
2. Start the operation and record a visible nonzero progress value.
3. Press Q on the operation host during work.
4. Inspect progress and completion flags before attempting any restart.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The operation loses its original possessed host and interrupts.
**Expected flag condition:** signed_archive remains unset after interrupted work.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-053 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Designed interruption scenario; boundary behavior requires execution evidence.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-054 Signed archive interruption when support abandons host

**Status:** Designed not run.
**Goal:** Verify signed archive interruption when support abandons host as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** confronted set; valid paired work can start.
**Actor configuration:** Scientist operator plus required support host.
**Interaction target:** Signed archive.
**Before starting:** Record the target state and relevant flag expectation: signed_archive remains unset after interrupted work.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Arm coolant with the other amoeba.
2. Start the operation and record a visible nonzero progress value.
3. Switch to the coolant body and press Q.
4. Inspect progress and completion flags before attempting any restart.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The operation interrupts because the armed support no longer has its original possessed host.
**Expected flag condition:** signed_archive remains unset after interrupted work.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-054 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Designed interruption scenario; boundary behavior requires execution evidence.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-055 Signed archive interruption when pause during work

**Status:** Designed not run.
**Goal:** Verify signed archive interruption when pause during work as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** confronted set; valid paired work can start.
**Actor configuration:** Scientist operator plus required support host.
**Interaction target:** Signed archive.
**Before starting:** Record the target state and relevant flag expectation: signed_archive remains incomplete until resumed work finishes.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Arm coolant with the other amoeba.
2. Start the operation and record a visible nonzero progress value.
3. Pause through Esc, wait while paused, then resume.
4. Inspect progress and completion flags before attempting any restart.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Scaled work time does not advance while paused; valid positions allow continuation after resume.
**Expected flag condition:** signed_archive remains incomplete until resumed work finishes.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-055 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Designed interruption scenario; boundary behavior requires execution evidence.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-056 Containment relay interruption when support leaves range

**Status:** Designed not run.
**Goal:** Verify containment relay interruption when support leaves range as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** signed_archive set; valid paired work can start.
**Actor configuration:** Maintenance operator plus required support host.
**Interaction target:** Containment relay.
**Before starting:** Record the target state and relevant flag expectation: containment_offline remains unset after interrupted work.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Arm dispatch with the other amoeba.
2. Start the operation and record a visible nonzero progress value.
3. Switch to the dispatch operator and move clearly outside its 2.4-metre radius.
4. Inspect progress and completion flags before attempting any restart.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The dependent operation interrupts when HeldByOther becomes false.
**Expected flag condition:** containment_offline remains unset after interrupted work.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-056 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Designed interruption scenario; boundary behavior requires execution evidence.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-057 Containment relay interruption when operator leaves range

**Status:** Designed not run.
**Goal:** Verify containment relay interruption when operator leaves range as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** signed_archive set; valid paired work can start.
**Actor configuration:** Maintenance operator plus required support host.
**Interaction target:** Containment relay.
**Before starting:** Record the target state and relevant flag expectation: containment_offline remains unset after interrupted work.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Arm dispatch with the other amoeba.
2. Start the operation and record a visible nonzero progress value.
3. Move the operation host clearly outside the terminal 2.4-metre radius.
4. Inspect progress and completion flags before attempting any restart.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The operation interrupts when its operator moves out of range.
**Expected flag condition:** containment_offline remains unset after interrupted work.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-057 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Designed interruption scenario; boundary behavior requires execution evidence.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-058 Containment relay interruption when operator abandons host

**Status:** Designed not run.
**Goal:** Verify containment relay interruption when operator abandons host as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** signed_archive set; valid paired work can start.
**Actor configuration:** Maintenance operator plus required support host.
**Interaction target:** Containment relay.
**Before starting:** Record the target state and relevant flag expectation: containment_offline remains unset after interrupted work.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Arm dispatch with the other amoeba.
2. Start the operation and record a visible nonzero progress value.
3. Press Q on the operation host during work.
4. Inspect progress and completion flags before attempting any restart.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The operation loses its original possessed host and interrupts.
**Expected flag condition:** containment_offline remains unset after interrupted work.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-058 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Designed interruption scenario; boundary behavior requires execution evidence.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-059 Containment relay interruption when support abandons host

**Status:** Designed not run.
**Goal:** Verify containment relay interruption when support abandons host as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** signed_archive set; valid paired work can start.
**Actor configuration:** Maintenance operator plus required support host.
**Interaction target:** Containment relay.
**Before starting:** Record the target state and relevant flag expectation: containment_offline remains unset after interrupted work.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Arm dispatch with the other amoeba.
2. Start the operation and record a visible nonzero progress value.
3. Switch to the dispatch body and press Q.
4. Inspect progress and completion flags before attempting any restart.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The operation interrupts because the armed support no longer has its original possessed host.
**Expected flag condition:** containment_offline remains unset after interrupted work.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-059 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Designed interruption scenario; boundary behavior requires execution evidence.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-060 Containment relay interruption when pause during work

**Status:** Designed not run.
**Goal:** Verify containment relay interruption when pause during work as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** signed_archive set; valid paired work can start.
**Actor configuration:** Maintenance operator plus required support host.
**Interaction target:** Containment relay.
**Before starting:** Record the target state and relevant flag expectation: containment_offline remains incomplete until resumed work finishes.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Arm dispatch with the other amoeba.
2. Start the operation and record a visible nonzero progress value.
3. Pause through Esc, wait while paused, then resume.
4. Inspect progress and completion flags before attempting any restart.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Scaled work time does not advance while paused; valid positions allow continuation after resume.
**Expected flag condition:** containment_offline remains incomplete until resumed work finishes.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-060 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Designed interruption scenario; boundary behavior requires execution evidence.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-061 Journalist uplink accepts Civilian

**Status:** Designed not run.
**Goal:** Verify journalist uplink accepts civilian as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** containment_offline set; evidence_broadcast unset; below pursuit.
**Actor configuration:** Civilian host.
**Interaction target:** Plaza journalist uplink.
**Before starting:** Record the target state and relevant flag expectation: evidence_broadcast sets after completion.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Possess a civilian after containment is offline.
2. Approach the Plaza uplink and start E work.
3. Remain at the terminal for its configured ten seconds.
4. Inspect the broadcast flag and extraction instructions.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Any possessed human profession is eligible for this solo broadcast.
**Expected flag condition:** evidence_broadcast sets after completion.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-061 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-062 Journalist uplink accepts Guard

**Status:** Designed not run.
**Goal:** Verify journalist uplink accepts guard as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** containment_offline set; evidence_broadcast unset; below pursuit.
**Actor configuration:** Guard host.
**Interaction target:** Plaza journalist uplink.
**Before starting:** Record the target state and relevant flag expectation: evidence_broadcast sets after completion.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Possess a guard after containment is offline.
2. Approach the Plaza uplink and start E work.
3. Remain at the terminal for its configured ten seconds.
4. Inspect the broadcast flag and extraction instructions.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Any possessed human profession is eligible for this solo broadcast.
**Expected flag condition:** evidence_broadcast sets after completion.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-062 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-063 Journalist uplink accepts Scientist

**Status:** Designed not run.
**Goal:** Verify journalist uplink accepts scientist as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** containment_offline set; evidence_broadcast unset; below pursuit.
**Actor configuration:** Scientist host.
**Interaction target:** Plaza journalist uplink.
**Before starting:** Record the target state and relevant flag expectation: evidence_broadcast sets after completion.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Possess a scientist after containment is offline.
2. Approach the Plaza uplink and start E work.
3. Remain at the terminal for its configured ten seconds.
4. Inspect the broadcast flag and extraction instructions.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Any possessed human profession is eligible for this solo broadcast.
**Expected flag condition:** evidence_broadcast sets after completion.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-063 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-064 Journalist uplink accepts Maintenance

**Status:** Designed not run.
**Goal:** Verify journalist uplink accepts maintenance as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** containment_offline set; evidence_broadcast unset; below pursuit.
**Actor configuration:** Maintenance host.
**Interaction target:** Plaza journalist uplink.
**Before starting:** Record the target state and relevant flag expectation: evidence_broadcast sets after completion.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Possess a maintenance after containment is offline.
2. Approach the Plaza uplink and start E work.
3. Remain at the terminal for its configured ten seconds.
4. Inspect the broadcast flag and extraction instructions.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Any possessed human profession is eligible for this solo broadcast.
**Expected flag condition:** evidence_broadcast sets after completion.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-064 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-065 Journalist uplink rejects blob

**Status:** Designed not run.
**Goal:** Verify journalist uplink rejects blob as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** containment_offline set; evidence_broadcast unset.
**Actor configuration:** Free amoeba.
**Interaction target:** Plaza journalist uplink.
**Before starting:** Record the target state and relevant flag expectation: evidence_broadcast unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Leave the host before reaching the uplink.
2. Read its human-host requirement.
3. Attempt E as a blob.
4. Check that no broadcast progress is earned.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The human-host gate rejects free form.
**Expected flag condition:** evidence_broadcast unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-065 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-066 Pursuit interrupts active broadcast

**Status:** Designed not run.
**Goal:** Verify pursuit interrupts active broadcast as an isolated campaign behavior.
**Primary source:** [CampaignOperation.cs](../Assets/Parasite/Scripts/Missions/CampaignOperation.cs).
**Preconditions:** containment_offline set; reproducible pursuit setup available.
**Actor configuration:** Any human host.
**Interaction target:** Plaza journalist uplink.
**Before starting:** Record the target state and relevant flag expectation: evidence_broadcast unset after interrupted upload.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Start the upload while below pursuit.
2. Record intermediate progress.
3. Cause the global state to become Pursuing without leaving the terminal.
4. Inspect interruption feedback and the broadcast flag.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The requireCalm condition cancels an in-progress upload during pursuit.
**Expected flag condition:** evidence_broadcast unset after interrupted upload.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-066 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-067 Reload after signed archive

**Status:** Designed not run.
**Goal:** Verify reload after signed archive as an isolated campaign behavior.
**Primary source:** [MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
**Preconditions:** signed_archive set; containment_offline unset.
**Actor configuration:** Current player body before reload.
**Interaction target:** Lab checkpoint.
**Before starting:** Record the target state and relevant flag expectation: signed_archive must match recorded completion state.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Record mission flags and the current objective before reloading.
2. Use a controlled checkpoint reload without starting a New Game.
3. Inspect the restored objective and relevant world objects.
4. Reacquire bodies and verify unfinished support controls require valid operators.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Completed evidence survives; the dispatch/relay chapter remains actionable.
**Expected flag condition:** signed_archive must match recorded completion state.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-067 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-068 Reload after relay sabotage

**Status:** Designed not run.
**Goal:** Verify reload after relay sabotage as an isolated campaign behavior.
**Primary source:** [MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
**Preconditions:** containment_offline set; evidence_broadcast unset.
**Actor configuration:** Current player body before reload.
**Interaction target:** Service-alley checkpoint.
**Before starting:** Record the target state and relevant flag expectation: containment_offline must match recorded completion state.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Record mission flags and the current objective before reloading.
2. Use a controlled checkpoint reload without starting a New Game.
3. Inspect the restored objective and relevant world objects.
4. Reacquire bodies and verify unfinished support controls require valid operators.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** CCTV sabotage and open campus escape routes restore after reload.
**Expected flag condition:** containment_offline must match recorded completion state.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-068 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-069 Reload after broadcast

**Status:** Designed not run.
**Goal:** Verify reload after broadcast as an isolated campaign behavior.
**Primary source:** [MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
**Preconditions:** evidence_broadcast set; escaped_with_evidence unset.
**Actor configuration:** Current player body before reload.
**Interaction target:** Plaza checkpoint.
**Before starting:** Record the target state and relevant flag expectation: evidence_broadcast must match recorded completion state.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Record mission flags and the current objective before reloading.
2. Use a controlled checkpoint reload without starting a New Game.
3. Inspect the restored objective and relevant world objects.
4. Reacquire bodies and verify unfinished support controls require valid operators.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The player can resume the final drain objective without retransmitting evidence.
**Expected flag condition:** evidence_broadcast must match recorded completion state.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-069 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-070 Reload unfinished paired operation

**Status:** Designed not run.
**Goal:** Verify reload unfinished paired operation as an isolated campaign behavior.
**Primary source:** [MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
**Preconditions:** Operation started but its completion flag is unset.
**Actor configuration:** Current player body before reload.
**Interaction target:** Active cooperative checkpoint.
**Before starting:** Record the target state and relevant flag expectation: incomplete operation flag must match recorded completion state.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Record mission flags and the current objective before reloading.
2. Use a controlled checkpoint reload without starting a New Game.
3. Inspect the restored objective and relevant world objects.
4. Reacquire bodies and verify unfinished support controls require valid operators.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Transient console operators do not restore as phantom active hosts.
**Expected flag condition:** incomplete operation flag must match recorded completion state.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-070 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-071 Drain before broadcast

**Status:** Designed not run.
**Goal:** Verify drain before broadcast as an isolated campaign behavior.
**Primary source:** [CampaignExtraction.cs](../Assets/Parasite/Scripts/Missions/CampaignExtraction.cs).
**Preconditions:** evidence_broadcast unset.
**Actor configuration:** Free amoeba.
**Interaction target:** Original storm-drain extraction.
**Before starting:** Record the target state and relevant flag expectation: escaped_with_evidence unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Reach the original drain without the broadcast flag.
2. Read the evidence prerequisite.
3. Attempt E.
4. Inspect ending state and objective.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The extraction requires journalist evidence first.
**Expected flag condition:** escaped_with_evidence unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-071 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-072 Drain with human host

**Status:** Designed not run.
**Goal:** Verify drain with human host as an isolated campaign behavior.
**Primary source:** [CampaignExtraction.cs](../Assets/Parasite/Scripts/Missions/CampaignExtraction.cs).
**Preconditions:** Evidence broadcast and containment offline.
**Actor configuration:** Any human host.
**Interaction target:** Original storm-drain extraction.
**Before starting:** Record the target state and relevant flag expectation: escaped_with_evidence unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Return to the drain while still inside a host.
2. Read the leave-host instruction.
3. Attempt E without pressing Q.
4. Inspect that no final story starts.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** A possessed human cannot fit through the free-amoeba extraction.
**Expected flag condition:** escaped_with_evidence unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-072 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-073 Drain while pursued

**Status:** Designed not run.
**Goal:** Verify drain while pursued as an isolated campaign behavior.
**Primary source:** [CampaignExtraction.cs](../Assets/Parasite/Scripts/Missions/CampaignExtraction.cs).
**Preconditions:** Evidence broadcast and containment offline.
**Actor configuration:** Free amoeba in pursuit.
**Interaction target:** Original storm-drain extraction.
**Before starting:** Record the target state and relevant flag expectation: escaped_with_evidence unset.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Record the global Pursuing state.
2. Approach the extraction point.
3. Attempt E before losing pursuit.
4. Inspect denial and final mission progress.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The final exit rejects an active pursuit.
**Expected flag condition:** escaped_with_evidence unset.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-073 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-074 Valid drain to unchosen lake

**Status:** Designed not run.
**Goal:** Verify valid drain to unchosen lake as an isolated campaign behavior.
**Primary source:** [CampaignExtraction.cs](../Assets/Parasite/Scripts/Missions/CampaignExtraction.cs).
**Preconditions:** Evidence broadcast and containment offline; below pursuit.
**Actor configuration:** Free amoeba.
**Interaction target:** Original storm-drain extraction.
**Before starting:** Record the target state and relevant flag expectation: escaped_with_evidence stays unset before chosen resolution.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Reach the drain in free form.
2. Press E to start extraction.
3. Advance the protected escape dialogue normally.
4. Wait at the lake without choosing a future.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** Drain story reaches the lake without automatically choosing or publishing statistics.
**Expected flag condition:** escaped_with_evidence stays unset before chosen resolution.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-074 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.

### CAM-075 Final flag after explicit resolution

**Status:** Designed not run.
**Goal:** Verify final flag after explicit resolution as an isolated campaign behavior.
**Primary source:** [GameManager.cs](../Assets/Parasite/Scripts/Core/GameManager.cs).
**Preconditions:** Lake choices visible and ready.
**Actor configuration:** Player making explicit choice.
**Interaction target:** Original storm-drain extraction.
**Before starting:** Record the target state and relevant flag expectation: escaped_with_evidence set only at final publication.
**Procedure:** Execute the following actions in order; record any prerequisite that cannot be established.
1. Select Peace or Dominion explicitly.
2. Attempt a different choice after the first is committed.
3. Finish or advance the selected resolution.
4. Compare final statistics and the recorded immutable outcome.
5. Compare the recorded behavior with this case's designed result before changing any other objective.
**Expected behavior:** The final completion flag commits after a deliberate outcome resolves.
**Expected flag condition:** escaped_with_evidence set only at final publication.
**Expected evidence discipline:** An unmet setup prerequisite makes this attempt inconclusive; it does not establish a gameplay failure or a pass.
**Proposed capture:** Save a before/after screenshot or short recording named CAM-075 with the target and current objective visible.
**Evidence fields if executed:** Executable/source revision, platform, initial flags, actual steps, observed result and reproduction notes.
**Cleanup:** Return to a fresh or deliberately restored checkpoint state; document retained flags before another case starts.
**Coverage classification:** Source-derived acceptance criterion.
**Execution boundary:** No runtime action, new automated test or manual execution is claimed by this written case.


## Designed UX and input regression procedures — not new execution results

**Execution disclaimer:** every UX case below is **Designed; not run in this update**. This appendix creates QA procedures, not additional passes, failures, screenshots, timings or device-validation claims.
Existing aggregate evidence remains separate: the previously recorded UI run passed 3/3 and the focused opening/Ctrl+M run passed 3/3. Those aggregates do not mean these seventy-two new procedures were executed.
Physical keyboard delivery, physical controllers, unusual resolutions and scaling combinations below remain unverified until an operator actually records their execution.
Use an isolated local player or controlled Editor session. Record executable/source identity, actual resolution, graphics preset and input-device identity before beginning.
All source links below are relative to the final report location in Docs. Future evidence names are instructions, not claims that those files already exist.
Preserve the original report and its historical results; append individual future outcomes only after observing them. Do not replace a missing observation with an earlier aggregate pass.

### UX-001 — WASD movement stops on release
- Status: **Designed; not run in this update.**
- Goal: assess wasd movement stops on release using the concrete control and state transitions below.
- Primary source: [GameInput.cs](../Assets/Parasite/Scripts/Core/GameInput.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A free amoeba is controllable in the starting alley.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Hold W for two seconds while recording the active body.
- Step 2: Release W and leave all movement controls untouched.
- Step 3: Repeat with A, S and D against the same clear ground.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Each key supplies its expected direction while held.
- Expected result B: Released movement does not remain latched into the next interval.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-001; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-001 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-002 — Arrow-key movement matches WASD
- Status: **Designed; not run in this update.**
- Goal: assess arrow-key movement matches wasd using the concrete control and state transitions below.
- Primary source: [GameInput.cs](../Assets/Parasite/Scripts/Core/GameInput.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: The starting alley is clear and the camera yaw is unchanged.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Move forward with W and mark the direction relative to the camera.
- Step 2: Return to the marker and move with Up Arrow.
- Step 3: Repeat the comparison for left, down and right.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Both composites address the same movement action.
- Expected result B: Arrow-key movement does not trigger unrelated possession actions.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-002; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-002 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-003 — Diagonal input normalization
- Status: **Designed; not run in this update.**
- Goal: assess diagonal input normalization using the concrete control and state transitions below.
- Primary source: [GameInput.cs](../Assets/Parasite/Scripts/Core/GameInput.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A flat unobstructed section permits straight and diagonal travel.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record straight movement for a fixed real-time interval.
- Step 2: Return and hold W plus D for the same interval.
- Step 3: Compare normalized control magnitude or travel after accounting for collision.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The combined vector is bounded rather than adding two full-speed axes.
- Expected result B: Differences caused by walls are separated from an input-normalization defect.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-003; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-003 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-004 — Physical left-stick movement
- Status: **Designed; not run in this update.**
- Goal: assess physical left-stick movement using the concrete control and state transitions below.
- Primary source: [GameInput.cs](../Assets/Parasite/Scripts/Core/GameInput.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A physical controller is connected; this device check is not yet executed.
- Execution setup: Use the named physical device; record model and connection type. No controller execution is implied.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record controller model and any visible stick drift at rest.
- Step 2: Move the left stick through forward, side and diagonal positions.
- Step 3: Release the stick and compare the body with its resting position.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Left-stick input feeds the same bounded movement path.
- Expected result B: A neutral stick does not leave sustained intentional movement active.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-004; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-004 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-005 — Keyboard sprint transition
- Status: **Designed; not run in this update.**
- Goal: assess keyboard sprint transition using the concrete control and state transitions below.
- Primary source: [GameInput.cs](../Assets/Parasite/Scripts/Core/GameInput.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A possessed host stands on a clear route with sufficient stopping distance.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Walk forward without Shift and record the animation.
- Step 2: Hold left Shift while maintaining the same movement input.
- Step 3: Release Shift before releasing movement.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Sprint is held state rather than a permanent toggle.
- Expected result B: Returning to walk does not require entering another body.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-005; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-005 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-006 — Physical stick-click sprint
- Status: **Designed; not run in this update.**
- Goal: assess physical stick-click sprint using the concrete control and state transitions below.
- Primary source: [GameInput.cs](../Assets/Parasite/Scripts/Core/GameInput.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A physical gamepad and controllable host are available.
- Execution setup: Use the named physical device; record model and connection type. No controller execution is implied.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Walk with the left stick without pressing it.
- Step 2: Hold the left-stick button while moving along the same route.
- Step 3: Release the stick button and continue moving.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The mapped stick press requests sprint while held.
- Expected result B: Sprint release restores ordinary movement instead of remaining latched.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-006; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-006 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-007 — Blob hop button edge
- Status: **Designed; not run in this update.**
- Goal: assess blob hop button edge using the concrete control and state transitions below.
- Primary source: [GameInput.cs](../Assets/Parasite/Scripts/Core/GameInput.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: The free amoeba is grounded with headroom above it.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Press Space once and keep the key down through the landing.
- Step 2: Release Space after landing.
- Step 3: Press Space again and compare the second hop.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: A press produces a hop through the jump action.
- Expected result B: A held key is not treated as a new press every frame.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-007; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-007 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-008 — Mouse sensitivity endpoints
- Status: **Designed; not run in this update.**
- Goal: assess mouse sensitivity endpoints using the concrete control and state transitions below.
- Primary source: [SettingsOverlay.cs](../Assets/Parasite/Scripts/UI/SettingsOverlay.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Record the original sensitivity before changing Settings.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Set sensitivity to its lowest slider position and return to play.
- Step 2: Apply a repeatable mouse movement and record camera travel.
- Step 3: Repeat at the highest position, then restore the original setting.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The two endpoints produce visibly different camera response.
- Expected result B: The value remains inside the supported slider interval.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-008; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-008 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-009 — Physical right-stick look
- Status: **Designed; not run in this update.**
- Goal: assess physical right-stick look using the concrete control and state transitions below.
- Primary source: [PlayerCamera.cs](../Assets/Parasite/Scripts/Possession/PlayerCamera.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A physical controller is connected and gameplay has focus.
- Execution setup: Use the named physical device; record model and connection type. No controller execution is implied.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Hold the right stick at a repeatable horizontal displacement.
- Step 2: Repeat vertically and then release the stick.
- Step 3: Compare the camera while standing and while moving.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The right stick changes camera orientation in gameplay.
- Expected result B: Releasing the stick stops intentional look input without moving the body itself.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-009; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-009 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-010 — Camera pitch limits
- Status: **Designed; not run in this update.**
- Goal: assess camera pitch limits using the concrete control and state transitions below.
- Primary source: [PlayerCamera.cs](../Assets/Parasite/Scripts/Possession/PlayerCamera.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A controllable body has no nearby camera-obstructing ceiling.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Move the view upward until additional input no longer raises it.
- Step 2: Move downward until the lower bound is reached.
- Step 3: Return toward the horizon with ordinary input.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Pitch remains bounded instead of flipping over the body.
- Expected result B: Returning from a bound does not lock the camera at that extreme.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-010; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-010 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-011 — Possession range boundary
- Status: **Designed; not run in this update.**
- Goal: assess possession range boundary using the concrete control and state transitions below.
- Primary source: [PossessionManager.cs](../Assets/Parasite/Scripts/Possession/PossessionManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: An awake NPC is reachable on unobstructed ground.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Approach while aiming at the NPC and observe the prompt near five metres.
- Step 2: Move slightly farther away and attempt the same action.
- Step 3: Return inside the permitted distance and press E.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Possession is permitted only within the configured range and target rules.
- Expected result B: A refused distant press leaves the active body and host count unchanged.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-011; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-011 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-012 — Possession aiming cone
- Status: **Designed; not run in this update.**
- Goal: assess possession aiming cone using the concrete control and state transitions below.
- Primary source: [PossessionManager.cs](../Assets/Parasite/Scripts/Possession/PossessionManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: Two awake NPCs are visible and neither is blocked by geometry.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Aim directly at the nearer eligible NPC and note targeting feedback.
- Step 2: Rotate until that person is outside the aiming cone.
- Step 3: Aim back at the person and attempt possession.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The aiming rules determine the target rather than mere proximity alone.
- Expected result B: A changed camera heading does not silently select an unrelated person behind the player.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-012; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-012 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-013 — Possession through cover refusal
- Status: **Designed; not run in this update.**
- Goal: assess possession through cover refusal using the concrete control and state transitions below.
- Primary source: [PossessionManager.cs](../Assets/Parasite/Scripts/Possession/PossessionManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: An awake NPC stands across a solid wall or other environment collider.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Place the target within nominal distance but behind the obstacle.
- Step 2: Aim toward the hidden target and press E once.
- Step 3: Move around the obstacle and compare targeting on an exposed view.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Environment obstruction prevents the blocked possession path.
- Expected result B: The exposed control case distinguishes line-of-sight refusal from an invalid NPC.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-013; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-013 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-014 — Profession targeting prompt
- Status: **Designed; not run in this update.**
- Goal: assess profession targeting prompt using the concrete control and state transitions below.
- Primary source: [HUDController.cs](../Assets/Parasite/Scripts/UI/HUDController.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Eligible civilian, scientist, guard and maintenance hosts can be inspected.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Aim at one profession and record the possession prompt and body label.
- Step 2: Possess that host and read the active-body panel.
- Step 3: Repeat for another profession in a separate clean run.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The panel identifies the actual active host role.
- Expected result B: Job-specific guidance changes with the current host instead of retaining the previous role.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-014; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-014 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-015 — Target invalidation during launch
- Status: **Designed; not run in this update.**
- Goal: assess target invalidation during launch using the concrete control and state transitions below.
- Primary source: [PossessionManager.cs](../Assets/Parasite/Scripts/Possession/PossessionManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A controlled Editor fixture can invalidate an NPC after launch begins.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the active agent and target immediately before E.
- Step 2: Invalidate the target during the configured launch interval.
- Step 3: Observe the agent until movement or a stable free-blob state returns.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: An invalid target does not leave the agent permanently in transit.
- Expected result B: Any fallback blob remains controllable and does not duplicate the original agent.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-015; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-015 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-016 — Voluntary release and recovery
- Status: **Designed; not run in this update.**
- Goal: assess voluntary release and recovery using the concrete control and state transitions below.
- Primary source: [HostCharacter.cs](../Assets/Parasite/Scripts/Characters/HostCharacter.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A host is possessed away from traffic and pursuing guards.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Press Q and observe the host and newly visible blob.
- Step 2: Remain nearby without repossessing or interacting.
- Step 3: Observe the host through the approximate eight-second recovery period.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The host leaves possession and enters unconscious recovery.
- Expected result B: Recovery does not keep the former host attached as the active controlled body.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-016; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-016 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-017 — Captured-host ejection prompt
- Status: **Designed; not run in this update.**
- Goal: assess captured-host ejection prompt using the concrete control and state transitions below.
- Primary source: [PossessionManager.cs](../Assets/Parasite/Scripts/Possession/PossessionManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A repeatable guard encounter can capture the current host.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the capture prompt and active host state.
- Step 2: Press E while the ejection action is offered.
- Step 3: Move the blob away and inspect the surviving agent.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The capture interaction ejects rather than possessing an unrelated target.
- Expected result B: The active agent survives the ejection and receives the intended escape opportunity.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-017; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-017 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-018 — Input during possession transit
- Status: **Designed; not run in this update.**
- Goal: assess input during possession transit using the concrete control and state transitions below.
- Primary source: [PossessionManager.cs](../Assets/Parasite/Scripts/Possession/PossessionManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: An awake target is eligible for a normal possession launch.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Start possession and immediately press movement and a second possession input.
- Step 2: Observe the transition until a host or fallback blob is active.
- Step 3: Release all controls before the next action.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Transit does not create duplicate concurrent possession results.
- Expected result B: The resulting active body is consistent with the agent's final host reference.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-018; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-018 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-019 — Two-amoeba split limit
- Status: **Designed; not run in this update.**
- Goal: assess two-amoeba split limit using the concrete control and state transitions below.
- Primary source: [PossessionManager.cs](../Assets/Parasite/Scripts/Possession/PossessionManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A fresh run contains one active free amoeba.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Press V once and inspect both slots.
- Step 2: Press V again while two agents still exist.
- Step 3: Switch with Tab and inspect the agent count again.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The first split creates the permitted second agent.
- Expected result B: A later split request does not create a third controllable amoeba.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-019; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-019 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-020 — Inactive body stays positioned
- Status: **Designed; not run in this update.**
- Goal: assess inactive body stays positioned using the concrete control and state transitions below.
- Primary source: [PossessionManager.cs](../Assets/Parasite/Scripts/Possession/PossessionManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: Two free amoebas are standing on stable separate ground.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the first body's location and switch with Tab.
- Step 2: Move the second body along a short unobstructed route.
- Step 3: Switch back and compare the first location.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Only the selected body receives deliberate player movement.
- Expected result B: The inactive body does not follow the active body's route as a hidden duplicate input receiver.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-020; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-020 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-021 — Switching between two hosts
- Status: **Designed; not run in this update.**
- Goal: assess switching between two hosts using the concrete control and state transitions below.
- Primary source: [PossessionManager.cs](../Assets/Parasite/Scripts/Possession/PossessionManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: Each amoeba occupies a different valid human host.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the selected slot and current profession.
- Step 2: Press Tab and inspect camera target and body information.
- Step 3: Press Tab again without moving either host.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Selection advances to the other agent and returns on the next switch.
- Expected result B: Camera and profession panel agree on which human is controlled.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-021; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-021 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-022 — Single-agent switch refusal
- Status: **Designed; not run in this update.**
- Goal: assess single-agent switch refusal using the concrete control and state transitions below.
- Primary source: [PossessionManager.cs](../Assets/Parasite/Scripts/Possession/PossessionManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A fresh run contains only one amoeba.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the initial agent identity and camera.
- Step 2: Press Tab once and read any feedback.
- Step 3: Move normally after the refused switch.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: A single-agent switch request does not create or select an invalid second slot.
- Expected result B: The original active body remains controllable.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-022; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-022 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-023 — Split while possessing a host
- Status: **Designed; not run in this update.**
- Goal: assess split while possessing a host using the concrete control and state transitions below.
- Primary source: [PossessionManager.cs](../Assets/Parasite/Scripts/Possession/PossessionManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: One amoeba controls a host with enough adjacent free ground.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Press V and inspect the new free body.
- Step 2: Switch to the new amoeba and move it a short distance.
- Step 3: Switch back to the original possessed host.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The original host remains associated with the original agent.
- Expected result B: The second amoeba is a separate usable body rather than another reference to that host.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-023; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-023 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-024 — Camera continuity after Q
- Status: **Designed; not run in this update.**
- Goal: assess camera continuity after q using the concrete control and state transitions below.
- Primary source: [PlayerCamera.cs](../Assets/Parasite/Scripts/Possession/PlayerCamera.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A host is possessed with the camera settled behind it.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Press Q and track the camera during the handoff.
- Step 2: Move the free blob after the transition.
- Step 3: Compare the tracked target with the abandoned host.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The camera retargets the free blob.
- Expected result B: Following the blob does not continue tracking the unconscious host instead.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-024; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-024 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-025 — Camera collision near a wall
- Status: **Designed; not run in this update.**
- Goal: assess camera collision near a wall using the concrete control and state transitions below.
- Primary source: [PlayerCamera.cs](../Assets/Parasite/Scripts/Possession/PlayerCamera.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A safe wall and nearby open ground are available.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Back the active body toward the wall while keeping the view level.
- Step 2: Rotate the camera around the obstruction.
- Step 3: Return to open ground and inspect camera distance.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Obstacle avoidance is assessed for clipping or severe loss of the controlled body.
- Expected result B: The camera can recover its ordinary follow distance after leaving the wall.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-025; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-025 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-026 — Civilian distraction success
- Status: **Designed; not run in this update.**
- Goal: assess civilian distraction success using the concrete control and state transitions below.
- Primary source: [AbilitySystem.cs](../Assets/Parasite/Scripts/Possession/AbilitySystem.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A civilian host has a ready ability and nearby awake NPCs.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Aim at a reachable location and press F.
- Step 2: Observe the dispatched investigation or attention response.
- Step 3: Read the civilian cooldown guidance afterward.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: A successful distraction addresses the aimed location.
- Expected result B: The civilian profession's cooldown begins after successful use.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-026; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-026 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-027 — Guard radio dispatch
- Status: **Designed; not run in this update.**
- Goal: assess guard radio dispatch using the concrete control and state transitions below.
- Primary source: [AbilitySystem.cs](../Assets/Parasite/Scripts/Possession/AbilitySystem.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A guard host has a ready ability and another guard is within the ability radius.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Aim at a reachable location and press F.
- Step 2: Observe the other guard's investigation direction.
- Step 3: Compare the active guard with the dispatched NPC.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The radio requests investigation from other eligible guards.
- Expected result B: The controlled host is not mistakenly handed back to autonomous patrol by using the ability.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-027; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-027 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-028 — Scientist calm outside pursuit
- Status: **Designed; not run in this update.**
- Goal: assess scientist calm outside pursuit using the concrete control and state transitions below.
- Primary source: [AbilitySystem.cs](../Assets/Parasite/Scripts/Possession/AbilitySystem.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A scientist host has nonzero suspicion below pursuit and a ready ability.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the suspicion value.
- Step 2: Press F once while still below pursuit.
- Step 3: Compare the value and scientist cooldown guidance.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Successful reassurance lowers suspicion by the implemented amount, bounded at zero.
- Expected result B: The corresponding cooldown is shown after successful reassurance.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-028; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-028 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-029 — Scientist calm during pursuit
- Status: **Designed; not run in this update.**
- Goal: assess scientist calm during pursuit using the concrete control and state transitions below.
- Primary source: [AbilitySystem.cs](../Assets/Parasite/Scripts/Possession/AbilitySystem.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A scientist host is being pursued with a ready ability.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the pursuing state and suspicion.
- Step 2: Press F and read the refusal feedback.
- Step 3: Break sight by ordinary play rather than assuming the ability ended pursuit.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Reassurance does not cancel pursuit merely because F was pressed.
- Expected result B: Refusal is separated from a successful cooldown-consuming action.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-029; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-029 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-030 — Maintenance camera shutdown
- Status: **Designed; not run in this update.**
- Goal: assess maintenance camera shutdown using the concrete control and state transitions below.
- Primary source: [AbilitySystem.cs](../Assets/Parasite/Scripts/Possession/AbilitySystem.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A maintenance host has a ready ability and a security camera is within twenty-five metres.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the camera's current operational state.
- Step 2: Press F and observe its disabled period.
- Step 3: Wait for the temporary effect to expire without completing permanent relay sabotage.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The nearby camera becomes temporarily disabled.
- Expected result B: Temporary maintenance power loss is not confused with the permanent campaign relay result.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-030; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-030 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-031 — Maintenance ability with no camera
- Status: **Designed; not run in this update.**
- Goal: assess maintenance ability with no camera using the concrete control and state transitions below.
- Primary source: [AbilitySystem.cs](../Assets/Parasite/Scripts/Possession/AbilitySystem.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A maintenance host is farther than the ability range from all security cameras.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the profession cooldown before pressing F.
- Step 2: Press F once and read the feedback.
- Step 3: Move within range and reassess the cooldown.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The lack of a nearby camera produces refusal rather than a claimed successful cut.
- Expected result B: An unsuccessful attempt does not falsely start the normal successful-use cooldown.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-031; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-031 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-032 — Ability while a free amoeba
- Status: **Designed; not run in this update.**
- Goal: assess ability while a free amoeba using the concrete control and state transitions below.
- Primary source: [AbilitySystem.cs](../Assets/Parasite/Scripts/Possession/AbilitySystem.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: The player controls an exposed free blob.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record suspicion, active agent and profession panel.
- Step 2: Press F once without possessing a host.
- Step 3: Inspect feedback and verify ordinary blob movement still works.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The action explains that a host is required.
- Expected result B: No human profession ability is executed through the free blob.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-032; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-032 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-033 — Ability cooldown across same-role hosts
- Status: **Designed; not run in this update.**
- Goal: assess ability cooldown across same-role hosts using the concrete control and state transitions below.
- Primary source: [AbilitySystem.cs](../Assets/Parasite/Scripts/Possession/AbilitySystem.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: Two accessible hosts share one profession and that profession's ability can succeed.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Use the ability successfully in the first host.
- Step 2: Move into the second host before the cooldown expires.
- Step 3: Attempt F and compare the remaining cooldown.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Cooldown is associated with the profession rather than reset by swapping to another same-role host.
- Expected result B: Changing hosts does not provide an unintended immediate second successful ability.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-033; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-033 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-034 — Pause freezes movement and time
- Status: **Designed; not run in this update.**
- Goal: assess pause freezes movement and time using the concrete control and state transitions below.
- Primary source: [GameManager.cs](../Assets/Parasite/Scripts/Core/GameManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: The active body is moving through a safe area.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Release movement and note the run timer.
- Step 2: Press Esc and remain paused for several real seconds.
- Step 3: Resume and compare body position and accumulated gameplay time.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Pause switches the game out of Playing and freezes scaled simulation.
- Expected result B: The run timer does not add the full paused interval as gameplay time.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-034; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-034 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-035 — Esc from pause Settings
- Status: **Designed; not run in this update.**
- Goal: assess esc from pause settings using the concrete control and state transitions below.
- Primary source: [HUDController.Menus.cs](../Assets/Parasite/Scripts/UI/HUDController.Menus.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Gameplay is paused and the Settings submenu is open.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the pause state and visible submenu.
- Step 2: Press Esc once and inspect the remaining menu.
- Step 3: Press Esc again from the main pause view.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The first Esc closes the submenu while keeping gameplay paused.
- Expected result B: The second Esc resumes only after the submenu has been left.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-035; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-035 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-036 — Esc from pause Controls
- Status: **Designed; not run in this update.**
- Goal: assess esc from pause controls using the concrete control and state transitions below.
- Primary source: [HUDController.Menus.cs](../Assets/Parasite/Scripts/UI/HUDController.Menus.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Gameplay is paused and the Controls overlay is open.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Read the overlay and press Esc once.
- Step 2: Observe the main pause view and current cursor mode.
- Step 3: Resume through the visible Resume button.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Controls Back returns one menu level.
- Expected result B: The game remains paused until an explicit subsequent resume action.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-036; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-036 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-037 — Physical controller submenu Back
- Status: **Designed; not run in this update.**
- Goal: assess physical controller submenu back using the concrete control and state transitions below.
- Primary source: [HUDController.Menus.cs](../Assets/Parasite/Scripts/UI/HUDController.Menus.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: A physical controller is connected while the pause Settings view is open.
- Execution setup: Use the named physical device; record model and connection type. No controller execution is implied.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Press the East button once.
- Step 2: Inspect the main pause panel and current selected item.
- Step 3: Navigate to Resume and confirm deliberately.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The East button leaves the submenu without acting as Q in gameplay.
- Expected result B: Focus remains usable for selecting an ordinary pause action.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-037; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-037 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-038 — Resume restores cursor lock
- Status: **Designed; not run in this update.**
- Goal: assess resume restores cursor lock using the concrete control and state transitions below.
- Primary source: [GameManager.cs](../Assets/Parasite/Scripts/Core/GameManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: The main pause panel is open with the mouse visible.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Click Resume or select it and confirm.
- Step 2: Move the mouse after the pause view clears.
- Step 3: Pause again and inspect the cursor.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Gameplay returns to a locked look cursor.
- Expected result B: Pausing again restores a visible usable menu cursor.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-038; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-038 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-039 — Restart clears the current run
- Status: **Designed; not run in this update.**
- Goal: assess restart clears the current run using the concrete control and state transitions below.
- Primary source: [GameManager.cs](../Assets/Parasite/Scripts/Core/GameManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A run has accumulated at least one possession or objective.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the run statistics and current mission.
- Step 2: Pause and select Restart.
- Step 3: Inspect the new active body and mission after the city reloads.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Restart calls the fresh-run path rather than preserving completed campaign state.
- Expected result B: The opening does not unexpectedly replay on this restart path.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-039; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-039 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-040 — Quit to Menu and begin again
- Status: **Designed; not run in this update.**
- Goal: assess quit to menu and begin again using the concrete control and state transitions below.
- Primary source: [GameManager.cs](../Assets/Parasite/Scripts/Core/GameManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A paused run is available and no cinematic is active.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Select Quit to Menu and inspect title interaction.
- Step 2: Choose Begin Infiltration deliberately.
- Step 3: Inspect the new-game opening request and eventual alley handoff.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The title scene restores a usable visible cursor and menu focus.
- Expected result B: A deliberate new title start is distinguished from a checkpoint reload.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-040; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-040 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-041 — Title Settings keyboard focus
- Status: **Designed; not run in this update.**
- Goal: assess title settings keyboard focus using the concrete control and state transitions below.
- Primary source: [MainMenuController.cs](../Assets/Parasite/Scripts/UI/MainMenuController.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: The title menu is visible and no scene load is in progress.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Navigate to Settings with the keyboard and confirm.
- Step 2: Move through the settings controls and return with Back.
- Step 3: Inspect the selected item after the overlay closes.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The settings overlay selects its expected first control.
- Expected result B: Closing the overlay restores title-menu navigation instead of trapping focus in hidden content.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-041; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-041 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-042 — Slider keyboard navigation
- Status: **Designed; not run in this update.**
- Goal: assess slider keyboard navigation using the concrete control and state transitions below.
- Primary source: [SettingsOverlay.cs](../Assets/Parasite/Scripts/UI/SettingsOverlay.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Settings is open with its explicit vertical navigation available.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Navigate onto a slider using Up or Down.
- Step 2: Adjust its value with the horizontal navigation controls.
- Step 3: Navigate onward to the next control and Back.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: A slider remains adjustable without making other settings unreachable.
- Expected result B: Explicit navigation does not trap keyboard users on a hidden or inactive widget.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-042; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-042 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-043 — Master mute affects playing audio
- Status: **Designed; not run in this update.**
- Goal: assess master mute affects playing audio using the concrete control and state transitions below.
- Primary source: [AudioPreferences.cs](../Assets/Parasite/Scripts/Core/AudioPreferences.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Audio is audible and the original master preference has been recorded.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Open Settings while a sustained source or ambience is playing.
- Step 2: Move Master Volume to zero.
- Step 3: Restore the previous volume and compare existing sounds.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Listener gain mutes sounds already playing as well as later sounds.
- Expected result B: Restoring master volume does not erase the separate ambience balance.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-043; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-043 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-044 — Ambience gain remains independent
- Status: **Designed; not run in this update.**
- Goal: assess ambience gain remains independent using the concrete control and state transitions below.
- Primary source: [AudioPreferences.cs](../Assets/Parasite/Scripts/Core/AudioPreferences.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Master Volume is nonzero and the original ambience preference is known.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Reduce Ambience and Music while keeping Master unchanged.
- Step 2: Compare the bed with an allowed interface or gameplay one-shot.
- Step 3: Restore the ambience control to its initial value.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Ambience gain is independently controllable.
- Expected result B: Changing ambience does not write a different master-volume preference.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-044; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-044 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-045 — Audio preferences across restart
- Status: **Designed; not run in this update.**
- Goal: assess audio preferences across restart using the concrete control and state transitions below.
- Primary source: [AudioPreferences.cs](../Assets/Parasite/Scripts/Core/AudioPreferences.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Original audio preferences are recorded and a fresh application launch is possible.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Set a distinctive master value and a different ambience value.
- Step 2: Return from Settings, close the application normally and relaunch.
- Step 3: Read both slider values before altering them again.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Saved values reload independently, including a deliberate mute.
- Expected result B: This manual restart procedure is not considered executed merely because an API reload test previously passed.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-045; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-045 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-046 — Sensitivity persistence across scenes
- Status: **Designed; not run in this update.**
- Goal: assess sensitivity persistence across scenes using the concrete control and state transitions below.
- Primary source: [SettingsOverlay.cs](../Assets/Parasite/Scripts/UI/SettingsOverlay.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: The original sensitivity is recorded at the title.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Choose a distinctive sensitivity and close Settings.
- Step 2: Begin gameplay, pause and reopen Settings.
- Step 3: Compare the value without moving the slider first.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The scene transition preserves the selected mouse sensitivity.
- Expected result B: Opening the second Settings instance does not silently reset the preference.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-046; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-046 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-047 — Graphics cycle and label agreement
- Status: **Designed; not run in this update.**
- Goal: assess graphics cycle and label agreement using the concrete control and state transitions below.
- Primary source: [GraphicsPreset.cs](../Assets/Parasite/Scripts/Core/GraphicsPreset.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Record the current preset before cycling.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Activate the Graphics control through each displayed preset.
- Step 2: Observe the label and applicable render-scale or shadow changes.
- Step 3: Return to the original preset.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The sequence contains Low, Medium, High and Ultra.
- Expected result B: The visible label agrees with the applied preset rather than lagging one activation behind.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-047; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-047 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-048 — Graphics camera settings after reload
- Status: **Designed; not run in this update.**
- Goal: assess graphics camera settings after reload using the concrete control and state transitions below.
- Primary source: [GraphicsPreset.cs](../Assets/Parasite/Scripts/Core/GraphicsPreset.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: A known preset is selected before a checkpoint or city reload.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the current camera's antialiasing configuration in a permitted inspection session.
- Step 2: Reload the city through an existing path.
- Step 3: Compare the new main camera with the selected preset.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The new camera receives the selected preset on scene load.
- Expected result B: Reload does not quietly revert to a different antialiasing mode while leaving the old label visible.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-048; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-048 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-049 — Journal current mission visibility
- Status: **Designed; not run in this update.**
- Goal: assess journal current mission visibility using the concrete control and state transitions below.
- Primary source: [HUDController.Menus.cs](../Assets/Parasite/Scripts/UI/HUDController.Menus.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: The journal is opened during the eighth mission using a controlled QA fixture or real progression.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Read the current title and briefing at the automatic scroll position.
- Step 2: Read both current objectives without changing resolution.
- Step 3: Compare the viewport bounds with the last objective.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The journal focuses the current late mission instead of starting only at the first mission.
- Expected result B: Current objective text is reachable without overflowing permanently outside the clipped viewport.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-049; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-049 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-050 — Journal mouse wheel and drag
- Status: **Designed; not run in this update.**
- Goal: assess journal mouse wheel and drag using the concrete control and state transitions below.
- Primary source: [HUDController.Menus.cs](../Assets/Parasite/Scripts/UI/HUDController.Menus.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: The journal contains enough entries to exceed its viewport height.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Scroll toward earlier missions using the wheel.
- Step 2: Drag the content or scrollbar toward later entries.
- Step 3: Release the drag and inspect the resulting position.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The clipped list can reach both early and late campaign entries.
- Expected result B: Scrolling changes the journal rather than moving the active gameplay body.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-050; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-050 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-051 — Journal PageUp and PageDown
- Status: **Designed; not run in this update.**
- Goal: assess journal pageup and pagedown using the concrete control and state transitions below.
- Primary source: [HUDController.Menus.cs](../Assets/Parasite/Scripts/UI/HUDController.Menus.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: The main pause view is visible with an overflowing journal.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Hold PageUp briefly and inspect list travel.
- Step 2: Hold PageDown briefly and compare the direction.
- Step 3: Release both keys and read the current objective.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The keyboard can traverse the journal without a mouse.
- Expected result B: Release stops deliberate keyboard scrolling and does not resume gameplay.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-051; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-051 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-052 — Physical controller journal scrolling
- Status: **Designed; not run in this update.**
- Goal: assess physical controller journal scrolling using the concrete control and state transitions below.
- Primary source: [HUDController.Menus.cs](../Assets/Parasite/Scripts/UI/HUDController.Menus.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: A physical gamepad is connected while the main pause journal is open.
- Execution setup: Use the named physical device; record model and connection type. No controller execution is implied.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Move the right stick vertically and observe the list.
- Step 2: Release the stick and open Settings.
- Step 3: Move the right stick again while Settings remains open.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Right-stick scrolling applies to the visible journal context.
- Expected result B: The hidden journal is not treated as the active surface behind Settings.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-052; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-052 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-053 — Pause input isolation
- Status: **Designed; not run in this update.**
- Goal: assess pause input isolation using the concrete control and state transitions below.
- Primary source: [HUDController.Menus.cs](../Assets/Parasite/Scripts/UI/HUDController.Menus.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Pause Settings is open while the active body and agent count are recorded.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Press movement, E, Q, V, Tab, F and Space individually.
- Step 2: Inspect the body and slots without returning to gameplay.
- Step 3: Close only the submenu and compare the run state.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Paused input does not possess, split, switch, use abilities or move the player.
- Expected result B: Closing a submenu does not secretly apply an accumulated gameplay action.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-053; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-053 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-054 — Controls card toggle
- Status: **Designed; not run in this update.**
- Goal: assess controls card toggle using the concrete control and state transitions below.
- Primary source: [HUDController.cs](../Assets/Parasite/Scripts/UI/HUDController.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Gameplay is controllable and the controls card can be inspected.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Press F1 and read the displayed reference.
- Step 2: Press F1 again and inspect the gameplay view.
- Step 3: Pause and repeat the toggle where supported.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The card toggles visibility without changing the active body.
- Expected result B: The control reference does not become an unexplained permanent obstruction.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-054; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-054 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-055 — FPS display toggle
- Status: **Designed; not run in this update.**
- Goal: assess fps display toggle using the concrete control and state transitions below.
- Primary source: [HUDController.cs](../Assets/Parasite/Scripts/UI/HUDController.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Gameplay is running at a known resolution.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Press F3 and allow the counter's sampling interval to elapse.
- Step 2: Read the displayed FPS and frame-time units.
- Step 3: Press F3 again.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The display reports sampled values rather than a fixed advertised performance claim.
- Expected result B: The toggle hides the counter without changing the graphics preset.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-055; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-055 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-056 — Prompt swaps between interaction targets
- Status: **Designed; not run in this update.**
- Goal: assess prompt swaps between interaction targets using the concrete control and state transitions below.
- Primary source: [HUDController.cs](../Assets/Parasite/Scripts/UI/HUDController.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Two nearby interactables offer different labels or access conditions.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Aim at the first and record its prompt.
- Step 2: Aim at the second without pressing E.
- Step 3: Return to the first and compare text and key-cap styling.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Text and availability styling follow the current target.
- Expected result B: A stale denied label does not remain attached to a newly allowed interaction.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-056; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-056 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-057 — Objective text expands for long instructions
- Status: **Designed; not run in this update.**
- Goal: assess objective text expands for long instructions using the concrete control and state transitions below.
- Primary source: [HUDController.cs](../Assets/Parasite/Scripts/UI/HUDController.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Mission six or seven is active and its paired-body instructions are visible.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the complete objective panel at the normal desktop resolution.
- Step 2: Start a timed operation so progress text is appended.
- Step 3: Inspect the lower edge of the panel while progress changes.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The objective panel sizes to its text and includes active work progress.
- Expected result B: Long instructions remain interpretable rather than clipping the operative profession or console location.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-057; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-057 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-058 — Waypoint and compass direction
- Status: **Designed; not run in this update.**
- Goal: assess waypoint and compass direction using the concrete control and state transitions below.
- Primary source: [HUDController.Mission.cs](../Assets/Parasite/Scripts/UI/HUDController.Mission.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A current incomplete objective has a waypoint.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record its distance while moving toward it.
- Step 2: Turn away until the target leaves the camera view.
- Step 3: Compare screen-edge guidance and compass direction.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The guidance continues to identify the intended incomplete objective.
- Expected result B: Distance and direction changes correspond to movement and view rather than a stale marker.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-058; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-058 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-059 — UI readability at 1280 by 720
- Status: **Designed; not run in this update.**
- Goal: assess ui readability at 1280 by 720 using the concrete control and state transitions below.
- Primary source: [UIKit.cs](../Assets/Parasite/Scripts/UI/UIKit.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: The player can run at actual 1280 by 720; record the measured output size.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Capture title Settings and the main pause panel.
- Step 2: Capture the current mission journal and an ordinary target prompt.
- Step 3: Read text at native size without enlarging the saved capture.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Essential labels, values and actions can be read at the tested output size.
- Expected result B: This designed resolution review must not be reported as completed without new native-size inspection evidence.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-059; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-059 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-060 — UI bounds at a narrow desktop aspect
- Status: **Designed; not run in this update.**
- Goal: assess ui bounds at a narrow desktop aspect using the concrete control and state transitions below.
- Primary source: [HUDController.Menus.cs](../Assets/Parasite/Scripts/UI/HUDController.Menus.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: A supported local display mode with a narrower aspect than sixteen-to-nine is available.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the actual mode and open the main pause view.
- Step 2: Check the menu and journal outer edges.
- Step 3: Open Controls and Settings separately and inspect their edges.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Any offscreen control or unscrollable text is identified with exact bounds.
- Expected result B: An untested aspect ratio is recorded as unverified rather than assumed supported from one reference capture.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-060; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-060 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-061 — Muted cinematic comprehension
- Status: **Designed; not run in this update.**
- Goal: assess muted cinematic comprehension using the concrete control and state transitions below.
- Primary source: [LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Master Volume can be muted after its original value is recorded.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Enter the lakeside arrival with audio muted.
- Step 2: Read each caption and both choice descriptions.
- Step 3: Choose a future deliberately and read its concluding text.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The scene communicates the decision through visible text without requiring sound.
- Expected result B: Muting is not mistaken for missing dialogue functionality because the cinematic uses captions and effects.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-061; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-061 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-062 — M alone does not invoke the shortcut
- Status: **Designed; not run in this update.**
- Goal: assess m alone does not invoke the shortcut using the concrete control and state transitions below.
- Primary source: [GameInput.cs](../Assets/Parasite/Scripts/Core/GameInput.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: The active run is in Playing and no Ctrl key is held.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Press M once and release it.
- Step 2: Inspect the mission, active body and game state.
- Step 3: Continue ordinary movement to confirm gameplay remains active.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: M without Ctrl does not begin the presentation ending.
- Expected result B: No campaign completion flag or lakeside object appears from the unmodified key.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-062; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-062 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-063 — Left Ctrl plus M presentation jump
- Status: **Designed; not run in this update.**
- Goal: assess left ctrl plus m presentation jump using the concrete control and state transitions below.
- Primary source: [MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: Gameplay is active after the opening has completed or been skipped.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Hold left Ctrl, press M once, then release both keys.
- Step 2: Inspect the free blob at the final drain and advance the protected dialogue normally.
- Step 3: Stop at the lakeside choices without selecting either.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The shortcut uses the protected final escape path.
- Expected result B: The remaining campaign is bypassed without automatically choosing a future.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-063; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-063 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-064 — Right Ctrl plus M presentation jump
- Status: **Designed; not run in this update.**
- Goal: assess right ctrl plus m presentation jump using the concrete control and state transitions below.
- Primary source: [MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A separate fresh run is active in Playing.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Hold right Ctrl, press M once, then release both keys.
- Step 2: Observe the final escape and subsequent lake scene.
- Step 3: Compare the result with the left-Ctrl procedure without reusing its result record.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Either Ctrl key can form the implemented chord.
- Expected result B: A success with one modifier is not used as fabricated evidence for the other physical-key run.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-064; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-064 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-065 — Shortcut from a possessed host
- Status: **Designed; not run in this update.**
- Goal: assess shortcut from a possessed host using the concrete control and state transitions below.
- Primary source: [MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: A non-transit host is actively possessed and gameplay is Playing.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the host identity and current camera target.
- Step 2: Press Ctrl plus M and observe the release and drain placement.
- Step 3: Inspect the abandoned host and new active blob during protected dialogue.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The active amoeba leaves the host and is staged at the drain as a free blob.
- Expected result B: The abandoned host or capture state does not interrupt the protected ending.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-065; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-065 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-066 — Shortcut refusal during opening and Vale
- Status: **Designed; not run in this update.**
- Goal: assess shortcut refusal during opening and vale using the concrete control and state transitions below.
- Primary source: [GameManager.cs](../Assets/Parasite/Scripts/Core/GameManager.cs).
- Existing coverage pointer: [AcceptanceTests.cs](../Assets/Parasite/Tests/PlayMode/AcceptanceTests.cs); review its actual assertions before reusing evidence.
- Preconditions: The opening or Vale reveal is active in a separate test run.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the current story phase before pressing Ctrl plus M.
- Step 2: Press and release the chord without advancing the current dialogue.
- Step 3: Complete or skip that story normally and compare the handoff.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The gameplay-only shortcut does not replace an already active protected story.
- Expected result B: The original story does not later resume over a newly duplicated ending.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-066; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-066 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-067 — Repeated shortcut at the lake
- Status: **Designed; not run in this update.**
- Goal: assess repeated shortcut at the lake using the concrete control and state transitions below.
- Primary source: [LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: The lakeside scene already exists and no future has been chosen.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the epilogue instance count and Unchosen outcome.
- Step 2: Press Ctrl plus M repeatedly with full releases between presses.
- Step 3: Read both choices and inspect the instance count again.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The active ending is not replaced by another lake instance.
- Expected result B: The repeated chord cannot commit Peace or Dominion.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-067; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-067 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-068 — Held skip does not choose a future
- Status: **Designed; not run in this update.**
- Goal: assess held skip does not choose a future using the concrete control and state transitions below.
- Primary source: [LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: The lake arrival is active with a confirm key or South button held.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Reveal the choices while continuing to hold that input.
- Step 2: Attempt no fresh selection and wait past the activation delay.
- Step 3: Release the input and inspect choice readiness before deliberately confirming.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Held confirm input must be released before choice activation.
- Expected result B: The skip that reached the menu does not become an accidental first ending selection.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-068; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-068 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-069 — Waiting at the lakeside choice
- Status: **Designed; not run in this update.**
- Goal: assess waiting at the lakeside choice using the concrete control and state transitions below.
- Primary source: [LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: Both futures are visible and all confirmation controls are released.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the Unchosen outcome and protected game state.
- Step 2: Wait beyond the arrival caption duration without interacting.
- Step 3: Inspect the final summary and completion flag before choosing.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Waiting at the choice does not auto-select a future.
- Expected result B: Final statistics and the committed escape flag remain deferred until explicit resolution.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-069; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-069 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-070 — Peace choice and concluding beat
- Status: **Designed; not run in this update.**
- Goal: assess peace choice and concluding beat using the concrete control and state transitions below.
- Primary source: [LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: The lakeside choices are ready and neither outcome has been committed.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Select Live quietly deliberately.
- Step 2: Read the ordinary-life resolution before advancing it.
- Step 3: Open the final journey summary after the permitted beat.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Peace produces its own restraint-focused conclusion.
- Expected result B: The summary preserves the chosen future and accumulated gameplay statistics.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-070; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-070 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-071 — Dominion choice and concluding beat
- Status: **Designed; not run in this update.**
- Goal: assess dominion choice and concluding beat using the concrete control and state transitions below.
- Primary source: [LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: A separate run reaches ready lakeside choices with no existing committed outcome.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Select Become the world deliberately.
- Step 2: Read the conquest-focused resolution and inspect the changed visual tone.
- Step 3: Open the journey summary after the concluding beat.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: Dominion is distinguishable from Peace in text and the selected outcome.
- Expected result B: The choice does not pretend to launch an implemented world-infection gameplay campaign.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-071; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-071 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.

### UX-072 — Ending scene cleanup on menu return
- Status: **Designed; not run in this update.**
- Goal: assess ending scene cleanup on menu return using the concrete control and state transitions below.
- Primary source: [LakesideSet.cs](../Assets/Parasite/Scripts/UI/LakesideSet.cs).
- Existing coverage pointer: [SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs); review its actual assertions before reusing evidence.
- Preconditions: A lakeside ending has been chosen and the final statistics are visible.
- Execution setup: Use a focused local player or an explicitly controlled Editor fixture; identify which was used.
- Baseline record: note source/build identity, current mission, active body, agent count and existing run statistics.
- Preference baseline: record any graphics, sensitivity or audio values that the procedure may change.
- Step 1: Record the temporary camera, muted sources and scene objects in a permitted inspection session.
- Step 2: Choose Main Menu and inspect title rendering and input.
- Step 3: Begin another run or exit normally after checking the restored environment.
- Repeat boundary: reproduce the stated transition once more from the same documented preconditions before calling an intermittent failure resolved.
- Expected result A: The old lake, girl, temporary materials and camera do not remain as duplicate live staging.
- Expected result B: HUD, lighting, fog and audio restoration are checked independently instead of inferred from one successful button click.
- Isolation check: Keep the game in the phase specified above; do not convert a fixture-only observation into a standalone-player claim.
- Evidence capture: save before/after frames or a short video under a future execution directory named UX-072; include the actual output dimensions.
- Evidence detail: correlate visible feedback with the active body, game phase and timestamp; use permitted state inspection only when clearly labelled.
- Failure signal: record the first step where either stated expected result differs; retain the observed text or state rather than inventing a successful result.
- Cleanup controls: release every held movement, modifier, confirm and controller input before leaving the case.
- Cleanup state: restore recorded preferences and return through a normal menu/restart path; remove any controlled fixture actors or devices.
- Result entry: **Not executed in this update**; future execution must supply operator, environment, observation and evidence reference.
- Follow-up scope: if UX-072 fails, compare the linked source and actual assertion coverage before proposing a narrowly targeted regression fix.


## Designed release and presentation QA procedures

> **PROCEDURES NOT EXECUTED IN THIS UPDATE.** Every REL case below is **Designed — not run**. These records expand future testing instructions only; they add no successful runs, screenshots, benchmarks or acceptance counts.

Existing verified release evidence belongs to [TEST_REPORT.md](TEST_REPORT.md). Its historical results must remain distinct from these unexecuted procedures. A proposed evidence path below is a collection destination, not a claim that an artifact exists.

Source links are resolved relative to the final report in Docs. Perform any later Unity operation only through Tools/unity.sh and serialize project access. Preserve production sources and generated scenes during these read-only or disposable-fixture checks.

Each case requires an isolated execution record before its status can change. Record the actual environment, revision, result and limitations; retain failures and never infer a pass from source inspection alone.

### REL-001 — Deliberate new-game entry
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: A clean title menu is visible.
- Fixture preparation: Use an isolated preferences profile and fresh campaign.
1. Activate Begin Infiltration once.
2. Observe the first historical frame.
3. Wait until the skip prompt appears.
- Expected presentation: Letterboxing and subject captions replace the gameplay HUD.
- Expected state: The opening owns presentation before player movement begins.
- Negative control: Reject duplicate opening objects after one menu submission.
- Evidence to collect: Record menu activation and controller creation in one clip.
- Proposed record location: `Logs/release-qa/REL-001/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Return to the title without saving campaign progress.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect MainMenuController request and CityDirector consumption if absent.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-002 — Natural eighty-second playback
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The opening has started normally.
- Fixture preparation: Leave preview PlaybackSpeed at its normal value.
1. Start an external monotonic timer after the first frame.
2. Watch every caption without pressing skip.
3. Record the transition into controllable gameplay.
- Expected presentation: All eleven shots play in chronological order.
- Expected state: Gameplay resumes after the scheduled eighty-second timeline and handoff.
- Negative control: Do not infer timing from an accelerated test run.
- Evidence to collect: Collect timestamped video and elapsed controller time.
- Proposed record location: `Logs/release-qa/REL-002/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Quit the disposable run after the handoff.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Separate scene-loading time from cinematic timeline time.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-003 — Shot one subject close-up
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The opening clock is in seconds zero through five.
- Fixture preparation: Use the first shot of a deliberate new game.
1. Observe the dark opening resolve into the culture.
2. Read the first memory caption.
3. Check the nucleus against surrounding tank geometry.
- Expected presentation: The organism remains identifiable during the fade-in.
- Expected state: No historical contact increments possession statistics.
- Negative control: Reject missing subject meshes or permanent black frames.
- Evidence to collect: Capture a frame after the initial fade clears.
- Proposed record location: `Logs/release-qa/REL-003/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restart the disposable run before another shot test.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect subject scale and fade alpha before changing lighting.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-004 — Shot two Petrov introduction
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The clock is between five and twelve seconds.
- Fixture preparation: Observe normal playback rather than jumping to gameplay.
1. Follow the camera pullback.
2. Locate Petrov relative to the tank.
3. Read the four-tap caption at native resolution.
- Expected presentation: The scientist and organism occupy a coherent laboratory composition.
- Expected state: Historical Petrov remains a visual stand-in only.
- Negative control: Reject an unexpected gameplay interaction prompt over the lab.
- Evidence to collect: Capture both the composition and caption at a stated timestamp.
- Proposed record location: `Logs/release-qa/REL-004/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Exit through the title menu after recording.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Check stand-in transforms and the second camera path.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-005 — Shot three recognition
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The clock is between twelve and nineteen seconds.
- Fixture preparation: Enable audible effects at a recorded master gain.
1. Watch the culture while the tap rhythm repeats.
2. Read Petrov's recognition caption.
3. Compare visual pulses with the timed sounds.
- Expected presentation: The recognition moment remains understandable without lip animation.
- Expected state: The timeline advances on the unscaled presentation clock.
- Negative control: Reject claims that a recorded actor delivered the line.
- Evidence to collect: Collect caption video and separately label the synthesized effects.
- Proposed record location: `Logs/release-qa/REL-005/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restore the original audio gains after inspection.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Review caption scheduling and TapTimes if rhythm is unclear.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-006 — Shot four Vale distinction
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The clock is between nineteen and twenty-six seconds.
- Fixture preparation: Retain the ordinary historical actors and camera settings.
1. Identify Vale beyond the tank.
2. Read the differing interpretation captions.
3. Check that no release authorization appears.
- Expected presentation: Vale and Petrov remain visually distinguishable in the frame.
- Expected state: The escape operator's identity remains withheld.
- Negative control: Reject any screenshot annotation claiming Vale visibly opens the lock.
- Evidence to collect: Save a frame with its exact scheduled time.
- Proposed record location: `Logs/release-qa/REL-006/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Leave all actor transforms unchanged after capture.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect camera direction rather than adding narrative explanation.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-007 — Shot five two-lobe trial
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The clock is between twenty-six and thirty-four seconds.
- Fixture preparation: Observe the dedicated two-lobe shot.
1. Count the visible culture lobes.
2. Follow their common pulse.
3. Read the shared-memory caption.
- Expected presentation: Two lobes communicate one organism's continuity.
- Expected state: No third controllable amoeba is created.
- Negative control: Reject gameplay agents registered by historical visuals.
- Evidence to collect: Record visible lobe count and actual possession-agent count separately.
- Proposed record location: `Logs/release-qa/REL-007/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: End the temporary run after observation.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Compare VisualBlob construction with possession-manager registration.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-008 — Shot six contact trial
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The clock is between thirty-four and forty-three seconds.
- Fixture preparation: Use the maintenance volunteer's existing historical staging.
1. Watch the worker's contact reaction.
2. Locate Petrov's intervention.
3. Read End contact after the withdrawal beat.
- Expected presentation: The trial conveys unsettling but survivable contact.
- Expected state: The volunteer is not a real possessed gameplay host.
- Negative control: Reject a host-stat increase caused by this scene.
- Evidence to collect: Capture the worker and subtitle without describing unrendered filaments as verified.
- Proposed record location: `Logs/release-qa/REL-008/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Return to the menu without modifying historical actors.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Distinguish screenplay intent from the implemented procedural pose.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-009 — Shot seven interrupted rhythm
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The clock is between forty-three and fifty-one seconds.
- Fixture preparation: Set audio to an audible recorded level.
1. Observe Petrov's raised arm.
2. Listen for the interrupted tap pattern.
3. Read the request to stay with the rhythm.
- Expected presentation: The missing final beat gives the transition a readable pause.
- Expected state: The normal world remains protected while captions run.
- Negative control: Reject an added alarm or voice recording claim unsupported by assets.
- Evidence to collect: Collect timestamped audiovisual evidence of this transition.
- Proposed record location: `Logs/release-qa/REL-009/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restore prior audio settings after recording.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect scheduled taps before treating silence as an audio failure.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-010 — Shot eight release visibility
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The clock is between fifty-one and fifty-nine seconds.
- Fixture preparation: Use the corrected final-source camera path.
1. Locate the containment status lamp.
2. Watch its release color and latch movement.
3. Check the worker never fills the camera view.
- Expected presentation: The interlock is visible from the right/front camera gap.
- Expected state: No actor identity is shown authorizing release.
- Negative control: Reject the earlier fully occluded orange-torso composition.
- Evidence to collect: Capture the interlock at a documented midpoint and compare source revision.
- Proposed record location: `Logs/release-qa/REL-010/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restart normally after collecting the frame.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Review case-seven camera coordinates and nearby worker bounds.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-011 — Shot nine service conduit
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The clock is between fifty-nine and sixty-seven seconds.
- Fixture preparation: Keep the historical duct set available.
1. Follow the escaping culture along the pipe.
2. Check reflections on surrounding metal.
3. Read the service-conduit chapter label.
- Expected presentation: The culture stays readable against the enclosed passage.
- Expected state: The historical lab is hidden while the duct is shown.
- Negative control: Reject a city teleport being described as continuous physical traversal.
- Evidence to collect: Save a conduit frame with the active shot interval.
- Proposed record location: `Logs/release-qa/REL-011/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Exit normally so temporary materials are destroyed.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect duct visibility and camera travel if walls clip.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-012 — Shot ten real drain
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The clock is between sixty-seven and seventy-five seconds.
- Fixture preparation: Use the actual generated city and player spawn.
1. Observe the low drain-side composition.
2. Identify the real player amoeba.
3. Read the outside-containment caption.
- Expected presentation: The scene visibly changes from historical lab to city alley.
- Expected state: The player remains a single free amoeba.
- Negative control: Reject a historical duplicate remaining in the playable alley.
- Evidence to collect: Collect drain composition and agent-count evidence together.
- Proposed record location: `Logs/release-qa/REL-012/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Finish or skip normally after observation.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Compare CityLayout drain and player coordinates if framing diverges.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-013 — Shot eleven title handoff
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The clock is between seventy-five and eighty seconds.
- Fixture preparation: Retain the original captured gameplay camera pose.
1. Watch the PARASITE title appear.
2. Follow the easing camera movement.
3. Wait until the mission banner becomes available.
- Expected presentation: The title clears before normal gameplay presentation settles.
- Expected state: The player receives control at the intended alley spawn.
- Negative control: Reject a permanent title or black overlay after completion.
- Evidence to collect: Capture title visibility and a second frame after cleanup.
- Proposed record location: `Logs/release-qa/REL-013/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Quit the disposable run once control is verified.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect original-camera restoration and title visibility window.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-014 — Space skip
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: An opening is active past its initial debounce.
- Fixture preparation: Use a physical keyboard or clearly labelled queued-input fixture.
1. Press Space once.
2. Keep it held briefly.
3. Release and wait for gameplay.
- Expected presentation: The opening fades out and the skip prompt requests release.
- Expected state: The next controllable frame does not inherit an unwanted jump.
- Negative control: Reject a skip that leaves camera or input locked.
- Evidence to collect: Record key down, key up and first controllable frame.
- Proposed record location: `Logs/release-qa/REL-014/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Release every test key before leaving the run.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Check SkipHeld and Finish release gating if movement bleeds through.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-015 — Enter skip
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: An opening is active with no confirm key held.
- Fixture preparation: Start from the real title Begin action.
1. Press Enter after the debounce.
2. Release Enter.
3. Observe the first gameplay frame.
- Expected presentation: The caption overlay clears through the normal fade path.
- Expected state: The menu confirmation cannot immediately skip before release.
- Negative control: Reject an opening skipped solely by held title confirmation.
- Evidence to collect: Collect input timestamps and LastExitReason if instrumented.
- Proposed record location: `Logs/release-qa/REL-015/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Clear keyboard state and return to the title.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect skip arming before modifying menu navigation.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-016 — Escape skip
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: An opening is active and not already finishing.
- Fixture preparation: Ensure gameplay pause is initially closed.
1. Press Escape once.
2. Release Escape.
3. Check gameplay and the pause overlay.
- Expected presentation: The opening disappears without opening the pause menu.
- Expected state: Game state returns to Playing with ordinary world time.
- Negative control: Reject Escape both skipping and pausing in one transition.
- Evidence to collect: Save a post-skip frame including HUD visibility.
- Proposed record location: `Logs/release-qa/REL-016/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Release Escape and end the disposable run.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Check release consumption and GameManager pause-state guards.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-017 — Gamepad opening skip
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: A supported controller is connected before new game.
- Fixture preparation: Record controller model and whether input is physical.
1. Start the opening.
2. Press South then repeat with East in another run.
3. Release the button before testing movement.
- Expected presentation: Both documented skip bindings show the same clean transition.
- Expected state: No unwanted confirm or jump reaches gameplay after release.
- Negative control: Do not convert a binding inspection into a physical-controller pass.
- Evidence to collect: Collect device identity and button-event evidence.
- Proposed record location: `Logs/release-qa/REL-017/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Disconnect only after returning to the title.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Compare controller layout with SkipHeld and input-system devices.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-018 — Debounce and held submit
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The title Begin button has focus.
- Fixture preparation: Hold the confirmation used to activate Begin.
1. Keep holding through the first opening frames.
2. Release confirmation.
3. Press a fresh skip input after the debounce.
- Expected presentation: The opening remains visible until a deliberate eligible skip.
- Expected state: Skip arming requires the documented release condition.
- Negative control: Reject immediate playback cancellation caused by menu input carryover.
- Evidence to collect: Capture input continuity across the scene transition.
- Proposed record location: `Logs/release-qa/REL-018/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Release keys and reset the title fixture.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect realElapsed and skipArmed before blaming scene loading.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-019 — Restart replay exclusion
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: A deliberate new game has completed its opening.
- Fixture preparation: Use the ordinary in-game restart action.
1. Record current opening-instance absence.
2. Restart the campaign.
3. Observe the restored alley and HUD.
- Expected presentation: Restart reaches gameplay directly.
- Expected state: No new opening request is consumed on restart.
- Negative control: Reject historical-set reconstruction during restart.
- Evidence to collect: Collect scene transition and opening-instance evidence.
- Proposed record location: `Logs/release-qa/REL-019/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Return to the menu after the restarted run.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Compare RestartGame with the MainMenu-only request path.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-020 — Checkpoint replay exclusion
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: A real campaign checkpoint exists.
- Fixture preparation: Use a disposable checkpoint-capable run.
1. Trigger the normal checkpoint reset path.
2. Wait for the replacement city.
3. Check whether opening objects appear.
- Expected presentation: The player resumes at the checkpoint rather than watching origin shots.
- Expected state: Completed flags persist without replaying the opening.
- Negative control: Reject checkpoint reload resetting campaign facts solely for presentation.
- Evidence to collect: Record checkpoint identity and flags before and after reload.
- Proposed record location: `Logs/release-qa/REL-020/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Discard the fixture after checking restoration.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect request consumption and RunState rather than editing scenes.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-021 — Direct scene replay exclusion
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The city can be loaded by the existing test harness.
- Fixture preparation: Use a direct City acceptance load without title submission.
1. Load the city through that harness.
2. Wait for director readiness.
3. Inspect the presentation hierarchy.
- Expected presentation: No opening overlay or historical stage is created.
- Expected state: Automated direct loads remain available for acceptance testing.
- Negative control: Reject a test bypass being described as title-menu coverage.
- Evidence to collect: Record harness arguments and exact entry path.
- Proposed record location: `Logs/release-qa/REL-021/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Unload the test scene after inspection.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Check requested static state and initialization order.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-022 — Automation argument exclusion
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: A separate already-built executable is available.
- Fixture preparation: Use an isolated command-line launch plan.
1. Plan one launch with the existing autotest argument.
2. Plan another with the benchmark argument.
3. Inspect startup logs for opening creation.
- Expected presentation: Automation startup omits the new-game origin sequence.
- Expected state: No eighty-second narrative delay contaminates automation timing.
- Negative control: Reject ordinary no-argument play being silently excluded.
- Evidence to collect: Keep arguments and startup logs with the test record.
- Proposed record location: `Logs/release-qa/REL-022/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Terminate only the isolated fixture after completion.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Compare exact command-line tokens with StartIfRequested.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-023 — Camera driver restoration
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: An opening has captured a valid PlayerCamera.
- Fixture preparation: Record camera pose, FOV and enabled drivers before playback.
1. Watch or skip the opening.
2. Inspect PlayerCamera and CinemachineBrain.
3. Compare restored camera parameters with the snapshot.
- Expected presentation: The camera resumes ordinary third-person tracking.
- Expected state: Near/far planes and driver enabled states restore correctly.
- Negative control: Reject two camera drivers fighting after the handoff.
- Evidence to collect: Collect before/after parameter snapshots and an orbit clip.
- Proposed record location: `Logs/release-qa/REL-023/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Reset camera input and return to the title.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect capturedState and Cleanup parameter assignments.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-024 — Gameplay HUD restoration
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: A normal title entry can create the HUD.
- Fixture preparation: Record the gameplay Canvas before opening ownership.
1. Start and finish the opening.
2. Inspect the opening Canvas lifecycle.
3. Check objectives and ordinary controls display.
- Expected presentation: Cinematic text disappears and the gameplay HUD returns.
- Expected state: The prior HUD enabled state is preserved.
- Negative control: Reject overlapping old subtitles and new mission text.
- Evidence to collect: Capture one active-opening and one restored-HUD frame.
- Proposed record location: `Logs/release-qa/REL-024/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Close any diagnostic overlays before ending the run.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect canvas ownership rather than changing global sorting blindly.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-025 — Cursor restoration after skip
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The opening owns presentation and locks the cursor.
- Fixture preparation: Use an ordinary playable city window.
1. Skip and release input.
2. Move the gameplay camera.
3. Pause and unpause once after handoff.
- Expected presentation: Gameplay look works and pause exposes its normal pointer.
- Expected state: Cursor state follows the active GameManager state.
- Negative control: Reject a hidden pointer in a reopened pause menu.
- Evidence to collect: Record lock mode and visible state at each transition.
- Proposed record location: `Logs/release-qa/REL-025/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Unpause or quit through the normal menu.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Review ResumeAfterDialogue and SetCursorLocked together.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-026 — Temporary ambience gain restoration
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: Saved master and ambience gains are nondefault.
- Fixture preparation: Record current scene ambience gain before origin playback.
1. Start the opening.
2. Listen through the drain transition.
3. Finish and compare the scene gain.
- Expected presentation: The city ambience rises smoothly near the real-drain shots.
- Expected state: Saved preferences and previous ambience gain remain intact.
- Negative control: Reject a permanent volume reduction after skip or completion.
- Evidence to collect: Collect gain values and a labelled audio capture.
- Proposed record location: `Logs/release-qa/REL-026/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restore the original preference profile after the test.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Check oldAmbienceGain and Cleanup instead of altering master gain.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-027 — Historical statistics isolation
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: A fresh run has zero campaign statistics.
- Fixture preparation: Record hosts, captures, resets and flags before playback.
1. Watch the contact and two-lobe shots.
2. Complete the opening.
3. Inspect the same counters at handoff.
- Expected presentation: The story implies abilities without executing gameplay possession.
- Expected state: One free agent and untouched mission statistics remain.
- Negative control: Reject historical volunteers appearing in HostCharacter.All.
- Evidence to collect: Collect counters and real-host registry counts before and after.
- Proposed record location: `Logs/release-qa/REL-027/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Discard the new game after inspection.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Compare HumanoidRig stand-ins with HostFactory gameplay actors.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-028 — Interrupted opening cleanup
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: A disposable opening is actively running.
- Fixture preparation: Use a controlled diagnostic fixture rather than production edits.
1. Deactivate the opening GameObject deliberately.
2. Allow its OnDisable cleanup to execute.
3. Inspect cameras, audio and overlays.
- Expected presentation: The historical set and letterbox become inactive immediately.
- Expected state: Gameplay ownership restores without leaving input locked.
- Negative control: Reject an orphan Finish coroutine altering later presentation.
- Evidence to collect: Collect object lifecycle and restored-state snapshots.
- Proposed record location: `Logs/release-qa/REL-028/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Destroy the disabled fixture and return to the title.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Check GameObject deactivation versus component-only disabling semantics.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-029 — Scene-unload cleanup
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: An opening is active in a disposable run.
- Fixture preparation: Prepare a normal Quit to Menu transition.
1. Leave the scene during presentation.
2. Wait for the replacement title.
3. Inspect stale object references and audio.
- Expected presentation: The title renders without historical or city overlays.
- Expected state: Old-scene cleanup does not resume a replacement scene.
- Negative control: Reject a destroyed manager receiving a late resume call.
- Evidence to collect: Save lifecycle logs with both scene identities.
- Proposed record location: `Logs/release-qa/REL-029/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Begin a fresh fixture only after unload completes.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect the same-manager and same-scene cleanup guards.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-030 — First mission reannouncement
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Missions/MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
- Preconditions: The first mission exists before origin playback ends.
- Fixture preparation: Use a fresh title-driven campaign.
1. Finish the opening naturally.
2. Read the first objective banner.
3. Open the journal after control returns.
- Expected presentation: OUT OF THE DRAIN is visible after presentation ownership ends.
- Expected state: Reannouncement changes no flags or mission index.
- Negative control: Reject the mission banner being consumed only behind the cinematic.
- Evidence to collect: Capture banner timing and unchanged campaign state.
- Proposed record location: `Logs/release-qa/REL-030/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Close the journal and quit the disposable run.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect ReannounceCurrentMission rather than advancing the campaign.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-031 — Protected story time
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/GameManager.cs](../Assets/Parasite/Scripts/Core/GameManager.cs).
- Preconditions: The opening or Vale dialogue is active.
- Fixture preparation: Record world time and real time independently.
1. Enter the protected story state.
2. Observe both clocks for a measured interval.
3. Finish the story through its normal path.
- Expected presentation: Captions progress while the live scene appears stationary.
- Expected state: Time.timeScale is zero during story and one after playable resume.
- Negative control: Reject a wall-clock freeze being mistaken for narrative completion.
- Evidence to collect: Collect state, timeScale and monotonic timestamps.
- Proposed record location: `Logs/release-qa/REL-031/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restore the ordinary gameplay state before quitting.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Separate unscaled presentation timing from scaled simulation timers.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-032 — Nearby guard freeze
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Characters/GuardAI.cs](../Assets/Parasite/Scripts/Characters/GuardAI.cs).
- Preconditions: A disposable fixture places an investigating guard nearby.
- Fixture preparation: Record guard transform and target before protected dialogue.
1. Start Vale's real dialogue.
2. Observe the guard throughout several captions.
3. Resume and observe normal guard updates.
- Expected presentation: The guard does not approach during locked story input.
- Expected state: AI simulation resumes only after the story releases gameplay.
- Negative control: Reject a capture happening while the player cannot move.
- Evidence to collect: Collect guard positions and dialogue state over time.
- Proposed record location: `Logs/release-qa/REL-032/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Remove the fixture through a normal scene reload.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect guard update-state guards before changing detection distances.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-033 — Suspicion freeze under dialogue
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Stealth/SuspicionSystem.cs](../Assets/Parasite/Scripts/Stealth/SuspicionSystem.cs).
- Preconditions: A disposable fixture has elevated suspicion.
- Fixture preparation: Record suspicion value immediately before dialogue.
1. Start the protected reveal.
2. Wait through multiple captions.
3. Compare the value before normal gameplay resumes.
- Expected presentation: The meter does not grow from new story-time observations.
- Expected state: Suspicion logic honors the protected game state.
- Negative control: Reject a lowered value caused by fixture reset instead of freezing.
- Evidence to collect: Collect timestamped meter samples and game state.
- Proposed record location: `Logs/release-qa/REL-033/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Reset suspicion only after measurements are complete.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect state gating in suspicion update and violation handling.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-034 — Capture countdown protection
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/GameManager.cs](../Assets/Parasite/Scripts/Core/GameManager.cs).
- Preconditions: A disposable controlled fixture has a captured host timer.
- Fixture preparation: Record the countdown before protected presentation.
1. Enter a supported story sequence.
2. Wait longer than an ordinary short capture tick.
3. Compare remaining time before resuming.
- Expected presentation: No capture penalty resolves behind story presentation.
- Expected state: The countdown remains paused with the world simulation.
- Negative control: Reject a reset that silently increments capture statistics.
- Evidence to collect: Collect timer and capture-count samples together.
- Proposed record location: `Logs/release-qa/REL-034/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Reload the fixture rather than editing release state.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Trace host capture timers and GameManager protection consistently.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-035 — Unconscious recovery freeze
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Characters/HostCharacter.cs](../Assets/Parasite/Scripts/Characters/HostCharacter.cs).
- Preconditions: A released host is unconscious before dialogue.
- Fixture preparation: Record remaining recovery time in a diagnostic fixture.
1. Start the protected story.
2. Observe the unconscious host during captions.
3. Resume gameplay and wait for recovery.
- Expected presentation: The host does not stand up during the frozen interval.
- Expected state: Recovery continues on ordinary world time after resume.
- Negative control: Reject wall-clock time being counted as active recovery.
- Evidence to collect: Collect host-state transitions with scaled and real timestamps.
- Proposed record location: `Logs/release-qa/REL-035/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: End the fixture after the host returns to AI.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect HostCharacter timer update and story-state checks.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-036 — Traffic freeze during story
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/World/TrafficSystem.cs](../Assets/Parasite/Scripts/World/TrafficSystem.cs).
- Preconditions: Traffic is visible before protected presentation begins.
- Fixture preparation: Record a selected vehicle's transform and identity.
1. Enter the opening or an allowed dialogue fixture.
2. Observe the same vehicle for several seconds.
3. Resume and compare lane movement.
- Expected presentation: Traffic stops while the live world is frozen.
- Expected state: Vehicles resume their simplified lane behavior afterward.
- Negative control: Reject a camera move being misread as vehicle motion.
- Evidence to collect: Collect fixed-coordinate vehicle samples, not only screen positions.
- Proposed record location: `Logs/release-qa/REL-036/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Leave traffic enabled and reload the disposable scene.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect traffic update timing before altering lane geometry.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-037 — Reveal continuation after Vale
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Missions/MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
- Preconditions: Mission five is legitimately ready.
- Fixture preparation: Use a disposable run with the office unlocked.
1. Interact with Vale.
2. Read or deliberately advance each reveal line.
3. Observe the next mission after dialogue ends.
- Expected presentation: The reveal identifies Vale's engineered escape.
- Expected state: Gameplay continues into THE PART HE HID instead of final summary.
- Negative control: Reject a lakeside ending immediately after the office reveal.
- Evidence to collect: Capture final reveal caption and subsequent objective banner.
- Proposed record location: `Logs/release-qa/REL-037/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Quit the fixture without claiming a full campaign run.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Check Confronted completion and ResumeAfterDialogue ordering.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-038 — Escape dialogue protection
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Missions/MissionManager.cs](../Assets/Parasite/Scripts/Missions/MissionManager.cs).
- Preconditions: Broadcast and containment prerequisites are satisfied.
- Fixture preparation: Use the real drain extraction in a disposable run.
1. Activate extraction as a free amoeba.
2. Read the three escape captions.
3. Wait for the lakeside transition.
- Expected presentation: News, Petrov and Specimen 7 captions precede the lake.
- Expected state: Final Escaped remains uncommitted until explicit ending resolution.
- Negative control: Reject final completion being granted before a future is chosen.
- Evidence to collect: Collect flags at extraction, lake arrival and resolution.
- Proposed record location: `Logs/release-qa/REL-038/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restart after the complete recorded transition.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Compare CompleteEscape with PublishFinalEnding flag timing.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-039 — Lakeside arrival composition
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/UI/LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Preconditions: The real escape sequence has reached the epilogue.
- Fixture preparation: Use the implemented temporary lakeside set.
1. Observe the first arrival frame.
2. Locate the seated girl and shoreline.
3. Read the time-and-place heading.
- Expected presentation: A girl is seated beside water under sunset lighting.
- Expected state: No ending outcome is chosen at arrival.
- Negative control: Reject an empty lake being described as character presentation.
- Evidence to collect: Capture the full composition and unchosen outcome state.
- Proposed record location: `Logs/release-qa/REL-039/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Exit only through a supported menu or test cleanup.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect LakesideSet construction and camera ownership.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-040 — Seated character silhouette
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/UI/LakesideSet.cs](../Assets/Parasite/Scripts/UI/LakesideSet.cs).
- Preconditions: The lakeside set is active.
- Fixture preparation: Use the ordinary epilogue camera and no debug gizmos.
1. Inspect head, torso, arms and bent legs.
2. Watch the pose across several frames.
3. Compare body contact with the bank or seat.
- Expected presentation: The procedural figure reads as seated rather than floating upright.
- Expected state: The actor remains a presentation stand-in, not a new controlled host.
- Negative control: Reject claims of imported cinematic character animation.
- Evidence to collect: Collect a close enough frame to judge limb placement.
- Proposed record location: `Logs/release-qa/REL-040/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Dispose of the temporary scene through normal teardown.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect generated part transforms and model scale.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-041 — Sunset and water separation
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/UI/LakesideSet.cs](../Assets/Parasite/Scripts/UI/LakesideSet.cs).
- Preconditions: The lakeside arrival frame is visible.
- Fixture preparation: Use a stated graphics preset and resolution.
1. Locate the horizon, bank and water surface.
2. Watch highlights during a short hold.
3. Compare warm sky with cooler surrounding geometry.
- Expected presentation: Water is distinguishable from land without opaque rectangular edges.
- Expected state: The scene remains presentation-only with no swimming controls.
- Negative control: Reject reflection effects being called physical fluid simulation.
- Evidence to collect: Collect preset-labelled wide and shoreline frames.
- Proposed record location: `Logs/release-qa/REL-041/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restore the previous graphics preset after inspection.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Review water materials and scene lighting independently.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-042 — Arrival captions and pacing
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/UI/LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Preconditions: The epilogue has not yet exposed choices.
- Fixture preparation: Use normal unscaled playback and no held confirm.
1. Read the first arrival caption.
2. Wait through later arrival captions.
3. Allow or request Continue to choice.
- Expected presentation: Captions remain readable before the decision interface appears.
- Expected state: Arrival progression does not itself set Peace or Dominion.
- Negative control: Reject waiting through arrival as an explicit ending selection.
- Evidence to collect: Record caption timestamps and outcome state.
- Proposed record location: `Logs/release-qa/REL-042/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Complete a deliberate choice only after recording this case.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Check phaseAge thresholds separately from choice activation.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-043 — Explicit choice gate
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/UI/LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Preconditions: The two ending buttons have just appeared.
- Fixture preparation: Keep all confirm inputs released before eligibility.
1. Wait for button interaction to become available.
2. Navigate between the two choices.
3. Do not confirm either choice.
- Expected presentation: Both futures are openly visible with distinct descriptions.
- Expected state: Outcome remains Unchosen while only navigating or waiting.
- Negative control: Reject keyboard focus being interpreted as a decision.
- Evidence to collect: Collect selected UI object and outcome snapshots.
- Proposed record location: `Logs/release-qa/REL-043/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Leave through controlled test teardown without fabricating an outcome.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect phase and ConfirmHeld release gating.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-044 — Peace resolution
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/UI/LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Preconditions: The choices are ready and no outcome exists.
- Fixture preparation: Use a disposable ending fixture.
1. Select Live quietly deliberately.
2. Read the resulting caption and heading.
3. Advance to View your journey after release.
- Expected presentation: The resolution promises an ordinary life and restraint.
- Expected state: The immutable outcome becomes Peace and appears in summary.
- Negative control: Reject a generic summary that omits the chosen future.
- Evidence to collect: Capture choice, resolution and summary as three labelled artifacts.
- Proposed record location: `Logs/release-qa/REL-044/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restart before demonstrating the alternative ending.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Check Choose branch and final EndingData propagation.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-045 — Dominion resolution
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/UI/LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Preconditions: A fresh ending fixture has choices ready.
- Fixture preparation: Ensure Peace was not selected in this same fixture.
1. Select Become the world deliberately.
2. Read the altered heading and captions.
3. Proceed to the journey summary.
- Expected presentation: The resolution describes expanding control toward city and world.
- Expected state: The immutable outcome becomes Dominion.
- Negative control: Reject reusing the Peace screenshot as Dominion evidence.
- Evidence to collect: Collect distinct resolution and summary frames.
- Proposed record location: `Logs/release-qa/REL-045/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restart or teardown after completing the fixture.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Compare outcome-specific text and LakesideSet tick behavior.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-046 — Choice immutability
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/UI/LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Preconditions: One deliberate ending choice has been made.
- Fixture preparation: Keep the same epilogue instance for the negative probe.
1. Attempt to select the other hidden or inactive choice.
2. Advance only through supported resolution controls.
3. Inspect the final summary.
- Expected presentation: Only the first chosen resolution is shown.
- Expected state: Outcome cannot change after the Choice phase ends.
- Negative control: Reject a programmatic second call being treated as a new decision.
- Evidence to collect: Collect phase and outcome before and after the rejected attempt.
- Proposed record location: `Logs/release-qa/REL-046/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: End the completed run normally.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect ChoicesReady and phase validation in Choose.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-047 — Escape never selects a future
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/UI/LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Preconditions: The decision screen is active with no outcome.
- Fixture preparation: Ensure the arrival phase has finished.
1. Press and release Escape.
2. Wait without using a choice button.
3. Inspect outcome and both buttons.
- Expected presentation: The decision interface remains available.
- Expected state: Escape does not choose or finalize Peace or Dominion.
- Negative control: Reject a pause or skip action silently committing Escaped.
- Evidence to collect: Collect input-event and outcome evidence.
- Proposed record location: `Logs/release-qa/REL-047/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Reset the fixture after the negative check.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Trace phase-specific keyboard handling rather than menu defaults.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-048 — Held confirmation transition
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/UI/LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Preconditions: A confirm key is held before choices become visible.
- Fixture preparation: Use a disposable fixture with real input release events.
1. Continue from arrival while holding confirm.
2. Wait for the choice interface.
3. Release and only then make a fresh selection.
- Expected presentation: Choices do not activate through an inherited held press.
- Expected state: The player must deliberately confirm an eligible choice.
- Negative control: Reject automatic Peace caused by default focus and held Enter.
- Evidence to collect: Record held state, readiness and first selected outcome.
- Proposed record location: `Logs/release-qa/REL-048/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Clear all queued or physical inputs after the test.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Check ConfirmHeld and delayed interactable enabling.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-049 — Resolution-to-summary timing
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/UI/LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Preconditions: An explicit resolution is visible.
- Fixture preparation: Record the choice timestamp in real time.
1. Attempt immediate summary activation.
2. Wait through the minimum resolution interval.
3. Use View your journey or observe the supported timeout.
- Expected presentation: Resolution remains visible long enough to establish the choice.
- Expected state: Summary publication occurs once with the selected outcome.
- Negative control: Reject duplicate ending events from button plus timeout.
- Evidence to collect: Collect event count and real-time phase transitions.
- Proposed record location: `Logs/release-qa/REL-049/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restart after final summary inspection.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect FinishResolution phase and minimum-age guards.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-050 — Lakeside audio ownership
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/UI/LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Preconditions: A city with active audio reaches the lake.
- Fixture preparation: Record active AudioSource mute states before arrival.
1. Observe the lake's quiet ambience.
2. Complete the summary and teardown the fixture.
3. Compare restored source mute states.
- Expected presentation: City loops do not overpower the quiet-water presentation.
- Expected state: Existing sources restore their previous mute states on disposal.
- Negative control: Reject permanently muted city audio after a scene transition.
- Evidence to collect: Collect source-state snapshots and labelled audio samples.
- Proposed record location: `Logs/release-qa/REL-050/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restore the previous preference profile and unload the fixture.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect audioSnapshot and epilogue-owned shoreline source cleanup.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-051 — Repeated epilogue cleanup
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/UI/LakesideEpilogue.cs](../Assets/Parasite/Scripts/UI/LakesideEpilogue.cs).
- Preconditions: One ending fixture has completed normally.
- Fixture preparation: Record object and temporary material counts after teardown.
1. Start a second disposable ending fixture.
2. Complete a different explicit outcome.
3. Unload it through the same supported path.
- Expected presentation: No previous girl, camera or lake overlaps the second set.
- Expected state: Temporary resources and controller singleton do not accumulate.
- Negative control: Reject steadily growing sets across repeated demonstrations.
- Evidence to collect: Collect hierarchy and resource counts at equivalent lifecycle points.
- Proposed record location: `Logs/release-qa/REL-051/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Close both fixtures and return to title.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect LakesideSet.Dispose and epilogue OnDestroy ownership.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-052 — Lake shader availability
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/UI/LakesideSet.cs](../Assets/Parasite/Scripts/UI/LakesideSet.cs).
- Preconditions: A Linux player containing the final scene is available.
- Fixture preparation: Record graphics API and quality preset.
1. Reach the real lakeside scene.
2. Inspect water, sky and character materials.
3. Search the player log for shader-related errors.
- Expected presentation: Materials render without pink error surfaces.
- Expected state: Required shader variants are available in the built player.
- Negative control: Reject Editor-only success as proof of Linux shader inclusion.
- Evidence to collect: Collect a player screenshot and matching player-log excerpt.
- Proposed record location: `Logs/release-qa/REL-052/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Quit the isolated player without modifying shader assets.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Compare Shader.Find fallback choices with build inclusion.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-053 — Opening material availability
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/MatFactory.cs](../Assets/Parasite/Scripts/Core/MatFactory.cs).
- Preconditions: The final Linux executable can start a new game.
- Fixture preparation: Use the packaged player rather than an Editor-only view.
1. Watch the historical lab and culture.
2. Inspect transparent tank and emissive nucleus.
3. Review matching player logs.
- Expected presentation: Lab materials preserve usable contrast and culture visibility.
- Expected state: No missing shader changes the narrative silhouettes.
- Negative control: Reject a bright blob alone as proof that all materials loaded.
- Evidence to collect: Collect representative lab and conduit frames plus errors if any.
- Proposed record location: `Logs/release-qa/REL-053/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Exit after the opening handoff.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect MatFactory material construction and shader fallback paths.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-054 — Caption contrast at 720p
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The player can run at a genuine 1280 by 720 viewport.
- Fixture preparation: Record actual render size and UI scale.
1. Watch representative lab and drain captions.
2. Inspect longest caption wrapping.
3. Compare text against bright culture and dark letterbox.
- Expected presentation: Every required caption remains readable without truncation.
- Expected state: UI scale does not change mission state or timing.
- Negative control: Reject a resized 1080p image being labelled native 720p.
- Evidence to collect: Collect native-resolution images with dimensions preserved.
- Proposed record location: `Logs/release-qa/REL-054/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restore the original window size and preset.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Review text anchors and CanvasScaler rather than rewriting dialogue.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-055 — Caption contrast at 1080p
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/OpeningCinematic.cs](../Assets/Parasite/Scripts/Core/OpeningCinematic.cs).
- Preconditions: The player uses a genuine 1920 by 1080 viewport.
- Fixture preparation: Record preset and actual render scale separately.
1. Watch the origin at ordinary speed.
2. Inspect chapter, speaker and skip text.
3. Check the title against the alley background.
- Expected presentation: Text remains legible and does not collide with scene framing.
- Expected state: The last shot still hands off into ordinary HUD presentation.
- Negative control: Reject scaled screenshots as evidence of native pixel dimensions.
- Evidence to collect: Save original-size images and viewport metadata.
- Proposed record location: `Logs/release-qa/REL-055/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restore the previous display configuration.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect anchors and available width before reducing font sizes.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-056 — Master mute persistence
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/AudioPreferences.cs](../Assets/Parasite/Scripts/Core/AudioPreferences.cs).
- Preconditions: A disposable saved-settings profile is available.
- Fixture preparation: Record existing preference values before changing them.
1. Set master volume to mute through Settings.
2. Start the opening and later reach the lake.
3. Restart the application with the same profile.
- Expected presentation: Synthesized effects and ambience respect the muted master gain.
- Expected state: The saved mute setting survives reopening.
- Negative control: Reject narrative silence being assumed to mean missing assets.
- Evidence to collect: Collect settings values and an audio-level observation.
- Proposed record location: `Logs/release-qa/REL-056/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restore the user's original preference profile.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Check AudioPreferences application and AudioListener gain.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-057 — Ambience gain distinction
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/AudioPreferences.cs](../Assets/Parasite/Scripts/Core/AudioPreferences.cs).
- Preconditions: Master volume is audible and ambience gain is configurable.
- Fixture preparation: Choose clearly different master and ambience values.
1. Apply ambience reduction through Settings.
2. Listen to city ambience and a discrete UI effect.
3. Reach the quiet-water scene if using an ending fixture.
- Expected presentation: Ambience responds independently while discrete effects remain controlled.
- Expected state: Temporary cinematic ducking does not overwrite saved preferences.
- Negative control: Reject music/ambience labels implying recorded musical assets.
- Evidence to collect: Collect values and matched before/after audio samples.
- Proposed record location: `Logs/release-qa/REL-057/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restore both original gains after testing.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Trace ambience source multipliers separately from master volume.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-058 — Opening sound-event audit
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/FX/SfxBackend.cs](../Assets/Parasite/Scripts/FX/SfxBackend.cs).
- Preconditions: The opening is audible at documented gains.
- Fixture preparation: Use an ordinary playback with no accelerated clock.
1. Listen at the tap, contact, release and conduit transitions.
2. Compare event timing with source scheduling.
3. Note any repeated or missing effect.
- Expected presentation: Existing synthesized/UI effects support the captioned sequence.
- Expected state: No recorded dialogue asset is required for comprehension.
- Negative control: Reject sound-effect playback as proof of voice acting.
- Evidence to collect: Collect a time-coded sound-event list and audio excerpt.
- Proposed record location: `Logs/release-qa/REL-058/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Return gains to their prior values.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Compare TapTimes and shot-change sound triggers.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-059 — Quality preset visual review
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/GraphicsPreset.cs](../Assets/Parasite/Scripts/Core/GraphicsPreset.cs).
- Preconditions: A disposable preferences profile is available.
- Fixture preparation: Record the initial quality preset and render scale.
1. Apply Low, Medium, High and Ultra separately.
2. Inspect the same alley and lake views.
3. Return to the original preset.
- Expected presentation: Each preset renders identifiable world and readable UI.
- Expected state: Preset changes affect rendering rather than campaign flags.
- Negative control: Reject visual differences being labelled geometry loss without counts.
- Evidence to collect: Collect one matched view per stated preset.
- Proposed record location: `Logs/release-qa/REL-059/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restore the initial preset and preferences.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect GraphicsPreset mappings before comparing screenshots.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-060 — Render scale versus viewport
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/GraphicsPreset.cs](../Assets/Parasite/Scripts/Core/GraphicsPreset.cs).
- Preconditions: A genuine display resolution is known.
- Fixture preparation: Record viewport dimensions and graphics-preset selection.
1. Inspect the effective render-scale setting.
2. Capture a fixed gameplay view.
3. Compare UI pixel dimensions with scene sharpness.
- Expected presentation: UI and scene rendering are described with correct dimensions.
- Expected state: Benchmark records separate display resolution from render scale.
- Negative control: Reject calling a scaled scene native-resolution performance.
- Evidence to collect: Collect effective settings and a full-resolution screenshot.
- Proposed record location: `Logs/release-qa/REL-060/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restore the prior display and preset settings.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Review URP render scale rather than only window size.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-061 — Fresh performance protocol
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Docs/TEST_REPORT.md](../Docs/TEST_REPORT.md).
- Preconditions: An isolated Linux build and stable test machine are available.
- Fixture preparation: Record CPU, GPU, driver, API and power mode.
1. Choose a repeatable fixed route before measuring.
2. Exclude startup and screenshot encoding explicitly.
3. Collect frame times over a stated sample interval.
- Expected presentation: Performance evidence corresponds to the tested route and settings.
- Expected state: No historical baseline becomes a new measurement automatically.
- Negative control: Reject invented mean FPS or unrecorded percentile claims.
- Evidence to collect: Save raw frame samples, settings and route completion state.
- Proposed record location: `Logs/release-qa/REL-061/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Close the measured player and restore desktop settings.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Separate sustained performance from loading and test-harness overhead.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-062 — Frame-time distribution review
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Docs/TEST_REPORT.md](../Docs/TEST_REPORT.md).
- Preconditions: A future measurement has raw timestamped frame samples.
- Fixture preparation: Keep the raw file unchanged for audit.
1. Compute mean throughput from recorded duration.
2. Compute percentile frame times with stated definitions.
3. Identify worst samples and their scene context.
- Expected presentation: Hitches remain visible in the analysis rather than hidden by mean FPS.
- Expected state: Any incomplete route is reported as incomplete.
- Negative control: Reject constant-sixty guarantees from a high average.
- Evidence to collect: Collect analysis script version and raw sample checksum.
- Proposed record location: `Logs/release-qa/REL-062/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Preserve raw data and remove only temporary analysis copies.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Check units and percentile convention before comparing runs.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-063 — Geometry-count provenance
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Docs/TEST_REPORT.md](../Docs/TEST_REPORT.md).
- Preconditions: Saved static-city and PhotoTool totals are available.
- Fixture preparation: Record which scene and revision each report describes.
1. Read static generated geometry totals.
2. Read render-audit totals including gameplay meshes.
3. Compare scopes before calculating reductions.
- Expected presentation: The report explains differing triangle totals by included objects.
- Expected state: No runtime character mesh is silently omitted from an audit claim.
- Negative control: Reject mixing historical and release counts in one percentage.
- Evidence to collect: Collect report paths, revisions and explicit numerator/denominator.
- Proposed record location: `Logs/release-qa/REL-063/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Leave generated scenes and geometry data untouched.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect measurement scope before treating totals as contradictory.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-064 — Screenshot capture state restoration
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Tests/PlayMode/SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs).
- Preconditions: A disposable UI test fixture is running.
- Fixture preparation: Record main camera and root Canvas modes before capture.
1. Plan a call to the existing Capture helper.
2. Inspect its explicit-render output.
3. Compare camera and Canvas states after helper completion.
- Expected presentation: The image contains actual runtime UI over the intended scene.
- Expected state: RenderTexture, aspect, masks and canvas modes restore afterward.
- Negative control: Reject the capture helper changing later gameplay framing.
- Evidence to collect: Collect original PNG plus before/after render-state snapshots.
- Proposed record location: `Logs/release-qa/REL-064/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Dispose of the fixture through normal scene teardown.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Review Capture finally restoration before changing presentation code.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-065 — Screenshot provenance ledger
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Tests/PlayMode/SettingsUITests.cs](../Assets/Parasite/Tests/PlayMode/SettingsUITests.cs).
- Preconditions: A set of proposed release images has source files.
- Fixture preparation: Keep originals separate from guide copies.
1. Record source log, revision and capture method per image.
2. Record whether state was naturally reached or staged.
3. Verify dimensions and any permitted transformations.
- Expected presentation: Captions identify actual Unity renders versus staged UI fixtures.
- Expected state: A screenshot never implies a manual full campaign playthrough.
- Negative control: Reject invented filenames or claims for uncaptured scenes.
- Evidence to collect: Collect an image-to-source ledger and file checksums.
- Proposed record location: `Logs/release-qa/REL-065/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Retain originals and discard only temporary viewing copies.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Trace every guide image to its actual source artifact.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-066 — Linux package required inputs
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Tools/package_build.sh](../Tools/package_build.sh).
- Preconditions: An already-built Linux player exists in Builds/Linux.
- Fixture preparation: Use a separate audit directory without rebuilding.
1. Check executable, UnityPlayer.so and globalgamemanagers.
2. Check each mandatory documentation and licence input.
3. Read package script prerequisites before running a later release.
- Expected presentation: The intended package contains a complete Unity player layout.
- Expected state: Missing or empty mandatory inputs should block packaging.
- Negative control: Reject shipping the executable alone without its data directory.
- Evidence to collect: Collect a file manifest with sizes and required-input checklist.
- Proposed record location: `Logs/release-qa/REL-066/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Remove the audit copy without touching the real build.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect package preflight checks before blaming archive compression.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-067 — Linux launcher portability
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Tools/package_build.sh](../Tools/package_build.sh).
- Preconditions: A package has been extracted into a disposable directory.
- Fixture preparation: Preserve executable and library layout exactly.
1. Inspect packaged play.sh before execution.
2. Plan launch from a different working directory.
3. Confirm launcher forwards explicit command-line arguments.
- Expected presentation: The launcher resolves its own directory and starts the player.
- Expected state: The package does not depend on the development project path.
- Negative control: Reject requiring the Unity Editor for ordinary packaged play.
- Evidence to collect: Collect launcher text, permission bits and future launch log.
- Proposed record location: `Logs/release-qa/REL-067/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Remove the extracted audit directory after review.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Distinguish packaged launcher from the workstation root launcher.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-068 — Archive integrity verification
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Tools/package_build.sh](../Tools/package_build.sh).
- Preconditions: A final Linux tar.gz exists as a release candidate.
- Fixture preparation: Record archive size and checksum before inspection.
1. Plan gzip integrity verification.
2. List archive entries without modifying contents.
3. Check every entry stays within the intended Linux folder.
- Expected presentation: The archive is readable and contains expected player subtrees.
- Expected state: No debug-only backup directory is distributed.
- Negative control: Reject a successful compression command as sole integrity proof.
- Evidence to collect: Collect integrity output and complete archive-entry list.
- Proposed record location: `Logs/release-qa/REL-068/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Delete only temporary listing files after preserving evidence.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect exclusion patterns and archive root selection.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-069 — Atomic package replacement
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Tools/package_build.sh](../Tools/package_build.sh).
- Preconditions: An older release archive and new built player exist.
- Fixture preparation: Use an isolated packaging rehearsal location.
1. Inspect temporary archive creation on the same filesystem.
2. Verify integrity precedes the final rename.
3. Simulate failure only in an approved disposable rehearsal.
- Expected presentation: An unsuccessful candidate does not replace the previous release.
- Expected state: Final replacement is atomic after integrity verification.
- Negative control: Reject destructive failure testing against the actual release archive.
- Evidence to collect: Collect rehearsal paths and before/after checksums.
- Proposed record location: `Logs/release-qa/REL-069/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Remove rehearsal artifacts and preserve real releases.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Review temp directory placement and mv ordering.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-070 — Guide and screenshot portability
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Tools/package_build.sh](../Tools/package_build.sh).
- Preconditions: A packaged handbook references guide images.
- Fixture preparation: Use a fresh extracted candidate with no repository mount.
1. Open COMPLETE_GAME_GUIDE.md locally.
2. Follow every relative screenshot reference.
3. Follow included player-document references.
- Expected presentation: Images and documentation resolve inside the extracted player folder.
- Expected state: Guide readability does not require the original Logs directory.
- Negative control: Reject repository-only paths presented as packaged assets.
- Evidence to collect: Collect broken-link results and extracted folder inventory.
- Proposed record location: `Logs/release-qa/REL-070/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Remove the disposable extraction after review.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Compare copy destinations with relative links in the guide.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-071 — Licence-text inclusion
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Docs/THIRD_PARTY_ASSETS.md](../Docs/THIRD_PARTY_ASSETS.md).
- Preconditions: A release candidate includes third-party visual and audio assets.
- Fixture preparation: Use the recorded attribution list as the audit starting point.
1. Locate Rajdhani OFL and Kenney licence texts.
2. Compare attribution entries with included asset families.
3. Inspect archive licence paths rather than only repository files.
- Expected presentation: Required licence texts are accessible to package recipients.
- Expected state: Asset attribution remains linked from player documentation.
- Negative control: Reject general CC0 wording replacing a required font licence.
- Evidence to collect: Collect licence file names, sizes and source attribution entries.
- Proposed record location: `Logs/release-qa/REL-071/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Keep licences unchanged and discard temporary audit copies.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Compare package copy rules with THIRD_PARTY_ASSETS.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-072 — External asset attribution accuracy
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Docs/THIRD_PARTY_ASSETS.md](../Docs/THIRD_PARTY_ASSETS.md).
- Preconditions: The source attribution document is available.
- Fixture preparation: Audit existing records without downloading replacements.
1. Compare provider, author and source URL fields.
2. Distinguish Poly Haven, ambientCG and Kenney assets.
3. Note separate Unity-package attribution where recorded.
- Expected presentation: Documentation distinguishes synthesized effects from external assets.
- Expected state: No unverified universal licence claim is introduced.
- Negative control: Reject assigning one provider's licence to unrelated files.
- Evidence to collect: Collect an asset-family-to-record mapping with unresolved entries flagged.
- Proposed record location: `Logs/release-qa/REL-072/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Leave assets and attribution documents untouched.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Resolve evidence gaps from existing records before asserting compliance.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-073 — Build-log scope and identity
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Docs/TEST_REPORT.md](../Docs/TEST_REPORT.md).
- Preconditions: A completed Linux build log and source revision are available.
- Fixture preparation: Keep source and generated-city revisions separate.
1. Read the actual completion result.
2. Record elapsed build time and output size.
3. Verify error and warning counts from the same run.
- Expected presentation: Release notes name the precise build they describe.
- Expected state: Cached build duration is not presented as clean-build performance.
- Negative control: Reject combining one run's success with another run's timing.
- Evidence to collect: Collect revision IDs, log path and output manifest.
- Proposed record location: `Logs/release-qa/REL-073/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Preserve logs and avoid rerunning builds solely for this review.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Check timestamps and source identity before publishing build claims.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-074 — Acceptance-result reconciliation
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Docs/TEST_REPORT.md](../Docs/TEST_REPORT.md).
- Preconditions: Existing Editor and standalone reports are available.
- Fixture preparation: Treat each report as evidence for its own environment.
1. Read pass counts from complete summaries.
2. Identify focused reruns and earlier failed cases.
3. Map each claim to the corresponding log.
- Expected presentation: An earlier failed XML remains documented alongside later passes.
- Expected state: No future designed case adds to executed acceptance totals.
- Negative control: Reject replacing failed history with an inflated aggregate count.
- Evidence to collect: Collect a claim-to-log table with environment and revision.
- Proposed record location: `Logs/release-qa/REL-074/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Keep original logs immutable during reconciliation.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Differentiate scenario count, test-method count and rerun count.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.

### REL-075 — Presentation chord semantics
- **Status: Designed — not run.**
- Execution scope: this appendix specifies a later check; generating this record performs none of its actions.
- Source anchor: [Assets/Parasite/Scripts/Core/GameInput.cs](../Assets/Parasite/Scripts/Core/GameInput.cs).
- Preconditions: The real player is in ordinary gameplay.
- Fixture preparation: Use an isolated fixture with no active story dialogue.
1. Press M without Ctrl.
2. Then test left Ctrl plus M and right Ctrl plus M separately.
3. Observe the real final escape and choice behavior.
- Expected presentation: M alone does nothing and either Ctrl chord requests presentation escape.
- Expected state: The shortcut never selects Peace or Dominion automatically.
- Negative control: Reject treating shortcut progression as campaign-completion evidence.
- Evidence to collect: Collect key events, game state and outcome at the lake.
- Proposed record location: `Logs/release-qa/REL-075/`; create it only during an actual later execution.
- Evidence metadata: include source revision, player or Editor identity, operating system, graphics API and exact reproduction inputs.
- Timing record: label wall-clock observations separately from scaled game time and accelerated test playback.
- Cleanup: Restart between chord variants and release all keys.
- Cleanup verification: confirm the next fixture starts independently; disclose any preferences, input devices or temporary resources that could not be restored.
- Failure triage: Inspect GameInput chord detection and mission-state guards.
- Result discipline: preserve the observed failure or uncertainty; do not mark this designed case passed from a different scenario.
- Review criterion: compare the collected artifact with both expected fields and the cited source before assigning a later execution result.


## Local Ctrl M hotfix verification for version 0.2.1

This section records a local, uncommitted hotfix. The user requested that this fix not be pushed.
All earlier report content, including the designed procedure catalogue, remains unchanged above.
The prior release results describe version 0.2.0; the checks below identify what was rerun for 0.2.1.

The old handler required M's press frame while Ctrl was already held and silently rejected a request during possession transit.
The local handler uses an unordered named Input System action, buffers quick presses and retries a gameplay request for up to two seconds.
A received request now produces a visible toast and a `[PresentationShortcut]` log entry; ending choices remain explicit.

| Local hotfix check | Observed result | Evidence |
|---|---|---|
| CPU compilation | 66 runtime and 11 editor files pass | `Tools/compile_check.sh all` |
| Focused PlayMode | 2/2 cases pass: eight key-order/Ctrl-side/free-or-host combinations plus the existing drain-to-lake explicit-choice test | `Logs/ctrlm-fix-results.xml` |
| Linux rebuild | Succeeded, 425 MB, 12.20 seconds, zero errors and warnings | `Logs/ctrlm-fix-linux.log` |
| Built-player shortcut input | 10/10 pass, process exit 0: both Ctrl keys, both orders, free/possessed forms and same-update down/up fast taps | `Logs/ctrlm-fix-standalone.txt`, `Logs/ctrlm-local-verification.json` |
| Build identity | Player data contains version 0.2.1 | `Builds/Linux/Parasite_Data/globalgamemanagers` |

The ten standalone cases inject queued KeyboardState events through the actual built player's named input action with GameInput.Simulated false.
They verify key recognition, protected drain entry, safe host release and no automatic outcome; they do not finish the lakeside choice.
The focused Editor end-to-end case checks the later explicit choice. No fresh full thirteen-scenario campaign benchmark is claimed for this hotfix.
Native X11/Xvfb synthetic input could not reliably advance the title, so a physical desktop keypress pass is not claimed.
The exact user's native-input failure was not directly reproduced; the two source-proven missed-input conditions are addressed and regression-checked.
The two-second possession-transit retry is source-reviewed; it was not isolated as a separate executed transit-flight case.


## Local global Ctrl M verification for version 0.2.2

This is a local, uncommitted and unpushed fix. The user explicitly prohibited Git pushes.
All earlier report content remains intact. This section supersedes the v0.2.1 gameplay-only availability and two-second retry description;
the historical designed procedures above remain proposed tests, not additional executed passes.

The earlier listener lived on the gameplay manager, so it was unavailable at the initial title and ignored opening/pause/dialogue states.
The persistent PresentationShortcut now enables input before the first scene, retains requests across scene loads and resets,
and waits for a stable active body instead of expiring a request after two seconds. Title fade and opening/dialogue coroutines
are cancelled safely before starting the protected drain escape. Repeated presses preserve an existing finale and never choose a future.

| Local v0.2.2 check | Observed result | Evidence |
|---|---|---|
| CPU compilation | 67 runtime and 11 editor files pass | `Tools/compile_check.sh all` |
| Focused PlayMode | 8/8 test cases passed, zero failures | `Logs/ctrlm-global-results.xml`, `Logs/ctrlm-global.log` |
| Global phase matrix within PlayMode | Initial title, title controls/settings, Begin fade, opening, opening finish, pause, pause settings/controls, Vale dialogue and pending checkpoint reset all passed | `CtrlMWorksFromEveryPresentationPhase` |
| Other focused coverage | Both opening tests, eight Ctrl-side/key-order/free-or-host combinations, explicit drain-to-lake choice, settings/menu and journal/lakeside regressions passed | `Logs/ctrlm-global-results.xml` |
| Linux rebuild | Succeeded, 425 MB, 23.54 seconds, zero errors/warnings | `Logs/ctrlm-global-linux.log` |
| Actual built-player shortcut checks | 14/14 passed and process exited 0 | `Logs/ctrlm-global-standalone.txt`, `Logs/ctrlm-global-standalone-player.log` |
| Build identity | Built player data contains version 0.2.2 | `Builds/Linux/Parasite_Data/globalgamemanagers` |
| Recorded run state | Editor tests, build and standalone process all exit 0 | `Logs/ctrlm-global-verification.json` |

The fourteen standalone cases cover initial title, opening, paused settings, Vale dialogue, eight Ctrl-side/key-order/free-or-host
combinations and two same-update down/up fast taps. They queue KeyboardState events through the real built player's Input System
with GameInput.Simulated false. They check the protected escape, safe host release, no stale scene/dialogue reset and no automatic
ending selection. The focused Editor end-to-end test also exercises the later explicit lakeside choice.

This run does not claim native OS/physical keyboard validation, a new thirteen-scenario campaign run, a performance benchmark,
or a separate live possession-flight test. Request retention through transit is source-reviewed; the matrix directly exercises reset
and title-to-city transitions. The earlier v0.2.0 full campaign results remain historical. An already-running old process must be
closed and relaunched; verify v0.2.2 on the title screen before trying Ctrl+M. No code was committed or pushed.


## Major Upgrade verification for version 0.3.0 (14-Mission Campaign, Skinned 3D Characters, Authored Animation, Audio & Visuals)

This section documents the comprehensive verification of the major 0.3.0 upgrade.

### Key Additions & Systems Delivered
1. **14-Mission Extended Campaign:** Expanded from 8 to 14 missions, introducing:
   - Mission 3 (THE TAIL): dynamic stalking mechanic following Dr. Hana Petrov with distance bands and glance timing.
   - Mission 7 (HUNTER PROTOCOL): Protocol Hound bio-scanners, detection cones, and amoeba-only scanner archway bypass.
   - Missions 10-14 (THE SHADOW UPLINK, THE BLACK SITE ARCHIVE, CONTAINMENT PROTOCOL EPSILON, BLACKOUT, THE FINAL CONVERGENCE): subterranean black site infiltration, dual-host console operations, 3-stage district blackout grid puzzle, and final plaza broadcast escape.
2. **Skinned 3D Characters:** 20+ distinct human character models (Civilians, Guards, Scientists, Maintenance, Dr. Petrov, Dr. Vale, Lakeside Girl) generated via MPFB / MakeHuman Community replacing legacy blocky rigs, featuring procedural cloth palettes and dynamic skin/eye glow shaders.
3. **Authored Locomotion & Animation Pipeline:** Full humanoid Animator Controller (`HumanLocomotion.controller`) driving locomotion blends, state transitions (Normal, Possessed, Unconscious, Captured), and authored poses (Sit, Talk, Console, Panic, Alert, Point) using retargeted CMU mocap and Quaternius Universal Animation Libraries.
4. **Complete Audio Overhaul:** Full voice acting for story dialogue and pedestrian reactions, synthesized dynamic ambient city soundscapes, adaptive music tracks, and high-fidelity sound effects with persistent preferences.
5. **Next-Gen Visuals & Shaders:** 4K Belfast Sunset HDR skybox, procedural trees with 3 bark materials and leaf-cluster card atlases, URP custom shaders (Atmosphere, Foliage, Facade, Light Cones, Slime Trail), screen-space wet ground reflections, and bloom/lens dirt effects.
6. **Engine & Tooling Fixes:** Corrected `HumanoidRig` bone transform updating during off-screen evaluation / batchmode test runs via explicit `AlwaysAnimate` culling mode, and stabilized `PhotoTool` RenderTexture persistence across scene switches.

### Verification Matrix

| Verification suite | Observed result | Evidence |
|---|---|---|
| Roslyn CPU compile | OK, 99 runtime and 18 editor files pass with zero errors | `Tools/compile_check.sh all` |
| EditMode Rules Suite | 46/46 passed, 0 failures (TailRules, BioScanRules, BlackoutRules, AccessRules, AudioPreferences) | `Logs/editmode-results.xml` |
| SettingsUI PlayMode | 6/6 passed, 0 failures (Lakeside seated pose, settings persistence, journal scroll, explicit endings) | `Logs/ui-test-results.xml` |
| Opening Cinematic PlayMode | 2/2 passed, 0 failures (all 11 shots, unscaled timeline, keyboard skip bindings, clean handoff) | `Logs/audio-playmode-focus-results.xml` |
| Campaign Acceptance Scenarios | 16/16 passed, 0 failures across all 14 missions, including Route A (Main Gate), Route B (Staff Terminal), Route C (Service Tunnel), The Tail, Hunter Protocol, and Blackout | `Logs/core-playmode-results.xml`, `Logs/routes-playmode-results.xml`, `Logs/routes-bc-results.xml` |
| PhotoTool Environment Captures | 18/18 batch captures generated cleanly with zero missing scripts or exceptions | `Logs/photos.log`, `Logs/photos/*.png` |
| Character Photo Lineup | All character lineup and individual portrait captures generated cleanly | `Logs/chars-photos2.log`, `Logs/characters/*.png` |
| Animation State Verification | 29/29 expected animation states and transitions verified on reference skeleton | `Logs/animation/checks.txt` |
| Linux Standalone Rebuild | Succeeded, 933 MB player, zero compilation errors | `Logs/build-linux.log` |
| Standalone Player Shortcut Test | 14/14 passed, process exit 0 (persistent Ctrl+M from title, opening, pause, dialogue, and gameplay) | `Logs/upgrade-shortcut.txt` |

