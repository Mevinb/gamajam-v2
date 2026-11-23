"""Shared offline DSP toolkit for PARASITE's generated audio (music, ambience, SFX, voice post-processing).

Everything is deterministic (seeded) so re-running a generator reproduces the same files.
Buffers are float32/float64 numpy arrays: mono shape (n,), stereo shape (n, 2). Sample rate SR = 44100.
"""
from __future__ import annotations

import os
import subprocess
import tempfile

import numpy as np
import soundfile as sf
from numba import njit
from scipy import signal

SR = 44100
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
AUDIO_RES = os.path.join(ROOT, "Assets", "Parasite", "Resources", "Audio")
WORK = os.environ.get("PARASITE_AUDIO_WORK", os.path.expanduser("~/Data/parasite-work/audio"))


# ----------------------------------------------------------------------------- basics
def mtof(m):
    return 440.0 * 2.0 ** ((np.asarray(m, dtype=np.float64) - 69.0) / 12.0)


def db(x):
    return 20.0 * np.log10(np.maximum(1e-12, x))


def undb(d):
    return 10.0 ** (np.asarray(d) / 20.0)


def n_of(sec, sr=SR):
    return int(round(sec * sr))


def rng(seed):
    return np.random.default_rng(seed)


def to_stereo(x):
    return x if x.ndim == 2 else np.stack([x, x], axis=1)


def to_mono(x):
    return x if x.ndim == 1 else x.mean(axis=1)


def pan(mono, p=0.0):
    """Constant-power pan, p in [-1, 1]."""
    a = (p + 1.0) * np.pi / 4.0
    return np.stack([mono * np.cos(a), mono * np.sin(a)], axis=1)


def mix_into(dst, src, start=0, gain=1.0):
    """Add src into dst at sample offset start (clipped to dst length). Handles mono->stereo."""
    if start >= len(dst) or len(src) == 0:
        return dst
    if start < 0:
        src = src[-start:]
        start = 0
    n = min(len(src), len(dst) - start)
    if dst.ndim == 2 and src.ndim == 1:
        dst[start:start + n] += src[:n, None] * gain
    else:
        dst[start:start + n] += src[:n] * gain
    return dst


def fade(x, fin=0.0, fout=0.0, sr=SR, curve=2.0):
    x = x.copy()
    a, b = n_of(fin, sr), n_of(fout, sr)
    if a > 0:
        e = np.linspace(0, 1, a) ** curve
        x[:a] *= e if x.ndim == 1 else e[:, None]
    if b > 0:
        e = np.linspace(1, 0, b) ** curve
        x[-b:] *= e if x.ndim == 1 else e[:, None]
    return x


def env_adsr(n, a, d, s, r, sr=SR, hold=None):
    """ADSR envelope of n samples; note-off at hold seconds (default n - r)."""
    t = np.arange(n) / sr
    if hold is None:
        hold = max(0.0, n / sr - r)
    e = np.where(t < a, t / max(a, 1e-6), 1.0)
    dd = np.clip((t - a) / max(d, 1e-6), 0, 1)
    e = np.where(t >= a, 1.0 - (1.0 - s) * dd, e)
    rel = np.clip((t - hold) / max(r, 1e-6), 0, 1)
    level_at_hold = np.interp(hold, t, e) if n > 0 else 0
    e = np.where(t >= hold, level_at_hold * (1.0 - rel) ** 2, e)
    return e


def exp_env(n, tau, sr=SR, attack=0.002):
    t = np.arange(n) / sr
    return np.minimum(1.0, t / max(attack, 1e-6)) * np.exp(-t / tau)


# ----------------------------------------------------------------------------- noise
def white(n, seed=0):
    return rng(seed).uniform(-1, 1, n)


def pink(n, seed=0):
    w = rng(seed).standard_normal(n)
    b = [0.049922035, -0.095993537, 0.050612699, -0.004408786]
    a = [1, -2.494956002, 2.017265875, -0.522189400]
    p = signal.lfilter(b, a, w)
    return p / (np.max(np.abs(p)) + 1e-9)


def brown(n, seed=0):
    w = rng(seed).standard_normal(n)
    b = np.cumsum(w)
    b = signal.sosfilt(signal.butter(1, 20, "hp", fs=SR, output="sos"), b)
    return b / (np.max(np.abs(b)) + 1e-9)


