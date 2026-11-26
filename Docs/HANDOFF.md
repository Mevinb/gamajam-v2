# PARASITE — developer handoff

Read this before changing the project. The active request is a full realism/cinematic upgrade and continued mission expansion,
with frequent commits and pushes as **Mevinb**. This supersedes the historical scope freeze and local-only capture session.
The full request remains **in progress**. Evidence applies to its named source or binary; do not transfer an older pass to a new build.

## Current checkpoint — 26 November 2026

**LATEST USER PLAYTEST: 0.7.2-preview is open.** [Build/launch proof](verification/0.7.2-preview-build-and-launch-20261126.json).
WORK candidate `Builds/Candidates/full-story-20261126T073327996101Z-8c413e3e`; source commit `1cf8bfb`, snapshot `8a26a001…`.
Runtime `998430159a0a7899d39d8e75272a89b2d35c35cb0bff301265a5a6320d8c8aac`.
Build succeeded1135MB, zero errors/warnings, source and all older artifacts unchanged. Native basic01–05 passes5/5/native0/29.296s.
Desktop PID **289391**, parent289390, client `0x100000a`, RTX4050/Vulkan title menu v0.7.2 visually inspected.
Independent user service `parasite-playtest-072-8c413e3e.service`; WORK session
`Logs/user-playtest-full-story-20261126T073327996101Z-8c413e3e-desktop/session.json`.
The first tool-owned launcher was interrupted143; its native final code was unavailable. Its observation is retained separately.
**Preserve the current user session: no close/restart/input injection or competing GPU jobs. Recheck live state before work.**
The detailed coach is now built into this player. [Integration proof](verification/northline-coach-integration-preview-20261126.json):
expanded Editor9/11; both chapter/binding tests and seven material tests pass. Two strict tests retain the known Editor ring-buffer warning;
physical/glass assertions completed and six review images captured. Preview graphics/performance acceptance remains ongoing.
The broader upgrade goal remains active; main runtime remains separate at0.6.1. Older pending0.7.2 statements below are superseded.


The full upgrade/continued-expansion goal remains active. The earlier 0.7 playtest ended normally; the user's current request is a fresh build and launch of the latest work. A new 0.7.2 preview is being prepared, with no new build or launch result claimed yet.

- **USER 0.7 PLAYTEST ENDED:** PID155953/start3334957 is gone. Its recorded session exited normally0 after21711.645s at
  **2026-11-26T06:57:00.686080Z**; the agent did not close, restart or inject input into it. Candidate WORK
  `Builds/Candidates/full-story-20261126T005055601457Z-875bb782` remains retained, runtime408112a0899965a49a95c656e34b8161a942135ce8455793efafed49b8040ca5.
  Session WORK `Logs/user-playtest-full-story-20261126T005055601457Z-875bb782/session.json` is terminal. No current GPU reservation derives from that ended session.
- **PRIVATE0.7.1 BUILD TERMINAL/PASS:** root9396/native0, old owned Editor194358 gone;1119MB/121.229s pipeline/0errors/0warnings.
  [Build proof](verification/0.7.1-candidate-20261126.json). Candidate WORK `Builds/Candidates/full-story-20261126T014554516966Z-b287ee31`;
  runtime **66e4289a8c4ecaeb379334ba360f57512251c11275fcefa7aa34c1ecb2cd7b8f**, BUILD_INFO **bc9045f5574b8d43b600e20b1217a26f9d8142f851272ae2d19df14e3df02ff0**.
  Source before/after **de2acc082e446d2ef9420eea7912cdc35431439a2b6db3a762133a2ad4f6533f**, HEAD **70259deafd711f841c07225949b55733eaf2101a**,
  built from the then-clean pushed WORK branch `codex/northline-runtime`; all older BASE artifacts/source/070candidate unchanged. The later supported-renderer96 invocation below uses this exact private071binary.
  State WORK `Logs/full-story-candidate/build-runs/20261126T014554516966Z-b287ee31/runner-state.json`.
