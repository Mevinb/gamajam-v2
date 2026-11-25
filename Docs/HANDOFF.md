# PARASITE — developer handoff

Read this before changing the project. The active request is a full realism/cinematic upgrade and continued mission expansion,
with frequent commits and pushes as **Mevinb**. This supersedes the historical scope freeze and local-only capture session.
The full request remains **in progress**. Evidence applies to its named source or binary; do not transfer an older pass to a new build.

## Current checkpoint — 26 November 2026

The broad graphics/realism/cinematic/bug-fix/continued-mission goal remains active. Current development is isolated from the existing user playtest.
[Historical checkpoints](HANDOFF_HISTORY_20261126.md) retain every prior source pin, failure, job receipt and retirement pointer; old active-job labels there are historical.

### Live job and source lease

- **Worktree mechanics suite is LIVE:** root session **80827**, timeout **139183**, Editor **139250** at last actual observation.
  Worktree `/home/mevlec/.local/share/parasite-work/northline`, branch `codex/northline-runtime`, HEAD `623c47af54363ea841ab1d2958a23c17353704db`.
  Wrapper log `Logs/northline_mechanics_20261126b.log`; XML `Logs/northline-mechanics-20261126b.xml`.
  Exact class `Parasite.Tests.RailCampaignTests`, seven cases, owned3600s timeout, Null, nice15, idle I/O, two job workers.
  **Keep that checkout's C#/assets/metas fixed and do not launch another Editor until the actual process is terminal.**
  Poll the same handle/process; an observation timeout or old note never authorizes restart. No current whole-suite result.
- The live suite has emitted a real destroyed-body defect: five MissingReferenceException stacks from AmoebaController.Root
  via AudioBus.FocusPosition (music/listener/ambience), MissionManager.Update and HUD.TickCompass. AmoebaAgent.Body casts a
  destroyed Unity Blob to non-null IControllable; the2.2s checkpoint reload gap exposes it. Rail agent is preparing a Logs-only
  central lifetime/fail-closed proposal and actual body-loss tests; no live source edits are allowed before suite terminal.
  Later method-level PASS markers exist for coach rebind and coordinator destruction, but final NUnit counts remain pending.
- Frozen current worktree sources: RailCampaign `e4488bc6137b0f334097fbc6d901713f631b7f86c65ac6db75413ed37e6480c8`;
  RailAcceptance `6d2b653b38260431225a67e05253de0698c1b5dae16feafdcbd2874b806cb3ca`;
  AutoTest `1f998595a804a199fb6b7b780b2122360b94d1568abc7fa087a95e1591aede5c`.
  Actual worktree API compilation passes175runtime/33Editor/175LinuxMono plus five NUnit classes.
- The worktree contains the three reviewed Northline patches, actual imported Resources kit, movement/LOS fixes and the possession setup/control fixture.
  No source merge/build or code commit from this branch yet. A read-only explicit staging whitelist is being prepared; review it before any Git staging.
  Exclude InitTestScene/Temp/Logs/Library/Builds and seven unrelated cold-import film `.meta` normalizations. Do not revert metadata during the live suite.

### Proved Northline results and next actions

- Actual import passes35,449 checks: eleven models,7,336triangles, three original texture references, unchanged scene/dirty state.
  Exact selection registry/filter tests pass13/13. [Import/selection proof](verification/northline-import-selection-20261126.json).
- Actual production bug: parent-Rigidbody `hit.transform` falsely rejected the departure control's own child collider.
  The narrow `hit.collider.transform` ownership fix passes four real physics controls, including sibling/foreign occluders.
  [Red receipt](verification/northline-departure-own-collider-red-20261126.json), [green controls](verification/northline-collider-los-green-20261126.json).
- Full prepared-M23 **Dominion chapter passes** through real crossing/freight/signal/both-free boarding/coach endpoint/M26 summary:
  root59655/native0/chunkc2610e,106.664s. [Proof](verification/northline-dominion-editor-green-20261126.json).
  Prepared prior-summary/initial-role fixtures remain explicit; this is neither earned continuous26 nor native/GPU/human acceptance.
- Controlled possession-obstruction fixture passes: root74517/native0/chunk432b92,7.338s. Actual wall remains enabled through blocked original LOS,
  different same-NPC fallback, normalE/reciprocal ownership and2.5661m WASD movement. [Proof](verification/possession-obstruction-green-20261126.json).
  This does not reconstruct the unknown old native02 blocker or make a new native binary pass.
