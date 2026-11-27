# PARASITE expansion and realism upgrade

The current request reopens feature development and authorizes frequent commits and pushes to `Mevinb/gamajam-v2`.
Older handoff statements freezing the eight-mission release, or prohibiting pushes for its capture session, are historical.
The full upgrade remains in progress; a passing compile or a small acceptance suite does not establish that every bug is fixed.

## Current visual scope — 28 November 2026

The user's latest scope is visual realism only: no mission expansion, gameplay tests, PlayMode, benchmarks or player launches.
The historical requirement table below does not authorize those deferred actions. Linux/URP plus offline RTX Blender films remains
the chosen platform. The accepted limestone/curb/Van asset batch now has complete editable sources, portable recipes, actual
comparison/import/CityBuilder/post-source capture evidence and clean helper retirement.
[Current integration evidence](verification/visual-assets-round2-20261128.json) keeps imported-normal equality, stable identities,
collider/marker preservation and static image decisions distinct from final-player acceptance. No hair pilot was promoted.
The five frozen V5 films, full decoding/review, journaled publication/all-eight-film import and a fresh Linux build remain required;
existing strict coach warning/performance findings stay open.

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

## Current build and worktree — 26 November 2026

The earlier **0.7.0-preview** user playtest, a26-mission Linux build from the isolated `codex/northline-runtime` branch, ended normally0 at2026-11-26T06:57:00.686080Z after21711.645s. The user now requests a fresh build and launch;0.7.2preview preparation is in progress, with no new build/launch result claimed yet.
Its runtime SHA is `408112a0899965a49a95c656e34b8161a942135ce8455793efafed49b8040ca5`, source snapshot
`95d9939345f074a855c6f32da39aa6f622b25c4512998b86be038541d5a6a648`, source commit `6e12405`.
That player used RTX4050/Vulkan. The earlier0.5.1session was replaced through a normal window close and exited0.
The0.7candidate remains retained. Main runtime code remains0.6.1; the0.7feature branch is pushed and awaits further acceptance.
[Build/API proof](verification/0.7.0-candidate-20261126.json), [native basic5/5](verification/0.7.0-native-basic-20261126.json),
[visible launch](verification/0.7.0-user-playtest-20261126.json).

Northline adds missions24–26 after the earned harbor summary: an interlocked physical tram crossing, branch-specific sealed
freight routing, a two-body crank/duct signal and actual raised-coach boarding/departure. Eleven Blender models are imported
and bound to their moving parents. The existing80s opening and four lake films remain included. The rail kit is a foundation
for further realism work; it is not final material/detail or measured graphics acceptance.

The earlier full mechanics run passed5/7 and exposed two distinct defects. Actual collider ownership now fixes the departure
control sight check; destroyed-body access fails closed; the wrong-route freight return is moved south of the actual signal wall.
Focused retests prove actual coach/body-loss recovery and the full Peace retry/checkpoint/boarding/departure route; authored
binding tests pass2/2. These results do not turn the older full run into a fresh7/7, or prove native continuous26.
[Binding proof](verification/northline-authored-bindings-green-20261126.json),
[lifetime pass and retained route failure](verification/northline-lifetime-green-return-red-20261126.json),
[Peace recovery pass](verification/northline-peace-recovery-green-20261126.json).

Native prepared92 Peace/recovery passes with native0 and all integrity checks in193.311s.
[Native92 proof](verification/0.7.0-native-northline-peace-20261126.json).[Native93 Dominion](verification/0.7.0-native-northline-dominion-20261126.json) also passes with native0/integrity in106.747s.
On the exact private0.7.1candidate, [earned continuous95 Dominion26](verification/0.7.1-native-continuous-dominion26-20261126.json) now passes native0/supervisor0 in634.37s with full integrity; its within-chapter fixtures remain explicit. EarnedPeace94 remains unrun.
See `Docs/HANDOFF.md` for session and run-state paths. No GPU benchmark, full human playthrough, physical-input,
latest-binary movie/settings or final realism pass is claimed. Preserve any newly launched user playtest while developing further fixes/artwork.
The older graphics runners and0.6snapshots below require explicit retargeting before use with0.7.

The next0.7.1source fixes an actual missing-inactive-body ferry-credit defect and adds a usable pause-menu checkpoint action.
The original wrong-credit red and corrected recovery are preserved in [regression history](verification/body-free-state-regression-20261126.json);
[sharedhelper/default-selection17/17](verification/body-free-shared-editor-green-20261126.json) passes. [Fresh0.7.1build](verification/0.7.1-candidate-20261126.json) passes0/1119MB/zero BuildReport errors/warnings.
[Native17/18](verification/0.7.1-native-riverside-green-20261126.json) pass separately with final integrity.
[Original strict96 Null red](verification/0.7.1-body-free-native-red-20261126.json) remains retained: an HDR reflection-probe warning followed the shared functional calls. [Supported RTX4050/Vulkan96](verification/0.7.1-body-free-native-vulkan-green-20261126.json) now passes native0/supervisor0 in12.059s, with no unexpected logs, HDR warnings or ring-buffer warnings and all integrity checks passing. No warning/quality guard was weakened. The distinct earlier Editor Vulkan warning gate remains open. Continuous95 is terminal/pass;94 remains unrun.

