# The Exchange — proposed missions 27–29

Status: implementation blueprint, updated for WORK commit `cf8b57e` and the verified 0.7.3 checkpoint system. No mission, model or scene is added by this document. The earlier 0.7.2 user playtest ended normally; this plan does not reopen it.

Northline carries the player to an urban freight/medical interchange at first light. The next chapter makes the consignment delivered in mission 25 useful: Peace supplies a municipal cold store; Dominion supplies a public-address relay. The first lake choice remains immutable. The chapter shares the same yard and physical rules in both branches, with a different priority wagon, bay and final receiver.

The main new activity is a reversible shunting puzzle on a lateral traverser, followed by a readable electrical hazard route. Neither is a stationary two-person hold. The last mission combines the actual cargo, repaired feed and both free bodies in a protected outbound trip.

## Existing integration contract

Current source establishes these boundaries:

- `RailCampaign.FirstMissionIndex` is 23; appending its three missions produces 26 missions.
- `GameManager.PublishRailEnding` publishes `RailCampaign.Departed` only after `RailResolution.ReadyToPublish` proves the actual coach and captured passengers reached their endpoint. A timer or subtitle cannot publish that result.
- `GameManager.ContinueRail` currently requires `Ended`, mission index/count 23, all actual amoebae free, and `RunState.BeginRail()` before loading City.
- `MissionManager.BuildCampaign()` appends chapters only after their `Started` flag. Earlier summaries therefore retain their original mission count until the player deliberately continues.
- `GameManager.Awake()` builds the active district before CityDirector bakes navigation and spawns its NPC markers. New gameplay geometry belongs in a runtime district builder; generated scenes are never hand-edited.
- `RailCampaign.ReadyHost`, `FullyInside`, `ClearLine`, and registered/body identity rules are useful references, but their `Active` check is Northline-specific. Exchange needs its own checks or a separately reviewed shared implementation.

Add `ExchangeCampaign.FirstMissionIndex = 26` and append its three missions only when `RunState.ExchangeStarted`. The active chapter then contains 29 missions; the completed Northline summary still contains 26 before Continue.

`GameManager.ContinueExchange()` must require all of the following before its single scene load:

1. Exact current manager in `GameState.Ended`, `RunState.MissionIndex == 26`, and current mission list count 26.
2. A chosen Peace/Dominion outcome, the earned earlier chapter chain, and Northline `Started`, `CrossingPassed`, `Validated`, `FreightArrived`, `SignalReleased`, and `Departed`.
3. A nonempty normal registry, with every registered agent owning a live free blob and enabled controller; no host, transit, pending capture or destroyed body. A live Northline completion already proves its two distinct passengers through `RailResolution`. A saved earned summary uses the established checkpoint reconstruction of one fresh free body and must also be allowed to Continue. Do not fabricate a second body or require a new endpoint proof from the reconstructed coach. Reject a two-entry registry with one missing body; this is different from the valid fresh one-body registry after disk resume.
4. No existing Exchange start. `RunState.BeginExchange()` checks its persistent prerequisites again, adds only `exchange_started`, sets index 26 and the canonical arrival checkpoint, and preserves route, first lake outcome, earlier flags and accumulated statistics.

Do not continue automatically from a load, checkpoint, departure caption or new-game menu. The summary gains `Continue · The Exchange` in the existing single Continue slot, with explicit keyboard/gamepad navigation and focus. Exchange-specific summary text follows the existing latest-completed-chapter precedence. Its new departure flag must not expose an unrelated earlier Continue action.

## Playable flow

| Mission | What the player does | Authoritative completion |
| --- | --- | --- |
| 27 — MAKE ROOM | A SCIENTIST reads the seals on the two actual wagons. A MAINTENANCE host uses the traverser to park the blocking wagon in a siding, then aligns and transfers the branch's priority wagon to its receiving bay. The table can be moved and reversed freely while its safety interlocks are satisfied. | Correct stable wagon identity fully reaches its bay with brake set; the blocking wagon is physically parked clear of the shared throat. |
| 28 — BETWEEN THE FLASHES | A FREE body takes the low inspection route through a wet service gallery. A visible/audible electrical cycle gives safe intervals and refuge islands. Recover the actual fuse cassette, carry it to the cabinet and install it. A MAINTENANCE host opens the real isolation knife; a SCIENTIST verifies the now-powered branch receiver. | Actual cassette installed, knife physically fully open, isolated bus measured safe, and correct receiver verified using fresh interaction. |
| 29 — ALL THE WAY THROUGH | Transfer the same priority wagon from the receiving bay onto the outbound flatbed, secure its physical latch, then release both hosts. Put both FREE bodies entirely inside the protected passenger space and dispatch once pursuit is lost. Watch the train, wagon and actual bodies reach the outbound endpoint. | Captured wagon and passengers remain attached to the same valid train throughout collision-checked travel; the actual train reaches the declared endpoint and only its owning resolution publishes departure. |