# ----------------------------------------------------------------------------- filters
def _sos(kind, fc, order, sr):
    nyq = sr * 0.5
    if isinstance(fc, (tuple, list)):
        fc = [min(max(f, 5.0), nyq * 0.98) for f in fc]
    else:
        fc = min(max(fc, 5.0), nyq * 0.98)
    return signal.butter(order, fc, kind, fs=sr, output="sos")


def lowpass(x, fc, order=2, sr=SR):
    return signal.sosfilt(_sos("lowpass", fc, order, sr), x, axis=0)


def highpass(x, fc, order=2, sr=SR):
    return signal.sosfilt(_sos("highpass", fc, order, sr), x, axis=0)


def bandpass(x, lo, hi, order=2, sr=SR):
    return signal.sosfilt(_sos("bandpass", (lo, hi), order, sr), x, axis=0)


def peaking(x, fc, gain_db, q=1.0, sr=SR):
    A = 10 ** (gain_db / 40)
    w0 = 2 * np.pi * fc / sr
    alpha = np.sin(w0) / (2 * q)
    b = [1 + alpha * A, -2 * np.cos(w0), 1 - alpha * A]
    a = [1 + alpha / A, -2 * np.cos(w0), 1 - alpha / A]
    return signal.lfilter(np.array(b) / a[0], np.array(a) / a[0], x, axis=0)


def shelf(x, fc, gain_db, high=True, sr=SR):
    A = 10 ** (gain_db / 40)
    w0 = 2 * np.pi * fc / sr
    cw, sw = np.cos(w0), np.sin(w0)
    alpha = sw / 2 * np.sqrt(2)
    s = 1 if high else -1
    b0 = A * ((A + 1) + s * (A - 1) * cw + 2 * np.sqrt(A) * alpha)
    b1 = -2 * s * A * ((A - 1) + s * (A + 1) * cw)
    b2 = A * ((A + 1) + s * (A - 1) * cw - 2 * np.sqrt(A) * alpha)
    a0 = (A + 1) - s * (A - 1) * cw + 2 * np.sqrt(A) * alpha
    a1 = 2 * s * ((A - 1) - s * (A + 1) * cw)
    a2 = (A + 1) - s * (A - 1) * cw - 2 * np.sqrt(A) * alpha
    return signal.lfilter(np.array([b0, b1, b2]) / a0, np.array([a0, a1, a2]) / a0, x, axis=0)


@njit(cache=True)
def svf(x, cutoff, res, mode):
    """TPT state-variable filter with per-sample cutoff (Hz). mode 0 LP, 1 BP, 2 HP. res 0..0.97."""
    n = x.shape[0]
    y = np.zeros(n)
    ic1 = 0.0
    ic2 = 0.0
    k = 2.0 - 2.0 * res
    for i in range(n):
        fc = min(max(cutoff[i], 10.0), SR * 0.45)
        g = np.tan(np.pi * fc / SR)
        a1 = 1.0 / (1.0 + g * (g + k))
        a2 = g * a1
        a3 = g * a2
        v3 = x[i] - ic2
        v1 = a1 * ic1 + a2 * v3
        v2 = ic2 + a2 * ic1 + a3 * v3
        ic1 = 2.0 * v1 - ic1
        ic2 = 2.0 * v2 - ic2
        if mode == 0:
            y[i] = v2
        elif mode == 1:
            y[i] = v1
        else:
            y[i] = x[i] - k * v1 - v2
    return y


def svf_st(x, cutoff, res=0.2, mode=0):
    cutoff = np.broadcast_to(np.asarray(cutoff, dtype=np.float64), (len(x),)).copy()
    if x.ndim == 1:
        return svf(x.astype(np.float64), cutoff, res, mode)
    return np.stack([svf(x[:, c].astype(np.float64), cutoff, res, mode) for c in range(x.shape[1])], axis=1)


# ----------------------------------------------------------------------------- oscillators
@njit(cache=True)
def _blep(t, dt):
    if t < dt:
        t /= dt
        return t + t - t * t - 1.0
    if t > 1.0 - dt:
        t = (t - 1.0) / dt
        return t * t + t + t + 1.0
    return 0.0


