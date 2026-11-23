#!/usr/bin/env python3
"""PARASITE one-shot SFX: processed Kenney CC0 packs + OpenGameArt CC0 recordings + offline synthesis.
Writes Assets/Parasite/Resources/Audio/Sfx/<name>_<n>.ogg (variants are picked at runtime without repeats).
Sources (scratch, not shipped): $PARASITE_AUDIO_WORK/kenney/{impact-sounds,interface-sounds,sci-fi-sounds,
digital-audio,rpg-audio}, $PARASITE_AUDIO_WORK/oga/{dog_barking_mono.wav,park_ambience_birds.wav}.
Usage: Tools/audio/gen_sfx.sh [group ...]   groups: footsteps ui amoeba world vocal_fx misc
"""
from __future__ import annotations

import glob
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import common as C  # noqa: E402
from common import SR, n_of  # noqa: E402

OUT = os.path.join(C.AUDIO_RES, "Sfx")
KEN = os.path.join(C.WORK, "kenney")
OGA = os.path.join(C.WORK, "oga")
R = np.random.default_rng(1234)


def K(pack, name):
    path = os.path.join(KEN, pack, "Audio", name + ".ogg")
    return C.ffmpeg_read(path, mono=True)


def pitch(x, semis):
    from scipy import signal
    if abs(semis) < 1e-3:
        return x
    ratio = 2 ** (-semis / 12)
    up = int(round(ratio * 1000))
    g = np.gcd(up, 1000)
    return signal.resample_poly(x, up // g, 1000 // g)


def trim(x, thresh_db=-50, pad=0.005):
    a = np.abs(C.to_mono(x))
    if a.max() <= 0:
        return x
    idx = np.where(a > a.max() * C.undb(thresh_db))[0]
    s = max(0, idx[0] - n_of(pad))
    e = min(len(x), idx[-1] + n_of(0.03))
    return x[s:e]


def save(name, x, peak_db=-3.0, lufs=None, stereo=False):
    x = np.asarray(x, dtype=np.float64)
    if not stereo:
        x = C.to_mono(x)
    x = x - np.mean(x, axis=0)
    x = C.fade(trim(x), 0.002, 0.01)
    if lufs is not None:
        x = C.normalize_lufs(x, lufs, ceiling_db=peak_db)
    else:
        x = C.normalize_peak(x, peak_db)
    C.write_ogg(os.path.join(OUT, name + ".ogg"), x, q=5)


LUFS = {  # loudness targets for one-shots (footsteps stay peak-normalised: too short for integrated loudness)
    "possess_hit": -16, "possess_launch": -17, "split": -17, "eject": -16, "release": -18, "capture": -16, "contained": -17,
    "squelch": -18, "blob_hop": -21, "blob_land": -20, "blob_squish": -24, "car_horn": -17, "alarm_siren": -16,
    "alert_pursuit": -17, "alert_curious": -18, "lab_beep": -24, "drip": -24, "dog_bark": -20, "siren_distant": -19,
    "bird": -22, "jump": -24, "land": -21, "whoosh": -21, "door_open": -18, "door_close": -18, "terminal_beep": -21,
    "denied": -20, "switch": -21, "ui_click": -22, "ui_hover": -28, "ui_open": -21, "ui_close": -21, "ui_back": -22,
    "objective": -20, "checkpoint": -20, "radio_squelch": -24,
}


def save_set(base, items, **kw):
    if base in LUFS and "lufs" not in kw:
        kw["lufs"] = LUFS[base]
        kw["peak_db"] = min(kw.get("peak_db", -1.0), -1.0)
    for f in glob.glob(os.path.join(OUT, base + "_*.ogg")):
        tail = os.path.basename(f)[len(base) + 1:-4]
        if tail.isdigit() and int(tail) > len(items):
            os.remove(f)
            if os.path.exists(f + ".meta"):
                os.remove(f + ".meta")
    for i, x in enumerate(items):
        save(f"{base}_{i + 1}", x, **kw)
    print(f"  {base} ×{len(items)}", flush=True)


def layer(*parts):
    n = max(len(p[0]) + n_of(p[1]) for p in parts)
    out = np.zeros(n)
    for sig, t, g in parts:
        C.mix_into(out, C.to_mono(sig), n_of(t), g)
    return out


def room(x, sec=0.5, decay=0.35, wet=0.15, seed=0):
    ir = C.make_ir(sec, decay, 0.004, 6000, early=8, seed=seed)[:, 0]
    from scipy import signal
    w = signal.oaconvolve(x, ir)
    y = np.zeros(len(w))
    y[:len(x)] = x
    return y + w * wet


# ----------------------------------------------------------------------------- synthesis primitives
def squelch(dur=0.35, f0=900, f1=180, seed=0, bubbles=8, low=0.8, res=0.82):
    r = np.random.default_rng(seed)
    n = n_of(dur)
    t = np.arange(n) / SR
    x = t / dur
    nz = r.standard_normal(n)
    gate = np.clip(np.sin(2 * np.pi * r.uniform(25, 55) * t + r.uniform(0, 6)) * 0.6 + 0.6, 0, 1)
    env = np.minimum(1, t / 0.008) * (1 - x) ** 1.6
    out = np.zeros(n)
    for k, mul in enumerate((1.0, 1.9, 3.1)):
        cut = f0 * mul * (f1 / f0) ** (x ** 0.7) * (1 + 0.15 * np.sin(2 * np.pi * r.uniform(8, 18) * t))
        out += C.svf(nz, cut, res - 0.1 * k, 1) * (0.8 / (k + 1))
    out *= env * (0.55 + 0.45 * gate)
    # viscous low blob
    f = 170 * (60 / 170) ** x
    out += np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / (dur * 0.35)) * low * np.minimum(1, t / 0.004)
    for _ in range(bubbles):
        st = r.uniform(0.02, dur * 0.85)
        d = r.uniform(0.01, 0.03)
        fb = r.uniform(500, 1800)
        m = n_of(d)
        tt = np.arange(m) / SR
        b = np.sin(2 * np.pi * fb * (1 + 3 * tt / d) * tt) * np.exp(-tt / (d * 0.35)) * r.uniform(0.15, 0.4)
        C.mix_into(out, b, n_of(st))
    return out


