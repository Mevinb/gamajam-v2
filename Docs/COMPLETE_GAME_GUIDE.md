# PARASITE — complete game guide

**Linux x86-64 · 0.6.0 preview · 23 missions: city 1–14, riverside 15–17, island 18–20, Harbor 21–23**

You are Specimen 7, an escaped amoeba that can possess humans and split into two independent bodies. A borrowed host changes
where you can go and which controls you can use. Many later tasks need one host to hold a console while your other body acts.

The original story ends at the lake with an explicit Peace or Dominion choice. **Continue · After the lake** starts the riverside;
its ferry summary offers **Continue · The Far Shore**. The island rescue summary then offers **Continue · The Working Harbor**.
The first choice stays fixed and changes later story, roles and cargo requirements.

**Spoilers below.** For an unspoiled start, read the controls and host roles, then follow the yellow waypoints. This guide
explains the 0.6.0 preview mechanics. Check the `BUILD_INFO.json` beside your executable: the separately retained 0.5.1 player
remains a twenty-mission build. A walkthrough is not a full playthrough or final validation report.

## Getting started

From the extracted candidate folder, run the `./play.sh` beside its executable. Keep the executable, `Parasite_Data` and the Unity
libraries together. If permissions were lost during transfer, run `chmod +x play.sh Parasite.x86_64` in the extracted folder.
The supplied launcher selects NVIDIA offload where available and prefers Vulkan. To select OpenGL, use
`PARASITE_GRAPHICS_API=glcore ./play.sh`. A project-root launcher may select an older retained build.

Choose **Begin Infiltration**. The eighty-second opening tells the backstory before giving control at the storm drain.
Press Space, Enter or Esc and release to skip; gamepad South/East is also bound. Restart and checkpoint reloads do not replay it.
Settings on the title and pause menus save graphics, sensitivity and audio levels.

## Controls

| Input | Action |
|---|---|
| WASD / mouse | Move / look |
| Shift | Sprint |
| Space | Jump or hop |
| E | Aim at a person to possess, or at a control to interact; captured hosts can eject |
| Q | Leave the current host |
| V | Split, up to two amoebas |
| Tab | Switch active amoeba |
| F | Use the host profession's ability |
| Esc | Pause; close a submenu before resuming |
| F1 / F3 | Controls / FPS |
| Ctrl + M | Presentation jump to the original drain escape and explicit lake choice |

Aim inside five metres for possession. If the close-range prompt asks you to step back, make space and aim at the host's upper body. Read the contextual E prompt: it explains profession, permission and prerequisite
requirements. The objective panel and compass show the active mission and controls. Esc also opens the scrollable mission journal.

The presentation shortcut works from initial title, opening, pause, dialogue and gameplay. It never chooses a lake outcome.
It is ignored in the later chapter to preserve its progress. Queued-input tests do not substitute for testing physical controls.

## Host roles and survival

- **Civilian:** blends into public places, but lacks restricted credentials.
- **Guard:** opens security gates, holds dispatch and uses security controls.
- **Scientist:** uses staff terminals, copies research and operates scientific equipment.
- **Maintenance:** powers service routes, disables cameras and operates machinery.

Possessing someone in view, walking as a free amoeba in front of witnesses, or trespassing can raise suspicion. Break sight,
change bodies and move away. A burned host remains known to bio-scanners, so leaving that host matters more than simply waiting.
If a guard captures your body, eject and escape. If no playable amoeba survives, the last checkpoint reloads.

The inactive amoeba stays where you left it. A cooperative console stays held only while its supporting host remains in range.
Use V/Tab to assign the other amoeba and a different host to the paired terminal. Leaving, capture or pursuit can cancel work.
Completed chapter flags survive checkpoint reloads, while transient console ownership must be rearmed. Campaign checkpoints
are in-process state, not an on-disk save.

## Missions 1–6: find the creator

