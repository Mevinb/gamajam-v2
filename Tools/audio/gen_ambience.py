#!/usr/bin/env python3
"""PARASITE ambience loops: zone beds + positional world loops, all seamless.
Writes Assets/Parasite/Resources/Audio/Ambience/amb_*.ogg.
Recorded CC0 sources (OpenGameArt, scratch dir $PARASITE_AUDIO_WORK/oga): "High traffic road sounds" by IgnasD
(traffic_ignasd.ogg) and "Park Ambiences" by Thimras (park_ambience_wind.wav). Crowd walla = Kokoro TTS stems from
gen_voice.sh --vocals ($PARASITE_AUDIO_WORK/walla). Everything else is synthesised here.
Usage: Tools/audio/gen_ambience.sh [name ...]
"""
from __future__ import annotations

import glob
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import common as C  # noqa: E402
from common import SR, n_of  # noqa: E402

OUT = os.path.join(C.AUDIO_RES, "Ambience")
OGA = os.path.join(C.WORK, "oga")
R = np.random.default_rng(99)


def save(name, x, lufs, stereo=True, q=4):
    x = x if stereo else C.to_mono(x)
    x = x - np.mean(x, axis=0)
    x = C.normalize_lufs(x, lufs, ceiling_db=-2.0)
    C.write_ogg(os.path.join(OUT, name + ".ogg"), x, q=q)
    print(f"  Ambience/{name}.ogg {len(x) / SR:6.2f}s {C.loudness(x):6.1f} LUFS", flush=True)


def am(n, rate_lo, rate_hi, depth, seed, smooth=0.05):
    """Smooth random amplitude modulation (0..1 around 1-depth..1)."""
    r = np.random.default_rng(seed)
    k = max(4, int(n / SR * rate_hi * 2))
    pts = r.uniform(0, 1, k)
    m = np.interp(np.linspace(0, k - 1, n), np.arange(k), pts)
    m = C.lowpass(m, max(rate_lo, 0.1), order=1)
    m = (m - m.min()) / (m.max() - m.min() + 1e-9)
    return 1 - depth + depth * m


def droplet_field(n, count, fmin, fmax, amp, seed, dur=(0.002, 0.006)):
    r = np.random.default_rng(seed)
    out = np.zeros(n)
    for _ in range(count):
        d = r.uniform(*dur)
        m = n_of(d)
        i = r.integers(0, n - m - 1)
        burst = r.standard_normal(m) * np.exp(-np.arange(m) / (m * 0.3))
        out[i:i + m] += burst * r.exponential(amp)
    return C.bandpass(out, fmin, fmax)


def loopify(x, loop_sec, fade=2.0):
    return C.crossfade_loop(x, n_of(loop_sec), fade)


# ----------------------------------------------------------------------------- beds
def amb_city():
    traffic = C.ffmpeg_read(os.path.join(OGA, "traffic_ignasd.ogg"), mono=True)
    traffic = C.lowpass(C.highpass(traffic, 45), 2400)
    traffic = C.compress(traffic, -26, 2.5, 0.05, 0.5)
    n = len(traffic)
    L = traffic
    Rt = np.roll(traffic, n_of(21.0))  # decorrelated right channel from a different part of the recording
    bed = np.stack([L, Rt], 1)
    wind = C.ffmpeg_read(os.path.join(OGA, "park_ambience_wind.wav"), mono=False, start=30, dur=n / SR)[:n]
    wind = C.lowpass(C.highpass(wind, 80), 1400)
    hum = C.lowpass(C.brown(n, 5), 110)
    x = bed * 1.0 + wind * C.undb(-9) / (np.max(np.abs(wind)) + 1e-9) * np.max(np.abs(bed)) + np.stack([hum, np.roll(hum, 9000)], 1) * 0.12
    save("amb_city", loopify(x, n / SR - 2.6, 2.5), -22.0)


