# PARASITE 0.6.0 preview — controls and play

The 0.6.0 preview has fourteen city missions, three riverside missions, three island missions and three Harbor missions:
**23 in all**. The separately retained 0.5.1 player still has twenty missions. Use the `BUILD_INFO.json` beside your executable
to identify the build; these controls do not imply complete human, physical-controller or graphical validation.

## Opening and deliberate continuation

**Begin Infiltration** starts the eighty-second opening. Skip with **Space**, **Enter** or **Esc**, or gamepad **A/South** or
**B/East**, then release the input to enter gameplay. Restart and checkpoint reloads do not replay it. Live gameplay freezes
during protected story presentation.

After mission 14, the sunset lake requires **Live quietly (Peace)** or **Become the world (Dominion)**. Use the mouse or menu
navigation and confirm. Waiting, Escape and dialogue-skip input never choose an outcome. The first choice is locked.
Its summary offers **Continue · After the lake** for missions 15–17. The ferry summary then offers **Continue · The Far Shore**
for missions 18–20. The island rescue summary offers **Continue · The Working Harbor** for missions 21–23.
Continuation preserves the original choice, earlier flags and campaign statistics.

## Controls

| Keyboard / mouse | Gamepad | Action |
|---|---|---|
| W A S D or arrows | Left stick | Move |
| Mouse | Right stick | Look |
| Left Shift | Left stick click | Sprint |
| Space | A / South | Jump; hop as a free amoeba |
| E | X / West | Possess the aimed-at person; interact; eject when a host is captured |
| Q | B / East | Leave the current host; the host is unconscious for eight seconds |
| V | Y / North | Split off a second amoeba, up to two |
| Tab | Right bumper | Switch the active amoeba |
| F | Left bumper | Use the current host's profession ability |
| Esc | Start | Pause; return from a pause submenu before resuming |
| F1 | — | Show or hide the controls card |
| F3 | — | Toggle the FPS counter; `-showfps` starts with it visible |
| Left or right Ctrl + M | — | Presentation shortcut to the original escape and explicit lake choice |

Menus accept mouse input or keyboard/D-pad navigation and Enter/A confirmation. Gamepad B returns from controls/settings;
Esc/Start returns from pause submenus. Audio and sensitivity sliders apply/save immediately. Graphics are staged until Apply;
Cancel/Back discard only unapplied graphics and preserve the pause state and saved audio/input values.

## Graphics settings

- **Display:** game render scale50–150%, VSync and frame limit(Unlimited/30/60/90/120/144/240). The cap is used when VSync is off;
  compositor/display behavior can still limit presentation. Menus/captions retain output resolution.
- **Quality:** post AA(Off/FXAA/SMAA), separate MSAA(Off/2x/4x), shadows/distance, ambient occlusion and reflection detail.
  Unsupported sample counts show the renderer's effective value. High/Ultra reflections can use real surrounding captures;
  these are not hardware ray tracing.
- **Effects:** Bloom, grain and chromatic aberration for real-time scenes. Movie lighting/effects are already rendered into the footage.
- **Audio & Input:** live volume buses and mouse sensitivity; these stay saved when a graphics draft is cancelled.

Use mouse, Up/Down or D-pad to focus a row, Left/Right to change a choice and scroll wheel/drag to move a page. Apply saves;
Cancel reloads applied graphics; Defaults stages the normal starting profile; Back discards an unapplied draft. A changed profile
shows **Custom**, retaining its named baseline for other quality budgets. Selecting quality preserves separately chosen VSync/frame cap.
Older builds can have a different menu. Queued-input automation and physical keyboard/gamepad behavior are separate checks.

Ctrl+M works from the initial title, opening, pause menus, Vale dialogue and gameplay. A request waits through loading or body
changes. An ending already underway continues without restarting or choosing for you. The shortcut skips the remaining original
campaign for presentation and is ignored once the later chapter has started.

## Possession, splitting and work

Aim at a person within **five metres** and press **E** when the contextual possession prompt appears. **Q** leaves that body;
other people can notice the resulting unconscious host. **V** creates your second free amoeba; **Tab** changes which one you
control. The inactive half stays where you left it. Splitting or possessing in sight is suspicious.

At a timed control, press **E** to begin and stay close while progress fills. Paired controls need separate amoebas in different
hosts. Leave the supporting host at its console while the other works; releasing, changing or losing that host cancels its
support. Selecting the other amoeba does not by itself release the original host. After a checkpoint reload, find new hosts
and rearm unfinished cooperative controls. Completed progress remains within the current running process; there is no disk save.

| Host | Useful access and ability |
|---|---|
| Civilian | Public areas; F throws a distraction where you aim |
| Guard | Security badge and dispatch controls; F directs other nearby guards to investigate the aimed-at location |
| Scientist | Staff terminals and analysis; F lowers suspicion by 30 outside pursuit |
| Maintenance | Service routes and machinery; F disables cameras within 25 m for twenty seconds |