def whoosh(dur=0.5, lo=300, hi=2600, seed=0, peak_at=0.55):
    r = np.random.default_rng(seed)
    n = n_of(dur)
    x = np.arange(n) / n
    bell = np.where(x < peak_at, (x / peak_at) ** 2, ((1 - x) / (1 - peak_at)) ** 1.5)
    cut = lo + (hi - lo) * bell
    y = C.svf(r.standard_normal(n), cut, 0.45, 1) * bell
    return y


def thump(f0=120, f1=45, decay=0.12, dur=0.5):
    n = n_of(dur)
    t = np.arange(n) / SR
    f = f1 + (f0 - f1) * np.exp(-t / (decay * 0.4))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / decay) * np.minimum(1, t / 0.002)


def droplets(count, span, fmin=1200, fmax=3800, seed=0, amp=0.4):
    r = np.random.default_rng(seed)
    out = np.zeros(n_of(span + 0.08))
    for _ in range(count):
        d = r.uniform(0.012, 0.04)
        m = n_of(d)
        tt = np.arange(m) / SR
        f = r.uniform(fmin, fmax)
        b = np.sin(2 * np.pi * f * (1 + 1.5 * tt / d) * tt) * np.exp(-tt / (d * 0.3)) * r.uniform(0.3, 1.0) * amp
        C.mix_into(out, b, n_of(r.uniform(0, span)))
    return out


def splash(seed=0, size=1.0):
    r = np.random.default_rng(seed)
    n = n_of(0.22 * size)
    t = np.arange(n) / SR
    burst = C.bandpass(r.standard_normal(n), 700, 5200) * np.exp(-t / (0.04 * size)) * np.minimum(1, t / 0.002)
    return layer((burst, 0, 0.8), (droplets(int(6 * size), 0.12 * size, 900, 3200, seed + 1, 0.5), 0.01, 1.0))


def click(freq=3200, seed=0, body=0.4):
    r = np.random.default_rng(seed)
    n = n_of(0.08)
    t = np.arange(n) / SR
    c = C.svf(r.standard_normal(n) * np.exp(-t / 0.002), np.full(n, freq), 0.9, 1) * 2.5
    c += thump(260, 120, 0.012, 0.08) * body
    return c * np.exp(-t / 0.02)