def amb_rain():
    n = n_of(32.0)
    wash = np.stack([C.pink(n, 1), C.pink(n, 2)], 1)
    wash = C.lowpass(C.highpass(wash, 450), 6500) * am(n, 0.1, 0.3, 0.25, 3)[:, None]
    drops = np.stack([droplet_field(n, 2600, 1600, 6000, 0.25, 4), droplet_field(n, 2600, 1600, 6000, 0.25, 5)], 1)
    heavy = np.stack([droplet_field(n, 260, 600, 2500, 0.5, 6, (0.004, 0.012)), droplet_field(n, 260, 600, 2500, 0.5, 7, (0.004, 0.012))], 1)
    gutter = C.bandpass(C.white(n, 8), 300, 1300) * am(n, 0.3, 1.5, 0.6, 9) * 0.25
    x = wash * 0.5 + drops * 0.6 + heavy * 0.35 + np.stack([gutter, np.roll(gutter, 7000)], 1)
    save("amb_rain", loopify(x, 30.0), -24.0)


def amb_lab():
    loop = 40.0
    n = n_of(loop + 2.0)
    t = np.arange(n) / SR
    air = np.stack([C.pink(n, 11), C.pink(n, 12)], 1)
    air = C.lowpass(C.highpass(air, 70), 900) * am(n, 0.05, 0.2, 0.15, 13)[:, None]
    hum = sum(a * np.sin(2 * np.pi * f * t) for f, a in ((60, 0.05), (120, 0.08), (180, 0.03), (240, 0.015)))
    buzz = C.bandpass(np.sign(np.sin(2 * np.pi * 120 * t)) * 0.5, 2000, 6000) * 0.012
    comp_env = np.clip(np.minimum((t - 5) / 2, (25 - t) / 2), 0, 1)
    fridge = (np.sin(2 * np.pi * 47 * t) * 0.06 + C.lowpass(C.white(n, 14), 300) * 0.05) * comp_env
    tick = np.zeros(n)
    for k in range(0, int(loop)):            # equipment relay ticks every ~1 s, very faint
        if R.random() < 0.35:
            C.mix_into(tick, C.bandpass(R.standard_normal(n_of(0.01)), 2000, 6000) * 0.05, n_of(k + R.uniform(0, 0.9)))
    mono = hum + buzz + fridge + tick
    x = air * 0.6 + np.stack([mono, mono], 1)
    save("amb_lab", loopify(x, loop), -27.0)


def amb_tunnel():
    loop = 40.0
    n = n_of(loop + 2.0)
    rumble = np.stack([C.lowpass(C.brown(n, 21), 90), C.lowpass(C.brown(n, 22), 90)], 1) * am(n, 0.05, 0.2, 0.4, 23)[:, None]
    flow = C.bandpass(C.white(n, 24), 180, 900) * am(n, 0.2, 1.0, 0.5, 25) * 0.25
    creaks = np.zeros(n)
    for k in range(3):
        m = n_of(1.2)
        imp = np.zeros(m)
        imp[::n_of(0.035)] = R.uniform(0.2, 1.0, len(imp[::n_of(0.035)]))
        cr = C.svf(imp * np.exp(-np.arange(m) / SR / 0.4), np.full(m, R.uniform(380, 700)), 0.96, 1)
        C.mix_into(creaks, cr * 0.05, n_of(R.uniform(2, loop - 3)))
    drips = np.zeros(n)
    for k in range(int(loop * 1.2)):
        m = n_of(0.12)
        tt = np.arange(m) / SR
        f0 = R.uniform(900, 1700)
        d = np.sin(2 * np.pi * np.cumsum(f0 * (1 + 1.2 * (1 - np.exp(-tt / 0.012)))) / SR) * np.exp(-tt / 0.03)
        C.mix_into(drips, d * R.uniform(0.02, 0.08), n_of(R.uniform(0, loop)))
    mono = flow + creaks + drips
    dry = rumble * 0.8 + np.stack([mono, np.roll(mono, 3000)], 1)
    wet = C.reverb(dry, C.make_ir(4.0, 3.5, 0.04, 3000, seed=26), wet=0.5, dry=0.8)[:n]
    save("amb_tunnel", loopify(wet, loop), -26.0)


