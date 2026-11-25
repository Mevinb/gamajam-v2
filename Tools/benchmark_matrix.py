#!/usr/bin/env python3
"""Run serial, rendered-player GPU benchmarks and retain complete artifact-specific evidence."""
import argparse
import csv
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import time

PROJECT = Path(__file__).resolve().parents[1]


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def utc():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def active_render_processes():
    found = []
    for entry in Path("/proc").iterdir():
        if not entry.name.isdigit():
            continue
        try:
            name = (entry / "comm").read_text().strip()
            if name in {"Unity", "blender", "Parasite.x86_64"}:
                state = next(line for line in (entry / "status").read_text().splitlines() if line.startswith("State:"))
                if "Z (zombie)" not in state:
                    found.append({"pid": int(entry.name), "name": name})
        except (OSError, ProcessLookupError):
            pass
    return found


def parse_report(text):
    result = {}
    frames = re.search(r"Frames: (\d+) over ([\d.]+)s  avg ([\d.]+) FPS \(([\d.]+) ms\)  p99 ([\d.]+) ms", text)
    if frames:
        result.update(frames=int(frames[1]), sampled_seconds=float(frames[2]),
                      average_fps=float(frames[3]), average_ms=float(frames[4]), p99_ms=float(frames[5]))
    route = re.search(r"route (COMPLETE|INCOMPLETE|INTERRUPTED), (\d+)/(\d+) waypoints reached", text)
    if route:
        result.update(route=route[1], reached=int(route[2]), waypoints=int(route[3]))
    for label, key in [("GPU", "gpu"), ("Resolution", "resolution"), ("Preset", "preset"), ("Memory", "memory")]:
        match = re.search(rf"^{label}: (.+)$", text, re.MULTILINE)
        if match:
            result[key] = match[1]
    return result


def parse_telemetry(path):
    samples = []
    with path.open(newline="") as source:
        for row in csv.reader(source):
            try:
                # nvidia-smi may retain units in CSV fields; preserve the raw file and parse their numeric prefix.
                values = [re.match(r"^\s*(-?\d+(?:\.\d+)?)", value) for value in row[2:7]]
                if len(values) != 5 or not all(values):
                    continue
                samples.append([float(value[1]) for value in values])
            except ValueError:
                continue  # Headers, driver notices and unsupported-field reports are retained in the raw file.
    samples = [row for row in samples if len(row) == 5]
    if not samples:
        return {"samples": 0}
    return {"samples": len(samples), "scope": "GPU-wide during player process, including startup, captures and desktop baseline",
            "average_gpu_percent": round(sum(row[0] for row in samples) / len(samples), 2),
            "peak_gpu_memory_mb": max(row[1] for row in samples),
            "maximum_temperature_c": max(row[2] for row in samples),
            "average_power_w": round(sum(row[3] for row in samples) / len(samples), 2),
            "average_graphics_clock_mhz": round(sum(row[4] for row in samples) / len(samples), 2)}