def tone_seq(notes, shape="sine", gap=0.02, amp=0.6):
    out = []
    for f, d in notes:
        n = n_of(d)
        t = np.arange(n) / SR
        s = np.sin(2 * np.pi * f * t)
        if shape == "square":
            s = np.tanh(s * 4) * 0.7
        env = np.minimum(1, t / 0.004) * np.minimum(1, (d - t) / 0.012)
        out.append(s * env * amp)
        out.append(np.zeros(n_of(gap)))
    return np.concatenate(out)


# ----------------------------------------------------------------------------- groups
def footsteps():
    conc = [trim(K("impact-sounds", f"footstep_concrete_{i:03d}")) for i in range(5)]
    grass = [trim(K("impact-sounds", f"footstep_grass_{i:03d}")) for i in range(5)]
    snow = [trim(K("impact-sounds", f"footstep_snow_{i:03d}")) for i in range(5)]
    plate = [trim(K("impact-sounds", f"impactPlate_light_{i:03d}")) for i in range(5)]
    soft = [trim(K("impact-sounds", f"impactSoft_medium_{i:03d}")) for i in range(5)]
    rpg = [trim(K("rpg-audio", f"footstep0{i}")) for i in range(10)]

    def v6(srcs, fn):
        return [fn(srcs[i % len(srcs)], i) for i in range(6)]

    save_set("fs_concrete", v6(conc + rpg[:3], lambda x, i: C.peaking(pitch(x, R.uniform(-1, 1)), 250, 2, 1.0)), peak_db=-4)
    save_set("fs_asphalt", v6(conc, lambda x, i: layer((C.lowpass(pitch(x, R.uniform(-2, -0.5)), 3800), 0, 1.0),
                                                       (C.bandpass(R.standard_normal(n_of(0.05)), 2000, 7000) * np.exp(-np.arange(n_of(0.05)) / SR / 0.01), 0.004, 0.08))),
             peak_db=-4)
    save_set("fs_cobble", v6(conc, lambda x, i: layer((C.peaking(pitch(x, R.uniform(0, 1.5)), 2600, 4, 1.2), 0, 1.0),
                                                      (C.peaking(pitch(x, R.uniform(2, 4)), 3000, 3, 1.0), R.uniform(0.008, 0.02), 0.45))),
             peak_db=-4)
    save_set("fs_tile", v6(conc, lambda x, i: room(layer((C.highpass(C.peaking(pitch(x, R.uniform(0.5, 2)), 3500, 5, 1.2), 180), 0, 1.0),
                                                         (click(R.uniform(2800, 4200), i, 0.2), 0.002, 0.25)), 0.4, 0.25, 0.18, i)),
             peak_db=-4)
    save_set("fs_metal", v6(conc, lambda x, i: layer((pitch(x, R.uniform(-1, 0.5)), 0, 0.8),
                                                     (C.lowpass(pitch(plate[i % 5], R.uniform(-3, 0)), 5000), 0.003, 0.55))), peak_db=-4)
    save_set("fs_grass", v6(grass, lambda x, i: pitch(x, R.uniform(-1, 1))), peak_db=-5)
    save_set("fs_gravel", v6(snow, lambda x, i: C.peaking(C.highpass(pitch(x, R.uniform(1, 3)), 300), 3500, 4, 0.8)), peak_db=-5)
    save_set("fs_splash", [C.highpass(splash(100 + i, R.uniform(0.7, 1.1)), 300) for i in range(6)], peak_db=-5)
    save_set("fs_heel", [click(R.uniform(2600, 3600), 200 + i, 0.6) for i in range(4)], peak_db=-5)
    save_set("fs_boot", [layer((C.lowpass(pitch(soft[i % 5], -3 + R.uniform(-1, 1)), 900), 0, 1.0), (thump(110, 55, 0.04, 0.2), 0, 0.4)) for i in range(4)], peak_db=-4)


