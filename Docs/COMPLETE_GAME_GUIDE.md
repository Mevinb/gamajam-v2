# PARASITE — complete game guide

**Linux x86-64 · Fourteen initial missions, three riverside missions and three island missions in later source**

You are Specimen 7, an escaped amoeba that can possess humans and split into two independent bodies. A borrowed host changes
where you can go and which controls you can use. Many later tasks need one host to hold a console while your other body acts.

The original story ends at the lake with an explicit Peace or Dominion choice. Its summary offers **Continue · After the lake**
to start the riverside chapter. The chosen future persists and changes that chapter's story and radio roles.

**Spoilers below.** For an unspoiled start, read the controls and host roles, then follow the yellow waypoints. This guide
explains current source behavior; the exact built-player validation belongs to [TEST_REPORT.md](TEST_REPORT.md).

## Getting started

From the project root or the extracted Linux build folder, run `./play.sh`. Keep the executable, `Parasite_Data` and the Unity
libraries together. If permissions were lost during transfer, run `chmod +x play.sh Parasite.x86_64` in the extracted folder.
The project launcher selects NVIDIA; extracted builds can use PRIME offload with
`__NV_PRIME_RENDER_OFFLOAD=1 __GLX_VENDOR_LIBRARY_NAME=nvidia ./play.sh`.

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

Aim inside five metres for possession. Read the contextual E prompt: it explains profession, permission and prerequisite
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

**Build scope:** the retained **0.5.0-preview** playtest binary contains mission 18 only. Missions 19–20 here describe the
later live source. Use [TEST_REPORT.md](TEST_REPORT.md) for exact rebuilt-player and validation scope; this walkthrough does
not upgrade the retained binary or establish a human playthrough.

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

Continue to the dry-refuge marker to establish the island checkpoint. The later M19–M20 source then introduces Riley, Morgan
and Avery.

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

## Graphics and cinematics

Low/Medium/High/Ultra trade resolution, shadows, anti-aliasing, ambient occlusion, local lights and reflection quality. Ultra
uses 125% render scale and 4x MSAA and can be expensive on a 6 GB GPU; benchmark results must name the settings. Try High if frame times
are uneven. F3 shows FPS. Presets should apply consistently to new cinematic cameras as well as the gameplay camera.

The new sets are authored in Blender: containment laboratory, service conduit, slatted bench and detailed shoreline.
The current upgrade also adds RTX/OptiX path-traced Blender film inserts. Real-time Linux gameplay remains URP with screen-space
reflections and probes; this is distinct from hardware ray tracing. See the current test report for actual footage/player-decode
verification, and the handoff for rebuild instructions.

![Blender-authored laboratory rendered in Unity](Screenshots/upgrade/01-blender-laboratory.png)

![The corrected, illuminated service conduit](Screenshots/upgrade/02-blender-conduit.png)

![RTX-rendered lake arrival playing beneath Unity story captions](Screenshots/upgrade/03-rtx-lake-arrival.png)

These are real Editor/GPU verification captures. The lake image shows the decoded Blender film; the laboratory and conduit
show the authored game sets. They are not a claim of an extended human playthrough or final-player benchmark.

## Verification and credits

[TEST_REPORT.md](TEST_REPORT.md) names exact tested artifacts, suites and remaining checks. Automated scenarios use simulated
movement/queued input and do not establish a human playthrough or physical gamepad pass. Old screenshots and benchmarks belong
to their original build, not automatically to the current upgrade.

[KNOWN_ISSUES.md](KNOWN_ISSUES.md) records validation limits. [THIRD_PARTY_ASSETS.md](THIRD_PARTY_ASSETS.md) records models, textures,
fonts, animation and audio provenance. [HANDOFF.md](HANDOFF.md) describes source architecture, generated scenes and serialized Unity
commands. [UPGRADE_TRACKER.md](UPGRADE_TRACKER.md) preserves the full active expansion request and completion criteria.
