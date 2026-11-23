# PARASITE — 0.2.2

**Local hotfix:** Ctrl+M is available from the title screen, opening cinematic, pause menus, Vale dialogue and gameplay. A request during loading or a body change waits for the game to be ready. The title shows v0.2.2. The appended hotfix section in TEST_REPORT.md records the checks run for this build.

You are Specimen 7, an escaped amoeba. Possess people, borrow their credentials and split into two bodies through eight missions.
Meeting Dr. Vale is the midpoint: secure his signed evidence, disable containment, broadcast the truth and escape.

## Start playing

This demo targets **Linux x86-64**. Extract the entire archive. Keep the executable, `Parasite_Data` and Unity libraries together.

Open the extracted `Linux` folder and run `./play.sh` in a terminal. If executable permissions were lost during transfer, run `chmod +x play.sh Parasite.x86_64` first. On an NVIDIA hybrid laptop, you can select the discrete GPU with `__NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia ./play.sh`.

Select **Begin Infiltration**. Settings in the title and pause menus save graphics quality, mouse sensitivity, master volume and ambience volume. Try a lower graphics preset if performance is uneven.

The new-game backstory lasts **80 seconds** and can be skipped with **Space**, **Enter** or **Esc** (gamepad **A/South** or **B/East**).
Release the skip key/button to begin playing. The opening does not replay after Restart or checkpoint reloads.

## Quick controls

**WASD** move · **Mouse** look · **Shift** sprint · **Space** hop/jump · **E** possess/interact/eject · **Q** leave host · **V** split · **Tab** switch body · **F** host ability · **Esc** pause · **F1** controls · **F3** FPS.

**Judges' presentation shortcut:** press **Ctrl + M at any point** to skip ahead to the final escape and sunset-lake choices.
You must still explicitly select **Live quietly (Peace)** or **Become the world (Dominion)**; no outcome is chosen automatically.

Aim at someone within 5 metres and press **E** to possess them. Follow yellow mission waypoints. Guards use the main gate, scientists use the staff entrance and maintenance workers use the service tunnel. Being seen as a free amoeba raises suspicion. When a guard captures your host, press **E** to eject and escape.

After Vale's reveal, keep a maintenance body at coolant while a scientist copies evidence. Next, hold guard dispatch while a second
maintenance body overloads the relay. Broadcast at the Plaza uplink, then leave your host and escape through the original drain.
The final sunset-lake scene shows a seated girl and asks you to choose **Live quietly (Peace)** or **Become the world (Dominion)**.
Waiting or skipping cannot choose for you. Cinematics freeze the live city; the final summary appears after your explicit choice.

The package includes an illustrated `COMPLETE_GAME_GUIDE.md` with its screenshots, plus detailed `CONTROLS.md`, `KNOWN_ISSUES.md`, `TEST_REPORT.md` and `THIRD_PARTY_ASSETS.md`. Keep the `Screenshots` folder beside the guide. Consult `TEST_REPORT.md`
for this artifact's actual validation results. Bundled font and audio licence texts are in `Licenses/`.
Physical gamepad operation and an extended human playthrough remain unverified.
