# Campaign checkpoints on disk

The next preview adds **Continue checkpoint** to the title screen. New infiltration starts a fresh session; the first successful autosave replaces the slot. The existing 0.7.2 player is unchanged.

The slot preserves mission flags, chapter, the original lake choice, checkpoint location/name and campaign statistics. It deliberately rebuilds physical actors through the existing checkpoint path: one fresh free amoeba, normal NPCs and authored hardware. Split again when a task needs two bodies. Held controls, possessed bodies, travelling cargo references and partial protected coach motion are never serialized.

Checkpoint/flag changes coalesce after the complete mission-advancement stack. Direct flag removals are checked once a second; statistics refresh at least every 30 seconds. Quit to Menu and normal application quit flush a stable checkpoint. Protected ending/travel windows keep the last stable save until the earned summary is published. A completed-summary save restores the actual summary and its Continue action rather than replaying or inventing departure credit.

`campaign-v1.json` lives in Unity's per-user persistent data directory. A versioned checksummed payload is written to a fresh temporary file, flushed and atomically promoted; the prior valid primary becomes `.bak`. A corrupt current payload can recover the valid backup. A newer-version primary blocks both backup rollback and overwrite. Read failures never rewrite the files or mutate RunState. Failed writes keep the current session playable and report the save failure once.

Only explicit New/Resume/Restart actions activate the save session. Bare scene loads and RunState.NewGame fixtures do not. Editor/batch/automation entrypoints default off; tests opt into their own temporary directory. Resume clears any pending new-game film request.

[Verification history](verification/campaign-persistence-20261126.json): current storage 24/24 and actual menu/scene flow 5/5 pass with native Editor exit0. The initial invalid checkpoint-name fixture and isolated-profile licensing refusal remain recorded. New Linux-target build/API and broader campaign checks remain pending; this does not claim final release acceptance.