@njit(cache=True)
def osc_saw(freq, phase0):
    n = freq.shape[0]
    y = np.zeros(n)
    ph = phase0
    for i in range(n):
        dt = freq[i] / SR
        y[i] = 2.0 * ph - 1.0 - _blep(ph, dt)
        ph += dt
        if ph >= 1.0:
            ph -= 1.0
    return y


@njit(cache=True)
def osc_square(freq, phase0, pw):
    n = freq.shape[0]
    y = np.zeros(n)
    ph = phase0
    for i in range(n):
        dt = freq[i] / SR
        v = 1.0 if ph < pw else -1.0
        v += _blep(ph, dt)
        t2 = ph - pw
        if t2 < 0.0:
            t2 += 1.0
        v -= _blep(t2, dt)
        y[i] = v
        ph += dt
        if ph >= 1.0:
            ph -= 1.0
    return y


def osc_sine(freq, phase0=0.0):
    freq = np.asarray(freq, dtype=np.float64)
    return np.sin(2 * np.pi * (np.cumsum(freq) / SR) + phase0)


def supersaw(f0, n, voices=7, detune=0.18, seed=0, drift=0.002):
    """Detuned unison saw stack, stereo spread. detune in semitones (outer voices)."""
    r = rng(seed)
    out = np.zeros((n, 2))
    t = np.arange(n) / SR
    for v in range(voices):
        d = (v / (voices - 1) - 0.5) * 2 * detune if voices > 1 else 0
        lfo = 1 + drift * np.sin(2 * np.pi * r.uniform(0.05, 0.25) * t + r.uniform(0, 6.28))
        f = f0 * 2 ** (d / 12) * lfo
        s = osc_saw(np.full(n, 1.0) * f, r.uniform(0, 1))
        p = (v / (voices - 1) - 0.5) * 1.6 if voices > 1 else 0
        out += pan(s, p) / voices ** 0.7
    return out


# ----------------------------------------------------------------------------- instruments
@njit(cache=True)
def _karplus(n, period, decay, bright, seed, pluck_pos):
    np.random.seed(seed)
    N = max(2, int(period))
    frac = period - int(period)
    buf = np.random.uniform(-1.0, 1.0, N)
    # pluck-position comb (removes some harmonics, more natural)
    pp = max(1, int(N * pluck_pos))
    tmp = buf.copy()
    for i in range(N):
        buf[i] = tmp[i] - tmp[(i + pp) % N] * 0.5
    # soften initial excitation by brightness
    lp = 0.0
    for i in range(N):
        lp = lp + bright * (buf[i] - lp)
        buf[i] = lp
    y = np.zeros(n)
    idx = 0
    last = 0.0
    ap_state = 0.0
    c = (1.0 - frac) / (1.0 + frac)
    for i in range(n):
        cur = buf[idx]
        nxt = buf[(idx + 1) % N]
        avg = decay * (0.5 * (cur + nxt))
        # allpass for fractional tuning
        out = c * avg + ap_state
        ap_state = avg - c * out
        buf[idx] = out
        y[i] = cur
        idx = (idx + 1) % N
        last = out
    return y


def pluck(freq, dur, vel=1.0, decay=0.996, bright=0.6, seed=0, pos=0.18):
    n = n_of(dur)
    y = _karplus(n, SR / freq, decay, bright, seed, pos)
    y = y * exp_env(n, dur * 0.6, attack=0.001) * vel
    return highpass(y, 40)


def epiano(freq, dur, vel=0.8, seed=0, release=0.35):
    """FM electric piano (Rhodes-like): 1:1 FM with decaying index + bell tine + tremolo."""
    n = n_of(dur + release)
    t = np.arange(n) / SR
    hold = dur
    amp = np.where(t < hold, np.exp(-t / (1.4 + 200 / freq)), np.exp(-hold / (1.4 + 200 / freq)) * np.exp(-(t - hold) / 0.12))
    amp *= np.minimum(1, t / 0.003)
    idx = (1.6 * vel + 0.4) * np.exp(-t / 0.5) + 0.25
    mod = np.sin(2 * np.pi * freq * t) * idx
    body = np.sin(2 * np.pi * freq * t + mod)
    tine = np.sin(2 * np.pi * min(freq * 7.0, 4200.0) * t) * np.exp(-t / 0.025) * 0.1 * vel
    bark = np.sin(2 * np.pi * freq * 2 * t + 0.6 * np.sin(2 * np.pi * freq * t)) * np.exp(-t / 0.25) * 0.15 * vel
    y = (body * 0.85 + tine + bark) * amp * vel
    return y


