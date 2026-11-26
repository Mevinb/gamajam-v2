# PARASITE — developer handoff

Read this before changing the project. The active request is a full realism/cinematic upgrade and continued mission expansion,
with frequent commits and pushes as **Mevinb**. This supersedes the historical scope freeze and local-only capture session.
The full request remains **in progress**. Evidence applies to its named source or binary; do not transfer an older pass to a new build.

## Current checkpoint — 26 November 2026

The full graphics/cinematic/mission expansion goal remains active. The immediate user request is a fresh build and launch for testing.

- **CURRENT BUILD IS LIVE:** root exec session **10336**, supervisor **150195**, owned Editor **150218**.
  State: `/home/mevlec/.local/share/parasite-work/northline/Logs/full-story-candidate/build-runs/20261126T002329795146Z-7e40abcb/runner-state.json`.
  Fresh output: `Builds/Candidates/full-story-20261126T002329795146Z-7e40abcb` relative to that worktree.
  It is a cold shader compile, Null backend, two job workers, unchanged `Tools/unity.sh`/global flock. No build result yet.
  **Keep WORK source/metadata/HEAD fixed; poll this actual job, never restart because observation expired.**
- WORK: `/home/mevlec/.local/share/parasite-work/northline`, branch `codex/northline-runtime`, pushed HEAD
  `6e1240565612035fd6cf9d8c9236dd592f9abf3f`; feature commit `c9282a1`, separate importer normalization `6e12405`.
  Version **0.7.0-preview**, **26 missions**. Reviewed full source SHA `95d9939345f074a855c6f32da39aa6f622b25c4512998b86be038541d5a6a648`.
  Includes three Northline missions, eleven Blender models, moving attachments, compound-collider LOS fix and central destroyed-body guards.
- **Focused fixes and bindings are proved:** [authored binding tests2/2](verification/northline-authored-bindings-green-20261126.json);
  [body lifetime2/2 and actual coach destruction pass](verification/northline-lifetime-green-return-red-20261126.json);
  [full Peace physical recovery passes](verification/northline-peace-recovery-green-20261126.json), root76466/native0,193.036s.
  The exact earlier wrong-route failure is preserved: same carriage hit the solid signal wall at local(1.838,.15,22.997).
  Return corridor/visible rails now useZ20.5; same carriage returns physically in40.404s within the unchanged70s deadline.
  Earlier full mechanics5/7 result remains historical; its two failures now have passing focused retests. No fresh whole-suite7/7 claim.
  Earlier Dominion, coordinator disable/destroy, rebind, continuation, four LOS controls, import35449/selection13 and possession obstruction proofs remain named.
- Seven film `.meta` changes are a separately reviewed/committed Unity cold-import normalization: only unused customWidth/customHeight −1→1.
  All seven movie files match the original media bytes; resizing/transcoding settings are unchanged. No rendered decode claim follows from this metadata review.
- Runner: BASE `Logs/worktree-build-stage/build_worktree_candidate.py` SHA `396abbe9d0b31856417e20a32525c259d46469de269a32a34767db74c9f010b2`.
  Ready plan `Logs/worktree-build-stage/reviewed-plan-070-20261126.json`; actual dry preflight passed. It protects BASE artifacts/source before/after,
  uses WORK/root storage with10GiB reserve and exact owned Editor/import-worker identity checks. No relocation/deletion was performed.
- Next: actual build terminal/integrity receipt, `Tools/compile_check.sh all` using the new WORK player cache, native01–05 via
  WORK `Logs/smoke_preview_070.py`, then user launch via WORK `Logs/launch_preview_070.py` with the actual build-state path.
  No native26/earned94–95/rendered graphics/FPS/human-playthrough pass is claimed. Source and candidate tests are separate.
- The old user **0.5.1** player is still PID51569/start683333. Preserve it during build/native checks. The immediate build-and-run request
  will be fulfilled by replacing that old session with the new preview after the new build passes smoke checks. WORK
  `Logs/close_previous_preview.py` verifies that exact process/window and uses standard WM_DELETE_WINDOW; it has only been run read-only so far.
  Never use broad kill commands or inject gameplay inputs. The user controls the launched preview.
- BASE/main code remains0.6.1, source `dc5fb1ed8fe416e4c1333943ca00564fa8c89231200e4b8ac645e0dd40e27d7b` while the build protection lease is active.
  Untracked `Docs/GRAPHICS_UPGRADE_PLAN.md` is unrelated: preserve it unstaged. All retained `Builds/Linux`0.5.0, old user0.5.1,
  0.6.0/0.6.1 candidates/archives and all2760 completed RTX/OptiX film frames remain protected. Do not rerender completed films.
  [0.6.1 candidate](verification/0.6.1-candidate-20261126.json), [native90](verification/0.6.1-native-peace-20261126.json),
  [native91](verification/0.6.1-native-dominion-20261126.json), [portable basic4/5](verification/0.6.1-portable-basic-red-20261126.json).
  The old archive is immutable and does not contain the new possession setup correction.

Linux/URP plus offline RTX-rendered Blender cinematics and improved real-time shaders is the user's selected direction.
No gameplay hardware ray tracing is implemented. Physical input, final graphics/performance acceptance, disk campaign saving and continued expansion remain separate work.
Historical invocation identities and source pins are in [HANDOFF_HISTORY_20261126.md](HANDOFF_HISTORY_20261126.md); their old live labels are not current liveness.

## Artifact and verification boundaries

Source and candidate0.6.0 have twenty-three missions: original14, riverside15–17, island18–20 and harbor21–23, with immutable
explicit Peace/Dominion continuation. The running0.5.1user player remains the separate twenty-mission build.
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
   The current portable archive and isolated extracted basic player are already verified. The user's rendered0.5.1process
   remains open; a choice to close it for GPU checks has been requested asynchronously. Silence is not permission to close it.

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