- **071 NATIVE95 TERMINAL/PASS:** [Continuous Dominion26 proof](verification/0.7.1-native-continuous-dominion26-20261126.json), root54898,
  native0/supervisor0,634.37s, exactPASS95/SUMMARY1/1, Null, all source/HEAD/071/070artifact integrity checks pass, no cancellation.
  WORK `Logs/native-071-focused/95-20261126a/execution.json`; old supervisor195558/player195561 are gone. Existing within-chapter
  role/position/Calm/timeScale/simulated-input fixtures remain explicit; four earned chapter boundaries reach the actualM26coach/body endpoint and summary.
  This closes that named071continuous-Dominion check, not continuousPeace94, default27, rendered appearance or human acceptance. Its source freeze ended with the run.
- **071 native normal river branches pass:** [17Peace and18Dominion](verification/0.7.1-native-riverside-green-20261126.json), two separate invocations,
  roots91824/20467, native0/super0,28.234/28.188s, exactselectedPASS/SUMMARY1/1 each,allsource/071artifact/070user integrity pass,no cancellation.
  Those existing chapter fixtures are explicit; these river passes do not independently prove continuous26/default27/rendered/human acceptance.
- **071 NATIVE96 SUPPORTED-RENDERER PASS; ORIGINAL NULL RED RETAINED:** [RTX4050/Vulkan proof](verification/0.7.1-body-free-native-vulkan-green-20261126.json),
  root53840, native0/supervisor0,12.059s, exactPASS96/SUMMARY1/1, zero HDR and ring-buffer warnings, strict unexpected-log guard clean,
  all immutable candidate and separately reviewed current-development source checks pass. The player remains the old071/de2accsource; later source is not claimed in that binary.
  [Original Null red](verification/0.7.1-body-free-native-red-20261126.json) remains root53749/native1/supervisor1/10.101s: functional calls finished before
  the strict guard rejected `Warning: HDR Render Texture not supported, disabling HDR on reflection probe.`. No warning suppression, probe/HDR quality cut or test weakening was applied.
  [Null warning characterization](verification/null-hdr-warning-characterization-20261126.json) is diagnostic scope. The earlier Editor Vulkan ring-warning gate remains distinct and open.
- **0.7 native branches now pass independently:** [92Peace/recovery](verification/0.7.0-native-northline-peace-20261126.json),root22591/native0/super0,193.311s;
  [93Dominion](verification/0.7.0-native-northline-dominion-20261126.json),root66651/native0/super0,106.747s. ExactonePASS/SUMMARY1/1/Null,
  all source/HEAD/full-candidate/user checks pass, no cancellation. They prepareM23/initialroles; they do not prove earned94/95, default27 or graphics/human acceptance.
  Existing [0.7 build/API](verification/0.7.0-candidate-20261126.json), [basic5/5](verification/0.7.0-native-basic-20261126.json),
  [visible launch](verification/0.7.0-user-playtest-20261126.json) remain tied to the unchanged0.7 binary/source95d9/commit6e12405.
- **New actual ferry defect fixed in source:** a destroyed inactive blob remainedIsFree; the shared ferry guard could publishM17 with no physical second body.
  Root77623 actualred1/2/native2 records deadIsFree/allFree/ferryAllowedTrue, StateEnded/DepartedTrue/MissionIndex17.
  Source `IsFree` now requires liveBlob, preserving hidden/disabled identities; sharedAllFree rejects the missing body without pruning/spawning/autoReset.
  The pause menu now has separate **Restart checkpoint** and **Restart game** actions; seven rows fit its panel. The recovery test invokes the actual checkpoint button,
  reloads the real scene/new registry, and uses fresh normalE to earn the ferry. Player-facing denial points to that action.
  [Exact regression history](verification/body-free-state-regression-20261126.json) preserves own-log/input-driver fixture failures and final focused2/2/native0.
  Both normalriverbranches and earlier lifetime controls also pass in named runs. No fresh single6/6 claim is made for the mixed earlier batch.
  [Sharedhelper/selection17/17](verification/body-free-shared-editor-green-20261126.json),root30574/native0,8.311s,
  exercises actual menu recovery plus exact96/default27 registry. Actual compile176runtime/33Editor/176LinuxMono passes.
  Commit674b8e6 contains fix/UI/sharedacceptance/exactopt-in96; the retained0.7binary does not contain it. Native96 now passes on supported RTX/Vulkan, with its separate original Null strict-log failure retained above.
