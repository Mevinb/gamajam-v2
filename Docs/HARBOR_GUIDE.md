# The Working Harbor — missions 21–23

**For the 0.6.0 preview candidate.** The retained 0.5.1 build has twenty missions and does not contain this chapter.
This is a walkthrough of the implemented mechanics, not a claim of a complete human playthrough or final graphical acceptance.

## Reaching the Harbor

Finish the original fourteen city missions and explicitly choose **Live quietly (Peace)** or **Become the world (Dominion)**
at the lake. Choose **Continue · After the lake**, finish riverside missions 15–17 and depart on the ferry. Choose
**Continue · The Far Shore**, finish island missions 18–20 and depart with the passengers and all free amoebas aboard the rescue
launch. Its summary offers **Continue · The Working Harbor**.

The original future, earlier objectives and statistics remain. The Harbor begins with one free amoeba; **V** can make a second,
and **Tab** switches between them. There is still a two-amoeba limit. A short captioned arrival ends in normal play.
Ctrl+M does not skip into this chapter or change its future.

## 21. A MEASURE OF PRESSURE

Borrow the public **Maintenance engineer** and follow the pressure-valve markers. Each **E** interaction toggles a real handle:

| Valve | Open contribution |
|---|---:|
| A — pump | +3 |
| B — pump | +2 |
| C — bleed | −1 |

The target and maximum are **4**. From the initial closed handles, a safe order is **C → A → B**. All three open give 3 + 2 − 1
= 4. Stay at that setting for **two gameplay seconds**; the lock then lifts physically. You do not need to hold E during the wait.

Opening A then B without the bleed reaches 5. Relief resets **all three handles and their saved settings** to closed; it does
not hurt or move your character. Retry with the bleed open before the final pump. The gauge and status line show live pressure
and the two-second balance. A free amoeba, scientist, guard or inactive host cannot work a Maintenance valve.

Pause freezes the balance and gate motion. If a checkpoint reload occurs before completion, earned valve positions return but
the balance timer starts again at zero. A completed lock stays open. Walk through after the leaf has lifted clear.

## 22. COLD CHAIN / SIGNAL IN TRANSIT

Borrow a **Scientist** and use the public cargo scanner outside the fenced conveyor. Four marked sealed lots circulate through
the arch. Your original lake future determines the real shipment you must select:

| Future | Required lots | Shipment |
|---|---|---|
| Peace — COLD CHAIN | **A17, A32** | Antidote |
| Dominion — SIGNAL IN TRANSIT | **R09, R21** | Relay equipment |

Read the prompt's **current ID, next ID and remaining window**. When a required lot is inside the arch, press **E at the
scanner**. Each moving window lasts about two gameplay seconds at normal belt speed. Empty windows, the other branch's lots
and the wrong profession are denied. Standing at the scanner does not scan automatically.

A correct scan sends that same physical crate along the diversion. Wait for it to reach its marked bay; the scan alone does
not complete delivery. You can scan the other required lot before the first arrives. Both actual arrivals finish the manifest.
Keep out of the fenced trench: its cargo path is not a walking route.

**Optional brake:** Maintenance can press **E** at the nearby brake to stop the main belt for five gameplay seconds. A scientist
cannot operate that brake. You can split and park the two roles at their controls, then switch with Tab, or use one scientist
and time the moving windows. Scanned cargo already on the separate diversion keeps travelling. Pause freezes the brake countdown
and cargo movement.

**Checkpoint recovery:** the same four stable lots are recreated without duplicates. Unscanned lots circulate again. Scanned
but undelivered lots restart at separate approaches three metres before their own bays; they still have to physically finish
that leg. Delivered lots stay in their bays. The five-second brake is transient and must be activated again if needed.

## 23. AN OPEN FREQUENCY / TWO MINDS, ONE SIGNAL

The manifest must be delivered first. This task needs free amoebas, not supporting human hosts:

1. Use **V** if only one amoeba is present. Release each host with **Q**, using **Tab** to check the other half too.
2. Put one free amoeba fully inside each separate relay pad. Both whole bodies must fit on the pads and be grounded; merely
   putting a centre point across the edge is insufficient.
3. Lose active pursuit and keep both physical emitter sight lines clear.
4. As the active free body, face either nearby node and press **E once**. Keep both bodies still for **three gameplay seconds**.

The inactive half stays on its pad. You can use Tab to select either half; do not move it during the handshake. Movement,
leaving a pad, capture, entering a host, a blocked beam or renewed pursuit cancels the attempt. Resolve the reason, settle both
free bodies and press E again. A checkpoint reload restarts unfinished handshake progress.

**Pause freezes the timer; it does not grant completion or automatically cancel the attempt.** On resuming, the same physical
requirements must still hold. Only a successful full handshake triggers the short captioned resolution and the Harbor summary.
The original Peace/Dominion future remains unchanged.

## Session progress and help

Campaign checkpoints last only within this running game; there is no campaign disk save. Closing the game or starting over
loses that run. Graphics/audio/input preferences persist separately.

Use **Esc** for pause and the mission journal, **F1** for controls and **F3** for FPS. See [CONTROLS.md](CONTROLS.md),
[COMPLETE_GAME_GUIDE.md](COMPLETE_GAME_GUIDE.md) and [AFTERLAKE_GUIDE.md](AFTERLAKE_GUIDE.md) for the earlier route and bindings.
The Harbor lines use captions; no new prerecorded Harbor voice performance is implied.
