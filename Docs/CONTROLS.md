# PARASITE — Controls & How It Plays

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
| Esc | Start | Pause (Resume · Restart · Controls · mouse sensitivity · Quit) |
| F1 | — | Show / hide the controls card |
| F3 | — | FPS counter (launch with `-showfps` to start with it on) |

Menus work with the mouse, or with arrows/WASD/D-pad + Enter/A.

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

Inside the lab, find Dr. Vale's office and confront him. The end screen shows your route, time, hosts possessed,
peak suspicion and captures.