def fm_bell(freq, dur, vel=0.7, ratio=3.5, index=2.5, tau=None):
    n = n_of(dur)
    t = np.arange(n) / SR
    tau = tau or dur * 0.4
    env = np.minimum(1, t / 0.002) * np.exp(-t / tau)
    ienv = index * np.exp(-t / (tau * 0.5))
    y = np.sin(2 * np.pi * freq * t + ienv * np.sin(2 * np.pi * freq * ratio * t))
    y += 0.3 * np.sin(2 * np.pi * freq * 2.01 * t) * np.exp(-t / (tau * 0.3))
    return y * env * vel


def pad_voice(freq, dur, attack=1.2, release=1.8, cutoff=1800, seed=0, detune=0.12, voices=5, res=0.15, lfo=0.15):
    n = n_of(dur + release)
    s = supersaw(freq, n, voices=voices, detune=detune, seed=seed)
    t = np.arange(n) / SR
    cut = cutoff * (1 + 0.25 * np.sin(2 * np.pi * lfo * t + seed))
    s = svf_st(s, cut, res=res)
    e = env_adsr(n, attack, 0.5, 0.85, release, hold=dur)
    return s * e[:, None]


def string_voice(freq, dur, attack=0.35, release=0.6, bright=2600, seed=0, vib=0.004):
    """Bowed-string-like: supersaw with vibrato, formant-ish band emphasis."""
    n = n_of(dur + release)
    r = rng(seed)
    t = np.arange(n) / SR
    out = np.zeros((n, 2))
    for v in range(4):
        f = freq * 2 ** ((v - 1.5) * 0.06 / 12) * (1 + vib * np.sin(2 * np.pi * (5.2 + 0.3 * v) * t + r.uniform(0, 6)) * np.minimum(1, t / 0.6))
        out += pan(osc_saw(f, r.uniform(0, 1)), (v - 1.5) * 0.4)
    out = svf_st(out, bright, res=0.1)
    out = peaking(out, 1200, 3, 0.8)
    e = env_adsr(n, attack, 0.3, 0.9, release, hold=dur)
    return out * e[:, None] * 0.35


def choir_voice(freq, dur, vowel="a", attack=0.8, release=1.5, seed=0):
    formants = {"a": [(800, 1.0), (1150, 0.5), (2900, 0.25)], "o": [(450, 1.0), (800, 0.45), (2830, 0.15)],
                "u": [(325, 1.0), (700, 0.3), (2530, 0.1)], "e": [(400, 1.0), (1600, 0.4), (2700, 0.25)]}[vowel]
    n = n_of(dur + release)
    src = supersaw(freq, n, voices=6, detune=0.1, seed=seed)
    t = np.arange(n) / SR
    src *= (1 + 0.005 * np.sin(2 * np.pi * 5 * t))[:, None]
    y = np.zeros_like(src)
    for f, g in formants:
        y += bandpass(src, f * 0.88, f * 1.12, order=2) * g
    e = env_adsr(n, attack, 0.4, 0.9, release, hold=dur)
    return y * e[:, None] * 1.6


def bass_voice(freq, dur, vel=0.8, cutoff=700, drive=1.5, seed=0, release=0.08, sub=0.6):
    n = n_of(dur + release)
    t = np.arange(n) / SR
    s = osc_saw(np.full(n, freq), 0.0) * 0.6 + osc_sine(np.full(n, freq)) * sub
    cut = cutoff * (0.35 + 0.65 * np.exp(-t / 0.12)) + 60
    s = svf_st(s, cut, res=0.25)
    s = np.tanh(s * drive) / np.tanh(drive)
    e = env_adsr(n, 0.004, 0.15, 0.75, release, hold=dur)
    return s * e * vel


