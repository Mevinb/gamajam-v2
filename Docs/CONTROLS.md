# PARASITE 0.2.2 (local) — Controls & How It Plays

## Opening and final choice

**Begin Infiltration** starts the 80-second backstory. Skip with **Space**, **Enter** or **Esc** (gamepad **A/South** or **B/East**),
then release the key/button to enter gameplay. Checkpoint reloads and Restart do not replay the opening.
The live city, guards and capture timers freeze during the opening, Vale's reveal and the final cinematic.

After the eight missions, the final scene shows a girl seated beside a sunset lake. Choose **Live quietly (Peace)** or
**Become the world (Dominion)** using the mouse or menu navigation and confirm. Waiting, Escape or a dialogue-skip input never
chooses an outcome. Your first choice is locked; the final statistics follow its resolution.

## Controls

| Keyboard / Mouse | Gamepad | Action |
|---|---|---|
| W A S D (or arrows) | Left stick | Move |
| Mouse | Right stick | Camera |
| Shift | Left stick click | Sprint |
| Space | A / South | Jump (hop as a blob) |
| E | X / West | Possess the person you aim at · interact (doors, terminals, Dr. Vale) · **eject** when captured |
| Q | B / East | Leave the current host (the host stays unconscious for 8 s) |
| V | Y / North | Split off a second amoeba (max 2) |
| Tab | Right bumper | Switch control to the other amoeba |
| F | Left bumper | Host ability: civilian **distraction** · guard **radio dispatch** · scientist **professional calm** (−30 suspicion) · maintenance **cut the power** (cameras off 20 s) |
| Esc | Start | Pause (Resume · Restart · Controls · Settings · Quit); return from a submenu before resuming |
| F1 | — | Show / hide the controls card |
| F3 | — | FPS counter (launch with `-showfps` to start with it on) |
| Ctrl + M | — | Judges' presentation shortcut: jump to the final escape and sunset-lake choices, including from the initial title |

Menus work with the mouse, or with arrows/WASD/D-pad + Enter/A.
Settings in both the title and pause menus save graphics quality, mouse sensitivity, master volume and ambience/music.
Sliders apply immediately. Gamepad B returns from controls/settings; Esc/Start also returns from pause submenus.

For a short judges' presentation in local v0.2.2, press **Ctrl + M** from the title screen, opening, pause menus, Vale dialogue or gameplay to jump to the final escape and sunset-lake epilogue. Requests wait safely through loading or body changes. An ending already underway continues without restarting or choosing for you.
This skips the remaining campaign for the presentation. You still choose **Live quietly (Peace)** or **Become the world (Dominion)**;
the shortcut never chooses an outcome automatically.

## Possession
Aim at a person within **5 m** (a marker and an `[E] Possess …` prompt appear) and press **E**: the amoeba leaps
into them and you control their body. People further away show a red "too far" prompt. **Q** leaves the host;
they collapse, unconscious, for 8 s (a timer ring floats above them). Anyone who finds an unconscious body becomes
suspicious.

## Split & switch
**V** splits off a second free amoeba next to you (max two; numbered **1** and **2** in the HUD and above each
body). **Tab** swaps control between them; the other one waits where it is. Use one to scout or hold a host while
the other moves. Splitting where someone can see it is suspicious.

## Suspicion
The meter at the top runs 0–100: **CALM** (0–29) → **CURIOUS** (30–59) → **INVESTIGATING** (60–84) →
**PURSUIT** (85–100). It rises when people see your blob, see a possession or a split, find a knocked-out body,
or catch a host somewhere their job doesn't allow. It decays once nobody is watching. NPCs show **?** (curious)
or **!** (alarmed) above their heads. In pursuit, guards try to grab you: a captured host gives you a few seconds
to **press E and eject** (an "ESCAPE WINDOW" follows, while guards are stunned). A guard catching a free blob
contains it; if no amoeba is left you return to the last checkpoint.

## Routes into Vale Biotech
| Host | Credential | Route |
|---|---|---|
| Security Guard | Security badge | **Main Gate** checkpoint |
| Scientist | Staff credentials | **Staff Terminal** (unlocks a staff door) |
| Maintenance Worker | Maintenance keys | **Service Tunnel** |

Inside the lab, find Dr. Vale's office and confront him. His reveal starts three more missions; you can keep playing.
The final end screen follows the evidence broadcast, storm-drain escape and your explicit lakeside choice, showing your chosen
future, original infiltration route, total time, hosts possessed, peak suspicion and captures across the campaign.

## Missions

The eight-mission campaign shipped in v0.2.0. Local v0.2.2 adds global presentation-shortcut handling; its Linux rebuild,
8/8 focused PlayMode tests and 14/14 built-player queued-keyboard checks passed. Check `TEST_REPORT.md` for each version's
actual coverage; the full campaign suite was not rerun for this shortcut fix.

1. **Out of the Drain** — leave the storm-drain alley, take your first host.
2. **Word on the Street** — read the newsstand on City Plaza, eavesdrop on Dr. Petrov at the café.
3. **Credentials** — pick a way in: guard → Main Gate · scientist + café keycard → Staff Terminal ·
   maintenance + Substation 4 reset → Service Tunnel.
4. **Vale's Research** — download files at the Lab A and Lab B terminals; Dr. Vale's office has a two-person rule:
   split (V), put each amoeba in a staff host, badge both readers within 8 s (Tab to switch).
5. **Creator** — confront Dr. Vale. His dialogue reveals that the research you stole was bait; gameplay resumes afterward.
6. **The Part He Hid** — as **maintenance**, press **E** at the service-room coolant bypass and leave that body there.
   **Tab** to the other amoeba, possess a **scientist** and copy Vale's signed archive in his office for **8 seconds**.
   The two different hosts must stay at their respective terminals. Moving or losing either host interrupts the copy.
7. **Break the Leash** — put a **guard** at the lobby dispatch override and press **E** to hold it. **Tab** back to a
   **maintenance** body and overload the east service-alley containment relay for **7 seconds**. The guard must stay at dispatch.
   Success permanently disables security cameras and opens the campus exits.
8. **Outside the Experiment** — as **any host**, send the signed evidence through the **City Plaza journalist uplink** for
   **10 seconds**. A pursuit blocks or interrupts the upload. After it succeeds, return to the south storm-drain alley,
   leave your host with **Q**, and press **E** at the drain as a **free amoeba** to escape and reach the lakeside epilogue.
   Choose **Live quietly** or **Become the world** to decide your future before the final summary. There is no automatic choice.

Paired consoles need a separate amoeba at each position; one host cannot operate both. Press **E** once to start work, then stay
close while progress fills. Completed evidence, sabotage and broadcast objectives survive checkpoint reloads. After a reload,
reacquire hosts and rearm any unfinished paired controls; temporary body positions are not saved.

Yellow waypoints show where to go. Security cameras (red blinking lamp) sweep the campus and lab: they spot free
amoebas, trespassers and bodies. A maintenance host can cut their power with F.
