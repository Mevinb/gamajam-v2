# Known issues and validation limits

- Characters, including the seated lakeside figure, use stylized procedural bodies and animation.
- Cinematics use captions and sound effects; there is no recorded voice acting.
- Puddles use lightweight reflected lighting and softened procedural shores rather than full water simulation.
- Ambient traffic uses simplified kinematic lanes. Cars waiting at an intersection can eventually pass through cross traffic to avoid a permanent deadlock.
- Prop mesh detail is selected during city generation and merged into static batches. These selections do not switch with camera distance; the graphics preset's LOD bias only affects objects that support runtime LODs.
- The automated campaign exercises all three entry routes, but its simulated input is not a substitute for an extended human playthrough or physical gamepad testing.
- Linux x86-64 is the sole demo target. Windows packaging was cancelled at the user's request and is outside this release's scope.
- The recorded performance benchmark belongs to the earlier five-mission build, not a fresh 0.2.0 measurement. It reports an incomplete return leg and frame-time hitches; its FPS describes only that sampled route, resolution and render scale.

The workstation has limited free space. Build artifacts and verification captures stay in ignored `Builds/` and `Logs/` directories.