A separately versioned detailed coach package is authored and CPU-round-trip verified:11144coach/15972kit triangles,10real glass apertures,
198closed components and three original2K PBR maps. [Source proof](verification/northline-coach-source-v2-20261126.json).
A newer uncommitted WORK revision is now integrated in Assets/runtime with two owned URP materials, three dedicated2048 A/N/M maps and32 precise seat-part colliders for eight assemblies. WORK `Logs/coach-import32-20261126a.json` passes67247structure/data checks; material tests pass7/7 and `Logs/coach-fitting32-20261126c.xml` passes1/1 physical blocking/hop/landing/aisle/boarding checks in14.4128031s. The expanded root-owned RTX/Vulkan smoke is in progress, with no result claimed here. Declared rendered appearance/performance and remaining binding/chapter checks remain scoped separately; this coach is not in either retained0.7or private0.7.1binary.

## Historical source and delivery boundary — 25 November 2026

The new 0.6.0-preview working tree adds a three-mission Working Harbor chapter after the completed island rescue, for 23
missions. Maintenance balances a physical pressure lock; a scientist routes branch-specific moving cargo into actual bays;
two separate free amoebae complete a continuously validated relay. Nine Blender model inputs pass267Null import checks.
Continuation, pressure recovery, in-flight cargo recovery and arrival cancellation have scoped tests; final Peace/Dominion
source branches pass2/2 in272.328seconds with Editor exit0. The0.6native candidate now passes selected08/25/26/27,4/4,
and the portable archive's fresh extracted player passes01–05,5/5; both are isolated Null-graphics logic checks.
[Native proof](verification/0.6.0-native-selected-20261126.json), [package proof](verification/0.6.0-package-20261126.json).
The remaining sixteen logic cases now also pass with native exit 0. Across the three named invocations, 25 distinct cases
pass on this exact candidate; rendered 21/22 remain. The original supervisor's numeric-order failure is preserved beside a
separate read-only verifier's exit 0 and 21 passing checks. [Remaining-native proof](verification/0.6.0-native-remaining-20261126.json).
Rendered appearance, physical controls and performance remain pending. The running user0.5.1
player still has20missions. [Harbor implementation/evidence](HARBOR_EXPANSION.md).

The first twenty missions comprise original14, branch-sensitive riverside15–17 and island18–20. Explicit Peace/Dominion and
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
[Original/riverside proof](verification/0.5.1-native-original-river-20261125.json) preserves both failures. Later live-host aiming
and ordinary physical spacing pass the full Route 08 in the Null Editor; post-hint focused cases pass 2/2. Source 0.5.2 is
superseded by the0.6candidate, whose corrected native Route08 passes. Rendered21 remains required.
[Moving-host proof](verification/moving-host-aim-20261125.json).

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

## Earlier staged verification — retarget before use

- The Blender security-booth package and unused Editor helpers now pass CPU round-trip and eleven actual Unity import checks:
  one 6,012-triangle mesh, reused gate atlas, no new textures or colliders. Scene integration, exact world comparison and native
  appearance remain pending. [Booth proof](verification/city-security-booth-20261125.json).
- Updated gate-capture schema 2 passes CPU compilation and retains raw semantic light/camera inputs and restoration checks.
  Fresh BEFORE/AFTER renders are required; the historical mismatch remains open. [Fixture proof](verification/gate-capture-fixture-20261125.json).
- Use the existing0.6candidate for rendered21, exact-player media/water/settings and isolated performance checks. Its native
  25 distinct logic cases already pass across the three named runs; do not repeat them because older notes call them pending. Keep prepared Null/simulated logic
  separate from graphics and physical input. Historical failures remain in their original artifact-specific reports.
- Characterize the Editor warning without suppressing its guard; exercise native Custom controls/persistence/frame pacing and
  story/audio/settings behavior on the new binary.
- Finish the gate's matched visual comparison, native role access and performance checks; the integrated art is already in the
  existing0.5.1user player and0.6candidate, with saved physics/light inventory and actual door motion/navigation checks passing.
- Measure the final binary at declared settings, listen to voices and exercise physical keyboard/gamepad. The0.6portable
  archive and isolated extracted basic player are already verified. The package shell script modifies the retained preview;
  use an explicit-candidate path for future packages.
- Continue the requested mission/realism expansion. Compilation, completed films and narrow passing suites do not close the goal.

`TEST_REPORT.md` retains artifact-specific history; `KNOWN_ISSUES.md` records limits. Builds, caches and captures remain ignored.

Latest delivery: [0.7.2-preview build and RTX/Vulkan desktop launch](verification/0.7.2-preview-build-and-launch-20261126.json). Zero build errors/warnings; native basic5/5. New detailed coach included. Preserve PID289391 desktop service user test. Expanded Editor9/11 retains two known ring-buffer warning failures; full goal remains active.

## 0.7.3 follow-up

[Build/native save proof](verification/0.7.3-candidate-and-native-save-20261126.json):1135MB, zero build errors/warnings, actual LinuxMono180 API and native basic5/5 pass. [Campaign disk checkpoints](CAMPAIGN_SAVES.md) now have storage24/24, menu5/5 and actual private native quit/relaunch/resume with exits0/0. [Northline signs](verification/northline-world-labels-20261126.json) pass ownership2/2 and paired GPU visibility1/1; coach images are corrected, but strict coach tests retain Editor ring-buffer warning failures. The0.7.2user session ended normally0 after821.103s. Current campaign remains26; Exchange27–29 is a pushed design blueprint, not runtime content. Full expansion/realism goal remains active.