Abilities have cooldowns shown by the game. Read the E prompt for profession, prerequisite, range and pursuit restrictions.
Security cameras can spot free amoebas, trespassing hosts and bodies. Suspicion rises through **Calm**, **Curious**,
**Investigating** and **Pursuit**; break sight and change bodies. A burned host stays known to bio-scanners. If captured in a host,
press E during the ejection window. Losing every playable amoeba returns you to the checkpoint.

## Mission route

Follow the objective panel, compass and yellow waypoints. The [complete guide](COMPLETE_GAME_GUIDE.md) gives route details;
the [riverside guide](AFTERLAKE_GUIDE.md) expands missions 15–17 and the [Harbor guide](HARBOR_GUIDE.md) covers 21–23.

1. **OUT OF THE DRAIN:** leave the alley and take a host.
2. **WORD ON THE STREET:** read the plaza newsstand and listen at the café.
3. **THE TAIL:** follow Petrov without crowding or losing her; collect the dead drop.
4. **CREDENTIALS:** choose guard/main gate, scientist/keycard/staff terminal, or maintenance/power/service tunnel.
5. **VALE'S RESEARCH:** copy both research terminals and use two staff hosts at the office readers.
6. **CREATOR:** confront Vale; gameplay resumes after the protected reveal.
7. **HUNTER PROTOCOL:** evade bio-scans, cross the arch as free amoeba and trip the duty-officer breaker.
8. **THE PART HE HID:** maintenance holds coolant while another scientist copies the signed archive.
9. **BREAK THE LEASH:** a guard holds dispatch while another maintenance host overloads the relay.
10. **THE SHADOW UPLINK:** broadcast preliminary evidence without pursuit.
11. **THE BLACK SITE ARCHIVE:** create a diversion and obtain the archive as a scientist.
12. **CONTAINMENT PROTOCOL EPSILON:** overload backup power and override the security grid.
13. **BLACKOUT:** cut three transformers within the limit using three distinct human palm prints.
14. **THE FINAL CONVERGENCE:** broadcast the archive and return to the original drain as a free amoeba; choose at the lake.
15. **AFTER THE STILL WATER:** answer Petrov and isolate the contaminated floodgate.
16. **BORROWED HEARTS / A WILLING NETWORK:** analyse runoff and coordinate guard/scientist hosts at the shelter records.
17. **THE OPEN CHANNEL / THE FIRST MANY:** power the mast, transmit with another host and leave all hosts before the ferry.
18. **UNDER THE WATERLINE:** reach the island refuge by tidal culvert as a blob or service gate as maintenance.
19. **THE QUARANTINE WALK / EVERY VOICE:** escort protected survivors on Peace or complete marked courier handoffs on Dominion.
20. **A LIGHT IN THE FOG / A SIGNAL BEYOND THE ISLAND:** carry and mount the signal, board every passenger and depart with all amoebas free.
21. **A MEASURE OF PRESSURE:** Maintenance balances A (+3), B (+2), C (−1) at 4 for two seconds; overflow safely resets the handles.
22. **COLD CHAIN / SIGNAL IN TRANSIT:** Scientist scans moving A17/A32 on Peace or R09/R21 on Dominion; wait for physical bay arrivals.
23. **AN OPEN FREQUENCY / TWO MINDS, ONE SIGNAL:** two free bodies fully on separate pads hold a clear, stationary three-second link after E.

On Peace, ask survivors to follow with E in a civilian/scientist host and lead them through cover they physically reach.
Losing or releasing the leader requires a fresh E regroup, even if you reclaim the same body. On Dominion, mark each possessed
courier's own handoff with E, walk to their named lane and release with Q; wait for the eight-second recovery and autonomous arrival.

The signal case must be carried to its mount in the same human host. Tab and pause preserve valid custody; release, capture,
host change or checkpoint reload returns an unmounted case to the cache. Before rescue departure, every passenger must be awake,
unoccupied and physically on deck. Release every borrowed host with Tab/Q, bring every surviving free half aboard, finish pending
leaps and lose pursuit. Merely standing on the gangway is insufficient.

In the Harbor, E toggles a valve, scans the lot currently in the arch, or starts a relay attempt; none of those actions requires
holding the key down. The optional Maintenance brake lasts five gameplay seconds. A relay needs both whole grounded free bodies
on separate pads, no pursuit and clear emitter sight lines. Movement, capture, possession or obstruction cancels it; pause freezes
it, and checkpoint reloads restart it. See the [Harbor guide](HARBOR_GUIDE.md) for recovery details.

Campaign progress/checkpoints last only in this running game; closing it does not create a campaign save. Graphics/audio/input
preferences persist separately. Automated simulated movement and queued input do not establish a complete human playthrough,
physical keyboard/gamepad behavior or physical speaker output.