1. **OUT OF THE DRAIN:** leave the alley and take the first host. Learn E/Q possession and V/Tab splitting.
2. **WORD ON THE STREET:** read the plaza newsstand, then listen to Petrov at the café.
3. **THE TAIL:** follow Petrov through Market Street and Avenue North. Stay about 4–20 metres away and stop during her glances.
   Crowding her or losing the trail sends her back to her last leg checkpoint. Take the dead drop when she leaves it.
4. **CREDENTIALS:** choose an infiltration route: a guard opens the main gate; a scientist obtains the café keycard and uses the
   staff terminal; maintenance restores power and uses the service tunnel. You need only one route to continue.
5. **VALE'S RESEARCH:** copy both research terminals, then badge both office readers with separate staff hosts within eight seconds.
6. **CREATOR:** confront Dr. Vale. The reveal is protected dialogue; gameplay resumes when it ends. This is the midpoint.

## Missions 7–10: expose the experiment

7. **HUNTER PROTOCOL:** avoid the hunters' scans. The scanner arch admits a free amoeba rather than a human host. Borrow the
   duty officer to trip the breaker and recall the hunters. A burned body needs to be abandoned.
8. **THE PART HE HID:** keep maintenance at the coolant control while the other amoeba, in a scientist host, copies the signed
   archive for eight seconds. Plan both bodies before starting the download.
9. **BREAK THE LEASH:** a guard holds lobby dispatch while a different maintenance host overloads the east-alley relay for seven
   seconds. The sabotage permanently disables the relevant CCTV and opens exits.
10. **THE SHADOW UPLINK:** reach the plaza uplink and transmit preliminary evidence for ten seconds without pursuit.

## Missions 11–14: the black site and escape

11. **THE BLACK SITE ARCHIVE:** a guard triggers the sub-vault evacuation diversion at lobby security; a scientist breaches Lab A
    and downloads the black-site mainframe for ten seconds.
12. **CONTAINMENT PROTOCOL EPSILON:** maintenance overloads the east-alley auxiliary generator, then guard/scientist overrides the
    forecourt security-grid pylon for eight seconds.
13. **BLACKOUT:** cut all three plaza transformers within one thirty-five-second reroute window. Every switch logs a different
    human palm print. Pre-position your two bodies and arrange a third host for the last switch. Missing the window restores
    the circuits and clears the palm-print log, allowing another attempt.
14. **THE FINAL CONVERGENCE:** transmit the unredacted archive at the plaza master antenna for twelve seconds. Eject with Q and
    return as a free amoeba to the original south-alley storm drain. The escape leads to the sunset lake.

## The lake: an explicit future

The seated girl at the lake represents your new life after the city. Watch the arrival or skip to the choices. Neither a timer
nor a skip input chooses. Release held skip input before the choices become active.

- **Live quietly (Peace):** keep a small life and leave other minds their own.
- **Become the world (Dominion):** begin a wider network under one will.

The first resolved choice is immutable. Its resolution leads to the original campaign statistics. You can stop there, restart,
return to the title, or choose **Continue · After the lake**. Continuing preserves statistics and the chosen future, places you
at a riverside landing as one free amoeba, and appends three missions.

## Missions 15–17: after the still water

15. **AFTER THE STILL WATER:** borrow a host to answer Petrov's emergency receiver, then use maintenance to isolate a contaminated
    floodgate. Peace helps despite a promise of quiet; Dominion seeks a new foothold.
16. **BORROWED HEARTS / A WILLING NETWORK:** a scientist analyses polluted runoff. Split with V, keep a guard holding shelter
    dispatch, and use another scientist host to protect or acquire evacuation records.
17. **THE OPEN CHANNEL / THE FIRST MANY:** maintenance holds mast power while another host transmits. Peace accepts a civilian
    or scientist; Dominion accepts a guard or scientist. Keep pursuit clear, then eject and use the ferry intake as free amoeba.

See the [post-lake walkthrough](AFTERLAKE_GUIDE.md) for precise cooperative-control and checkpoint recovery instructions.

## Missions 18–20: The Far Shore

