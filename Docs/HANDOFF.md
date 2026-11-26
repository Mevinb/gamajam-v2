# PARASITE — developer handoff

Read this before changing the project. The active request is a full realism/cinematic upgrade and continued mission expansion,
with frequent commits and pushes as **Mevinb**. This supersedes the historical scope freeze and local-only capture session.
The full request remains **in progress**. Evidence applies to its named source or binary; do not transfer an older pass to a new build.

## Current checkpoint — 26 November 2026

The full realism/bug-fix/continued-expansion goal remains **active**. Linux/URP plus offline RTX Blender films and real-time shader improvements is the chosen direction. Gameplay hardware ray tracing is not implemented.

- **LATEST PRIVATE CANDIDATE0.7.3 PASSES BUILD + INITIAL NATIVE SAVE/RESUME.**
  [Exact proof](verification/0.7.3-candidate-and-native-save-20261126.json). WORK `Builds/Candidates/full-story-20261126T141830932558Z-ad668a55`;
  source commit `cf8b57e9c89b84919fa11e17f77baf78d202aefb`, snapshot `4badf2f4a57c832277743e902a6e4cb1d1291635a7f05b9d4188d3a13aff1634`.
  Runtime `34b4156eb0ab4585f0d73c5fd589b4215c2d559663e9b03550d15fe1e56258f1`, BUILD_INFO `1ba07ec5e5ced7f1c0ca12d803e66e6780e6b3dc878e6192f7fb96ea2cb07020`.
  Build1135MB/native0/zero errors+warnings. Source and all older protected artifacts unchanged. Actual post-build runtime180/Editor33/LinuxMono180 API checks pass.
  Native basic01–05 passes5/5/native0/29.665s, with explicit Null/simulated scope. Build runner root13151/Editor13170 are terminal.
- **Campaign saving implemented:** versioned/checksummed atomic `campaign-v1.json` with last-valid backup; corrupt-current recovery;
  a future primary blocks both fallback and overwrite. Only explicit New/Resume/Restart activates the session, with default Editor/automation opt-out.
  Coalesced captures follow the full flag/mission stack; direct removals and periodic stats persist; protected endings keep the last stable save.
  Normal checkpoint reconstruction makes one fresh free body; live host/controller/hold/transit references are not saved. Earned summaries restore actual Continue UI.
  [Design and evidence](CAMPAIGN_SAVES.md), [regression history](verification/campaign-persistence-20261126.json).
  Final storage24/24 EditMode and actual menu/scene5/5 PlayMode pass with Editor0. Initial invalid-name fixture/licensing refusal are retained.
- **Production native save/resume also passes:** WORK `Logs/native-save-ui-073/20261126c/execution.json`,80.414s,
  two independent normal players exit0/0 on RTX4050/Vulkan; no AutoTest flags or RunState mutation. Private Xvfb/XTest keys,
  isolated profile, menu/gameplay screenshots inspected. Initial Storm Drain checkpoint/time2.38928175→2.41167998 survives process quit/relaunch.
  Opening image inspected then explicitly skipped; Resume goes directly to gameplay. Both logs have zero ring/HDR warnings and null-reference exceptions.
  Runtime/BUILD_INFO unchanged; no cancellation or owned-stop signal in this passing run. This proves initial checkpoint/time across processes,
  not an entire native26-mission save/resume, physical-human input, full80s playback or voice listening. Earlier cold-window/focus harness failures remain.
- **Northline sign rendering fixed in source:** five owned font-atlas materials use existing WorldText depth/culling; atlas rebuild, disable/destroy and renderer handoff are handled.
  The first three signs face their south approach; the last two keep their north-facing side. [Proof](verification/northline-world-labels-20261126.json).
  Ownership2/2 and matched-pair GPU pixel test1/1 pass:14043front/0back/0occluded glyph pixels. Production atmosphere stays enabled.
  Actual coach exterior/aisle PNGs no longer show mirrored glyphs through the coach. The original brightness-metric false positive is retained with quantified cause.
  **Both fresh strict coach tests remain red on Editor Graphics Ring Buffer warnings**, despite completed physical/glass assertions. No suppression/quality cut closes that gate.
- **User playtest0.7.2 ended normally:** WORK `Logs/user-playtest-full-story-20261126T073327996101Z-8c413e3e-desktop/session.json`,
  PID289391 exited0 after821.103s at07:52:54.797181UTC. Root did not close/restart or inject input into that user session.
  Its candidate/runtime998430… remains unchanged. A machine reboot occurred later; the first073dry probe had no receipt and is retained empty;
  the fresh read-only retry and actual completed build have final receipts. No user player was reopened. Recheck real processes before any job.