Mission 27 introduces a spatial sequencing problem rather than another binary switch code. The common throat fits one wagon: the blocker must first be taken from lane 1 to the west siding. Only then can the table collect the priority wagon from lane 0 and deliver it to the east receiver. Peace prioritizes the marked cold-store wagon; Dominion prioritizes the relay wagon. Labels, paint bands and the scientist's readable manifest all agree. The non-priority wagon is never silently deleted.

Use fresh E interactions to select the next adjacent table stop and to engage/release each wagon's mechanical brake. A stop preview shows the actual lane name. The table moves only between adjacent aligned lanes, and loading/unloading is a separate operation after full alignment. It cannot choose a completed destination and grant credit immediately. A wrong legal move remains reversible; a blocked move stops at the last safe pose with a concrete reason. It does not invent a return teleport or force a long reset.

Mission 28 offers two routes: the short low service route for a free amoeba, and a longer exposed personnel walkway accessible to hosts. Both pass readable hazard boundaries. The maintenance role can permanently isolate a completed bus after retrieving the cassette; a host costume alone does not confer arbitrary electrical immunity. The gallery includes opaque refuge cabinets on dry raised plinths, so a free body can wait through the cycle. The fuse survives drops and failed attempts. Losing it outside safe footing restores the same named cassette at its last authored safe rack, without installation credit.

For mission 29, transfer and latch the real priority wagon; do not replace it with a new visual copy at the outbound train. A brake/latch animation has a colliding final pose, and transport authorization captures that exact wagon Rigidbody, latch, train and two actual free bodies. The short outbound journey is a chapter resolution, not a scripted promise that delivery happened somewhere off-screen.

## Layout and movement contract

Proposed `ExchangeLayout.Origin = (2000, 0, -220)`, isolated from the existing Northline origin `(1500, 0, -220)`. All coordinates below are local metres. The district fits x `[-47, 47]`, z `[-34, 68]`; static perimeter and physical floors close the playable space. Scene reload reconstructs only this active district.

| Item | Local position / bounds |
| --- | --- |
| Arrival checkpoint | `(-35, .15, -26)`, dry 8×8 m apron beside a stationary arrival coach |
| Scientist / maintenance starting workers | `(-31, .15, -24)` / `(-39, .15, -23)`, outside the wagon swept volumes |
| Manifest desk | `(-29, .15, -14)`, readable wagon identity plate at eye reach |
| Traverser | centre `(0, .15, -7)`, deck 4.0×9.0 m; adjacent x stops `-18, 0, 18` |
| Wagon lanes | loading lane x `-18`, shared throat x `0`, receiving lane x `18`; track z `[-18, 12]` |
| West blocker siding | `(-18, .15, 9)`, fully contains blocker plus 0.15 m clearance |
| Priority receiver | `(18, .15, 9)`, complete cart-envelope containment required |
| Cargo recovery rack / checkpoint | `(24, .15, 16)` / `(29, .15, 14)`, outside all moving hardware |
| Gallery | x `[15, 37]`, z `[20, 44]`; walking floor top `.15` |
| Low service entrance | `(17, .15, 21)`, 0.90 m actual aperture, wide enough for the real 0.64 m free-body capsule |
| Personnel route | x `[33, 37]`, z `[18, 46]`, minimum 2.0 m clear width |
| Live bands | z `[25, 27]` and `[34, 36]`, x `[18, 32]`; separate physical trigger shapes and rendered wet strips |
| Refuge plinths | `(22, .25, 30)` and `(28, .25, 39)`, minimum 2.4×2.4 m usable footing |
| Cassette rack / isolation cabinet | `(21, .15, 42)` / `(35, .15, 44)` |
| Branch receiver / final checkpoint | `(27, .15, 47)` / `(31, .15, 48)` |
| Outbound flatbed | centre `(18, .15, 52)`, cargo deck fits the same wagon; passenger compartment occupies a separate protected 3.2×6.0 m envelope |
| Boarding ramp | foot `(11, .15, 54)`, landing `(16.3, .75, 54)`, slope compatible with the current controller; no forced player warp |
| Outbound endpoint | train start + `(0, 0, 12)` with a fully supported collision-checked track corridor |