# ----------------------------------------------------------------------------- drums
def kick(vel=1.0, f0=140, f1=44, decay=0.32, click=0.5, seed=0):
    n = n_of(decay * 3)
    t = np.arange(n) / SR
    f = f1 + (f0 - f1) * np.exp(-t / 0.035)
    y = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / decay)
    cl = highpass(white(n, seed), 1500) * np.exp(-t / 0.004) * click
    y = np.tanh((y + cl) * 1.4)
    return y * vel


def snare(vel=1.0, tone=185, decay=0.18, seed=0, bright=5000):
    n = n_of(decay * 4)
    t = np.arange(n) / SR
    body = np.sin(2 * np.pi * tone * t) * np.exp(-t / 0.05) * 0.7
    body += np.sin(2 * np.pi * tone * 1.6 * t) * np.exp(-t / 0.03) * 0.3
    nz = bandpass(white(n, seed), 1200, bright) * np.exp(-t / decay) * 1.3
    return np.tanh((body + nz) * 1.2) * vel


def hat(vel=0.6, decay=0.045, seed=0, open_=False):
    d = 0.25 if open_ else decay
    n = n_of(d * 4)
    t = np.arange(n) / SR
    # metallic: sum of square-ish partials + noise
    y = np.zeros(n)
    for f in (205.3, 304.4, 369.6, 522.7, 540.0, 800.0):
        y += np.sign(np.sin(2 * np.pi * f * 7.8 * t))
    y = y * 0.15 + white(n, seed) * 0.8
    y = highpass(y, 7000, order=4) * np.exp(-t / d)
    return y * vel


def tom(freq=110, vel=0.8, decay=0.35, seed=0):
    n = n_of(decay * 4)
    t = np.arange(n) / SR
    f = freq * (1 + 0.6 * np.exp(-t / 0.04))
    y = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / decay)
    y += lowpass(white(n, seed), 1500) * np.exp(-t / 0.02) * 0.3
    return np.tanh(y * 1.3) * vel


def taiko(vel=1.0, seed=0):
    n = n_of(2.0)
    t = np.arange(n) / SR
    f = 62 + 40 * np.exp(-t / 0.05)
    y = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.55)
    y += lowpass(white(n, seed), 600) * np.exp(-t / 0.06) * 0.6
    y += bandpass(white(n, seed + 1), 900, 3000) * np.exp(-t / 0.012) * 0.3
    return np.tanh(y * 1.5) * vel


def shaker(vel=0.4, seed=0):
    n = n_of(0.12)
    t = np.arange(n) / SR
    e = np.minimum(1, t / 0.02) * np.exp(-t / 0.03)
    return bandpass(white(n, seed), 4000, 12000) * e * vel


# ----------------------------------------------------------------------------- space / dynamics
def make_ir(sec=2.5, decay=1.8, predelay=0.02, damp=6000, early=8, width=1.0, seed=0, sr=SR):
    """Synthetic stereo reverb impulse response: early reflections + decorrelated exponential noise tail
    whose high frequencies decay faster (damping)."""
    n = n_of(sec, sr)
    r = rng(seed)
    t = np.arange(n) / sr
    ir = np.zeros((n, 2))
    for c in range(2):
        nz = r.standard_normal(n)
        env = np.exp(-6.9 * t / decay)  # -60 dB at `decay`
        tail = nz * env
        # frequency-dependent decay: blend a lowpassed copy as time goes on
        lp = lowpass(tail, damp, order=1, sr=sr)
        lp2 = lowpass(tail, damp * 0.25, order=1, sr=sr)
        k = np.clip(t / max(decay, 0.1), 0, 1)
        tail = lp * (1 - k) + lp2 * k
        fade_in = np.clip((t - predelay) / 0.015, 0, 1)
        ir[:, c] = tail * fade_in
        for e in range(early):
            d = predelay + r.uniform(0.003, 0.06)
            i = n_of(d, sr)
            if i < n:
                ir[i, c] += r.uniform(0.2, 0.6) * (1 if r.random() > 0.5 else -1)
    mid = ir.mean(axis=1, keepdims=True)
    ir = mid + (ir - mid) * width
    ir /= np.sqrt(np.sum(ir ** 2) / 2) + 1e-9
    return ir