def ui():
    I = lambda n: K("interface-sounds", n)
    save_set("ui_hover", [C.lowpass(I(f"tick_00{i}"), 9000) for i in (1, 2, 4)], peak_db=-9)
    save_set("ui_click", [I(f"select_00{i}") for i in (1, 2, 3)], peak_db=-6)
    save_set("ui_open", [layer((I("maximize_006"), 0, 1.0), (whoosh(0.25, 800, 4000, 3) * 0.25, 0, 1.0))], peak_db=-6)
    save_set("ui_close", [layer((I("minimize_006"), 0, 1.0), (whoosh(0.25, 4000, 800, 4, 0.3) * 0.25, 0, 1.0))], peak_db=-6)
    save_set("ui_back", [I("back_002")], peak_db=-6)
    save_set("denied", [I("error_004"), I("error_006")], peak_db=-5)
    save_set("switch", [I("switch_002"), I("switch_005")], peak_db=-6)
    # objective: soft pluck + bell (G5 → D6, in key); checkpoint: rising fifth on bells
    obj = layer((C.pluck(C.mtof(79), 1.2, 0.7, seed=1), 0, 0.6), (C.fm_bell(C.mtof(86), 1.6, 0.5, ratio=2.0, index=1.0), 0.12, 0.6))
    save_set("objective", [C.to_mono(C.reverb(obj, C.make_ir(1.5, 1.2, 0.01, 7000, seed=3), 0.2))], peak_db=-6)
    chk = layer((C.fm_bell(C.mtof(74), 1.8, 0.6, ratio=2.0, index=1.2), 0, 0.6), (C.fm_bell(C.mtof(81), 2.0, 0.6, ratio=2.0, index=1.2), 0.16, 0.6),
                (C.fm_bell(C.mtof(86), 2.2, 0.5, ratio=2.0, index=1.0), 0.32, 0.5))
    save_set("checkpoint", [C.to_mono(C.reverb(chk, C.make_ir(2.0, 1.6, 0.01, 7000, seed=4), 0.25))], peak_db=-6)
    # terminals and doors
    beeps = [K("digital-audio", "twoTone1"), K("digital-audio", "threeTone1"), C.ffmpeg_read(os.path.join(C.AUDIO_RES, "terminal_beep.ogg"))]
    save_set("terminal_beep", [C.lowpass(b, 9000) for b in beeps], peak_db=-6)
    save_set("door_open", [layer((K("sci-fi-sounds", f"doorOpen_00{i}"), 0, 1.0), (C.ffmpeg_read(os.path.join(C.AUDIO_RES, "door_clunk.ogg")), 0.35, 0.35))
                           for i in range(3)], peak_db=-3)
    save_set("door_close", [layer((K("sci-fi-sounds", f"doorClose_00{i}"), 0, 1.0), (thump(90, 45, 0.08, 0.4), 0.3, 0.3)) for i in range(3)], peak_db=-3)