The traverser and wagons need physical kinematic Rigidbodies, explicit box compounds and swept-volume occupancy checks. Before each actual movement step, test the entire carried compound; never move a collider through a body because its control trigger was clear. Detect both active and inactive player bodies, possessed NPCs, ordinary NPCs and other cargo. A table cannot move while an unattached passenger is inside its swept envelope. Normal pedestrians stay on the separated personnel walkway; their patrol points must not cross the table or live electrical bands.

The table's deck must be flush with loading platforms at every stop. Navigation links are enabled only while the table is fully docked; moving links remain disabled. The authored visual's scale and inverse-parent binding cannot alter collision sizes. Reserve 0.15 m clearance around cargo, 0.90 m minimum low-route aperture and 1.6 m minimum two-way aisle. Test the current 0.55 m free-body hop against all reachable bench/step shapes.

## Electrical cycle and fair recovery

Start with a legible repeating cycle: 6 s safe, 2 s warning, 3 s live, 2 s cooldown. Warning strips, the cabinet pointer and an audible relay click precede the live state. Pausing freezes every phase, exposure and movement counter. Rendering indicators read the same controller state as the actual hazard; animation cannot advertise a safe interval while contact remains live.

The two bands are permanently disconnected only when the actual isolation knife has finished opening with the installed cassette. Until then, a registered player's capsule overlapping a live band's wet volume accumulates exposure. One second of continuous live exposure requests the ordinary checkpoint recovery exactly once; leaving the volume clears exposure. It does not destroy, duplicate, forcibly release or transfer ownership of an amoeba. Apply the same rule to an inactive owned body so parking it in a hazard cannot confer immunity. Ordinary unpossessed story workers are excluded, as in owned fall recovery. New arrival recovery resumes on a dry checkpoint and resets transient electrical phase to its initial safe phase.

A cancelled cassette pickup/drop must retain a single stable cassette identity. Pickup validates current registered active agent, actual reach/LOS, body state and physical object availability. Carrying follows that exact active body; switching agents leaves the cassette with its owning body or performs an explicit drop, never an invisible inventory transfer. Capture, host ejection, controller loss or scene unload retires the carry attachment deterministically. Checkpoint restore places the cassette at its canonical rack unless installation had already been physically earned.

These timings are initial tuning values, not proof of fair traversal. Acceptance must walk the real route and demonstrate that the largest vulnerable capsule can reach each refuge during the declared window. If it cannot, revise the authored distance or timing from that evidence; do not add invulnerability or teleportation to make the test pass.

## Proposed persistent flags

Define all constants in `ExchangeCampaign` with a single `exchange_` namespace:

| Constant | Value | Set only after |
| --- | --- | --- |
| `Started` | `exchange_started` | Deliberate guarded continuation from completed M26 summary |
| `ArrivalSeen` | `exchange_arrival_seen` | Optional non-credit arrival presentation finishes or is deliberately skipped |
| `Identified` | `exchange_consignment_identified` | Scientist reads the actual required wagon's stable seal |
| `BlockerParked` | `exchange_blocker_parked` | Same blocking wagon fully settles in its clear siding, with brake set |
| `PriorityDocked` | `exchange_priority_docked` | Same identified priority wagon fully settles in its branch receiver, with brake set |
| `FuseRecovered` | `exchange_fuse_recovered` | First actual cassette pickup; informational progress only, never a substitute for its current custody |
| `FuseInstalled` | `exchange_fuse_installed` | Same cassette is physically seated and latched into the actual cabinet |
| `BusIsolated` | `exchange_bus_isolated` | Real knife reaches its open limit and the hazardous bus becomes safe |
| `FeedRestored` | `exchange_feed_restored` | Scientist verifies the correct actual receiver with installed cassette and safe bus |
| `CargoSecured` | `exchange_cargo_secured` | Same priority wagon physically sits on the actual outbound flatbed with final latch/brake engaged |
| `Departed` | `exchange_departed` | Owning protected resolution proves actual train, cargo and passengers at endpoint |