def reverb(x, ir, wet=0.3, dry=1.0):
    x2 = to_stereo(x)
    out = np.zeros((len(x2) + len(ir) - 1, 2))
    for c in range(2):
        out[:, c] = signal.oaconvolve(x2[:, c], ir[:, c])
    res = out * wet
    res[:len(x2)] += x2 * dry
    return res


@njit(cache=True)
def _chorus(x, sr, depth, base, rate, phase):
    n = x.shape[0]
    y = np.zeros(n)
    maxd = int((base + depth) * sr) + 4
    buf = np.zeros(maxd)
    w = 0
    for i in range(n):
        buf[w] = x[i]
        d = (base + depth * 0.5 * (1 + np.sin(2 * np.pi * rate * i / sr + phase))) * sr
        rp = w - d
        while rp < 0:
            rp += maxd
        i0 = int(rp)
        fr = rp - i0
        y[i] = buf[i0 % maxd] * (1 - fr) + buf[(i0 + 1) % maxd] * fr
        w = (w + 1) % maxd
    return y


def chorus(x, depth=0.004, base=0.012, rate=0.35, mix=0.5):
    x2 = to_stereo(x)
    l = _chorus(x2[:, 0].astype(np.float64), SR, depth, base, rate, 0.0)
    r = _chorus(x2[:, 1].astype(np.float64), SR, depth, base, rate * 1.13, 1.7)
    return x2 * (1 - mix) + np.stack([l, r], axis=1) * mix


def delay(x, time, fb=0.35, mix=0.3, lp=3500, pingpong=True):
    x2 = to_stereo(x)
    d = n_of(time)
    out = x2.copy()
    tap = x2.copy()
    g = 1.0
    for k in range(1, 8):
        g *= fb
        if g < 0.02:
            break
        tap = lowpass(tap, lp, order=1)
        sh = np.zeros_like(x2)
        if d * k < len(x2):
            sh[d * k:] = tap[:len(x2) - d * k]
        if pingpong and k % 2 == 1:
            sh = sh[:, ::-1]
        out += sh * g * mix
    return out


@njit(cache=True)
def _compress(x, sr, thresh, ratio, att, rel, knee):
    n = x.shape[0]
    y = np.zeros_like(x)
    env = 0.0
    a = np.exp(-1.0 / (att * sr))
    r = np.exp(-1.0 / (rel * sr))
    for i in range(n):
        lvl = 0.0
        for c in range(x.shape[1]):
            v = abs(x[i, c])
            if v > lvl:
                lvl = v
        if lvl > env:
            env = a * env + (1 - a) * lvl
        else:
            env = r * env + (1 - r) * lvl
        ldb = 20.0 * np.log10(max(env, 1e-9))
        over = ldb - thresh
        if over <= -knee / 2:
            gr = 0.0
        elif over >= knee / 2:
            gr = over * (1 - 1 / ratio)
        else:
            gr = (1 - 1 / ratio) * (over + knee / 2) ** 2 / (2 * knee)
        g = 10.0 ** (-gr / 20.0)
        for c in range(x.shape[1]):
            y[i, c] = x[i, c] * g
    return y


def compress(x, thresh=-18, ratio=3, attack=0.01, release=0.15, knee=6, makeup=0.0):
    x2 = to_stereo(x).astype(np.float64)
    y = _compress(x2, SR, thresh, ratio, attack, release, knee) * undb(makeup)
    return y if x.ndim == 2 else y[:, 0]


@njit(cache=True)
def _limit(x, sr, ceiling, look, rel):
    n = x.shape[0]
    la = int(look * sr)
    peak = np.zeros(n)
    for i in range(n):
        m = 0.0
        for c in range(x.shape[1]):
            v = abs(x[i, c])
            if v > m:
                m = v
        peak[i] = m
    # required gain per sample, then look-ahead min + smoothing
    req = np.ones(n)
    for i in range(n):
        if peak[i] > ceiling:
            req[i] = ceiling / peak[i]
    g = np.ones(n)
    cur = 1.0
    r = np.exp(-1.0 / (rel * sr))
    # backward pass: spread minima over look-ahead window
    mn = np.ones(n)
    for i in range(n - 1, -1, -1):
        v = req[i]
        if i + 1 < n:
            nxt = mn[i + 1] + (1.0 - mn[i + 1]) / max(la, 1)
            if nxt < v:
                v = nxt
        mn[i] = v
    for i in range(n):
        t = mn[i]
        if t < cur:
            cur = t
        else:
            cur = r * cur + (1 - r) * t
        g[i] = cur
    y = np.zeros_like(x)
    for i in range(n):
        for c in range(x.shape[1]):
            y[i, c] = x[i, c] * g[i]
    return y