def amoeba():
    s1 = K("sci-fi-sounds", "slime_001")  # 4.7 s of continuous squishing: sliced into separate gestures
    cut = lambda a, b: C.fade(s1[n_of(a):n_of(b)], 0.01, 0.06)
    slime = [trim(K("sci-fi-sounds", "slime_000")), cut(0.95, 1.5), cut(2.1, 2.7), cut(3.15, 3.75)]
    softh = [trim(K("impact-sounds", f"impactSoft_heavy_{i:03d}")) for i in range(5)]
    starts = [0.3, 1.25, 1.9, 2.55, 3.3, 4.1]
    save_set("blob_squish", [layer((C.lowpass(cut(a, a + 0.22), 7000), 0, 0.8),
                                   (C.lowpass(squelch(R.uniform(0.12, 0.2), R.uniform(700, 1100), R.uniform(250, 400), 300 + i, 3, 0.4), 6000), 0, 0.5))
                             for i, a in enumerate(starts)], peak_db=-6)
    save_set("blob_hop", [layer((squelch(0.28, 500, 1300, 400 + i, 5, 0.5), 0, 1.0), (whoosh(0.2, 400, 1500, i) * 0.3, 0.05, 1.0)) for i in range(4)], peak_db=-4)
    save_set("blob_land", [layer((squelch(0.3, 1400, 250, 500 + i, 9, 1.0), 0, 1.0), (droplets(8, 0.2, 800, 2600, i), 0.03, 0.6),
                                 (thump(130, 50, 0.06, 0.3), 0, 0.5)) for i in range(4)], peak_db=-3)
    save_set("possess_launch", [layer((whoosh(0.55, 250, 3200, 600 + i), 0, 1.0), (pitch(slime[i % 4], 3), 0, 0.6),
                                      (squelch(0.25, 600, 1500, 610 + i, 6, 0.4), 0, 0.7)) for i in range(3)], peak_db=-2)
    save_set("possess_hit", [layer((squelch(0.45, 1500, 160, 700 + i, 12, 1.0), 0, 1.0), (C.lowpass(softh[i], 2500), 0, 0.8),
                                   (pitch(slime[(i + 1) % 4], -2), 0.02, 0.6), (thump(150, 42, 0.12, 0.6), 0, 0.7)) for i in range(3)], peak_db=-1.5)
    save_set("release", [layer((pitch(slime[(i + 2) % 4][::-1], -1), 0, 0.7), (squelch(0.4, 300, 1200, 800 + i, 10, 0.5), 0.05, 0.9),
                               (whoosh(0.4, 800, 300, 810 + i, 0.3) * 0.4, 0.1, 1.0)) for i in range(3)], peak_db=-2)
    save_set("split", [layer((squelch(0.7, 200, 1600, 900 + i, 14, 0.7, 0.88), 0, 1.0), (pitch(slime[i + 1], 2), 0.25, 0.7),
                             (squelch(0.25, 1800, 300, 910 + i, 8, 0.8), 0.45, 0.9), (thump(200, 70, 0.05, 0.2), 0.47, 0.6)) for i in range(2)], peak_db=-2)
    save_set("eject", [layer((squelch(0.5, 2000, 200, 1000 + i, 16, 1.0), 0, 1.0), (whoosh(0.45, 400, 3000, 1010 + i, 0.2), 0, 0.6),
                             (C.lowpass(softh[(i + 2) % 5], 2000), 0, 0.7), (droplets(14, 0.35, 700, 2500, i), 0.05, 0.7)) for i in range(3)], peak_db=-1.5)
    save_set("squelch", [squelch(0.6, 1200, 140, 1100 + i, 14, 0.9) for i in range(3)], peak_db=-2)
    # capture: metal clank + electric zap; contained: force field zap
    clank = [C.ffmpeg_read(os.path.join(C.AUDIO_RES, f"capture_clank_{i}.ogg")) for i in (1, 2)]
    metal = [trim(K("impact-sounds", f"impactMetal_heavy_{i:03d}")) for i in range(5)]
    field = [trim(K("sci-fi-sounds", f"forceField_00{i}")) for i in range(5)]
    save_set("capture", [layer((clank[i], 0, 0.9), (metal[i], 0.01, 0.6), (C.highpass(field[i], 400), 0.06, 0.5)) for i in range(2)], peak_db=-1.5)
    zap = C.ffmpeg_read(os.path.join(C.AUDIO_RES, "contained_zap.ogg"))
    save_set("contained", [layer((zap, 0, 0.8), (field[2 + i], 0, 0.7), (thump(90, 35, 0.2, 0.8), 0, 0.6)) for i in range(2)], peak_db=-1.5)