- **DETAILED COACH INTEGRATED IN WORK SOURCE, NOT YET A NEW PLAYER:** The original authored package in commit70259de has a newer uncommitted
  revision in WORK `ArtSource/NorthlineCoach`, manifest0de23f46b09641b5cfe94ff2fdb52bdaf9adb9f544e3359784b1ab51e915c5e1.
  The11144triangle coach/15972triangle kit retains198closed components,10real glass apertures, original bounds/pivot and ten byte-identical other models.
  Runtime integration now includes two owned URP materials, three dedicated2048 A/N/M textures and **32 precise seat-part colliders** for eight assemblies.
  Seat pans top at1.25m; broad eight-box staging was replaced so the legitimate0.55m hop remains possible. New colliders have explicit zero centres/unit sizes.
  WORK `Logs/coach-import32-20261126a.json` records **67247checks/no failures**, actual imported structure/data only. Material tests pass7/7;
  `Logs/coach-fitting32-20261126c.xml` passes1/1 in14.4128031s with physical blocking, hop/landing, aisle traversal and original boarding/control checks.
  Actual compile176runtime/33Editor/176LinuxMono passes. An expanded RTX/Vulkan smoke run is root-owned/in progress; no result is claimed here.
  Binding, rendered appearance, chapter/lifecycle and performance checks retain their named scope. Neither retained0.7 nor private0.7.1 includes this new coach.
- **Temporary061 extraction retired after full review:** [retirement proof](verification/061-extraction-retirement-20261126.json),root87248/native0.
  Exact193files/17dirs matched retained fee38 archive/CRC/modes and originalmanifest;0unique/userfiles/links/specials/xattrs. Fresh content/identity/reference checks,
  recorded same4opaque systemd/PAM/SSH identities and otherUID visibility limits. Only generated package directory removed; **1173475328B actually reclaimed**.
  Archive/candidate/reports/native4/5 history/profiles/parentownership token remain. Reconstruction command/evidence BASE `Logs/extraction-retirement-061-stage`.
  Historical extraction is now reconstructible evidence, not a runnable present directory. Current0.7 source/build/session unaffected. Recheck space before writes.
- Main runtime source remains0.6.1/DC5; WORK feature/bug/source-art commits are pushed, code integration to main awaits further native/graphics acceptance.
  Preserve unrelated untracked `Docs/GRAPHICS_UPGRADE_PLAN.md` unstaged. Keep all retained BASE0.5.0/0.5.1/0.6.0/0.6.1builds/releases, the ended0.7candidate and private0.7.1candidate,
  failed cold-build evidence and2760completed RTX/OptiX film frames. No rerender needed. No0.7/0.7.1portable archive exists yet.

Next: finish the current coach smoke and fresh0.7.2build/visible launch requested by the user. EarnedPeace94 remains unrun; full/default/rendered/media/settings/performance gates remain.
Preserve original pinned071run receipts and their original pending-pins refusal. Never transfer0.7source95d9 or0.7.1/de2accresults to the new coach source/player.
The earlier0.7GPUreservation ended with that session; preserve any newly launched user playtest once it exists.
Linux/URP plus offline RTX Blender films and improved real-time shaders is the chosen direction; gameplay hardware RT is not implemented.
Disk campaign saving, physical input/listening, final realism quality and measured performance remain separate work. Historical checkpoint labels are in HANDOFF_HISTORY_20261126.md.

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