# ----------------------------------------------------------------------------- positional loops (mono)
def amb_crowd():
    from scipy import signal
    files = sorted(glob.glob(os.path.join(C.WORK, "walla", "*.wav")))
    if not files:
        print("  (no walla stems — run gen_voice.sh --vocals first)")
        return
    clips = []
    for f in files:
        x = C.read(f, sr=SR, mono=True)
        clips.append(C.highpass(C.lowpass(x, 3400), 160))
    loop = 30.0
    n = n_of(loop + 2.5)
    near = np.zeros(n)
    for k in range(26):
        c = clips[R.integers(0, len(clips))]
        semis = R.uniform(-1.2, 1.2)
        start = -1.0 + (loop + 1.5) * (k + R.uniform(0, 1)) / 26
        up = int(round(2 ** (-semis / 12) * 1000))
        g = np.gcd(up, 1000)
        c = signal.resample_poly(c, up // g, 1000 // g)
        C.mix_into(near, c * R.uniform(0.25, 0.8), n_of(start))
    far = np.zeros(n)
    slots = 72  # stratified placement keeps the murmur continuous (no holes)
    for k in range(slots):
        c = clips[R.integers(0, len(clips))]
        st = -1.5 + (loop + 2.0) * (k + R.uniform(0, 1)) / slots
        C.mix_into(far, c * R.uniform(0.3, 0.7), n_of(st))
    far = C.lowpass(far, 900)
    clinks = np.zeros(n)
    for k in range(18):
        f = R.uniform(2400, 5200)
        b = C.fm_bell(f, 0.25, R.uniform(0.2, 0.5), ratio=2.76, index=1.5, tau=0.05)
        C.mix_into(clinks, b * 0.12, n_of(R.uniform(0, loop)))
    x = near * 0.55 + far * 0.6 + clinks
    x = C.to_mono(C.reverb(x, C.make_ir(1.2, 0.8, 0.01, 5000, seed=31), wet=0.25))[:n]
    save("amb_crowd", loopify(x, loop, 2.5), -22.0, stereo=False)


def amb_fountain():
    loop = 20.0
    n = n_of(loop + 2.0)
    splash = C.lowpass(C.highpass(C.white(n, 41), 350), 8000) * am(n, 2, 12, 0.5, 42)
    drops = droplet_field(n, 3500, 900, 5000, 0.3, 43, (0.003, 0.012))
    gurgle = C.bandpass(C.white(n, 44), 140, 520) * am(n, 1, 4, 0.6, 45) * 0.6
    x = splash * 0.5 + drops * 0.7 + gurgle
    save("amb_fountain", loopify(x, loop), -20.0, stereo=False)


def amb_drain():
    loop = 15.0
    n = n_of(loop + 2.0)
    flow = C.bandpass(C.white(n, 51), 400, 2200) * am(n, 1, 6, 0.6, 52) * 0.4
    bubbles = np.zeros(n)
    for k in range(int(loop * 9)):
        d = R.uniform(0.015, 0.05)
        m = n_of(d)
        tt = np.arange(m) / SR
        f = R.uniform(220, 900)
        b = np.sin(2 * np.pi * f * (1 + 2.5 * tt / d) * tt) * np.exp(-tt / (d * 0.4))
        C.mix_into(bubbles, b * R.uniform(0.1, 0.4), n_of(R.uniform(0, loop + 1.5)))
    drops = droplet_field(n, 500, 1200, 4000, 0.3, 53, (0.004, 0.01))
    x = flow + bubbles * 0.6 + drops * 0.5
    x = C.to_mono(C.reverb(x, C.make_ir(0.8, 0.6, 0.005, 4000, seed=54), wet=0.3))[:n]
    save("amb_drain", loopify(x, loop), -22.0, stereo=False)


def amb_steam():
    loop = 10.0
    n = n_of(loop + 2.0)
    hiss = C.bandpass(C.white(n, 61), 1500, 9500) * am(n, 0.5, 3, 0.45, 62)
    hiss += C.bandpass(C.white(n, 63), 400, 1500) * am(n, 0.3, 2, 0.6, 64) * 0.3
    save("amb_steam", loopify(hiss, loop, 1.5), -22.0, stereo=False)


def amb_aircon():
    loop = 8.0
    n = n_of(loop + 1.0)
    t = np.arange(n) / SR
    motor = sum(a * np.sin(2 * np.pi * f * t + p) for f, a, p in ((47.0, 0.4, 0), (94.0, 0.25, 1), (141.0, 0.12, 2), (188.0, 0.06, 3)))
    blade = 1 + 0.35 * np.sin(2 * np.pi * 12.5 * t)
    air = C.lowpass(C.highpass(C.white(n, 71), 120), 1800) * blade * 0.6
    rattle = np.zeros(n)
    for k in range(int(loop * 6.25)):
        if R.random() < 0.6:
            C.mix_into(rattle, C.bandpass(R.standard_normal(n_of(0.008)), 800, 3500) * R.uniform(0.05, 0.25), n_of(k / 6.25))
    x = motor * 0.5 + air + rattle
    save("amb_aircon", loopify(x, loop, 1.0), -21.0, stereo=False)


def amb_car_engine():
    loop = 3.0
    n = n_of(loop + 0.6)
    t = np.arange(n) / SR
    f0 = 30.0  # firing frequency at idle-ish; runtime pitch follows speed
    x = np.zeros(n)
    for k in range(1, 16):
        amp = (1.0 / k ** 0.9) * (1.3 if k in (2, 4) else 1.0)
        mod = 1 + 0.25 * np.sin(2 * np.pi * (1.0 / loop) * k * t + k)  # periodic over the loop
        x += amp * mod * np.sin(2 * np.pi * f0 * k * t + R.uniform(0, 6))
    x = np.tanh(x * 0.6)
    intake = C.bandpass(C.white(n, 81), 300, 1500) * (0.6 + 0.4 * np.sin(2 * np.pi * f0 * t) ** 2) * 0.25
    x = C.lowpass(x + intake, 1800)
    save("amb_car_engine", loopify(x, loop, 0.5), -18.0, stereo=False)


def amb_car_tyres():
    loop = 6.0
    n = n_of(loop + 1.0)
    hiss = C.bandpass(C.white(n, 91), 700, 6500) * am(n, 1, 8, 0.4, 92)
    spray = droplet_field(n, 1800, 1500, 7000, 0.3, 93, (0.003, 0.008))
    rumble = C.lowpass(C.brown(n, 94), 160) * 0.5
    save("amb_car_tyres", loopify(hiss + spray * 0.5 + rumble, loop, 1.0), -20.0, stereo=False)


def amb_cctv_servo():
    loop = 2.0
    n = n_of(loop + 0.4)
    t = np.arange(n) / SR
    whine = np.sin(2 * np.pi * 280 * t) * 0.4 + np.sin(2 * np.pi * 560 * t) * 0.2 + np.sin(2 * np.pi * 1120 * t) * 0.08
    gears = C.bandpass(C.white(n, 101), 1500, 5000) * (0.5 + 0.5 * np.sin(2 * np.pi * 40 * t) ** 2) * 0.25
    save("amb_cctv_servo", loopify(C.lowpass(whine + gears, 4500), loop, 0.3), -22.0, stereo=False)


JOBS = {k: v for k, v in globals().items() if k.startswith("amb_") and callable(v)}


def main():
    os.makedirs(OUT, exist_ok=True)
    for name in sys.argv[1:] or list(JOBS):
        JOBS[name]()


if __name__ == "__main__":
    main()
