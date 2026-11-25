# PARASITE — The Working Harbor (0.6.0 preview)

You are Specimen 7, an escaped amoeba. Borrow human bodies, use their credentials and split into two independent bodies to
expose Vale Biotech. This preview has **23 missions**: fourteen in the city, an explicit **Peace or Dominion** choice at the
lake, three riverside missions, three island missions and three new Harbor missions.

This guide belongs to the **0.6.0 preview candidate**. The separately retained **0.5.1 preview** remains a twenty-mission
build; these instructions do not add the Harbor to it. Check the `BUILD_INFO.json` beside the executable to identify your copy.
The preview is still being verified; see [NATIVE_SCOPE.md](NATIVE_SCOPE.md). Automated checks are narrower than a complete human playthrough, physical-controller
validation or a performance benchmark.

## Run on Linux

From the extracted release folder, run:

```bash
./play.sh
```

Keep `Parasite.x86_64`, `Parasite_Data`, `UnityPlayer.so` and the other supplied libraries together. If executable permissions
were lost, run `chmod +x play.sh Parasite.x86_64` in that folder. Use the launcher beside this executable when several builds
are installed. The launcher prefers NVIDIA/Vulkan where available; `PARASITE_GRAPHICS_API=glcore ./play.sh` selects OpenGL.

Choose **Begin Infiltration**. The eighty-second opening can be skipped with Space, Enter or Esc, or gamepad South/East;
release the input to enter play. It does not replay on checkpoint reloads. Aim at a person within five metres and follow the
contextual prompt. If a host is too close to aim at, step back and aim at their upper body.

## Controls and the story route

WASD/arrows move · Mouse looks · Left Shift sprints · Space jumps · **E** possesses/interacts/ejects when captured · **Q** leaves
a host · **V** splits · **Tab** switches amoebas · **F** uses a profession ability · Esc pauses · F1 shows controls · F3 shows FPS.
Gamepad bindings and menu navigation are in [CONTROLS.md](CONTROLS.md).

Each later chapter starts only when you choose its summary button:

| Finish | Choose | Next missions |
|---|---|---|
| City missions 1–14, then the lake's explicit future | **Continue · After the lake** | Riverside 15–17 |
| Riverside ferry departure | **Continue · The Far Shore** | Island 18–20 |
| Island rescue-launch departure | **Continue · The Working Harbor** | Harbor 21–23 |

The first lake choice stays fixed. Waiting or skipping a scene does not choose a future or continue for you. Ctrl+M is a
presentation shortcut to the original escape/lake; it is ignored after the riverside chapter has begun.

## The new Harbor

- **Pressure:** as Maintenance, work valves A (+3), B (+2) and bleed C (−1). Keep the gauge at **4 for two gameplay seconds**.
  Above 4, relief safely resets all three handles. Opening C, then A, then B is one safe solution; the physical lock lifts.
- **Cargo:** as Scientist, press E at the scanner while a required sealed crate is in the moving arch. Peace needs **A17 and
  A32** antidote lots; Dominion needs **R09 and R21** relay lots. The prompt shows current/next IDs and the scan window.
  Maintenance can brake the main belt for **five gameplay seconds**, but timing it with one scientist is enough. A scan
  reroutes a real crate; the mission waits until both required crates physically reach their bays. Stay outside the fence.
- **Relay:** release both hosts, split with V if needed, and use Tab to place **two distinct free amoebas fully on separate
  pads**. Lose pursuit and clear both emitter sight lines. Press E at either node, then keep both bodies still for **three
  gameplay seconds**. Moving, leaving a pad, possession, capture or a blocked beam cancels the attempt. Pause freezes it.

[HARBOR_GUIDE.md](HARBOR_GUIDE.md) explains recovery and timing. The [complete walkthrough](COMPLETE_GAME_GUIDE.md) covers all
23 missions; [AFTERLAKE_GUIDE.md](AFTERLAKE_GUIDE.md) expands the riverside cooperative controls.

## Checkpoints, settings and presentation

**Campaign progress is saved only within the running game.** Checkpoint reloads retain earned objectives and restore one
free amoeba; find hosts and split again when needed. Closing the game does not create a campaign save. Play Again, Restart
and a new Begin Infiltration start over.

In the Harbor, earned valve positions survive a checkpoint but their two-second balance restarts. Unscanned crates return
to the loop; scanned, undelivered lots restore at separate bay approaches and must finish moving. Delivered lots stay in their
bays. The brake and unfinished relay handshake restart. No duplicate crate or extra amoeba is added.

Title/pause Settings has Display, Quality, Effects and Audio & Input pages. **Apply** uses and saves staged graphics;
**Cancel/Back** discard an unapplied graphics draft. Audio and sensitivity apply immediately. Quality presets preserve your
VSync/frame cap; Custom choices persist between launches. These preferences are separate from campaign progress.

The Linux game uses real-time URP shaders, screen-space reflections and probes. The Blender story films use offline RTX/OptiX
path tracing; this does not make gameplay hardware ray traced. The films are silent image streams with live game audio and
captions. Existing narration is synthesized; the new Harbor introduction/resolution uses captions without newly recorded voices.

Keep [THIRD_PARTY_ASSETS.md](THIRD_PARTY_ASSETS.md) and all supplied `Licenses/` files with the game when sharing it.
