#!/usr/bin/env python3
"""Listen-proxy QA for generated audio: loudness (EBU R128 integrated), true-ish peak, clipping, silence, DC,
octave-band balance, loop-seam continuity (last vs first samples) and an optional spectrogram (sox).
Usage: analyze.py [--spec DIR] [--bands] files_or_dirs...
"""
import argparse
import glob
import os
import subprocess
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import common as C  # noqa: E402

BANDS = [31, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000]


def bands(x, sr):
    m = C.to_mono(x)
    spec = np.abs(np.fft.rfft(m * np.hanning(len(m)))) ** 2
    f = np.fft.rfftfreq(len(m), 1 / sr)
    out = []
    for c in BANDS:
        lo, hi = c / np.sqrt(2), c * np.sqrt(2)
        out.append(10 * np.log10(spec[(f >= lo) & (f < hi)].sum() + 1e-12))
    out = np.array(out)
    return out - out.max()


def seam(x):
    """Jump at the loop seam relative to the typical sample-to-sample step (≈1 = continuous)."""
    m = C.to_mono(x)
    d = np.abs(np.diff(m))
    typical = np.percentile(d, 99) + 1e-9
    jump = abs(m[0] - m[-1])
    return jump / typical


def analyze(path, args):
    x = C.ffmpeg_read(path, sr=C.SR, mono=False)
    sr = C.SR
    dur = len(x) / sr
    peak = np.max(np.abs(x)) if len(x) else 0
    lufs = C.loudness(x) if dur > 0.05 else float("nan")
    clip = int(np.sum(np.abs(x) >= 0.999))
    rms = np.sqrt(np.mean(x ** 2)) if len(x) else 0
    silent = rms < 1e-4
    dc = float(np.mean(x))
    s = seam(x)
    line = f"{os.path.relpath(path, C.ROOT):70s} {dur:7.2f}s {lufs:6.1f} LUFS pk {C.db(peak):6.1f} clip {clip:3d} seam {s:5.2f}"
    flags = []
    if silent:
        flags.append("SILENT")
    if clip:
        flags.append("CLIP")
    if abs(dc) > 0.01:
        flags.append(f"DC {dc:.3f}")
    if ("/Music/mus_" in path or "/Ambience/" in path) and "mus_opening" not in path and s > 3:
        flags.append("SEAM?")
    if flags:
        line += "  <-- " + " ".join(flags)
    print(line)
    if args.bands:
        print("      bands " + " ".join(f"{c}:{v:5.1f}" for c, v in zip(BANDS, bands(x, sr))))
    if args.spec:
        os.makedirs(args.spec, exist_ok=True)
        png = os.path.join(args.spec, os.path.basename(path).replace(".ogg", ".png"))
        subprocess.run(["sox", path, "-n", "remix", "1", "spectrogram", "-x", "1200", "-y", "400", "-z", "90", "-t",
                        os.path.basename(path), "-o", png], check=False)
    return dict(path=path, dur=dur, lufs=lufs, peak=peak, clip=clip, silent=silent, seam=s)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("paths", nargs="+")
    ap.add_argument("--spec")
    ap.add_argument("--bands", action="store_true")
    args = ap.parse_args()
    files = []
    for p in args.paths:
        files += sorted(glob.glob(os.path.join(p, "**/*.ogg"), recursive=True)) if os.path.isdir(p) else [p]
    res = [analyze(f, args) for f in files]
    bad = [r for r in res if r["silent"] or r["clip"]]
    total = sum(os.path.getsize(r["path"]) for r in res)
    print(f"{len(res)} files, {total / 1e6:.1f} MB, {len(bad)} flagged (silent/clipping)")


if __name__ == "__main__":
    main()