def write_summary(folder, evidence):
    (folder / "matrix.json").write_text(json.dumps(evidence, indent=2) + "\n")
    lines = ["# Rendered-player benchmark matrix", "", f"Source: `{evidence['build']['source_commit']}`",
             "", "These are uncapped rendered-player samples. Startup and screenshot encoding are excluded from FPS samples.",
             "Telemetry is GPU-wide during the entire process, including startup, captures and the desktop baseline. An incomplete route is not a pass.", "",
             "| District | Preset | Status | Route | Average FPS | p99 ms |",
             "|---|---|---|---|---:|---:|"]
    for case in evidence["cases"]:
        measured = case.get("measurements", {})
        lines.append(f"| {case['district']} | {case['preset']} | {case['status']} | "
                     f"{measured.get('reached', '?')}/{measured.get('waypoints', '?')} | "
                     f"{measured.get('average_fps', '—')} | {measured.get('p99_ms', '—')} |")
    (folder / "README.md").write_text("\n".join(lines) + "\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--width", type=int, default=1920)
    parser.add_argument("--height", type=int, default=1080)
    parser.add_argument("--graphics-api", choices=["default", "glcore", "vulkan"], default="default")
    parser.add_argument("--popup-window", action="store_true", help="Request a borderless client window to avoid desktop decoration size changes")
    parser.add_argument("--presets", nargs="+", choices=["Low", "Medium", "High", "Ultra"], default=["High", "Ultra"])
    parser.add_argument("--districts", nargs="+", choices=["city", "riverside", "island"], default=["city", "riverside"])
    parser.add_argument("--timeout", type=float, default=900, help="Per-process diagnostic timeout, seconds")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    if args.width < 320 or args.height < 200 or args.timeout <= 0:
        parser.error("Use a valid rendered resolution and positive timeout.")
    build = json.loads((PROJECT / "Builds/Linux/BUILD_INFO.json").read_text())
    runtime = PROJECT / "Builds/Linux/Parasite_Data/Managed/Parasite.Runtime.dll"
    expected = build["files"]["Parasite_Data/Managed/Parasite.Runtime.dll"]["sha256"]
    if sha256(runtime) != expected:
        raise SystemExit("Built runtime no longer matches BUILD_INFO.json; do not mix benchmark artifacts.")
    folder = args.out.resolve()
    cases = [(district, preset) for district in args.districts for preset in args.presets]
    if args.dry_run:
        print(json.dumps({"build": build["source_commit"], "resolution": [args.width, args.height],
                          "graphics_api": args.graphics_api, "output": str(folder), "cases": cases}, indent=2))
        return 0
    active = active_render_processes()
    if active:
        raise SystemExit("Finish other Unity/Blender/player work before isolated benchmarks: " + json.dumps(active))
    if folder.exists() and any(folder.iterdir()):
        raise SystemExit("Use a new output directory so existing benchmark evidence is preserved.")
    folder.mkdir(parents=True, exist_ok=True)
    evidence = {"started_utc": utc(), "build": build, "resolution": [args.width, args.height],
                "display": os.environ.get("DISPLAY"), "batch_mode": False,
                "graphics_api_requested": args.graphics_api, "popup_window": args.popup_window, "cases": []}
    for district, preset in cases:
        if sha256(runtime) != expected:
            raise SystemExit("Runtime changed between benchmark cases.")
        directory = folder / f"{district}-{preset.lower()}"
        directory.mkdir()
        report = directory / "report.txt"
        command = [str(PROJECT / "play.sh"), "-benchmark", "-benchmarkPreset", preset,
                   "-benchmarkDistrict", district, "-screen-width", str(args.width),
                   "-screen-height", str(args.height), "-screen-fullscreen", "0",
                   "-autotestOut", str(report), "-logFile", str(directory / "player.log")]
        if args.graphics_api != "default":
            command.append("-force-" + args.graphics_api)
        if args.popup_window:
            command.append("-popupwindow")
        case = {"district": district, "preset": preset, "command": command, "started_utc": utc(), "status": "RUNNING"}
        evidence["cases"].append(case)
        write_summary(folder, evidence)
        print(f"Running {district} / {preset} at {args.width}x{args.height}", flush=True)
        telemetry = None
        began = time.monotonic()
        with (directory / "gpu.csv").open("w") as gpu, (directory / "launcher.log").open("w") as launcher:
            try:
                telemetry = subprocess.Popen(["nvidia-smi", "--query-gpu=timestamp,name,utilization.gpu,memory.used,temperature.gpu,power.draw,clocks.gr",
                                              "--format=csv", "--loop=1"], stdout=gpu, stderr=subprocess.STDOUT)
                completed = subprocess.run(command, cwd=PROJECT, stdout=launcher, stderr=subprocess.STDOUT, timeout=args.timeout)
                case["exit_code"] = completed.returncode
            except subprocess.TimeoutExpired:
                case.update(exit_code=124, diagnostic="Player exceeded the explicit benchmark timeout.")
            finally:
                if telemetry is not None and telemetry.poll() is None:
                    telemetry.terminate()
                    try:
                        telemetry.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        telemetry.kill()
                        telemetry.wait()
        case.update(finished_utc=utc(), wall_seconds=round(time.monotonic() - began, 3))
        text = report.read_text() if report.exists() else ""
        measured = parse_report(text)
        case["measurements"] = measured
        case["gpu_telemetry"] = parse_telemetry(directory / "gpu.csv")
        case["status"] = "PASS" if (case["exit_code"] == 0 and measured.get("route") == "COMPLETE"
            and measured.get("reached", 0) == measured.get("waypoints", -1)
            and measured.get("frames", 0) > 0 and "RTX 4050" in measured.get("gpu", "")
            and f"{args.width}x{args.height}" in measured.get("resolution", "")
            and measured.get("preset", "").startswith(preset + ",")) else "FAIL"
        expected_api = {"glcore": "OpenGLCore", "vulkan": "Vulkan"}.get(args.graphics_api)
        if expected_api and expected_api not in measured.get("gpu", ""):
            case.update(status="FAIL", diagnostic="Requested graphics API was not used by the rendered player.")
        write_summary(folder, evidence)
        print(f"{case['status']} {district} / {preset}: {measured}", flush=True)
    evidence["finished_utc"] = utc()
    write_summary(folder, evidence)
    return 0 if all(case["status"] == "PASS" for case in evidence["cases"]) else 1


if __name__ == "__main__":
    raise SystemExit(main())
