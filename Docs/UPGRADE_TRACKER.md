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

Twenty missions are implemented: original14, branch-sensitive riverside15–17 and island18–20. Explicit Peace/Dominion and
Continue gates remain. The full80s opening and four lake films, eight island voices, authored quarantine-wall kit, centred
free-body camera and reflection/movie ownership fixes are integrated. Full frame/decode provenance and named Editor/player
checks are recorded in [TEST_REPORT.md](TEST_REPORT.md); these do not establish that the full open-ended request is complete.

The earlier pre-gate **0.5.1-preview graphics candidate** is a1,101MB working-tree build over`bbebc8c`, runtime`80e65c6b...`, at
`Builds/Candidates/full-story-20261125T170047223995Z-63921b41`. Strictfull-five media(8checks/7images), queuedshortcuts14/14 and
renderedwater22 each pass on RTX4050/Vulkan with nativeexit0 and zero ring warnings. Source/preview hashes are unchanged.
[Candidate proof](verification/0.5.1-graphics-candidate-20261125.json) separates those narrow checks from native Custom editing,
frame-pacing, continuous80s story observation and a complete human playthrough. Native 23/24 now pass 2/2 with exit 0 on
this runtime, in 832.873 seconds, including both rescue branches and twelve recovery helpers.
[Exact rescue proof](verification/0.5.1-native-rescue-20261125.json) distinguishes prepared Null/simulated logic from graphics
and human testing. The other native run finished 19/21 with exit 1 in 947.161 seconds. Case 08 exposed a moving-target test-driver
failure; case 21 needs a real GPU for its camera subcheck and cannot pass under that Null invocation.
[Original/riverside proof](verification/0.5.1-native-original-river-20261125.json) preserves both failures. A later focused live-host
aim test passes; full corrected Route 08 and rendered 21 remain required.

Independent settings now have real Display/Quality/Effects controls, staged Apply/Cancel/Defaults, Custom persistence and live
Audio/Input. Existing profile quality is preserved.135EditMode rules pass. GPU cases verify actual Bloom pixels, owned renderer
cleanup/source-asset preservation, late cameras, CustomLow/high reflection images across reload and queued keyboard/gamepad UI.
The remaining Editor Vulkan warning gate is **open**: the no-flag serializedIndividualFaces collection records13pass/1warning
failure/1expectedNullskip/native2 with9raw warnings. Controlled14pass observations with residual warnings are diagnostic controls,
not a production all-green result. Current narrow native gates have none; allocator cause remains unproven. No suppression or
quality cut closes that gap. [Settings proof](verification/independent-graphics-settings-20261125.json).

Probe-only regeneration preserves canonical world geometry, physics/nav/door data, lights, global settings and all11CityVolume
components. Only8probe time-slicing values and17existing NPC default serializations differ.
[Semantic comparison](verification/serialized-probe-regeneration-20261125.json) applies before gate integration.

The user-requested **gate WIP** is now built at`Builds/Candidates/full-story-20261125T173043238618Z-0f595731`,0.5.1-preview,
1,117MB/nativebuild0/zeroerrorswarnings/source+immutablepreview unchanged. Native media8checks/7images,14queuedshortcuts and
renderedwater22 pass native0/zero ring warnings after an absolute-output runner correction. The user's RTX/Vulkan playtest is
launched; it is not a completed campaign or final release. [WIP build proof](verification/0.5.1-gate-playtest-20261125.json).

Gate import21checks and realGPUregeneration pass. Seven saved-contract checks retain the exact329collider/4door/144light
inventory; actual99-ray closed/open/reclosed aperture, original door motion and open navigation pass. The before8capture set
restoresfully. AFTERcapture still fails static matching despite all8camera/settings/rain fields matching and full restoration.
DynamicAmoebaLight variation is a hypothesis, not a proved sole cause: raw light rows were not persisted. Accepted matched
visual comparison, native role access/full routes/performance and final campaign checks remain pending. The identical runtimeDLL
to the earlier pre-gate build does not transfer results across the changed scene/art snapshot.

The earlier0.5.0 candidate was launched for the user and exited normally0 after130.601s. Its interrupted headless run records
01–05pass,06duty-officer aim failure and no final receipt after reboot. The yaw/pitch fixture correction passes focused15 and
shared06/07/08/14/16 in the Editor without changing possession rules. Six desktop0.5.0 baseline routes pass; the217.1ms river
High spike remains unexplained. Their FPS numbers do not predict0.5.1 or the pilot. The immutable`Builds/Linux`0.5.0-preview
(M18/four lake films) and retained0.4.2archive remain older separate artifacts.

## Required next verification

- The Blender security-booth package and unused Editor helpers now pass CPU round-trip and eleven actual Unity import checks:
  one 6,012-triangle mesh, reused gate atlas, no new textures or colliders. Scene integration, exact world comparison and native
  appearance remain pending. [Booth proof](verification/city-security-booth-20261125.json).
- Updated gate-capture schema 2 passes CPU compilation and retains raw semantic light/camera inputs and restoration checks.
  Fresh BEFORE/AFTER renders are required; the historical mismatch remains open. [Fixture proof](verification/gate-capture-fixture-20261125.json).
- Verify the corrected full Route 08 and rerun camera-dependent case 21 on a real GPU. The original run is terminal 19/21,
  exit 1, with both failures retained. Scenarios 23/24 already
  pass and rendered 22 passes separately. Keep prepared Null/simulated logic separate from graphics and physical input.
- Characterize the Editor warning without suppressing its guard; exercise native Custom controls/persistence/frame pacing and
  story/audio/settings behavior on the new binary.
- Finish gate-pilot integration and actual collision/lighting/motion/visual/performance checks before including it in a new build.
- Measure the final binary at declared settings, listen to voices, exercise physical keyboard/gamepad, then verify a fresh archive
  and isolated extracted player. The package shell script modifies the retained preview; use an explicit-candidate path.
- Continue the requested mission/realism expansion. Compilation, completed films and narrow passing suites do not close the goal.

`TEST_REPORT.md` retains artifact-specific history; `KNOWN_ISSUES.md` records limits. Builds, caches and captures remain ignored.