- Current movement driver uses full-capsule/floor/nav checks, max3physical bypasses, one4s lateral+forward action cap inside the original leg deadline.
  Manifest arrival alone is tightened `.25→.05` at the same authored target/budget to see through the actual grille gap.
  No NPC relocation, collision ignore, range/cone/LOS relaxation or artificial progress grants were used for these fixes.
- After the live seven-case suite is terminal, preserve exact exit/XML/counts; fix real failures without weakening predicates.
  Then run the **two RailArtBindingTests** serially in the worktree. Their physics/nav/control signatures, moving attachments, scaling and full physical chapter are still unverified.
  New native opt-ins92/93 are prepared rail branches;94/95 earn M23→M26. Default27 and90/91 scopes stay unchanged.
  All need an actual fresh build and runtime evidence; do not transfer old binary results.

### Base checkout, release and user preview

- Main checkout `/home/mevlec/Data/Parasite` still has0.6.1 code; its source snapshot is
  `dc5fb1ed8fe416e4c1333943ca00564fa8c89231200e4b8ac645e0dd40e27d7b`. Untracked `Docs/GRAPHICS_UPGRADE_PLAN.md` belongs to unrelated work: preserve it unstaged.
- Candidate `Builds/Candidates/full-story-20261125T222359513713Z-2389d119`, source commit `8f7b0e77be4ff73110d4816fdcf0bf9526bba31f`;
  Runtime `13334130a851d6717c34f7619ae41395afe6cf847bf341bdc03bf709553f6d9f`, BUILD_INFO `fab619d772a3951307d73ca1dff8bdb9b2d2ee08ae20ba3d629981c00633273b`.
  Build succeeds0/1,118MB/127.806s/zero BuildReport errors/warnings; source and both previews unchanged. Separate Kestrel heartbeat notices remain raw.
  [Build proof](verification/0.6.1-candidate-20261126.json).
- Exact native continuous23 branches **90Peace and91Dominion pass independently**, native0/supervisor0/SUMMARY1/1 each and all final integrity checks.
  [Peace](verification/0.6.1-native-peace-20261126.json), [Dominion](verification/0.6.1-native-dominion-20261126.json).
  One tightly leased worktree import overlapped91; that lease ended. These are fixture-declared Null logic checks, not graphics/FPS/human/default27 coverage.
- Portable archive `Builds/Releases/PARASITE-0.6.1-preview-Linux.tar.gz`:615836474B,
  SHA `fee38fdc4cab9593a9025a999b65ca6f27c7b5c20412b79c3c8383b54096fe8e`. Packaging verifies211entries/39links and immutable candidate/docs.
  Fresh extraction verifies193files/17dirs and all hashes/modes in
  `/home/mevlec/.local/share/parasite-artifacts/extractions/061-20261125T230520564657Z-5ce965f6/PARASITE-0.6.1-preview-Linux`.
  **Fresh native01–05 is4/5/native1**,02 LOSfalse at4.9m; not a full portable runtime pass. [Exact result](verification/0.6.1-portable-basic-red-20261126.json).
  New setup correction exists only in the worktree, not this binary. Original archive/overlay/extraction remain unchanged.
- **User graphical0.5.1 playtest remains open:** PID51569/start683333, candidate `Builds/Candidates/full-story-20261125T173043238618Z-0f595731`, RTX/Vulkan.
  Do not close, restart or inject input without user steering. Earlier async GPU-release question remains unanswered; silence is not approval.
  GPU benchmarks/rendered checks require that session to be released. No new rendered0.6.1/26 playtest is claimed.
- Retained `Builds/Linux`0.5.0, prior0.6.0 candidate/archive, source art, all2,760 completed RTX/OptiX frames and user preview are protected.
  The superseded3894 verification data is retired with exact reconstruction/retained-code/lossless-log receipts; see history, never call it runnable.
- Disk last observed: Data~2.6GiB free, root filesystem~14GiB. A read-only full-candidate archival plan to root storage is being reviewed for the next build.
  Never delete/move protected artifacts blindly or lower the build helper's2.5GiB minimum reserve. Recheck actual capacity before writing.
  The storage audit finds that relocating private0.6.1alone still does not meet the former3GiB Data reserve. Hunter is preparing
  a dry-default runner adaptation for a fresh candidate under the isolated worktree's root filesystem while retaining all
  protected-preview paths in the base project. No build/relocation/removal is authorized or executed from that plan yet.

The current runtime annex does not require scene regeneration. Any later CityBuilder/MissionLayout/CityLayout builder change still requires the real-GPU generation workflow, never a Null lightmap bake.
Use existing `Tools/unity.sh` in the intended checkout with its unchanged global flock. All test launches are root-owned and serialized.
Commit/push as Mevinb only; do not merge the26 feature merely because its API/import or one branch is green.

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
