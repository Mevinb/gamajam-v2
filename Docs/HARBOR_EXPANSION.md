# Working Harbor — 0.6.0-preview source

The working tree implements missions 21–23 after the island rescue. The running user player is still the separate
0.5.1 build with twenty missions. No Harbor native-player, rendered-gameplay or human acceptance result is claimed.

## Playable chapter

A maintenance host balances feeds A (+3), B (+2) and bleed C (-1). Pressure must stay at 4 for two scaled seconds before
the physical collision gate lifts. Overflow safely resets every valve. Earned handle positions survive a checkpoint;
the unfinished balance interval restarts.

A scientist scans moving physical lots in the actual arch: Peace requires A17/A32 antidote, Dominion R09/R21 relay
consignments. Scanning changes a route; delivery requires both the rigidbody and displayed crate to reach its assigned
bay. The optional maintenance brake lasts five scaled seconds. Four stable crate bodies survive reload without duplication.

The relay requires two distinct free amoebae fully on separate pads, clear physical sight lines and no pursuit. Normal
E starts a fresh three-second handshake. Ownership, movement or an obstructed beam cancels it; pause freezes it. Both
bodies must remain free before the finite branch resolution publishes its summary.

The island summary offers **Continue · The Working Harbor**. Its guards retain the first lake choice and require the
completed prior chapters. The annex is built before normal navigation and NPC creation. The protected arrival is a finite,
caption-only presentation; no new prerecorded narration is claimed.

## Verified evidence

[The chapter proof](verification/harbor-chapter-20261126.json) pins logs, XMLs, process exits and source hashes. These are
Null-device Unity Editor PlayMode results with simulated input.

- Continuation guards and the real summary button pass 1/1; pressure overflow, pause, transient recovery and physical gate
  crossing pass 1/1. Both Editor processes exit 0.
- Earlier complete Peace and Dominion runs pass separately in 131.471 and 142.599 seconds, exit 0. These precede the final
  arrival-lifecycle source correction. The final-source run passes both branches in 272.328 seconds: Dominion 142.972496,
  Peace 129.292570; 2/2 pass, 0 failures/skips, Editor exit 0 (root session 40089, chunk `fe4e20`). Its source hashes include
  the 0.6.0-preview version and scenarios 25/26/27 registrations.
- The in-flight cargo regression first fails in 55.295 seconds, exit 2: two genuinely scanned lots reload at the same arch
  point with 1 m of actual box penetration. The corrected run passes in 56.684 seconds, exit 0. All six crate pairs are checked
  without overlap after reload and during recovery; both crates then physically reach their bays before the manifest advances.
- Recovery retains earned sorting and deliberately restages each undelivered lot on its own three-metre final approach.
  It does not preserve the exact previous world position or grant delivery on reload. The recorded post-ready travel is about
  2.948 m over 1.354 scaled seconds; both body and displayed pose must arrive.
- Arrival cancellation initially fails both lifecycle tests, exit 2. Disabling a running owner still emitted a late cue,
  two banners and `ArrivalSeen`; disabling/re-enabling before Start could leave play paused. Permanent retirement, coroutine
  cancellation and manager/scene ownership checks produce 2/2 passes in 14.614 seconds, exit 0, with the original full-window
  observation retained.
- The brake diagnostic measured zero physical drift while displayed poses settled about 15.7 mm. The fixture retains a 1 mm
  immediate rigidbody check, allows bounded natural interpolation settling, then retains the 1 mm display-stability check.
  The production brake, interpolation and five-second duration were unchanged.

[The separate art proof](verification/harbor-art-20261126.json) records nine Blender exports, 8,328 distinct triangles and
244 closed outward solids. The runtime repeats 34 model instances / 58,156 triangles plus 920 paving triangles; the existing
ferry is separate. Corrected static imports pass all 267 actual Null Editor checks, exit 0. The earlier 114-check report
retains 18 failures across the nine legacy-animation imports. The `animationType=None` correction preserves the original
shared gate atlas and metadata. CPU authoring images and import checks do not establish rendered appearance.

## Scope and remaining gates

Chapter fixtures prepare the prior twenty-mission summary and initial role ownership. Harbor completion is earned through
normal movement, E, physical timing and crate/pad checks. Controlled capture callbacks, pursuit values and pending-leap
negatives are rule probes, not a human stealth playthrough or an observed guard catch. Arrival lifecycle fixtures prepare
presentation state only and grant no Harbor objective completion.

AutoTest scenarios 25/26/27 are registered; no all-27 suite result is claimed. An obstructed diversion probe, free-body
containment during a running link, a fresh native candidate, RTX gameplay captures and performance remain separate gates. GPU work waits for the user's current player to finish.

Unity jobs use `Tools/unity.sh`. Tests isolate game preferences under `Logs/harbor-editor-user/config` while linking the
installed application's existing licensed Unity profile. No credentials were copied. The original isolated startup exits 198
before tests because it has no activated Unity profile; that log remains separate from gameplay failures.
