# PARASITE — The Far Shore (0.5.0 candidate)

You are Specimen 7, an escaped amoeba. Borrow human bodies, split in two, infiltrate Vale Biotech and expose the black-site
archive across fourteen missions. At the sunset lake, explicitly choose **Peace** or **Dominion**. Continue through three
riverside missions and three Far Shore island missions: twenty missions in all.

The candidate integrates a full eighty-second RTX-rendered Blender opening and four full lake films, eight island voice clips,
a Blender quarantine-wall kit with 2K PBR maps, camera/reflection corrections and safe native movie cleanup. **Final verification
is in progress.** `BUILD_INFO.json` identifies the binary; [TEST_REPORT.md](TEST_REPORT.md) records actual checks and unresolved
gates. Do not interpret earlier successful runs or an encoded movie as a full pass for this candidate.

## Run on Linux

Extract the entire archive, enter its `Linux` folder and run `./play.sh`. Keep `Parasite.x86_64`, `Parasite_Data` and all supplied
Unity libraries together. If executable permissions were lost, run `chmod +x play.sh Parasite.x86_64`.

The launcher selects NVIDIA offload and prefers Vulkan when NVIDIA is available. To use OpenGL, run
`PARASITE_GRAPHICS_API=glcore ./play.sh`; explicit Unity graphics API flags take priority. Use the launcher beside this
executable so you run this binary rather than a different retained project build.

Choose **Begin Infiltration**. The eighty-second opening skips with Space/Enter/Esc or gamepad South/East; release the input
to enter play. It does not replay on restart or checkpoint reload. Title and pause settings save graphics, sensitivity, master,
music, ambience, effects and dialogue levels. High uses 100% render scale; Ultra uses 125% render scale and 4× MSAA.

## Controls and continuation

WASD or arrows move · Mouse look · Left Shift sprint · Space jump · E possess/interact/eject when captured · Q leave host ·
V split · Tab switch · F profession ability · Esc pause · F1 controls · F3 FPS. Aim within five metres for possession and read
the contextual prompt. At a timed control, press E to start and remain close while it works.

Ctrl+M jumps to the original escape/lake from the initial title, opening, pause, dialogue or gameplay. It never chooses a future
and is ignored in the later chapter. At the lake, choose **Live quietly** or **Become the world**. After the resolution,
**Continue · After the lake** starts the riverside chapter; after its ferry summary, **Continue · The Far Shore** starts the island.

Use V again if you need two amoebas after a chapter/checkpoint reload. Supporting consoles must stay held by one host while a
different host operates the paired control. On the island, Peace survivors follow a civilian/scientist leader; Dominion couriers
need their own marked lane, release and recovery. A rescue signal must reach its mount in the same carrier's custody. Departure
requires every named passenger awake and physically aboard, all owned amoebas free and aboard, no pending leap and no pursuit.

## Guides, provenance and validation

Read [CONTROLS.md](CONTROLS.md), [COMPLETE_GAME_GUIDE.md](COMPLETE_GAME_GUIDE.md) and [AFTERLAKE_GUIDE.md](AFTERLAKE_GUIDE.md)
for controls and walkthroughs. [KNOWN_ISSUES.md](KNOWN_ISSUES.md) records remaining limits. Automated movement and queued-key
checks do not establish physical keyboard/gamepad behavior or an extended human playthrough. Checkpoints preserve completed
progress within this running process; persistent disk saving is not implemented.

The project retains a separate **0.5.0-preview** containing M18 and four full lake films but not M19/M20 or subsequent fixes.
The older retained **0.4.2 archive** contains two short inserts. Neither is relabelled by this candidate's documentation;
compare `BUILD_INFO.json` and artifact-specific test receipts.

All five full films are silent 1080p24 streams; captions, narration, taps and music come from the game. Linux gameplay uses
URP shaders, screen-space reflections and probes, while Blender footage uses offline RTX/OptiX path tracing. The Linux player
does not use built-in hardware ray tracing. Voices are synthesized. Keep the font/audio licence texts in `Licenses/` and the
[asset credits](THIRD_PARTY_ASSETS.md) with the game when sharing it.