def limit(x, ceiling_db=-1.0, look=0.004, release=0.08):
    x2 = to_stereo(x).astype(np.float64)
    y = _limit(x2, SR, float(undb(ceiling_db)), look, release)
    y = np.clip(y, -undb(ceiling_db), undb(ceiling_db))
    return y if x.ndim == 2 else y[:, 0]


def loudness(x, sr=SR):
    import pyloudnorm as pyln
    meter = pyln.Meter(sr)
    x2 = to_stereo(x) if x.ndim == 1 else x
    # pyloudnorm needs >= 0.4 s
    if len(x2) < int(0.45 * sr):
        x2 = np.concatenate([x2, np.zeros((int(0.45 * sr) - len(x2) + 1, x2.shape[1]))])
    if x.ndim == 1:
        return meter.integrated_loudness(x2[:, 0])
    return meter.integrated_loudness(x2)


def normalize_lufs(x, target, ceiling_db=-1.0, sr=SR):
    l = loudness(x, sr)
    if not np.isfinite(l):
        return x
    y = x * undb(target - l)
    if np.max(np.abs(y)) > undb(ceiling_db):
        y = limit(y, ceiling_db)
    return y


def normalize_peak(x, peak_db=-1.0):
    m = np.max(np.abs(x)) + 1e-12
    return x * (undb(peak_db) / m)


def fold_loop(x, loop_len):
    """Make a seamless loop: fold everything after loop_len (reverb/release tails) back onto the start."""
    y = x[:loop_len].copy()
    tail = x[loop_len:]
    while len(tail) > 0:
        k = min(len(tail), loop_len)
        y[:k] += tail[:k]
        tail = tail[k:]
    return y


def crossfade_loop(x, loop_len, fade_sec=1.0):
    """Seamless loop for non-metric material: equal-power crossfade of the overflow into the head."""
    f = n_of(fade_sec)
    assert len(x) >= loop_len + f
    y = x[:loop_len].copy()
    k = np.linspace(0, 1, f)
    a, b = np.sin(k * np.pi / 2), np.cos(k * np.pi / 2)
    if y.ndim == 2:
        a, b = a[:, None], b[:, None]
    y[:f] = x[:f] * a + x[loop_len:loop_len + f] * b
    return y


# ----------------------------------------------------------------------------- IO
def write_ogg(path, x, q=5, sr=SR):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    x = np.asarray(x, dtype=np.float32)
    peak = float(np.max(np.abs(x))) if len(x) else 0.0
    if peak > 0.999:
        x = x * (0.999 / peak)
    with tempfile.NamedTemporaryFile(suffix=".wav", dir=WORK, delete=False) as tmp:
        wav = tmp.name
    try:
        sf.write(wav, x, sr, subtype="PCM_24")
        subprocess.run(["oggenc", "-Q", "-q", str(q), "-o", path, wav], check=True)
    finally:
        os.remove(wav)
    return path


def read(path, sr=SR, mono=False):
    x, r = sf.read(path, always_2d=False)
    if x.ndim == 2 and mono:
        x = x.mean(axis=1)
    if r != sr:
        g = np.gcd(int(r), int(sr))
        x = signal.resample_poly(x, sr // g, r // g, axis=0)
    return x.astype(np.float64)


def ffmpeg_read(path, sr=SR, mono=True, start=None, dur=None):
    """Decode any format via ffmpeg (used for .ogg/.mp3 sources)."""
    cmd = ["ffmpeg", "-v", "error"]
    if start is not None:
        cmd += ["-ss", str(start)]
    if dur is not None:
        cmd += ["-t", str(dur)]
    cmd += ["-i", path, "-f", "f32le", "-ac", "1" if mono else "2", "-ar", str(sr), "-"]
    raw = subprocess.run(cmd, check=True, capture_output=True).stdout
    x = np.frombuffer(raw, dtype=np.float32).astype(np.float64)
    return x if mono else x.reshape(-1, 2)