Mission 27 needs `Identified && BlockerParked && PriorityDocked`; mission 28 needs `FuseInstalled && BusIsolated && FeedRestored`; mission 29 needs `Departed`. Each named physical controller is the only runtime publisher of its earned flags. Merely selecting a destination, elapsed movement time, subtitle completion, waypoint proximity or `MissionIndex` cannot publish them.

Keep dispatch authorization, selected table stop, current table pose, in-flight wagon attachment, carry owner, electrical phase/exposure, pursuit, moving latch fraction and protected-resolution leases transient. They are not save flags. On ordinary checkpoint/disk restore, reconstruct earned physical milestones at canonical safe endpoints; do not resume a partially completed movement or hold. If `CargoSecured` is present, reconstruct the one priority wagon on the outbound flatbed, never also at its earlier `PriorityDocked` bay. Validate impossible flag combinations before restore. The root campaign-save work owns the actual disk schema; this chapter must register its legitimate flag dependencies rather than serialize Unity object references.

The Exchange release must advance the envelope and checkpoint schema to version2 and support explicit version1 migration. Version1's validated range ends at Northline26. Writing Exchange27–29 progress as version1 would make0.7.3 treat it as corruption, potentially recover an older backup and overwrite newer progress. Keep the canonical slot path compatible with0.7.3's future-version guard: a version2 primary must be refused by the old reader without fallback or overwrite. Verify the original version1 checksum before migration, preserve all earned fields/outcome/statistics, and leave files unchanged during reads. A migrated Northline summary stays at26 with no Exchange flags until deliberate Continue; migration cannot award the new chapter.

Mission checkpoints: arrival; after M27 complete at the cargo apron; after M28 complete at the final dry apron. A mid-puzzle reset preserves earned permanent milestones and rebuilds only the unfinished physical stage. The final protected journey requires fresh boarding and authorization after restore; loading a save must not publish `Departed` or auto-Continue.

## Runtime and authoring files

Keep one MonoBehaviour per file. Proposed new files under `Scripts/Missions/`:

- `ExchangeCampaign.cs`: flags, three mission definitions, branch priority identity, access helpers and checkpoint announcements.
- `ExchangeLayout.cs`: shared metre-scale points, bounds, route waypoints and authored aperture dimensions.
- `ExchangeDistrict.cs`: static floor/perimeter, worker/patrol markers and explicit physical compounds before navigation bake.
- `ExchangeTraverser.cs`: adjacent-stop motion, exact wagon identity/attachment, reversible loading transfers and swept safety interlocks.
- `ExchangeWagon.cs`: stable identity, current physical owner, brake/latch state and canonical restore stage. No duplicate spawn on late callbacks.
- `ExchangePower.cs`: one electrical state machine, two live volumes, warning cues, actual isolation knife and owned-exposure recovery.
- `ExchangeCassette.cs`: single real cassette, pickup/drop/installation custody and safe-rack recovery.
- `ExchangeWorkControl.cs`: fresh interaction dispatch only; role/identity/LOS/permission checks delegate to their owner.
- `ExchangeDeparture.cs`: actual cargo, latch, track and free-boarder readiness; captures one immutable authorization lease.
- `ExchangeResolution.cs`: actual collision-checked train travel, preserved ownership cleanup, endpoint publication and interruption rollback.
- `ExchangeArrival.cs`: short arrival framing/subtitles and normal skip cleanup; no mission credit beyond `ArrivalSeen`.
- `ExchangeAuthoredArt.cs`: strict actual imported mesh/material binding, inverse parent scale and physics signature before/after.

Touch existing `RunState` and `GameManager` only for the new chapter predicate/spawn/begin/build/subscription/Continue/publish/recovery-name integration. `MissionManager` appends the three missions and routes `OnFlag` to the newest active chapter before Northline. `HUDController.Menus` adds one Exchange Continue action and latest-summary branch. Build a dedicated shared protected-transport helper only if its migration preserves and reruns the existing RailResolution ownership suite; do not casually change working Northline transport while adding the new chapter.

Blender source belongs in `ArtSource/Exchange/` with a reproducible `Tools/blender/exchange/` recipe and packed `.blend`. Proposed pieces: traverser with deck/guide wheels/side protection; cold-store and relay wagons sharing one chassis with distinct real loads; adjacent-stop gate/bumper; siding brake shoe; isolation cabinet with separate moving knife handle; glazed fuse cassette; insulated cable rack; wet warning-strip trough; dry refuge cabinet; marked receiver; outbound cargo/passenger flatbed. Moveable visual parts have explicit pivots and units. Reuse the coach where appropriate instead of exporting another nearly identical coach.

