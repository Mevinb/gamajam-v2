#!/usr/bin/env python3
"""PARASITE adaptive score — composed and rendered offline with numpy (no samples, CC0 / original work).

Key D minor. Leitmotif = the opening's "four taps" (three repeated notes, a pause, then the resolving beat).
Outputs Assets/Parasite/Resources/Audio/Music/*.ogg (44.1 kHz stereo Vorbis):
  mus_city_calm / mus_lab_calm / mus_tension / mus_chase — sample-locked 84 BPM, 24-bar stems (68.571 s each)
  mus_title, mus_story, mus_opening (80 s, follows the cinematic's shots), mus_lakeside, mus_end_peace,
  mus_end_dominion, stinger_mission; Sfx/ending_sting (reveal/escape hit).
Loops are rendered with their reverb/release tails folded back onto the start, so they repeat seamlessly.
Usage: Tools/audio/gen_music.sh [name ...]
"""
from __future__ import annotations

import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import common as C  # noqa: E402
from common import SR, n_of, mtof, pan  # noqa: E402

OUT = os.path.join(C.AUDIO_RES, "Music")
R = np.random.default_rng(7)


class Mix:
    """Bus-based arrangement buffer. Positions in seconds; signals mono (panned) or stereo."""

    def __init__(self, seconds, tail=8.0):
        self.length = seconds
        self.n = n_of(seconds)
        self.total = self.n + n_of(tail)
        self.buses = {}

    def bus(self, name):
        if name not in self.buses:
            self.buses[name] = np.zeros((self.total, 2))
        return self.buses[name]

    def put(self, name, sig, t, gain=1.0, p=0.0):
        if t >= self.total / SR:
            return
        x = sig if sig.ndim == 2 else pan(sig, p)
        C.mix_into(self.bus(name), x, n_of(max(0.0, t)), gain)


IRS = {}


def ir(name):
    if name not in IRS:
        IRS[name] = {
            "hall": lambda: C.make_ir(4.0, 3.2, 0.03, 5500, early=10, width=1.0, seed=11),
            "room": lambda: C.make_ir(1.4, 0.9, 0.012, 7000, early=12, width=0.8, seed=12),
            "plate": lambda: C.make_ir(2.6, 2.0, 0.008, 9000, early=4, width=1.0, seed=13),
            "cave": lambda: C.make_ir(5.0, 4.5, 0.05, 3500, early=8, width=1.0, seed=14),
        }[name]()
    return IRS[name]


def render(mix: Mix, spec: dict, loop=True, lufs=-16.0, master_comp=True):
    """spec: bus -> dict(rev=name, wet=float, hp=Hz, lp=Hz, gain=dB, comp=bool, width=float)"""
    out = np.zeros((mix.total, 2))
    sends = {}
    for name, buf in mix.buses.items():
        s = spec.get(name, {})
        x = buf
        if s.get("hp"):
            x = C.highpass(x, s["hp"])
        if s.get("lp"):
            x = C.lowpass(x, s["lp"])
        if s.get("comp"):
            x = C.compress(x, -22, 2.5, 0.01, 0.2, makeup=2)
        if s.get("width") is not None:
            mid = x.mean(axis=1, keepdims=True)
            x = mid + (x - mid) * s["width"]
        x = x * C.undb(s.get("gain", 0.0))
        out += x
        if s.get("rev"):
            sends.setdefault(s["rev"], np.zeros_like(out))
            sends[s["rev"]] += x * s.get("wet", 0.25)
    for name, send in sends.items():
        wet = C.reverb(send, ir(name), wet=1.0, dry=0.0)[: mix.total]
        out += C.highpass(wet, 120)
    if master_comp:
        out = C.compress(out, -20, 1.8, 0.03, 0.3, knee=8)
    if loop:
        out = C.fold_loop(out, mix.n)
    else:
        # one-shots: trim the tail where it decays below -70 dB
        env = np.max(np.abs(out), axis=1)
        last = np.where(env > C.undb(-70) * env.max())[0]
        out = out[: (last[-1] + n_of(0.2)) if len(last) else mix.n]
        out = C.fade(out, 0.0, 0.05)
    out = C.normalize_lufs(out, lufs, ceiling_db=-1.2)
    return out


def save(name, x, sub="Music", q=5):
    path = os.path.join(C.AUDIO_RES, sub, name + ".ogg")
    C.write_ogg(path, x, q=q)
    print(f"  {sub}/{name}.ogg  {len(x) / SR:6.2f}s  {C.loudness(x):6.1f} LUFS  peak {C.db(np.max(np.abs(x))):5.1f} dBFS", flush=True)


def jit(ms=6):
    return R.uniform(-ms, ms) / 1000.0


# ----------------------------------------------------------------------------- harmony
# (name, bass midi, pad voicing) — two bars each, 24-bar gameplay loop
PROG = [
    ("Dm9", 38, [50, 53, 57, 60, 64]),
    ("Bbmaj7", 34, [50, 53, 57, 58]),
    ("Gm9", 31, [46, 50, 53, 57]),
    ("A7sus4", 33, [45, 50, 52, 55]),
    ("Dm9", 38, [50, 53, 57, 60, 64]),
    ("Fmaj7", 41, [48, 52, 53, 57]),
    ("Bbmaj7#11", 34, [50, 53, 57, 64]),
    ("C6/9", 36, [50, 52, 55, 57]),
    ("Gm9", 31, [46, 50, 53, 57]),
    ("Dm/F", 41, [50, 53, 57, 62]),
    ("Ebmaj7#11", 39, [46, 50, 55, 57]),
    ("A7", 33, [49, 52, 55, 57]),
]
BPM = 84.0
SPB = 60.0 / BPM
BARS = 24
LOOP = BARS * 4 * SPB  # 68.571 s


def T(bar, beat=0.0, spb=SPB):
    return (bar * 4 + beat) * spb