def world():
    # car horns (two detuned square tones, bandpassed)
    horns = []
    for i, pattern in enumerate(([0.35], [0.12, 0.18], [0.7])):
        segs = []
        for d in pattern:
            n = n_of(d)
            t = np.arange(n) / SR
            s = C.osc_square(np.full(n, 405.0 + i * 12), 0.0, 0.5) + C.osc_square(np.full(n, 510.0 + i * 10), 0.3, 0.5) * 0.8
            s = np.tanh(C.bandpass(s, 300, 3000) * 2) * np.minimum(1, t / 0.01) * np.minimum(1, (d - t) / 0.03)
            segs += [s, np.zeros(n_of(0.09))]
        horns.append(C.lowpass(np.concatenate(segs), 4500))
    save_set("car_horn", horns, peak_db=-3)
    save_set("radio_squelch", [layer((C.bandpass(R.standard_normal(n_of(0.12)), 1000, 5500) * np.exp(-np.arange(n_of(0.12)) / SR / 0.04), 0, 0.8),
                                     (tone_seq([(1850 - i * 120, 0.05)]), 0.1, 0.4)) for i in range(3)], peak_db=-6)
    # tunnel drips: plinks
    drips = []
    for i in range(6):
        n = n_of(0.25)
        t = np.arange(n) / SR
        f0 = R.uniform(800, 1500)
        f = f0 * (1 + 1.2 * (1 - np.exp(-t / 0.012)))
        d = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.035) * np.minimum(1, t / 0.0015)
        d += C.highpass(R.standard_normal(n), 3000) * np.exp(-t / 0.003) * 0.15
        drips.append(room(d, 0.9, 0.7, 0.25, i))
    save_set("drip", drips, peak_db=-6)
    # lab machine beeps
    pats = [[(1046, 0.07)], [(1318, 0.06), (1318, 0.06)], [(880, 0.12), (1174, 0.12)], [(2093, 0.04)] * 3, [(660, 0.18)], [(1568, 0.08), (1046, 0.1)]]
    save_set("lab_beep", [C.lowpass(tone_seq(p, "square" if i % 3 == 0 else "sine", 0.05, 0.5), 5000) for i, p in enumerate(pats)], peak_db=-8)
    # distant thunder (stereo, 2D)
    th = []
    for i in range(3):
        n = n_of(R.uniform(6, 9))
        t = np.arange(n) / SR
        b = C.brown(n, 1300 + i)
        env = np.zeros(n)
        for _ in range(R.integers(3, 6)):
            c = R.uniform(0.2, 0.6) * n / SR
            env += np.exp(-((t - c) / R.uniform(0.4, 1.4)) ** 2) * R.uniform(0.5, 1)
        env += np.exp(-t / 3) * np.minimum(1, t / 0.4) * 0.6
        rumble = C.lowpass(b, 380) * env
        th.append(C.reverb(np.stack([rumble, C.lowpass(C.brown(n, 1400 + i), 380) * env], 1), C.make_ir(3, 2.5, 0.05, 1500, seed=i), 0.4)[:n + n_of(1.5)])
    for i, x in enumerate(th):
        save(f"thunder_{i + 1}", C.fade(x, 0.3, 1.5), peak_db=-4, stereo=True)
    print("  thunder ×3", flush=True)
    # distant sirens: wail / yelp / hi-lo, with distance filtering, slow doppler and outdoor reverb
    sir = []
    for i, kind in enumerate(("wail", "yelp", "hilo")):
        dur = 11.0
        n = n_of(dur)
        t = np.arange(n) / SR
        if kind == "wail":
            f = 900 + 500 * np.sin(2 * np.pi * t / 3.6 - 1.2)
        elif kind == "yelp":
            f = 950 + 450 * np.sin(2 * np.pi * t / 0.38)
        else:
            f = np.where((t % 1.2) < 0.6, 950.0, 760.0)
        f = f * (1.03 - 0.06 * t / dur)  # passing by: slight downward doppler
        ph = 2 * np.pi * np.cumsum(f) / SR
        s = np.sin(ph) + 0.35 * np.sin(2 * ph) + 0.15 * np.sin(3 * ph)
        env = np.sin(np.pi * np.clip(t / dur, 0, 1)) ** 1.5
        s = C.lowpass(np.tanh(s * 1.5) * env, 2200)
        s = C.to_mono(C.reverb(s, C.make_ir(3.0, 2.4, 0.08, 2500, seed=20 + i), 0.6))[:n + n_of(2)]
        sir.append(C.fade(s, 0.5, 2.0))
    save_set("siren_distant", sir, peak_db=-4)
    # dog barks from the OGA CC0 recording, split into short phrases + distance processing
    dog = C.read(os.path.join(OGA, "dog_barking_mono.wav"), mono=True)
    env = np.convolve(np.abs(dog), np.ones(n_of(0.02)) / n_of(0.02), mode="same")
    on = env > env.max() * 0.08
    segs, i = [], 0
    while i < len(on):
        if on[i]:
            j = i
            while j < len(on) and (on[j] or (j + n_of(0.12) < len(on) and on[j:j + n_of(0.12)].any())):
                j += 1
            if j - i > n_of(0.06):
                segs.append((max(0, i - n_of(0.01)), min(len(dog), j + n_of(0.05))))
            i = j
        i += 1
    barks = [dog[a:b] for a, b in segs] or [dog]
    phrases = []
    for k in range(3):
        parts = [(barks[(k + m) % len(barks)], m * R.uniform(0.32, 0.5), R.uniform(0.7, 1.0)) for m in range(1 + k % 3)]
        p = layer(*parts)
        p = C.lowpass(C.highpass(p, 250), 3200)
        phrases.append(C.to_mono(C.reverb(p, C.make_ir(2.0, 1.6, 0.04, 3000, seed=40 + k), 0.45)))
    save_set("dog_bark", phrases, peak_db=-4)
    # dusk birds: chirp phrases cut from the Thimras park recording (CC0)
    birds = C.ffmpeg_read(os.path.join(OGA, "park_ambience_birds.wav"), mono=True, start=20, dur=200)
    hp = C.highpass(birds, 1800, order=4)
    frame = n_of(0.05)
    nf = len(hp) // frame
    e = np.sqrt(np.mean(hp[:nf * frame].reshape(nf, frame) ** 2, axis=1))
    order = np.argsort(e)[::-1]
    picked = []
    for fi in order:
        c = fi * frame
        if all(abs(c - p) > n_of(4) for p in picked):
            picked.append(c)
        if len(picked) >= 6:
            break
    chirps = []
    for c in picked:
        seg = hp[max(0, c - n_of(0.4)):c + n_of(0.9)]
        chirps.append(C.fade(C.lowpass(seg, 9000), 0.08, 0.25))
    save_set("bird", chirps, peak_db=-6)
    # alarm siren (gameplay), pursuit stab, heartbeat
    n = n_of(1.5)
    t = np.arange(n) / SR
    f = 820 + 260 * np.sin(2 * np.pi * 1.3 * t - 1.5)
    ph = 2 * np.pi * np.cumsum(f) / SR
    al = np.tanh((np.sin(ph) + 0.4 * np.sin(2 * ph) + 0.2 * np.sin(3 * ph)) * 2.0)
    al = C.to_mono(C.reverb(C.bandpass(al, 400, 4500) * np.minimum(1, t / 0.03) * np.minimum(1, (1.5 - t) / 0.1), C.make_ir(1.2, 0.9, 0.02, 4000, seed=50), 0.2))
    save_set("alarm_siren", [al], peak_db=-3)
    stab = np.zeros(n_of(2.0))
    for j, m in enumerate((38, 44, 50, 51)):
        nn = n_of(1.2)
        sv = C.supersaw(C.mtof(m), nn, voices=5, detune=0.12, seed=60 + j)
        sv = C.svf_st(sv, 500 + 3000 * np.exp(-np.arange(nn) / SR / 0.15), res=0.2) * C.env_adsr(nn, 0.005, 0.3, 0.5, 0.5)[:, None]
        C.mix_into(stab, C.to_mono(sv), 0, 0.3)
    C.mix_into(stab, C.taiko(1.0, seed=61), 0, 0.8)
    stab = C.to_mono(C.reverb(stab, C.make_ir(2.0, 1.5, 0.02, 5000, seed=62), 0.3))
    save_set("alert_pursuit", [stab], peak_db=-2)
    save_set("alert_curious", [C.ffmpeg_read(os.path.join(C.AUDIO_RES, "alert_curious.ogg"))], peak_db=-4)
    hb = np.zeros(n_of(1.0))
    C.mix_into(hb, thump(68, 36, 0.09, 0.6), n_of(0.01), 1.0)
    C.mix_into(hb, thump(60, 34, 0.08, 0.6), n_of(0.29), 0.75)
    C.mix_into(hb, C.lowpass(R.standard_normal(n_of(0.06)), 220) * np.exp(-np.arange(n_of(0.06)) / SR / 0.015), n_of(0.01), 0.5)
    C.write_ogg(os.path.join(OUT, "heartbeat_1.ogg"), C.normalize_peak(hb[:n_of(1.0)], -2), q=5)  # exact 1.0 s loop
    print("  heartbeat ×1", flush=True)


def misc():
    cloth = [trim(K("rpg-audio", f"cloth{i}")) for i in range(1, 5)]
    soft = [trim(K("impact-sounds", f"impactSoft_medium_{i:03d}")) for i in range(5)]
    conc = [trim(K("impact-sounds", f"footstep_concrete_{i:03d}")) for i in range(5)]
    save_set("jump", [layer((cloth[i], 0, 0.8), (whoosh(0.3, 300, 1500, 70 + i) * 0.35, 0, 1.0)) for i in range(3)], peak_db=-5)
    save_set("land", [layer((C.lowpass(soft[i], 3000), 0, 0.9), (conc[i], 0.01, 0.6), (conc[(i + 2) % 5], 0.06, 0.45)) for i in range(3)], peak_db=-3)
    save_set("whoosh", [whoosh(R.uniform(0.35, 0.6), 250, 2800, 80 + i) for i in range(3)], peak_db=-4)


GROUPS = dict(footsteps=footsteps, ui=ui, amoeba=amoeba, world=world, misc=misc)


def main():
    os.makedirs(OUT, exist_ok=True)
    for g in sys.argv[1:] or list(GROUPS):
        print(f"[{g}]", flush=True)
        GROUPS[g]()


if __name__ == "__main__":
    main()