Use one new 2K opaque PBR atlas (albedo, +Y normal, metallic/smoothness) plus a separate URP transparent glass material for cabinet/cassette glazing. No new gameplay ray-tracing claim: Linux/URP remains the runtime target, with existing realtime shaders/reflections and offline RTX Blender films. Keep a source triangle/texture budget and measure it after authoring; proposed ceiling 30k triangles for the whole new kit and 17 MiB full-mip compressed atlas payload. Imported meshes have no colliders or behaviours; explicit gameplay compounds remain separate. Bake label text into the authored atlas or mount proper one-sided signage; avoid floating mirrored labels through walls.

## Acceptance before inclusion in a user build

The following are requirements, not existing results:

1. Entry/summary: actual M26 departure reaches its endpoint, restores bodies, shows one Exchange Continue, then one real button activation enters M27. Also load a recorded earned Northline summary from disk, reconstruct its canonical one free body, and Continue successfully without replaying departure or inventing a second body. Reject forged earlier summary, index/count mismatch, unfinished/interrupted live coach, missing/destroyed registry bodies, still-possessed body and repeated Continue. Outcome/route/prior flags/statistics are unchanged. Verify version1 migration and old0.7.3 refusal to overwrite a version2 primary even when a valid older backup exists. Keep the existing default27 acceptance-case selection unchanged; register new Exchange cases as explicit opt-ins until their scope is deliberately reviewed. The mission list remains26 until Exchange Continue.
2. Traverser physical puzzle, both branches: navigate/possess real scientist and maintenance workers; identify correct actual seal; clear actual blocker; route actual priority wagon through table to correct bay. Assert identities and actual compound bounds at every endpoint. Wrong wagon/bay/role, unsettled brake, stale E, blocked LOS and moving table deny credit. Make a legal wrong move and reverse it through normal controls. Body/cargo obstruction stops before overlap and resumes only after actual clearance.
3. Hazard fairness: traverse each interval/refuge using normal input at unchanged movement/jump values. Check visible/audible state agreement, pause semantics and no progress by idle time. Actual active and inactive owned bodies trigger one recovery after continuous exposure; safe refuge and ordinary NPC fixtures do not. Preserve all completed M27 milestones and original ownership policy after reload.
4. Cassette custody: real pickup/drop/re-pick/install; wrong active agent, transit, host capture/ejection, destroyed carrier, missing cassette, late callback and scene unload never duplicate or silently install it. Restore before pickup, during carry, after installation and after isolation at canonical safe states.
5. Actual power/receiver: capture or move the operator during knife travel, destroy/disable cabinet/cassette/receiver, or invalidate credentials. No earned flag appears early. Finish only at actual safe knife angle with the correct installed cassette and branch receiver.
6. Outbound mission, both branches: transfer the same wagon onto the actual flatbed, latch it, release hosts, board both distinct free bodies using the actual ramp, lose pursuit and dispatch. Wrong cargo, partial passenger capsule, missing body, moving latch, blocked travel and duplicate authorization deny departure. Runtime readiness derives from actual objects, never just permanent flags.
7. Protected trip lifecycle: disable/destroy coordinator, carriage, captured wagon, passenger/controller or manager before attach, during travel and at endpoint; verify exactly-owned cleanup and rollback, no stale publish, no residual parent/disabled controller, and no fabricated body. Fresh checkpoint recovery re-creates safe unfinished transport and requires fresh E. A normal skip may finish narration but cannot skip actual travel or occupancy validation.
8. Earned continuous route: original infiltration → explicit lake choice/Continue → riverside → island → harbor → Northline → Exchange actual summary with all chapter boundaries earned in order. Run Peace and Dominion separately. Keep within-chapter fixtures explicit; these are not human playthrough proof.
9. Art/GPU: actual Blender export/import counts/pivots/UVs/material slots and dedicated texture hashes match; authored replacement preserves the explicit collision/nav signature. Capture actual table/low-route/cabinet/cargo/glass views on RTX4050/Vulkan and inspect them. Measure final-binary timing/VRAM at declared settings in an isolated workload; compile/Null data passes do not imply appearance or performance acceptance.

Until these exist and pass within their named scope, this is a proposed chapter. The open 0.7.2 user build retains its existing 26 missions.