- **Campaign remains26 playable missions.** WORK `Docs/EXCHANGE_CHAPTER.md` is a concrete27–29 blueprint only;
  traverser logistics, electrical gallery/fuse custody and protected flatbed extraction are not runtime content yet.
  [Next realism pass](https://github.com/Mevinb/gamajam-v2/blob/codex/northline-runtime/Docs/REALISM_NEXT_PASS.md) identifies skin-surface baking and authored lab props.
- Existing named proofs remain scoped:071continuousDominion95 passes634.37s on old071/de2acc; Peace94 remains unrun.
  071supported-renderer96 passes with zero warnings; its original Null HDR failure is preserved. These results do not transfer wholesale to073.
- Main runtime source remains0.6.1/DC5. WORK `codex/northline-runtime` contains the pushed source features; code integration to main still awaits broader native/graphics acceptance.
  Preserve unrelated untracked `Docs/GRAPHICS_UPGRADE_PLAN.md`. All older retained builds/releases and2760completed RTX/OptiX film frames remain; no rerender is needed.
  Root free space was8585519104B after073build. Check fresh space before another candidate/cache job; no retained artifact was deleted in this work.

Next: implement the Exchange continuation with earned entry/physical/custody/recovery acceptance and persistence updates; author the skin/lab realism pilots;
investigate the Editor ring warning without weakening the original log guard; verify latest-binary full media/settings/performance and broader campaign/save branches.
No full human playthrough, final realism, per-game GPU-memory/FPS or all-bugs-fixed claim is made. Older checkpoint detail is retained in
[history](HANDOFF_HISTORY_20261126_073.md) and artifact-specific proofs. Prefer the latest candidate for new acceptance; retarget older staged runners explicitly.

## Artifact and verification boundaries

Source and candidate0.6.0 have twenty-three missions: original14, riverside15–17, island18–20 and harbor21–23, with immutable
explicit Peace/Dominion continuation. The retained historical0.5.1user candidate remains the separate twenty-mission build.
The full five films, eight island voices and Blender quarantine-wall kit are integrated. Film source/frame/full-decode provenance,
normal80s Editor story/voice/skip/handoff, wall import/traversal/raster/camera and reflection ownership proofs remain named in
[TEST_REPORT.md](TEST_REPORT.md), [UPGRADE_TRACKER.md](UPGRADE_TRACKER.md) and their linked records. Normal80s story is Editor
scope; the current player's strict movie gate proves initial images/advancement/terminal-seek cleanup.

The earlier **0.5.0** player `Builds/Candidates/full-story-20261125T143348976819Z-ea740a95`, runtime `2603c572...`, was launched
for the user and exited normally0 after130.601s. Its interrupted23-case headless run records01–05pass and06duty-officer aim
failure before reboot, with no final receipt. The yaw-only fixture missed the unchanged15° cone; corrected yaw/pitch feedback
passes focused15 and shared06/07/08/14/16 in the headless Editor. [Aim proof](verification/hunter-camera-aim-20261125.json).
The old binary remains unchanged; the pre-gate0.5.1player includes the corrected driver. The new gate WIP has its own scene/source snapshot and does not inherit those campaign results.

Six focused desktop performance routes apply to that older0.5.0binary: High/Ultra city169.2/111.7FPS, river212.9/140.5,
island252.2/169.5, all native0/focused/AIactive. RiversideHigh has one217.1ms worst frame with unproven cause. Per-game GPU
memory/timing and dedicated lab coverage are unavailable. These numbers do not predict the new settings/pilot binary.
[Baseline proof](verification/graphics-baseline-0.5.0-20261125.json).

Keep immutable `Builds/Linux` **0.5.0-preview** intact: M18/four full lake films, no M19/M20 or later fixes. Its runtime is
`8ce33854dd55496442655cef1c61ce404b9d5d9dc077d5606c89a28c88a9fabd`; original playtest records remain in `Logs/user-playtest`.
The older0.4.2archive contains two short inserts. Launch a private candidate using its adjacent `play.sh`; the project-root
launcher selects the retained preview. No human launch/playthrough of the new0.5.1graphics candidate is claimed here.

## Required next work

The next graphics jobs are already staged and root-reviewed; do not recreate these runners. Default dry invocations exit 0
and detect both the existing user player and any active Editor without launching a player or writing Assets:

- `Logs/candidate06-gpu-stage/run_gpu.py` (SHA256 `1b80c5979493eec06922bc6816eb9052cdd973d1ee60b9720362b4eac23232f4`)
  plans exact-candidate films, shortcuts, rendered water22, island21 and native settings checks.
- `Logs/candidate06-benchmark-stage/run.py` (SHA256 `98159e5e390c9a66870475ceb086033270410d34ed76b9510a35e936f145d851`)
  plans isolated, focused 1920x1080 Vulkan High/Ultra city/riverside/island runs. The immutable candidate has no harbor benchmark
  route; harbor performance remains unmeasured. CPU-heavy work must also finish before benchmarks.
- `Logs/gate-booth-workflow-stage/gate_compare.py` (SHA256 `aa17b2bbbe9e5ee5cb54a7b2ccffaa8b26c70c141e90725c77ecf486d938cf9f`)
  stages the complete 37-file generated-asset comparison and exact restoration. Its adjacent README and `booth-hook.patch`
  describe the later booth integration. No asset swap, frozen snapshot preparation, regeneration or capture has run.

Both native runners retain the immutable build identity and support an explicit reviewed frozen-source snapshot, recording
every delta from the built source. New test helpers are not claimed to exist in the packaged candidate. Read the adjacent
READMEs before execution; use fresh absolute Logs output paths and never close the user player from a runner.
The reviewed post-recovery snapshot is `Logs/candidate06-gpu-stage/reviewed-source-20261126-physical-recovery.json`, SHA256
`9532d9ba42ae2e0a381f1859d5951513b774a71cdaeb574902d28640802b8763`. Its exact build delta is one modified acceptance
helper plus the new test class/meta, with no removals. It becomes stale if any further source or asset changes.
That9532 snapshot is now historical: current source is eedd after the seven-file helper/diagnostic update and verification build.
Use the new build-run source snapshot (204952/0c8cccab) for any current-source freeze; no artifact inherits another binary's tests.

1. Use the existing verified0.6candidate/archive. All 25 selected Null-logic cases pass across the three named invocations;
   do not repeat them merely because an older note calls them pending. Render-dependent21/22, exact-player media/settings
   and declared GPU benchmarks remain. Keep prepared headless/simulated logic separate from graphics and physical-input proof.
   The remaining16 run and separate metadata reanalysis are both terminal; source9532 is their verified baseline.
   New source changes require a fresh reviewed source snapshot before later graphics jobs.
2. Characterize the remaining Editor Vulkan warning with bounded controls; do not rewrite its original unexpected-log guard
   or call residual-warning controls a complete fix. Exercise native Custom settings/persistence/frame pacing on the new binary.
3. Resolve the gate AFTERstatic capture-contract failure before accepting matched visual comparisons. Saved physics/light inventory
   and actual door/aperture/navigation pass; native role access/full routes, declared performance and final-release acceptance remain separate.
4. Measure the final binary at declared settings and continue the active
   graphics/gameplay expansion. Physical keyboard/gamepad, subjective voice listening and disk campaign saving remain separate.
   The historical0.6portable archive and isolated extracted basic player are already verified. The earlier0.5.1and0.7user sessions have ended; any newly launched user playtest must be preserved.

## Cinematic production and scheduling

All2,760productionframes are complete under `Logs/full-cinematic-renders/full-production-20261125T003711Z`; no render resume
is needed for those completed films. Preserve packed sources, frame hashes and frozen driver/policy. Check real processes before
GPU work. If a future render needs a safe stop, use its STOP and wait for actual Blender/supervisor completion. Headless logic
can coexist with graphics work; performance measurements require isolated declared workloads. Check disk space before imports,
new builds and bakes. `Tools/package_build.sh` modifies the retained `Builds/Linux`; use explicit-candidate packaging instead.

## Environment and commands

- Project: `~/Data/Parasite`; remote `https://github.com/Mevinb/gamajam-v2`, branch `main`.
- Unity 6000.3.29f1: `~/Data/Unity/Editor/6000.3.29f1/Editor/Unity`; URP 17.3, Cinemachine 3, Input System.
- Blender: `~/Data/Blender/current/blender` (not on PATH). Existing pipelines live in `Tools/blender/`.
- Linux x86-64 remains the target. The user selected RTX-rendered Blender cinematics plus improved real-time shaders.
- GPU: RTX 4050 Laptop GPU, 6 GB; RAM 16 GB. Check `df -h .` before large imports/renders. Check live free space before large jobs; build/cache/import usage changes quickly.
- Launch a candidate using its adjacent `play.sh`; the project-root `./play.sh` selects retained `Builds/Linux`. The launcher selects NVIDIA offload and prefers Vulkan on NVIDIA systems.
  Use `PARASITE_GRAPHICS_API=glcore ./play.sh` for OpenGL; explicit Unity API flags take priority.

```bash
Tools/compile_check.sh all
Tools/unity.sh buildscenes -executeMethod Parasite.EditorTools.CityBuilder.BuildAllBatch
Tools/unity.sh editmode -nographics -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml
Tools/unity.sh playmode -runTests -testPlatform PlayMode -testResults Logs/playmode-results.xml
grep -E '\[AutoTest\] (PASS|FAIL)' Logs/playmode.log
Tools/unity.sh photos -executeMethod Parasite.EditorTools.PhotoTool.CaptureBatch
Tools/unity.sh build-linux -executeMethod Parasite.EditorTools.BuildAutomation.BuildLinux
./play.sh -autotest -autotestOut /tmp/parasite-acceptance.txt
./play.sh -benchmark -benchmarkPreset High -benchmarkDistrict city -autotestOut /tmp/parasite-benchmark.txt
./play.sh -benchmark -benchmarkPreset Ultra -benchmarkDistrict riverside -autotestOut /tmp/parasite-riverside-benchmark.txt
Tools/package_build.sh linux
```

`Tools/unity.sh <log-name> <args>` serializes Unity with flock and writes `Logs/<log-name>.log`. Never open the editor GUI or
start another editor process on this project during a batch job. Poll the returned process/session; an old lock file is not
proof a job is alive. Coordinate Blender GPU renders with Unity graphics jobs, especially benchmarks on this 6 GB card.
Explicit `DISPLAY`/`XAUTHORITY`, including an Xvfb display, are preserved by `Tools/display.sh`.

`Tools/compile_check.sh all` now includes a Linux-Mono player API check, using only references/defines from the last valid
Bee player cache and enumerating current source anew. `runtime`, `editor` and `player` modes are explicit; unknown modes fail.
A successful Linux build seeds this cache. Unity-version, package lock or assembly-definition changes require a fresh build;
the player check fails clearly rather than silently using Editor assemblies. It does not replace shader/build/player tests.

After edits to editor builders, `MissionLayout`, or `CityLayout`: **compile → regenerate scenes → PlayMode → Linux rebuild**.
The generated Bootstrap and City scenes must never be hand-edited. Test source changes with the relevant focused suite,
then run campaign regressions when shared behavior or mission progression changes. Package only after the build and its
verification report agree. `Logs/`, `Library/`, and `Builds/` are ignored; they must not be committed.

## Current game architecture

All runtime source is under `Assets/Parasite/Scripts`, assembly `Parasite.Runtime`; editor tooling is in `Assets/Parasite/Editor`.
Use one MonoBehaviour per file, with the file matching the class name. Unity scene serialization depends on this.

- **Boot:** City scene's `World/CityDirector` creates GameManager, SuspicionSystem, MissionManager, PossessionManager,
  AbilitySystem, bakes the collider NavMesh under `World`, spawns `NpcSpawn` markers through `HostFactory`, then creates camera,
  traffic, HUD, ambience and weather. `GameManager.Awake` builds the riverside annex before the bake when the later chapter is active.
- **State:** `Core/GameManager` owns Playing/Paused/Ending/Ended/Resetting. Static `RunState` survives checkpoint scene reloads,
  including mission flags, index, statistics and immutable lakeside outcome; New Game clears it. There is no disk campaign-save claim.
- **Input/possession:** `GameInput` defines Input System actions and acceptance-only simulated controls. `PossessionManager`
  supports two amoebas, aimed E possession, Q release, V split and Tab switching. `PlayerCamera` uses Cinemachine third-person follow.
- **Humans:** `HostCharacter` switches between NavMeshAgent and CharacterController; do not enable both CC and HitBox.
  `HumanoidRig` loads Blender/MakeHuman skinned variants and humanoid animation. `HostBrain` and `GuardAI` handle routines/pursuit.
- **Stealth:** `SuspicionSystem` tracks witnessed violations, cameras report directly, hunters track burned bodies, and guards capture hosts.
- **Missions:** `MissionManager.BuildCampaign` supplies 14 initial missions. Flags drive progression/objectives/waypoints; completed
  evidence and sabotage flags restore world state. `MissionProp`, `CampaignOperation`, `CooperativeConsole`, `DualAuthReader`,
  `AccessDoor`, and `StaffTerminal` implement role and prerequisite gates. A cooperative console remains held by that same host;
  its paired operation needs a different possessed host, and cancellation does not preserve transient work.
- **Later chapter:** `AfterlakeCampaign` appends three missions only after the original summary's deliberate Continue action.
  `AfterlakeDistrict` builds a bounded riverside annex and NPC markers before NavMesh baking. Peace/Dominion changes the story,
  records objective, and radio role choices. Completion requires the free amoeba at the ferry. The first lakeside outcome persists.
- **Presentation:** Deliberate New Game requests the 80-second `OpeningCinematic`; reload/restart/direct City loads do not replay it.
  `LakesideEpilogue` requires explicit Peace/Dominion before publishing the original summary. Waiting or skipping never chooses.
  Both use unscaled time while live gameplay is frozen. Blender set assets and PBR surfaces are loaded through `CinematicArt`.
- **UI:** code-built uGUI in `HUDController` partial files; `MainMenuController`, `SettingsOverlay` and `UIKit` share input/navigation.
  Escape closes a pause submenu before resuming. Journal entries are built from the current campaign rather than hard-coded counts.
- **Graphics:** `GraphicsSettingsData`/`GraphicsSettingsStore` persist independent choices; `GraphicsPreset` retains named-profile compatibility and applies active Custom data across scenes/new game cameras. `GraphicsSettingsRuntime` owns pipeline/renderer/feature/post copies and normal retirement. GroundWater's
  reflections are screen-space depth-buffer ray marches; they are not hardware ray tracing. `LampLights` budgets local lights.
  Custom atmosphere/foliage/facade/amoeba shaders and packed PBR textures provide the real-time look.
- **Audio/FX:** `AudioPreferences`, voice/narration/ambience/music systems; `FxApi`, `SfxBackend`, `AmoebaVisual`, `HostGlow`;
  weather and slime trail. Temporary cinematic gain must not overwrite saved preferences.
- **Builders:** `CityBuilder` + `Editor/World/*`, `MissionPlacement`, `ProjectSetup`, `FxSetup`; `PhotoTool` captures environments,
  while PlayMode UI capture renders actual overlay canvases through the camera. `BuildAutomation` creates the Linux player.

## Campaign

| Mission | Core objective |
|---|---|
| 1 OUT OF THE DRAIN | Leave the alley and borrow the first host. |
| 2 WORD ON THE STREET | Read the plaza newsstand and eavesdrop at the café. |
| 3 THE TAIL | Follow Petrov without crowding or losing her, then recover her dead drop. |
| 4 CREDENTIALS | Guard/main gate, scientist/keycard/staff terminal, or maintenance/power/tunnel. |
| 5 VALE'S RESEARCH | Copy two terminals and use two separate hosts at the office readers. |
| 6 CREATOR | Confront Vale; protected dialogue returns control afterward. |
| 7 HUNTER PROTOCOL | Evade bio-scans, bypass the arch as free amoeba, trip the duty-officer breaker. |
| 8 THE PART HE HID | Maintenance holds coolant while another scientist copies the signed archive. |
| 9 BREAK THE LEASH | Guard holds dispatch while another maintenance host overloads the relay. |
| 10 THE SHADOW UPLINK | Broadcast preliminary evidence without pursuit. |
| 11 THE BLACK SITE ARCHIVE | Trigger a diversion, then download the black-site archive as a scientist. |
| 12 CONTAINMENT PROTOCOL EPSILON | Overload backup power and override the forecourt security grid. |
| 13 BLACKOUT | Cut three transformers in 35 seconds using three distinct human palm prints. |
| 14 THE FINAL CONVERGENCE | Broadcast the unredacted archive and extract as free amoeba through the original drain. |
| Lake | Watch the arrival, explicitly choose Peace or Dominion, then reach the original summary. |
| 15 AFTER THE STILL WATER | Answer Petrov's distress signal and isolate a poisoned floodgate. |
| 16 BORROWED HEARTS / A WILLING NETWORK | Analyse runoff, coordinate guard/scientist hosts at the shelter records. |
| 17 THE OPEN CHANNEL / THE FIRST MANY | Hold mast power with maintenance, transmit with another host, leave by ferry as free amoeba. |
| 18 UNDER THE WATERLINE | Cross the tidal culvert as a free amoeba or open the service latch as maintenance, then reach the inner refuge. |
| 19 THE QUARANTINE WALK / EVERY VOICE | Lead three people through cover to the clinic, or mark, steer and release three couriers at their handoff lanes. |
| 20 A LIGHT IN THE FOG / A SIGNAL BEYOND THE ISLAND | Carry the signal case in the same host, mount it, board every person and free amoeba, then depart. |

The presentation shortcut remains Ctrl+M (either Ctrl, either press order) through initial title/opening/pause/dialogue/gameplay.
It does not choose a lakeside outcome. It must not restart the old finale during the riverside chapter. Existing queued-input
checks do not establish a native physical keyboard or physical gamepad pass. Do not guess a replacement shortcut.

## Blender/art pipeline

- `Tools/blender/characters/`: MakeHuman/MPFB models, authored garments and textures; installed under `Art/Characters`.
- `Tools/blender/animation/`: retargeted/authored clips and previews.
- `Tools/blender/environment/`: tree meshes, bark and leaf atlases, HDR environment assets.
- `Tools/blender/cinematics/build_sets.sh`: deterministic original laboratory, conduit, bench and shoreline sets.
  Editable sources: `ArtSource/Cinematics`; runtime exports/textures: `Resources/Cinematics`; importer: `CinematicArtImporter`.
- `Tools/blender/cinematics/build_films.sh`: two RTX/OptiX establishing inserts, three seconds each; packed film scenes and
  derived device/codec/SHA evidence in `ArtSource/Cinematics`. Verify built-player decode with `-cinematicTestOut <absolute-path>`.
- `Tools/blender/riverside/build_props.sh`: eight original ferry/clinic/radio/floodgate/bank assemblies; editable source and
  CPU export verification in `ArtSource/Riverside`, game assets in `Resources/Riverside`.
- Asset generation must finish before a frozen Unity test run. Publish verified FBX/PNG/video files atomically from staging.
  Blender-to-Unity export needs the verified handedness conversion; size-only bounds checks miss front/back reflection.
- Consult `Docs/THIRD_PARTY_ASSETS.md` for external licences; keep source provenance alongside new assets.

## Verification and remaining work

`Docs/TEST_REPORT.md` owns artifact-specific evidence. `Docs/KNOWN_ISSUES.md` lists limits. `Docs/UPGRADE_TRACKER.md` retains the
full active request and the evidence needed for completion. An implementation or an old passing XML is not proof the new
feature works in the final player. Report unverified player video decode, physical controls, long playthroughs and performance honestly.

## Gotchas

- Unity 6 licensing must be active. Use Unity Hub sign-in only if logs show a licensing problem.
- Story freezes simulation at timeScale=0; use realtime waits/unscaled camera/audio/skip clocks, never speed acceptance by unfreezing AI.
- Commit the original Escaped flag only after an explicit resolved lake outcome. Keep continuation separate from replaying that choice.
- `Physics.IgnoreCollision` resets when colliders disable. Preserve HostCharacter's CC/HitBox switching.
- Unity object fake-null makes `?.`/`??` unsafe for destroyed objects; prefer `if (obj)` checks.
- Disabling a runtime-mutated quality feature must restore its lower-quality setting as well as enabling its higher-quality setting.
- The city atmosphere's height fog must not be applied to isolated cinematic stages far below the city.
- Keep ContributeGI off generated geometry unless deliberately introducing baked GI. Do not skip environment/reflection generation.
- World text needs `WorldTextFont` and the depth-tested shader, including atlas-rebuild refresh; do not mutate the shared UI font material.
- Use deterministic `ShopSeed`/local RNG for generated decoration; preserve unrelated shared RNG draw counts.
- Tests have no focused GameView: route queued keyboard events through DynamicUpdate/IgnoreFocus and restore settings after tests.
- Editor batch ScreenCapture/WaitForEndOfFrame is unreliable for overlay UI. Use the existing URP SingleCameraRequest capture helper.
- Cold shader builds may take many minutes. Poll the live process; an observation timeout is not a failed job.
- `pkill -f` can kill its own shell. Identify the exact PID and only terminate the intended task.
- Git identity is Mevinb only. Commit/push working features often, without Devin or generated-by trailers.
