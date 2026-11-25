# 0.5.0 focused desktop graphics baseline

Six sequential native runs pass on the pinned 0.5.0 player built from clean source `2f3fde9`, using the RTX 4050 Laptop GPU,
Vulkan and the real `:0` desktop at 1920×1080. Every process exits 0, reaches every original waypoint and preserves the runtime
DLL hash. Every recorded profiler sample is focused. VSync is off and the frame limit is unlimited; AI and traffic remain active.
The AC supply reports online and the power profile is balanced before and after each run.

| Route | Preset | Waypoints | Average FPS | p99 frame ms | Worst frame ms |
|---|---|---:|---:|---:|---:|
| City exterior | High | 5/5 | 169.2 | 10.92 | 36.0 |
| City exterior | Ultra | 5/5 | 111.7 | 12.84 | 27.7 |
| Riverside | High | 7/7 | 212.9 | 9.57 | **217.1** |
| Riverside | Ultra | 7/7 | 140.5 | 10.06 | 23.3 |
| Island | High | 20/20 | 252.2 | 8.68 | 24.2 |
| Island | Ultra | 20/20 | 169.5 | 10.53 | 23.9 |

High reports 1.00 render scale; Ultra reports 1.25. Startup and screenshot encoding are outside the sampled walk windows.
The profiler additionally excludes the first sample at each leg boundary. These are route measurements, not guarantees for
every view or the proposed graphics upgrade. The city route visits the alley and three exterior infiltration entrances; it
does not provide a dedicated laboratory-interior or close-up character baseline. The riverside/island tours use prepared Peace
fixtures and seeded access prerequisites, so their completion does not prove mission completion or a human playthrough.

The 217.1 ms riverside High frame is retained. Aggregate Main/PlayerLoop maxima are about 217 ms and the render-thread-wait
maximum is 213.276 ms. Those similar maxima suggest a possible rendering/driver synchronization stall, but they are not
frame-aligned evidence of one cause. No per-frame hitch timestamps, GC samples or GPU frame-time timer were collected.
The CPU render-thread counter, GPU utilization and total frame time cannot substitute for GPU-only timing.

Each job uses fresh XDG configuration/cache directories. The first City High run did not find Unity's Vulkan PSO cache;
subsequent runs loaded the shared `/tmp/Parasite Jam Team/PARASITE/vulkan_pso_cache.bin`, which also grew during Riverside High.
This is not a fully cold-cache or cache-order-independent comparison. Cache growth is context for later investigation, not
proof of the hitch's cause.

NVML whole-job peak global GPU memory is 1,699/1,641 MiB for City High/Ultra, 1,628/1,771 MiB for Riverside and 1,639/1,779 MiB
for Island. These totals include desktop/driver and other GPU clients; they are not per-player residency. Unity reports
graphics-driver memory as 0, which is unavailable rather than zero VRAM use. The one-second NVML telemetry also includes
startup, screenshot and shutdown periods, unlike the frame statistics. No native graphics ring-buffer warnings occur in any run.

[The exact proof](graphics-baseline-0.5.0-20261125.json) pins all reports, profiler sidecars, focus receipts, telemetry,
screenshots, power snapshots and the binary identity. It retains missing metrics and the recorded limitations.