**Build scope:** the 0.6.0 preview includes this full island chapter and the Harbor continuation below. The separately
retained 0.5.1 build stops after mission 20. The older 0.5.0 preview contains mission 18 but not 19–20; this guide does not
upgrade either older executable.

After mission 17's ferry summary, choose **Continue · The Far Shore**. The first lake outcome, earlier flags and statistics
persist. Travel reunites your free halves into one amoeba at the island jetty; the island adds no third amoeba capacity.

### 18. UNDER THE WATERLINE

Reach the inner refuge by either route; both are optional alternatives to the same arrival objective.

- **Free amoeba:** use the low tidal culvert. Its repeating gameplay-time cycle is **6 seconds low tide, 2 seconds rising-water
  warning, then 4 seconds closed surge**. Watch the lamp and countdown. A surge returns an exposed blob to its last physically
  visited dry point. Initially that is the entry landing; the right-hand sheltered bay beyond the sluice becomes a retry only
  after that same blob reaches it. The bay shelters you from the surge. These local tide retry points reset with the scene.
- **Maintenance:** borrow the **Island jetty engineer** on the public outer jetty. Use **E** at the mechanical service latch,
  then walk through the human service gate. The latch is immediate, not a held console. A human cannot fit the low culvert.

Continue to the dry-refuge marker to establish the island checkpoint. Missions 19–20 then introduce Riley, Morgan and Avery.

### 19. THE QUARANTINE WALK / EVERY VOICE

**Peace — THE QUARANTINE WALK:** borrow the nearby **Island refuge volunteer** (civilian) or **Island relief scientist**.
Aim at a survivor and use **E** to ask them to follow, then lead them to their clinic destination. Their minds remain protected;
you cannot possess them. Keep close and use cover. The person must actually reach a cover point to save it: the leader merely
touching a waypoint is insufficient. Sustained guard sight sends a follower back to visited cover. Civilian **F** distractions
or a second borrowed guard body can help clear the route.

**Tab** may select your other amoeba while the original host stays possessed as leader. Releasing, losing or changing that
host cancels the agreement; even reclaiming the same body needs a fresh **E** regroup. An unboarded survivor returns to visited
cover (the holding court if none was reached), or the clinic if already delivered. Each actual clinic arrival commits separately.

**Dominion — EVERY VOICE:** possess each courier in turn. Use the **Mark this courier's handoff [E]** prompt while controlling
that courier, walk to their own named handoff lane, then release with **Q** close to it. Allow the normal eight-second
unconscious recovery and their autonomous walk beyond the lane. Arriving while still possessed does not complete delivery.
Releasing away from the lane does not count. After capture or renewed possession, make a fresh mark-and-release attempt.
Delivered couriers become protected from further possession. Complete all three people before taking the rescue signal.

### 20. A LIGHT IN THE FOG / A SIGNAL BEYOND THE ISLAND

Use **E** at the signal cache in an ordinary human host; the **Island supply marshal** stands nearby. Carry the physical case
to the northern flare mount and use **E** there in that same carrier. Switching selected amoeba with **Tab** or pausing preserves
valid custody; releasing, capture, changing host or checkpoint reload returns an unmounted case to its cache. The pickup flag
alone is not inventory. A mounted signal persists and establishes the rescue-jetty checkpoint.

For **Peace**, regroup in a civilian/scientist host and lead the survivors from the clinic up the gangway. For **Dominion**,
delivered couriers walk toward the mounted signal and their boat berths themselves. Wait for all three to finish parking;
standing on the gangway is not boarding. Spare **Island rescue volunteer** and **Island launch engineer** hosts remain near the
jetty if you need a fresh body.

For departure, all three named passengers must be awake, unoccupied and physically on the deck. Release **every** borrowed host:
use **Tab/Q** for each owned amoeba, including inactive halves, then walk every surviving free half aboard. Wait for possession
leaps to finish and lose active pursuit. Only then use **E** at the rescue intake. A protected departure plays before the island
summary; the original lake choice remains unchanged.