def chord_at(bar):
    return PROG[(bar // 2) % len(PROG)]


def motif(m: Mix, bus, start, notes, step, inst, gain=1.0, p=0.0, gap=1.0):
    """Leitmotif: three repeated notes `step` apart, a pause, then the resolving note."""
    a, b = notes
    for k, t in enumerate([0, step, 2 * step, 3 * step + gap * step]):
        note = a if k < 3 else b
        m.put(bus, inst(mtof(note), 0.9 if k < 3 else 2.4, 0.55 if k < 3 else 0.75), start + t + jit(3), gain, p)


# ----------------------------------------------------------------------------- gameplay stems
def stem_city():
    m = Mix(LOOP)
    for bar in range(0, BARS, 2):
        name, bass, tones = chord_at(bar)
        t0 = T(bar)
        dur = 2 * 4 * SPB
        for i, nte in enumerate(tones):
            m.put("pad", C.pad_voice(mtof(nte), dur + 0.3, attack=1.6, release=2.2, cutoff=1700 + 200 * i, seed=bar * 10 + i,
                                     detune=0.1, voices=5), t0, 0.16)
        # sub bass: root on 1 (long), fifth/octave pickup on beat 4 of the 2nd bar
        m.put("bass", C.bass_voice(mtof(bass), 1.8 * SPB * 4 / 2, vel=0.8, cutoff=320, drive=1.2, sub=1.0, release=0.4), t0 + jit(), 0.5)
        m.put("bass", C.bass_voice(mtof(bass), 2.6 * SPB, vel=0.6, cutoff=300, drive=1.1, sub=1.0, release=0.4), T(bar + 1) + jit(), 0.42)
        m.put("bass", C.bass_voice(mtof(bass + 7 if bass + 7 < 48 else bass - 5), 0.9 * SPB, vel=0.5, cutoff=300, sub=1.0, release=0.2),
              T(bar + 1, 3) + jit(), 0.35)
        # electric piano comping (syncopated, soft), upper voicing
        for b2 in (0, 1):
            for beat, ln, vel in ((0.0, 1.5, 0.5), (2.5, 1.2, 0.38)):
                for j, nte in enumerate(tones[1:]):
                    nn = nte + 12 if nte < 55 else nte
                    m.put("ep", C.epiano(mtof(nn), ln * SPB, vel * R.uniform(0.85, 1.0)), T(bar + b2, beat) + jit(9) + j * 0.008,
                          0.16, p=(j - 1.5) * 0.25)
    # pluck arpeggio from bar 8 on (develops the loop), dotted-8th ping-pong delay on its bus
    for bar in range(8, BARS):
        name, bass, tones = chord_at(bar)
        up = sorted([t + 12 for t in tones])
        pat = [0, 2, 4, 2, 1, 3, 4, 3]
        for k in range(8):
            if bar % 4 == 3 and k >= 6:
                continue
            nte = up[pat[k] % len(up)]
            m.put("pluck", C.pluck(mtof(nte), 1.2, vel=R.uniform(0.5, 0.75), decay=0.997, bright=0.5, seed=bar * 8 + k),
                  T(bar, k * 0.5) + jit(5), 0.24, p=0.35 if k % 2 else -0.35)
    # leitmotif on a soft bell (bars 4, 12, 20) answering the opening
    for bar in (4, 12, 20):
        motif(m, "bell", T(bar, 0.0), (69, 74), SPB * 0.5, lambda f, d, v: C.fm_bell(f, d + 1.5, v, ratio=2.0, index=1.2), 0.22)
    # soft heartbeat pulse (kick lowpassed) on 1 and the "and" of 2, bars 16-24
    for bar in range(16, BARS):
        m.put("perc", C.kick(0.5, f0=90, f1=42, decay=0.25, click=0.0), T(bar, 0), 0.35)
        m.put("perc", C.kick(0.35, f0=85, f1=42, decay=0.2, click=0.0), T(bar, 1.5), 0.3)
        for k in range(8):
            m.put("perc", C.shaker(0.25 if k % 2 else 0.4, seed=bar * 8 + k), T(bar, k * 0.5) + jit(4), 0.25, p=0.3)
    m.buses["pluck"] = C.delay(m.bus("pluck"), SPB * 0.75, fb=0.35, mix=0.35, lp=2800)
    m.buses["ep"] = C.chorus(m.bus("ep"), mix=0.4)
    spec = {"pad": dict(rev="hall", wet=0.35, hp=90, width=1.2), "bass": dict(lp=400, gain=-4.5),
            "ep": dict(rev="hall", wet=0.25, hp=120, gain=1.5), "pluck": dict(rev="hall", wet=0.3, hp=200, lp=7000),
            "bell": dict(rev="hall", wet=0.45, hp=300), "perc": dict(rev="room", wet=0.15, lp=6000, gain=-3)}
    return render(m, spec, lufs=-17.0)


def stem_lab():
    m = Mix(LOOP)
    for bar in range(0, BARS, 2):
        name, bass, tones = chord_at(bar)
        t0 = T(bar)
        dur = 2 * 4 * SPB
        for i, nte in enumerate(tones):
            # glassy pad: thin supersaw, high-passed, slow tremolo
            v = C.pad_voice(mtof(nte + 12), dur + 0.3, attack=2.0, release=2.5, cutoff=2600, seed=200 + bar * 10 + i,
                            detune=0.06, voices=3, res=0.3, lfo=0.08)
            tt = np.arange(len(v)) / SR
            v *= (0.8 + 0.2 * np.sin(2 * np.pi * (0.5 + 0.1 * i) * tt))[:, None]
            m.put("pad", v, t0, 0.1)
        m.put("bass", C.osc_sine(np.full(n_of(dur + 0.5), mtof(bass))) * C.env_adsr(n_of(dur + 0.5), 1.0, 0.5, 0.9, 1.0), t0, 0.32)
        # machine pulse: lowpassed square, 8ths on root/fifth
        for k in range(16):
            nte = bass + 12 + (7 if k % 4 == 2 else 0)
            n = n_of(0.16)
            sq = C.osc_square(np.full(n, mtof(nte)), 0.0, 0.35) * C.exp_env(n, 0.05)
            sq = C.svf(sq, np.full(n, 900.0), 0.2, 0)
            m.put("pulse", sq, T(bar, k * 0.5) + jit(2), 0.1 * (1.0 if k % 2 == 0 else 0.6), p=-0.2)
    # sparse FM bell arpeggio (sterile), random subset of 16ths
    for bar in range(BARS):
        name, bass, tones = chord_at(bar)
        up = sorted([t + 24 for t in tones])
        for k in range(16):
            if R.random() < 0.28:
                nte = up[R.integers(0, len(up))]
                m.put("bells", C.fm_bell(mtof(nte), 2.2, R.uniform(0.3, 0.6), ratio=3.5, index=1.8, tau=0.5),
                      T(bar, k * 0.25) + jit(3), 0.14, p=R.uniform(-0.7, 0.7))
    for bar in (4, 12, 20):
        motif(m, "bells", T(bar, 0.0), (81, 86), SPB * 0.5, lambda f, d, v: C.fm_bell(f, d + 1.2, v, ratio=3.0, index=1.0), 0.22)
    m.buses["bells"] = C.delay(m.bus("bells"), SPB * 0.75, fb=0.45, mix=0.4, lp=4500)
    spec = {"pad": dict(rev="plate", wet=0.35, hp=250, width=1.3), "bass": dict(lp=200, gain=-2),
            "pulse": dict(rev="room", wet=0.2, hp=80), "bells": dict(rev="plate", wet=0.4, hp=600)}
    return render(m, spec, lufs=-18.0)


def stem_tension():
    m = Mix(LOOP)
    for bar in range(BARS):
        # low string ostinato (spiccato 8ths) on a D pedal, accents shape the bar
        for k in range(8):
            acc = 1.0 if k in (0, 3, 6) else 0.6
            nte = 38 if k != 7 else 39 if bar % 4 == 3 else 38
            m.put("ost", C.string_voice(mtof(nte), 0.12, attack=0.01, release=0.12, bright=1600, seed=bar * 8 + k, vib=0),
                  T(bar, k * 0.5) + jit(4), 0.75 * acc, p=-0.25)
            m.put("ost", C.string_voice(mtof(nte + 12), 0.1, attack=0.01, release=0.1, bright=2200, seed=999 + bar * 8 + k, vib=0),
                  T(bar, k * 0.5) + jit(4), 0.35 * acc, p=0.25)
        # ticking clock: soft 16th hats
        for k in range(16):
            m.put("tick", C.hat(0.5 if k % 4 == 0 else 0.25, decay=0.02, seed=bar * 16 + k), T(bar, k * 0.25) + jit(2), 0.35, p=0.4)
        if bar % 2 == 0:
            m.put("boom", C.taiko(0.7, seed=bar), T(bar, 0), 0.5)
    # dissonant tremolo swells (minor 2nd high strings) every 4 bars
    for bar in range(0, BARS, 4):
        for nte, p in ((69, -0.4), (70, 0.4), (62, 0.0)):
            v = C.string_voice(mtof(nte), 3.5 * 4 * SPB / 4 * 2, attack=2.5, release=1.5, bright=3000, seed=bar + nte, vib=0.006)
            tt = np.arange(len(v)) / SR
            v *= (0.55 + 0.45 * np.abs(np.sin(2 * np.pi * (BPM / 60.0 * 2) * tt)))[:, None]  # 8th-note tremolo
            m.put("trem", v, T(bar, 0), 0.32, p)
    # reverse-noise riser into every 8th bar
    for bar in range(7, BARS, 8):
        n = n_of(4 * SPB)
        nz = C.bandpass(C.white(n, bar), 800, 7000) * np.linspace(0, 1, n) ** 3
        m.put("riser", np.stack([nz, np.roll(nz, 300)], 1), T(bar, 0), 0.25)
    spec = {"ost": dict(rev="room", wet=0.25, hp=60, comp=True), "tick": dict(rev="room", wet=0.1, hp=5000, gain=-4),
            "boom": dict(rev="hall", wet=0.3, lp=900), "trem": dict(rev="hall", wet=0.4, hp=300, width=1.2),
            "riser": dict(rev="hall", wet=0.3, hp=500)}
    return render(m, spec, lufs=-19.0)


def stem_chase():
    m = Mix(LOOP)
    for bar in range(BARS):
        name, bass, tones = chord_at(bar)
        fill = bar % 4 == 3
        # drums
        for beat in (0, 1.5, 2.0) if bar % 2 else (0, 0.75, 2.0, 2.5):
            m.put("kick", C.kick(1.0, f0=150, f1=46, decay=0.28, click=0.6, seed=bar), T(bar, beat) + jit(2), 0.9)
        for beat in (1, 3):
            if fill and beat == 3:
                continue
            m.put("snare", C.snare(0.9, seed=bar * 2 + beat), T(bar, beat) + jit(3), 0.6)
        for k in range(16):
            m.put("hats", C.hat(0.7 if k % 2 == 0 else 0.4, decay=0.03 if k % 4 else 0.06, seed=bar * 16 + k),
                  T(bar, k * 0.25) + jit(2), 0.32, p=0.3)
        if fill:
            for k, (f, b) in enumerate(((180, 3.0), (150, 3.25), (120, 3.5), (95, 3.75))):
                m.put("toms", C.tom(f, 0.9, 0.3, seed=bar + k), T(bar, b), 0.7, p=-0.4 + k * 0.25)
        if bar % 2 == 0:
            m.put("taiko", C.taiko(1.0, seed=bar), T(bar, 0), 0.65)
        # driving 16th bass on the chord root with octave jumps
        for k in range(16):
            nte = bass + (12 if k % 4 == 2 else 0)
            while nte > 50:
                nte -= 12
            while nte < 33:
                nte += 12
            m.put("bass", C.bass_voice(mtof(nte), 0.14, vel=0.9 if k % 4 == 0 else 0.65, cutoff=900, drive=2.2, sub=0.5),
                  T(bar, k * 0.25) + jit(1), 0.5)
        # brass-like stabs on 1 and the "and" of 2
        for beat in (0.0, 1.5) if not fill else (0.0,):
            for j, nte in enumerate(tones[:4]):
                n = n_of(0.32)
                s = C.supersaw(mtof(nte + 12), n, voices=5, detune=0.12, seed=bar * 7 + j)
                s = C.svf_st(s, 600 + 2600 * np.exp(-np.arange(n) / SR / 0.08), res=0.15)
                s *= C.env_adsr(n, 0.008, 0.12, 0.5, 0.12)[:, None]
                m.put("stab", s, T(bar, beat) + jit(4), 0.22)
        # rising square arp, last 4 bars of every 8
        if bar % 8 >= 4:
            up = sorted([t + 24 for t in tones])
            for k in range(16):
                nte = up[k % len(up)] + (12 if k >= 8 and bar % 8 == 7 else 0)
                n = n_of(0.15)
                sq = C.osc_square(np.full(n, mtof(nte)), 0.0, 0.5) * C.exp_env(n, 0.06)
                sq = C.svf(sq, np.full(n, 2500.0), 0.2, 0)
                m.put("arp", sq, T(bar, k * 0.25), 0.08, p=0.5 if k % 2 else -0.5)
    # sidechain-style pump on bass/stabs from the kick grid
    pump = np.ones(m.total)
    for bar in range(BARS):
        for beat in range(4):
            i0 = n_of(T(bar, beat))
            n = n_of(0.22)
            if i0 + n < m.total:
                pump[i0:i0 + n] = np.minimum(pump[i0:i0 + n], 0.45 + 0.55 * np.linspace(0, 1, n) ** 0.7)
    for b in ("bass", "stab"):
        m.buses[b] *= pump[:, None]
    m.buses["arp"] = C.delay(m.bus("arp"), SPB * 0.75, fb=0.3, mix=0.3)
    spec = {"kick": dict(lp=8000, gain=0), "snare": dict(rev="room", wet=0.25, hp=120), "hats": dict(hp=6000, gain=-5),
            "toms": dict(rev="room", wet=0.3), "taiko": dict(rev="hall", wet=0.25, lp=2500, gain=-1),
            "bass": dict(lp=1800, comp=True), "stab": dict(rev="hall", wet=0.25, hp=180), "arp": dict(rev="hall", wet=0.25, hp=400)}
    return render(m, spec, lufs=-16.5)


# ----------------------------------------------------------------------------- cues
def cue_title():
    bpm = 76.0
    spb = 60.0 / bpm
    bars = 16
    m = Mix(bars * 4 * spb)
    prog = [(38, [50, 53, 57, 60, 64]), (34, [50, 53, 57, 58]), (31, [46, 50, 53, 57]), (33, [45, 49, 52, 55]),
            (38, [50, 53, 57, 60, 64]), (34, [50, 53, 57, 64]), (39, [46, 50, 55, 57]), (33, [45, 49, 52, 57])]
    for i, (bass, tones) in enumerate(prog):
        t0 = T(i * 2, 0, spb)
        dur = 8 * spb
        for j, nte in enumerate(tones):
            m.put("pad", C.pad_voice(mtof(nte), dur + 0.4, attack=1.8, release=2.5, cutoff=1100 + 120 * j, seed=500 + i * 10 + j,
                                     detune=0.12), t0, 0.15)
        m.put("bass", C.bass_voice(mtof(bass), 3.6 * spb, vel=0.8, cutoff=260, drive=1.1, sub=1.0, release=0.6), t0, 0.5)
        m.put("bass", C.bass_voice(mtof(bass), 3.4 * spb, vel=0.7, cutoff=260, drive=1.1, sub=1.0, release=0.6), t0 + 4 * spb, 0.45)
        for b2 in (0, 1):
            for beat, vel in ((0, 0.5), (1.5, 0.35), (3, 0.3)):
                for k, nte in enumerate(tones[1:]):
                    m.put("ep", C.epiano(mtof(nte + 12 if nte < 55 else nte), 1.1 * spb, vel), T(i * 2 + b2, beat, spb) + jit(8) + k * 0.01,
                          0.14, p=(k - 1.5) * 0.3)
    # trip-hop pulse from bar 4: soft kick / brushed snare / shaker
    for bar in range(4, bars):
        m.put("drums", C.kick(0.8, f0=110, f1=44, decay=0.3, click=0.2), T(bar, 0, spb), 0.55)
        m.put("drums", C.kick(0.6, f0=100, f1=44, decay=0.25, click=0.1), T(bar, 2.5, spb), 0.45)
        m.put("drums", C.snare(0.6, tone=170, decay=0.22, bright=3500, seed=bar), T(bar, 1, spb) + jit(5), 0.3)
        m.put("drums", C.snare(0.6, tone=170, decay=0.22, bright=3500, seed=bar + 50), T(bar, 3, spb) + jit(5), 0.3)
        for k in range(8):
            m.put("drums", C.shaker(0.35 if k % 2 else 0.2, seed=bar * 8 + k), T(bar, k * 0.5, spb) + jit(6), 0.3, 0.35)
    # melody (EP lead an octave up): motif + answer, twice
    mel = [  # (bar, beat, midi, beats)
        (0, 0, 69, 0.5), (0, 0.5, 69, 0.5), (0, 1, 69, 0.5), (0, 2, 74, 2.0),
        (2, 0, 72, 1.0), (2, 1, 70, 0.5), (2, 1.5, 69, 0.5), (2, 2, 65, 2.0),
        (4, 0, 67, 0.5), (4, 0.5, 67, 0.5), (4, 1, 67, 0.5), (4, 2, 70, 1.5), (4, 3.5, 69, 0.5),
        (6, 0, 69, 1.5), (6, 1.5, 64, 0.5), (6, 2, 61, 2.0),
    ]
    for rep in (8,):
        for bar, beat, nte, ln in mel:
            for offset, g in ((0, 1.0),) + (((rep, 0.9),) if True else ()):
                m.put("lead", C.epiano(mtof(nte + 12), ln * spb, 0.75), T(bar + offset, beat, spb) + jit(5), 0.24 * g)
    m.put("lead", C.fm_bell(mtof(86), 4.0, 0.5, ratio=2, index=1), T(15, 2, spb), 0.15)
    m.buses["lead"] = C.delay(m.bus("lead"), spb * 0.75, fb=0.3, mix=0.25)
    m.buses["ep"] = C.chorus(m.bus("ep"), mix=0.4)
    spec = {"pad": dict(rev="hall", wet=0.4, hp=90, width=1.2), "bass": dict(lp=380), "ep": dict(rev="hall", wet=0.25, hp=150),
            "drums": dict(rev="room", wet=0.18, lp=7000, gain=-2), "lead": dict(rev="hall", wet=0.35, hp=200)}
    return render(m, spec, lufs=-16.0)


def cue_story():
    bpm = 60.0
    spb = 1.0
    bars = 12
    m = Mix(bars * 4 * spb)
    # low drone D1/A1 strings, slowly breathing; avoids the 300 Hz-3 kHz speech band
    for bar in range(0, bars, 4):
        for nte, g in ((26, 0.5), (33, 0.4), (38, 0.25)):
            m.put("drone", C.string_voice(mtof(nte), 16.5, attack=4, release=4, bright=700, seed=bar + nte, vib=0.002), T(bar, 0, spb), g)
    # pulse: soft low heartbeat every 2 beats
    for bar in range(bars):
        for beat in (0, 2):
            m.put("pulse", C.kick(0.5, f0=70, f1=36, decay=0.3, click=0.0), T(bar, beat, spb), 0.35)
    # high glass shimmer and sparse low piano figure
    for bar in range(0, bars, 2):
        nte = [81, 84, 82, 77, 81, 76][bar // 2 % 6]
        m.put("glass", C.fm_bell(mtof(nte), 5, 0.3, ratio=2.0, index=0.6, tau=2.0), T(bar, 1, spb) + jit(20), 0.12, R.uniform(-0.6, 0.6))
    fig = [(0, 50), (1.5, 48), (3, 46), (4.5, 45)]
    for bar in (2, 6, 10):
        for b, nte in fig:
            m.put("piano", C.epiano(mtof(nte), 1.6, 0.35), T(bar, b, spb) + jit(15), 0.2)
    spec = {"drone": dict(rev="hall", wet=0.35, lp=1200), "pulse": dict(lp=200, gain=-2),
            "glass": dict(rev="hall", wet=0.5, hp=2000), "piano": dict(rev="hall", wet=0.4, lp=1500)}
    return render(m, spec, lufs=-20.0)


def cue_lakeside():
    bpm = 64.0
    spb = 60.0 / bpm
    bars = 16
    m = Mix(bars * 4 * spb)
    prog = [(41, [52, 55, 57, 60, 64]), (40, [52, 55, 60, 62]), (38, [50, 53, 57, 60, 64]), (34, [50, 53, 57, 62])]
    for i in range(8):
        bass, tones = prog[i % 4]
        t0 = T(i * 2, 0, spb)
        for j, nte in enumerate(tones):
            m.put("pad", C.pad_voice(mtof(nte), 8 * spb + 0.5, attack=2.5, release=3.0, cutoff=1500, seed=700 + i * 10 + j, detune=0.08),
                  t0, 0.12)
        m.put("bass", C.bass_voice(mtof(bass), 7.5 * spb, vel=0.6, cutoff=250, sub=1.0, release=1.0), t0, 0.4)
        for b2 in range(2):
            up = sorted([t + 12 for t in tones])
            for k in range(8):
                m.put("pluck", C.pluck(mtof(up[[0, 2, 4, 3, 1, 3, 2, 4][k] % len(up)]), 1.8, vel=R.uniform(0.45, 0.65), decay=0.998,
                                       bright=0.4, seed=i * 16 + b2 * 8 + k), T(i * 2 + b2, k * 0.5, spb) + jit(10), 0.2, 0.3 if k % 2 else -0.3)
    for bar in (4, 12):
        motif(m, "ep", T(bar, 0, spb), (72, 77), spb * 0.5, lambda f, d, v: C.epiano(f, d, v * 0.8), 0.25)
    m.buses["pluck"] = C.delay(m.bus("pluck"), spb * 0.75, fb=0.35, mix=0.3)
    spec = {"pad": dict(rev="hall", wet=0.45, hp=100, width=1.3), "bass": dict(lp=300),
            "pluck": dict(rev="hall", wet=0.35, hp=200, lp=7000), "ep": dict(rev="hall", wet=0.4, hp=200)}
    return render(m, spec, lufs=-18.0)


def cue_end_peace():
    bpm = 70.0
    spb = 60.0 / bpm
    bars = 16
    m = Mix(bars * 4 * spb)
    prog = [(38, [50, 54, 57, 61, 64]), (42, [49, 54, 57, 61]), (43, [50, 54, 59, 62]), (45, [49, 52, 57, 62])]
    for i in range(8):
        bass, tones = prog[i % 4]
        t0 = T(i * 2, 0, spb)
        for j, nte in enumerate(tones):
            m.put("strings", C.string_voice(mtof(nte), 8 * spb, attack=1.5, release=2.0, bright=2200, seed=900 + i * 10 + j), t0, 0.35,
                  (j - 2) * 0.3)
        m.put("bass", C.bass_voice(mtof(bass), 7.6 * spb, vel=0.6, cutoff=260, sub=1.0, release=1.2), t0, 0.42)
        for b2 in range(2):
            for beat, vel in ((0, 0.55), (1.5, 0.4), (2.5, 0.35)):
                for k, nte in enumerate(tones[1:]):
                    m.put("ep", C.epiano(mtof(nte + 12), 1.2 * spb, vel), T(i * 2 + b2, beat, spb) + jit(8) + k * 0.01, 0.14, (k - 1.5) * 0.3)
    # resolved leitmotif in D major: A A A ... D, then F# A D rising
    for bar in (0, 8):
        motif(m, "lead", T(bar, 0, spb), (69, 74), spb * 0.5, lambda f, d, v: C.fm_bell(f, d + 1.5, v, ratio=2.0, index=1.0), 0.3)
        for k, nte in enumerate((78, 81, 86)):
            m.put("lead", C.fm_bell(mtof(nte), 3.0, 0.5, ratio=2.0, index=0.9), T(bar + 2, k * 1.0, spb), 0.25)
    for bar in range(8, 16):
        for k in range(8):
            m.put("perc", C.shaker(0.3 if k % 2 else 0.2, seed=bar * 8 + k), T(bar, k * 0.5, spb) + jit(5), 0.25, 0.3)
        m.put("perc", C.kick(0.5, f0=95, f1=44, decay=0.3, click=0.0), T(bar, 0, spb), 0.4)
    m.buses["ep"] = C.chorus(m.bus("ep"), mix=0.35)
    spec = {"strings": dict(rev="hall", wet=0.4, hp=120, width=1.2), "bass": dict(lp=320), "ep": dict(rev="hall", wet=0.3, hp=150),
            "lead": dict(rev="hall", wet=0.45, hp=300), "perc": dict(rev="room", wet=0.15, lp=8000, gain=-3)}
    return render(m, spec, lufs=-17.0)


def cue_end_dominion():
    bpm = 70.0
    spb = 60.0 / bpm
    bars = 16
    m = Mix(bars * 4 * spb)
    prog = [(38, [50, 53, 57]), (39, [51, 55, 58]), (38, [50, 53, 57]), (36, [48, 52, 55]),
            (34, [50, 53, 58]), (39, [51, 55, 58]), (33, [49, 52, 57]), (39, [51, 55, 58])]
    for i, (bass, tones) in enumerate(prog):
        t0 = T(i * 2, 0, spb)
        for j, nte in enumerate(tones + [tones[0] + 12]):
            m.put("choir", C.choir_voice(mtof(nte), 8 * spb, vowel="a" if i % 2 == 0 else "o", attack=1.2, release=2.0, seed=1000 + i * 10 + j),
                  t0, 0.22, (j - 1.5) * 0.35)
        m.put("brass", C.pad_voice(mtof(bass + 12), 8 * spb, attack=0.6, release=1.5, cutoff=700, seed=1100 + i, detune=0.08, voices=5, res=0.25),
              t0, 0.35)
        m.put("sub", C.osc_sine(np.full(n_of(8 * spb), mtof(bass))) * C.env_adsr(n_of(8 * spb), 0.5, 0.5, 0.9, 1.0), t0, 0.35)
        top = tones[-1] + 24
        v = C.string_voice(mtof(top), 8 * spb, attack=2.0, release=2.0, bright=5000, seed=1200 + i, vib=0.005)
        tt = np.arange(len(v)) / SR
        v *= (0.6 + 0.4 * np.abs(np.sin(2 * np.pi * (bpm / 60.0 * 2) * tt)))[:, None]
        m.put("high", v, t0, 0.12, 0.2)
    for bar in range(bars):
        for beat in (0, 1.5, 2.0):
            m.put("taiko", C.taiko(1.0 if beat == 0 else 0.7, seed=bar * 3 + int(beat * 2)), T(bar, beat, spb) + jit(3), 0.55)
        if bar % 4 == 3:
            for k, b in enumerate((3.0, 3.25, 3.5, 3.75)):
                m.put("taiko", C.tom(90 - k * 8, 0.9, 0.4, seed=bar + k), T(bar, b, spb), 0.5, -0.3 + 0.2 * k)
    # the leitmotif turned into low brass: D D D ... A (an unresolved, conquering 5th)
    for bar in (0, 4, 8, 12):
        motif(m, "lowbrass", T(bar, 0, spb), (50, 45), spb * 0.5,
              lambda f, d, v: C.bass_voice(f, d, v, cutoff=900, drive=2.5, sub=0.3, release=0.3), 0.35)
    spec = {"choir": dict(rev="cave", wet=0.45, hp=150, width=1.3), "brass": dict(rev="hall", wet=0.3, lp=3500),
            "high": dict(rev="hall", wet=0.4, hp=500, width=1.3),
            "sub": dict(lp=120, gain=-2), "taiko": dict(rev="hall", wet=0.3, lp=3000), "lowbrass": dict(rev="hall", wet=0.3, lp=1600)}
    return render(m, spec, lufs=-16.0)


def cue_opening():
    """80 s score aligned with OpeningCinematic shots (0,5,12,19,26,34,43,51,59,67,75) and its tap times."""
    m = Mix(80.0, tail=6.0)
    # 0-5 memory: sub drone + faint glass
    m.put("drone", C.string_voice(mtof(26), 11, attack=3, release=3, bright=500, seed=1), 0.0, 0.5)
    m.put("glass", C.fm_bell(mtof(81), 6, 0.25, ratio=2, index=0.5, tau=2.5), 1.0, 0.15, -0.3)
    # first taps: low muted notes under the beeps
    taps = [1, 1.3, 1.6, 2.3, 6, 6.3, 6.6, 7.3]
    for k, t in enumerate(taps):
        m.put("taps", C.pluck(mtof(57 if k % 4 < 3 else 62), 1.2, 0.5, bright=0.35, seed=k), t, 0.25)
    # 5-12 Petrov: warm EP chords
    for t, tones in ((5.2, [50, 53, 57, 60, 64]), (8.6, [46, 50, 53, 57])):
        for j, nte in enumerate(tones):
            m.put("ep", C.epiano(mtof(nte + 12 if nte < 55 else nte), 3.2, 0.45), t + j * 0.02, 0.18, (j - 2) * 0.25)
            m.put("pad", C.pad_voice(mtof(nte), 3.4, attack=1.0, release=2.0, cutoff=1200, seed=20 + j), t, 0.1)
    # 12-19 learning the rhythm: plucks echo the three tap groups, rising
    groups = [(12.5, (69, 74)), (14.5, (72, 77)), (16.5, (76, 81))]
    for g, (start, (a, b)) in enumerate(groups):
        for k, dt in enumerate((0, 0.3, 0.6, 1.3)):
            m.put("taps", C.pluck(mtof(a if k < 3 else b), 2.0, 0.7, bright=0.55, seed=100 + g * 4 + k), start + dt, 0.3, -0.3 + 0.3 * g)
    m.put("pad", C.pad_voice(mtof(57), 7, attack=2, release=2, cutoff=1500, seed=30), 12.0, 0.1)
    # 19-26 Vale: cold, dissonant (Gm/Eb with a minor 2nd)
    for j, nte in enumerate((43, 46, 50, 51)):
        m.put("cold", C.string_voice(mtof(nte), 7, attack=2.5, release=2, bright=1400, seed=40 + j, vib=0.002), 19.0, 0.3, (j - 1.5) * 0.3)
    m.put("drone", C.string_voice(mtof(27), 8, attack=2, release=2, bright=400, seed=2), 19.0, 0.45)
    # 26-34 two places: split arpeggio L/R (Fmaj7#11 colour), wonder
    up = [65, 69, 71, 76, 79]
    for k in range(32):
        t = 26.0 + k * 0.25
        m.put("arp", C.pluck(mtof(up[k % 5] + (12 if k >= 16 else 0)), 1.6, 0.55, bright=0.6, seed=200 + k), t, 0.2, -0.75 if k % 2 == 0 else 0.75)
    for j, nte in enumerate((41, 53, 57, 64)):
        m.put("pad", C.pad_voice(mtof(nte), 8, attack=2, release=2.5, cutoff=1800, seed=50 + j), 26.0, 0.11)
    # 34-43 tension rising: ostinato + crescendo; stab at 37.4 "End contact."
    for k in range(int((43 - 34) / 0.357)):
        t = 34.0 + k * 0.357
        acc = 1.0 if k % 3 == 0 else 0.6
        m.put("ost", C.string_voice(mtof(38), 0.12, attack=0.01, release=0.12, bright=1500, seed=300 + k, vib=0), t, 0.6 * acc * min(1.0, 0.3 + k / 20))
    m.put("hit", C.taiko(1.0, seed=5), 37.4, 0.6)
    for j, nte in enumerate((50, 51, 57)):
        m.put("cold", C.string_voice(mtof(nte), 4.5, attack=3.5, release=1.0, bright=2500, seed=60 + j), 38.5, 0.3)
    # 43-51 tender: warm Dm9, EP, three taps at 45 left unanswered
    for j, nte in enumerate((50, 53, 57, 60, 64)):
        m.put("pad", C.pad_voice(mtof(nte), 7.5, attack=1.5, release=2.5, cutoff=1300, seed=70 + j), 43.0, 0.13)
        m.put("ep", C.epiano(mtof(nte + 12 if nte < 55 else nte), 3.0, 0.4), 43.2 + j * 0.03, 0.15)
    for k, t in enumerate((45, 45.3, 45.6)):
        m.put("taps", C.pluck(mtof(69), 2.0, 0.6, bright=0.5, seed=400 + k), t, 0.3)
    # 51-56.5 alarm: drums, dissonant brass, riser — then the wall stops (cut) at 56.5
    for k in range(int((56.4 - 51) / 0.357)):
        t = 51.0 + k * 0.357
        m.put("drums", C.kick(0.9, seed=k), t, 0.6) if k % 2 == 0 else m.put("drums", C.snare(0.7, seed=k), t, 0.35)
        m.put("drums", C.hat(0.5, seed=k), t + 0.178, 0.25, 0.3)
        if k % 4 == 0:
            m.put("drums", C.taiko(1.0, seed=k), t, 0.5)
    for j, nte in enumerate((38, 44, 50, 51)):
        v = C.pad_voice(mtof(nte), 5.5, attack=0.3, release=0.05, cutoff=1800, seed=80 + j, voices=5, res=0.2)
        m.put("brass", v, 51.0, 0.25)
    n = n_of(5.4)
    riser = C.bandpass(C.white(n, 9), 600, 8000) * np.linspace(0, 1, n) ** 2.5
    m.put("riser", np.stack([riser, np.roll(riser, 400)], 1), 51.0, 0.3)
    # 59-67 escape through the conduit: muted driving pulse building
    for k in range(int((67 - 59) / 0.178)):
        t = 59.0 + k * 0.178
        m.put("bass", C.bass_voice(mtof(38 if (k // 8) % 2 == 0 else 34), 0.12, vel=0.8, cutoff=500 + k * 12, drive=1.8), t, 0.35 * min(1, 0.4 + k / 30))
        if k % 4 == 0:
            m.put("drums", C.kick(0.7, f0=120, decay=0.25, seed=k), t, 0.5)
    # 67-75 outside: the city theme (Dm9 → Bbmaj7) with rain-soft EP and pluck
    for t, tones, bass in ((67.0, [50, 53, 57, 60, 64], 38), (71.0, [50, 53, 57, 58], 34)):
        for j, nte in enumerate(tones):
            m.put("pad", C.pad_voice(mtof(nte), 4.2, attack=1.2, release=2.5, cutoff=1300, seed=90 + j), t, 0.14)
            m.put("ep", C.epiano(mtof(nte + 12 if nte < 55 else nte), 1.6, 0.45), t + j * 0.015, 0.15)
        m.put("bass", C.bass_voice(mtof(bass), 3.8, vel=0.7, cutoff=280, sub=1.0, release=0.8), t, 0.45)
        upn = sorted([x + 12 for x in tones])
        for k in range(8):
            m.put("arp", C.pluck(mtof(upn[[0, 2, 4, 2, 1, 3, 4, 3][k] % len(upn)]), 1.2, 0.5, seed=500 + k), t + k * 0.5, 0.18, 0.3 if k % 2 else -0.3)
    # 75-80 "I could make the next beat.": the motif finally completes on D
    motif(m, "lead", 76.0, (69, 74), 0.36, lambda f, d, v: C.epiano(f, d, v), 0.32)
    for j, nte in enumerate((38, 50, 53, 57, 64)):
        m.put("pad", C.pad_voice(mtof(nte), 3.0, attack=1.0, release=3.0, cutoff=1400, seed=99 + j), 77.3, 0.13)
    # the wall stops: hard silence 56.5–59 (kill everything there except reverb tails of the cut)
    cut0, cut1 = n_of(56.5), n_of(59.0)
    for name, buf in m.buses.items():
        if name not in ("riser",):
            buf[cut0:cut1] *= np.linspace(1, 0, cut1 - cut0)[:, None] ** 8
    m.put("hit", C.taiko(1.0, seed=7), 56.5, 0.8)
    spec = {"drone": dict(rev="hall", wet=0.3, lp=900), "glass": dict(rev="hall", wet=0.5, hp=1500),
            "taps": dict(rev="hall", wet=0.4, hp=150, lp=7000), "ep": dict(rev="hall", wet=0.3, hp=150), "pad": dict(rev="hall", wet=0.4, hp=90),
            "cold": dict(rev="hall", wet=0.4, hp=150), "arp": dict(rev="hall", wet=0.35, hp=200, lp=7000), "ost": dict(rev="room", wet=0.25),
            "hit": dict(rev="hall", wet=0.35, lp=2000), "drums": dict(rev="room", wet=0.2), "brass": dict(rev="hall", wet=0.3, lp=2500),
            "riser": dict(rev="hall", wet=0.3, hp=400), "bass": dict(lp=1200), "lead": dict(rev="hall", wet=0.4, hp=150)}
    x = render(m, spec, loop=False, lufs=-17.0)
    return C.fade(x, 0.0, 3.0)


def stinger_mission():
    m = Mix(4.0, tail=3.0)
    for k, nte in enumerate((62, 66, 69, 74, 78)):
        m.put("pluck", C.pluck(mtof(nte), 2.5, 0.8, bright=0.6, seed=k), 0.0 + k * 0.09, 0.4, -0.4 + k * 0.2)
    m.put("bell", C.fm_bell(mtof(86), 3.5, 0.6, ratio=2.0, index=1.1), 0.45, 0.3)
    for j, nte in enumerate((50, 54, 57, 62, 66)):
        m.put("pad", C.pad_voice(mtof(nte), 1.6, attack=0.25, release=1.8, cutoff=2400, seed=j), 0.3, 0.14)
    m.put("hit", C.kick(0.7, f0=110, f1=45, decay=0.35, click=0.1), 0.45, 0.4)
    spec = {"pluck": dict(rev="hall", wet=0.35, lp=8000), "bell": dict(rev="hall", wet=0.45), "pad": dict(rev="hall", wet=0.4),
            "hit": dict(rev="room", wet=0.2, lp=500)}
    return render(m, spec, loop=False, lufs=-15.0, master_comp=False)


def ending_sting():
    m = Mix(6.0, tail=4.0)
    m.put("boom", C.taiko(1.0, seed=3), 0.0, 0.9)
    m.put("boom", C.kick(1.0, f0=70, f1=28, decay=1.2, click=0.0), 0.0, 0.7)
    for j, nte in enumerate((38, 39, 45, 50, 51, 56)):
        m.put("cluster", C.choir_voice(mtof(nte), 3.0, vowel="o", attack=1.6, release=2.0, seed=40 + j), 0.1, 0.25, (j - 2.5) * 0.3)
    n = n_of(2.0)
    rev = C.highpass(C.white(n, 4), 3000) * np.linspace(0, 1, n) ** 3
    rev = C.lowpass(rev, 9000)
    m.put("swell", np.stack([rev, np.roll(rev, 200)], 1), 1.0, 0.12)
    m.put("glass", C.fm_bell(mtof(93), 4, 0.4, ratio=3.5, index=1.5), 3.0, 0.2)
    spec = {"boom": dict(rev="hall", wet=0.3, lp=1500), "cluster": dict(rev="cave", wet=0.5, hp=100),
            "swell": dict(rev="hall", wet=0.4), "glass": dict(rev="hall", wet=0.5, hp=1500)}
    return render(m, spec, loop=False, lufs=-16.0, master_comp=False)


JOBS = {
    "mus_city_calm": stem_city, "mus_lab_calm": stem_lab, "mus_tension": stem_tension, "mus_chase": stem_chase,
    "mus_title": cue_title, "mus_story": cue_story, "mus_opening": cue_opening, "mus_lakeside": cue_lakeside,
    "mus_end_peace": cue_end_peace, "mus_end_dominion": cue_end_dominion, "stinger_mission": stinger_mission,
    "ending_sting": ending_sting,
}


def main():
    names = sys.argv[1:] or list(JOBS)
    for name in names:
        print(f"rendering {name} …", flush=True)
        x = JOBS[name]()
        if name.startswith("mus_") and name not in ("mus_opening",):
            pass
        save(name, x, sub="Sfx" if name == "ending_sting" else "Music")


if __name__ == "__main__":
    main()
