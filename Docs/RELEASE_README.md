# PARASITE — After the Still Water (0.4.0)

You are Specimen 7, an escaped amoeba. Borrow human bodies, split in two, infiltrate Vale Biotech and expose the black-site
archive across fourteen missions. The sunset lake requires an explicit Peace or Dominion choice. From its summary, choose
**Continue · After the lake** to play three further missions at a riverside shelter, clinic, floodgate and radio mast.

## Run on Linux

Extract the entire archive and keep `Parasite.x86_64`, `Parasite_Data` and the Unity libraries together. Run `./play.sh` in the
Linux folder. If permissions were lost, run `chmod +x play.sh Parasite.x86_64`. NVIDIA hybrid systems may select the discrete
GPU with `__NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia ./play.sh`.

Choose **Begin Infiltration**. The eighty-second opening skips with Space/Enter/Esc or gamepad South/East; release to enter play.
It does not replay on restart or checkpoint reload. Graphics, sensitivity and audio settings persist. High is a useful option
if Ultra's supersampling and MSAA are too expensive for your resolution.

## Controls and continuation

WASD move · Mouse look · Shift sprint · Space jump · E possess/interact · Q leave host · V split · Tab switch · F profession
ability · Esc pause · F1 controls · F3 FPS. Aim within five metres and read each contextual prompt.

Ctrl+M jumps to the original escape/lake from the initial title, opening, pause, dialogue or gameplay. It never chooses a future
and is ignored in the later chapter. At the lake, explicitly choose Live quietly or Become the world. After the resolution,
Continue enters the riverside chapter. Use V again if you need two amoebas after a chapter/checkpoint reload. Supporting
consoles must be held by one host while a different host operates the paired terminal. The ferry needs a free amoeba.

## Included documentation and validation

Read `COMPLETE_GAME_GUIDE.md` and `AFTERLAKE_GUIDE.md` for mission walkthroughs. `TEST_REPORT.md` records checks on this artifact;
`KNOWN_ISSUES.md` records remaining limits. Automated acceptance does not replace physical control tests or a long human playthrough.
Campaign checkpoints survive scene reloads in this process; they are not a persistent disk save.

The cinematic redesign uses Blender sets and RTX-rendered footage. Linux gameplay uses URP shaders, screen-space reflections
and probes; it does not use built-in hardware ray tracing. Font/audio licence texts are in `Licenses/`; see `THIRD_PARTY_ASSETS.md`
for other asset credits. Keep the documentation beside the executable when sharing the build.
