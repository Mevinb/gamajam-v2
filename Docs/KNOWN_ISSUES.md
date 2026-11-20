# Known issues and validation limits

- People use original procedural bodies and animation. Imported humanoid characters and motion capture remain a major visual improvement to pursue.
- Some puddles have visibly rectangular reflection edges. This is present in the baseline city renders and needs a separate material/decals pass.
- Ambient traffic uses simplified kinematic lanes. Cars waiting at an intersection can eventually pass through cross traffic to avoid a permanent deadlock; intersection right-of-way needs another pass.
- Prop mesh detail is selected during city generation and merged into static batches. These selections do not switch with camera distance; the graphics preset's LOD bias only affects objects that support runtime LODs.
- The automated campaign exercises all three entry routes, but its simulated input is not a substitute for an extended human playthrough or physical gamepad testing.
- Linux x86-64 is the sole demo target. Windows packaging was cancelled at the user's request and is outside this release's scope.
- The benchmark reports incomplete/timed-out legs explicitly. Its FPS describes the sampled route, resolution and render scale, not every possible view or hardware configuration.

The workstation has limited free space. Build artifacts and verification captures stay in ignored `Builds/` and `Logs/` directories.
