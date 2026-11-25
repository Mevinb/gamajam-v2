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

The new **0.5.1-preview graphics candidate** is a1,101MB working-tree build over`bbebc8c`, runtime`80e65c6b...`, at
`Builds/Candidates/full-story-20261125T170047223995Z-63921b41`. Strictfull-five media(8checks/7images), queuedshortcuts14/14 and
renderedwater22 each pass on RTX4050/Vulkan with nativeexit0 and zero ring warnings. Source/preview hashes are unchanged.
[Candidate proof](verification/0.5.1-graphics-candidate-20261125.json) separates those narrow checks from native Custom editing,
frame-pacing, continuous80s story observation and a complete human playthrough. Native23+24 is currentlyrunning nice10/Null
under`Logs/graphics-native-rescue`; its final receipt is still required. Other current-player campaign checks remain open.

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

The **gate realism pilot is an applied source/import checkpoint**, outside that native candidate. Builder hooks and3editor helpers
compile148/26/148. Actual Vulkan import passes21checks:2meshes/3,808triangles/3true2D2KBC7 maps/sharedPBR material; existing18
island importer/meta hashes remain unchanged. The baseline has329colliders/4doors/144lights. Gate scene regeneration, exact
physics/light comparison, motion/access/LOS/nav, matched before/after art review, performance and a new native build are pending.
Import readiness alone does not establish an integrated visual upgrade.

The earlier0.5.0 candidate was launched for the user and exited normally0 after130.601s. Its interrupted headless run records
01–05pass,06duty-officer aim failure and no final receipt after reboot. The yaw/pitch fixture correction passes focused15 and
shared06/07/08/14/16 in the Editor without changing possession rules. Six desktop0.5.0 baseline routes pass; the217.1ms river
High spike remains unexplained. Their FPS numbers do not predict0.5.1 or the pilot. The immutable`Builds/Linux`0.5.0-preview
(M18/four lake films) and retained0.4.2archive remain older separate artifacts.

## Required next verification

- Observe native23+24 to its real final result and complete remaining current-player campaign scenarios. Keep Null/simulated
  logic separate from rendered22 and physical input.
- Characterize the Editor warning without suppressing its guard; exercise native Custom controls/persistence/frame pacing and
  story/audio/settings behavior on the new binary.
- Finish gate-pilot integration and actual collision/lighting/motion/visual/performance checks before including it in a new build.
- Measure the final binary at declared settings, listen to voices, exercise physical keyboard/gamepad, then verify a fresh archive
  and isolated extracted player. The package shell script modifies the retained preview; use an explicit-candidate path.
- Continue the requested mission/realism expansion. Compilation, completed films and narrow passing suites do not close the goal.

`TEST_REPORT.md` retains artifact-specific history; `KNOWN_ISSUES.md` records limits. Builds, caches and captures remain ignored.