**Recovery:** checkpoints preserve each person's delivery, boarding and actually visited cover separately. Undelivered people
restore at visited cover or the holding court; delivered people restore at the Peace clinic or their Dominion handoff, and
boarded people restore at their berths. No duplicate people or extra amoebae are created. Resume unfinished escorts or courier
attempts with a fresh E interaction; collect an unmounted case again. A completed mounting needs no repeat. Campaign progress
lasts within this running session; this is not an on-disk save.

## Missions 21–23: The Working Harbor

After the island rescue departure, choose **Continue · The Working Harbor** on its summary. The earlier choice and statistics
persist; you arrive as one free amoeba. This is a deliberate continuation, not an automatic scene or a new choice of future.

### 21. A MEASURE OF PRESSURE

Borrow Maintenance and toggle **A (+3), B (+2), C bleed (−1)** with E. The target and maximum pressure are **4**. One safe order
from closed is **C → A → B**. At 4, wait **two gameplay seconds** for the physical gate to lift. A+B without bleed exceeds the
maximum and safely resets all three handles, without moving or damaging anyone. Retry the puzzle rather than forcing the gate.
Pause freezes it. Checkpoints retain earned handle settings but restart an unfinished two-second balance; a completed gate stays open.

### 22. COLD CHAIN / SIGNAL IN TRANSIT

As Scientist, press **E at the scanner while the required physical lot is inside the moving arch**. Peace needs antidote lots
**A17 and A32**; Dominion needs relay lots **R09 and R21**. The prompt shows the actual current/next lot and scan window. An
empty arch, wrong lot or wrong profession is denied. Each normal moving window lasts about two gameplay seconds.

Maintenance can brake the main belt for **five gameplay seconds** as an optional two-role strategy. One scientist can instead
time both scans. A validated lot physically diverts to its bay; both actual arrivals are required. Stay outside the fenced trench.
After a checkpoint, scanned but undelivered lots resume from separate bay approaches and must finish moving; delivered lots
remain in place, unscanned lots circulate, and no duplicate crate is added. The brake must be activated again after a reload.

### 23. AN OPEN FREQUENCY / TWO MINDS, ONE SIGNAL

Split with **V** if needed. Use **Tab/Q** to release every host, then put **two distinct FREE amoebas fully on separate pads**.
Both must be grounded, clear of pursuit and able to see their emitter points. At either nearby node, press **E once** and keep
both bodies still for **three gameplay seconds**. Moving, leaving a pad, capture, possession, pursuit or a blocked beam cancels
progress and needs a new E attempt. Pause freezes the timer; resume continues only if the requirements still hold. Checkpoints
restart an unfinished handshake. The completed link leads to the branch's captioned resolution and summary.

The [Harbor walkthrough](HARBOR_GUIDE.md) gives the valve solution, lot timing and recovery details. There is still a two-amoeba
limit, and the original Peace/Dominion choice remains unchanged.

## Graphics, progress and credits

Title/pause Settings separates Display, Quality, Effects and Audio & Input. Stage graphics, then choose **Apply** to save them;
**Cancel/Back** discard the draft. Audio and sensitivity apply immediately. The High baseline uses 100% game render scale;
Ultra requests 125% scale and 4x MSAA. Post AA and MSAA are separate; unsupported samples fall back to the renderer's effective value.
Custom choices persist, while selecting a quality preset keeps VSync/frame cap. Real-time effects do not change the story movies.

The Linux player uses real-time URP shaders, screen-space reflections and probes. Blender story films use offline RTX/OptiX
path tracing; gameplay does not use built-in hardware ray tracing. Existing voices are synthesized. The new Harbor scenes use
captions without newly recorded narration.

**Campaign progress lasts within the running game, not a disk save.** Checkpoints survive scene reloads in that session; closing
the game or beginning a new run loses that campaign. Graphics/audio/input preferences persist separately. This preview's
walkthrough does not establish a complete human playthrough, physical-controller pass or final performance result.

Keep the supplied licence texts and [asset credits](THIRD_PARTY_ASSETS.md) with the player when sharing it.
