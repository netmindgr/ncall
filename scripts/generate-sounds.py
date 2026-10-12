"""Generate the nCall sound set (GPL-3.0-or-later, netmind 2026).

All sounds are synthesised here, so they carry no third-party rights.
Designed for offices: calm timbres, mid register, soft attacks, short motifs
with pauses, and the same perceived loudness for every ringtone.

Output: 32 kHz, 16-bit, mono PCM WAV in source/res/sounds.

    python scripts/generate-sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 32000
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'source', 'res', 'sounds')
TWO_PI = 2 * math.pi
random.seed(2026)  # reproducible output


# ---------------------------------------------------------------- building blocks

def silence(seconds):
    return [0.0] * int(RATE * seconds)


def mix_at(base, sound, start, gain=1.0):
    i0 = int(RATE * start)
    need = i0 + len(sound)
    if len(base) < need:
        base.extend([0.0] * (need - len(base)))
    for i, v in enumerate(sound):
        base[i0 + i] += v * gain
    return base


def pad_to(samples, seconds):
    n = int(RATE * seconds)
    return samples + [0.0] * max(0, n - len(samples))


def lowpass(samples, cutoff):
    """One-pole low-pass: takes the edge off for open-plan offices."""
    a = math.exp(-TWO_PI * cutoff / RATE)
    out, y = [], 0.0
    for x in samples:
        y = (1 - a) * x + a * y
        out.append(y)
    return out


def reverb(samples, wet=0.16, tail=1.2):
    """Small-room Schroeder reverb (4 combs + 2 all-passes)."""
    dry = samples + [0.0] * int(RATE * tail)
    acc = [0.0] * len(dry)
    for ms, g in ((29.7, 0.77), (37.1, 0.75), (41.1, 0.73), (43.7, 0.71)):
        d = int(RATE * ms / 1000)
        buf = [0.0] * d
        idx = 0
        for i, x in enumerate(dry):
            y = buf[idx]
            buf[idx] = x + y * g
            idx = (idx + 1) % d
            acc[i] += y
    for ms, g in ((5.0, 0.7), (1.7, 0.7)):
        d = int(RATE * ms / 1000)
        buf = [0.0] * d
        idx = 0
        out = []
        for x in acc:
            b = buf[idx]
            y = -g * x + b
            buf[idx] = x + g * y
            idx = (idx + 1) % d
            out.append(y)
        acc = out
    return [d + wet * 0.25 * w for d, w in zip(dry, acc)]


def attack(t, seconds):
    return min(1.0, t / seconds) if seconds > 0 else 1.0


# ---------------------------------------------------------------- instruments

def modal(freq, seconds, partials, strike=0.002, noise=0.0, noise_tone=3000):
    """Struck instrument from (ratio, amplitude, decay per second) partials."""
    n = int(RATE * seconds)
    out = []
    phases = [random.random() * 0.2 for _ in partials]
    for i in range(n):
        t = i / RATE
        v = 0.0
        for (ratio, amp, decay), ph in zip(partials, phases):
            f = freq * ratio
            if f < RATE / 2.2:
                v += amp * math.exp(-decay * t) * math.sin(TWO_PI * f * t + ph)
        out.append(v * attack(t, strike))
    if noise:  # mallet / pluck contact noise
        click = lowpass([random.uniform(-1, 1) * math.exp(-i / (RATE * 0.004)) for i in range(int(RATE * 0.02))], noise_tone)
        for i, c in enumerate(click):
            out[i] += c * noise
    return out


def marimba(freq, seconds=1.0):
    # Rosewood bar: overtones tuned near 1 : 4 : 10, upper ones die quickly.
    return modal(freq, seconds, [(1.0, 1.0, 3.2), (3.93, 0.22, 11.0), (9.2, 0.05, 28.0)], strike=0.0015, noise=0.18, noise_tone=2500)


def kalimba(freq, seconds=1.4):
    # Thumb-piano tine: soft, slightly inharmonic, warm.
    return modal(freq, seconds, [(1.0, 1.0, 2.4), (2.0, 0.08, 6.0), (5.95, 0.10, 14.0)], strike=0.003, noise=0.08, noise_tone=1800)


def bell(freq, seconds=1.8):
    # Small soft bell: gentle, mostly harmonic partials.
    return modal(freq, seconds, [(1.0, 1.0, 2.0), (2.0, 0.30, 3.2), (3.01, 0.12, 5.0), (4.2, 0.05, 8.0)], strike=0.004)


def epiano(freq, seconds=2.2):
    """Electric-piano tone (two-operator FM), mellow and round."""
    n = int(RATE * seconds)
    out = []
    for i in range(n):
        t = i / RATE
        index = 0.25 + 1.6 * math.exp(-t * 6.0)          # brightness fades quickly
        mod = math.sin(TWO_PI * freq * t)
        tine = 0.12 * math.exp(-t * 18) * math.sin(TWO_PI * freq * 7.0 * t)
        v = math.sin(TWO_PI * freq * t + index * mod) + tine
        out.append(v * math.exp(-t * 1.4) * attack(t, 0.004))
    return out


def soft_tone(freqs, seconds, rise=0.03, fall=0.06):
    """Steady tone with smooth edges (call-progress and office ring)."""
    n = int(RATE * seconds)
    out = []
    for i in range(n):
        t = i / RATE
        env = min(1.0, t / rise, (seconds - t) / fall)
        out.append(sum(math.sin(TWO_PI * f * t) for f in freqs) / len(freqs) * max(env, 0.0))
    return out


# ---------------------------------------------------------------- output

def write(name, samples, loudness=0.12, peak=0.55):
    """Normalise to a common loudness (RMS over the audible part), cap the peak."""
    loud = [v for v in samples if abs(v) > 1e-4] or [0.0]
    rms = math.sqrt(sum(v * v for v in loud) / len(loud)) or 1.0
    gain = loudness / rms
    top = max(abs(v) for v in samples) * gain
    if top > peak:
        gain *= peak / top
    path = os.path.join(ROOT, name)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b''.join(struct.pack('<h', int(max(-1.0, min(1.0, v * gain)) * 32767)) for v in samples))
    print('%-26s %4.1f s' % (name, len(samples) / RATE))


# Equal-tempered note frequencies (A4 = 440 Hz)
def note(name):
    names = {'C': -9, 'C#': -8, 'D': -7, 'D#': -6, 'E': -5, 'F': -4, 'F#': -3, 'G': -2, 'G#': -1, 'A': 0, 'A#': 1, 'B': 2}
    return 440.0 * 2 ** ((names[name[:-1]] + 12 * (int(name[-1]) - 4)) / 12)


# ---------------------------------------------------------------- ringtones

def ring_netmind():
    """netmind: warm three-note motif, rising then answering, with a pause."""
    s = []
    for t, n in ((0.00, 'E5'), (0.17, 'G5'), (0.34, 'C6'), (0.95, 'D5'), (1.12, 'G5'), (1.29, 'B5')):
        mix_at(s, kalimba(note(n), 1.3), t, 0.9 if n in ('C6', 'B5') else 1.0)
    return pad_to(reverb(lowpass(s, 6000), wet=0.18), 3.8)


def ring_soft():
    """Electric piano: slow rolled Cmaj9 chord, then a gentle answer."""
    s = []
    for i, n in enumerate(('C4', 'E4', 'G4', 'B4', 'D5')):
        mix_at(s, epiano(note(n), 2.4), i * 0.09, 0.8)
    for i, n in enumerate(('A3', 'E4', 'G4', 'C5')):
        mix_at(s, epiano(note(n), 2.0), 1.6 + i * 0.09, 0.6)
    return pad_to(reverb(lowpass(s, 5000), wet=0.20), 4.4)


def ring_marimba():
    """Friendly marimba phrase."""
    s = []
    phrase = (('C5', 0.00), ('E5', 0.16), ('G5', 0.32), ('A5', 0.48), ('G5', 0.72), ('E5', 0.88), ('D5', 1.04), ('G5', 1.28))
    for n, t in phrase:
        mix_at(s, marimba(note(n), 0.9), t)
    return pad_to(reverb(s, wet=0.14), 3.6)


def ring_discreet():
    """Very quiet double chime for open-plan offices."""
    s = []
    mix_at(s, bell(note('B5'), 1.6), 0.0)
    mix_at(s, bell(note('G5'), 1.8), 0.36)
    return pad_to(reverb(lowpass(s, 3500), wet=0.18), 3.6)


def square_ish(freq, t):
    """Band-limited square-like wave (odd harmonics 1, 3, 5): the classic ringer buzz."""
    return (math.sin(TWO_PI * freq * t) + math.sin(TWO_PI * 3 * freq * t) / 3 * 0.8 + math.sin(TWO_PI * 5 * freq * t) / 5 * 0.5)


def trill(f1, f2, seconds, rate=20.0, wave_fn=square_ish, rise=0.01, fall=0.02):
    """Electronic ringer: alternates between two tones `rate` times per second."""
    n = int(RATE * seconds)
    out = []
    for i in range(n):
        t = i / RATE
        f = f1 if int(t * rate * 2) % 2 == 0 else f2
        env = min(1.0, t / rise, (seconds - t) / fall)
        out.append(wave_fn(f, t) * max(env, 0.0))
    return out


def ring_classic():
    """Classic electronic telephone ringer: ring-ring, pause."""
    burst = trill(950, 1190, 0.4)
    s = burst + silence(0.2) + burst
    return pad_to(lowpass(s, 5000), 3.0)


def ring_old_bell():
    """Electromechanical phone: a hammer strikes two bells 20 times a second."""
    def burst(seconds):
        b = []
        strikes = int(seconds * 20)
        for k in range(strikes):
            f = 1420 if k % 2 == 0 else 1565
            # small metal bell: inharmonic partials, rings on between strikes
            mix_at(b, modal(f, 0.6, [(1.0, 1.0, 9.0), (2.32, 0.45, 14.0), (4.07, 0.25, 22.0), (6.1, 0.1, 30.0)], strike=0.0008, noise=0.15, noise_tone=4000), k * 0.05, 0.6)
        return b
    s = []
    mix_at(s, burst(0.4), 0.0)
    mix_at(s, burst(0.4), 0.6)
    return pad_to(lowpass(reverb(s, wet=0.10, tail=0.5), 6000), 3.2)


def ring_desk():
    """Modern IP desk phone: softer sine trill, ring-ring, pause."""
    sine = lambda f, t: math.sin(TWO_PI * f * t) + 0.25 * math.sin(TWO_PI * 2 * f * t)
    burst = trill(note('G5'), note('B5'), 0.45, rate=16.0, wave_fn=sine, rise=0.04, fall=0.08)
    s = burst + silence(0.25) + burst
    return pad_to(reverb(lowpass(s, 4500), wet=0.08, tail=0.4), 3.4)


def ring_digital():
    """Short electronic triple trill (DECT style), twice, then a pause."""
    chirp = trill(1400, 1750, 0.12, rate=25.0, rise=0.005, fall=0.01)
    group = chirp + silence(0.07) + chirp + silence(0.07) + chirp
    s = group + silence(0.45) + group
    return pad_to(lowpass(s, 5000), 3.0)


netmind = ring_netmind()
write('ringtone.wav', netmind)                      # default ringtone: netmind
write('ringtones/classic.wav', ring_classic())
write('ringtones/oldbell.wav', ring_old_bell())
write('ringtones/office.wav', ring_desk())
write('ringtones/digital.wav', ring_digital())
write('ringtones/netmind.wav', netmind)
write('ringtones/soft.wav', ring_soft())
write('ringtones/marimba.wav', ring_marimba())
write('ringtones/discreet.wav', ring_discreet(), loudness=0.08)

# Call-progress tones: European 425 Hz (as in Greece), with soft edges.
write('ringing.wav', soft_tone((425,), 1.0) + silence(4.0), loudness=0.09)        # ringback
write('ringing2.wav', soft_tone((425,), 0.18) + silence(0.18) + soft_tone((425,), 0.18), loudness=0.08)  # call waiting
write('hangup.wav', (soft_tone((425,), 0.22) + silence(0.22)) * 3, loudness=0.08)  # call ended

# Message notifications: short and soft.
msg_in = []
mix_at(msg_in, kalimba(note('G5'), 0.7), 0.0)
mix_at(msg_in, kalimba(note('C6'), 0.8), 0.11)
write('msgin.wav', reverb(lowpass(msg_in, 6000), wet=0.12, tail=0.4), loudness=0.08)
write('msgout.wav', reverb(marimba(note('E5'), 0.4), wet=0.08, tail=0.25), loudness=0.06)
