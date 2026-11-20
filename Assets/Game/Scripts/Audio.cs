// Audio: port of the WebAudio object `A` (index.html lines 1187-1253) to a small software synthesiser.
// - Continuous layers (rain, wind, leaves, drone, chant, grind) and all one-shots are synthesised on the audio thread
//   in OnAudioFilterRead, mirroring the original node graphs (RBJ biquads, AudioParam automation, tanh shaper, compressor).
// - Voice clips (mp3/wav in Resources/Audio) play through pooled AudioSources.
// Everything is safe to call before init() (no-ops while !ok).
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Host component: lives next to a silent looping AudioSource and feeds the synth into its filter chain.</summary>

public static class A
{
    // ================= public state =================
    public static bool ok;
    /// <summary>Smoothed parameter (AudioParam + setTargetAtTime). Written by the game thread, smoothed on the audio thread.</summary>
    public class Param
    {
        public volatile float target; public float cur; public volatile float tc = 0.3f;
        public Param(float v) { target = v; cur = v; }
    }
    public static readonly Param rainG = new Param(0f), rainLP = new Param(6500f), rainLowG = new Param(0f), windG = new Param(0.1f), leafG = new Param(0f),
        droneG = new Param(0f), droneDis = new Param(0f), chantG = new Param(0f), grindG = new Param(0f), grindPan = new Param(0f), chantClipG = new Param(0f);
    /// <summary>JS A.set(param, v, tc): approach v exponentially with time constant tc.</summary>
    public static void set(Param p, float v, float tc = 0.3f) { if (!ok || p == null) return; p.tc = tc < 0.001f ? 0.001f : tc; p.target = v; }

    public static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    // ================= internals =================
    const int LP = 0, HP = 1, BP = 2;
    const int SINE = 0, SAW = 1, SQUARE = 2, TRI = 3;
    const int MAX_VOICES = 40, MAX_OSC = 6;
    const float MASTER = 0.9f;
    static readonly object sync = new object();
    static int sr = 48000; static float invSr = 1f / 48000f;
    static volatile bool paused;
    static bool inited, clipsLoaded;
    static GameObject host; static AudioSource synthSrc, chantSrc; static float chantVol;
    static float clock;   // seconds of un-suspended time (game thread)

    sealed class ClipSlot { public AudioSource src; public AudioLowPassFilter lp; public float busyUntil; }
    static readonly List<ClipSlot> slots = new List<ClipSlot>();

    // ---- RNGs (own xorshift: UnityEngine.Random must not be used on the audio thread) ----
    static uint rngA = 0x9E3779B9u, rngM = 0x1234ABCDu;
    static float White() { uint x = rngA; x ^= x << 13; x ^= x >> 17; x ^= x << 5; rngA = x; return (x >> 8) * (2f / 16777216f) - 1f; }
    /// <summary>Game-thread Math.random().</summary>
    static float mr() { uint x = rngM; x ^= x << 13; x ^= x >> 17; x ^= x << 5; rngM = x; return (x >> 8) * (1f / 16777216f); }

    // ---- RBJ biquad (WebAudio BiquadFilterNode: Q is in dB for lowpass/highpass, linear for bandpass) ----
    sealed class Biquad
    {
        float b0, b1, b2, a1, a2, z1, z2;
        public void Reset() { z1 = 0; z2 = 0; }
        public void Set(int type, float f, float q, int rate)
        {
            float fmax = rate * 0.47f; if (f > fmax) f = fmax; if (f < 10f) f = 10f;
            double w0 = 2.0 * Math.PI * f / rate; float cs = (float)Math.Cos(w0), sn = (float)Math.Sin(w0); float alpha, n0, n1, n2;
            if (type == BP) { alpha = sn / (2f * (q < 0.0001f ? 0.0001f : q)); n0 = alpha; n1 = 0f; n2 = -alpha; }
            else
            {
                alpha = sn / (2f * (float)Math.Pow(10.0, q / 20.0));
                if (type == LP) { n0 = (1f - cs) * 0.5f; n1 = 1f - cs; n2 = n0; } else { n0 = (1f + cs) * 0.5f; n1 = -(1f + cs); n2 = n0; }
            }
            float ia0 = 1f / (1f + alpha); b0 = n0 * ia0; b1 = n1 * ia0; b2 = n2 * ia0; a1 = -2f * cs * ia0; a2 = (1f - alpha) * ia0;
        }
        public float Process(float x)
        {
            float y = b0 * x + z1 + 1e-20f; z1 = b1 * x - a1 * y + z2; z2 = b2 * x - a2 * y; return y;
        }
    }

    // ---- AudioParam automation: setValueAtTime / exponentialRampToValueAtTime / linearRampToValueAtTime / setTargetAtTime ----
    sealed class Auto
    {
        struct Ev { public float t, v, tc; public byte kind; }
        readonly Ev[] ev; int n, idx, mode; float k, target, segV, segEnd; bool live;
        public float val;
        public Auto(int cap) { ev = new Ev[cap]; }
        public void Reset(float initial) { n = 0; idx = 0; mode = 0; val = initial; live = false; }
        void Add(byte kind, float t, float v, float tc) { if (n < ev.Length) { ev[n].kind = kind; ev[n].t = t; ev[n].v = v; ev[n].tc = tc; n++; live = true; } }
        public Auto Set(float t, float v) { Add(0, t, v, 0); return this; }
        public Auto Exp(float t, float v) { Add(1, t, v, 0); return this; }
        public Auto Lin(float t, float v) { Add(2, t, v, 0); return this; }
        public Auto Target(float t, float v, float tc) { Add(3, t, v, tc); return this; }
        /// <summary>Value at voice time t; must be called once per sample (dt = 1 / sampleRate).</summary>
        public float Next(float t, float dt)
        {
            if (!live) return val;
            while (true)
            {
                if (mode == 1 || mode == 2) { if (t >= segEnd) { val = segV; mode = 0; idx++; continue; } break; }
                if (idx >= n) { if (mode == 0) live = false; break; }
                byte kd = ev[idx].kind;
                if (kd == 1 || kd == 2)
                {
                    float dur = ev[idx].t - t;
                    if (dur <= dt) { val = ev[idx].v; mode = 0; idx++; continue; }
                    segEnd = ev[idx].t; segV = ev[idx].v;
                    if (kd == 1) { float a = val < 1e-6f ? 1e-6f : val, b = segV < 1e-6f ? 1e-6f : segV; val = a; k = (float)Math.Pow(b / a, dt / dur); mode = 1; }
                    else { k = (segV - val) * dt / dur; mode = 2; }
                    break;
                }
                if (t >= ev[idx].t)
                {
                    if (kd == 0) { val = ev[idx].v; mode = 0; }
                    else { target = ev[idx].v; float tc = ev[idx].tc < 1e-4f ? 1e-4f : ev[idx].tc; k = 1f - (float)Math.Exp(-dt / tc); mode = 3; }
                    idx++; continue;
                }
                break;
            }
            if (mode == 1) val *= k; else if (mode == 2) val += k; else if (mode == 3) val += (target - val) * k;
            return val;
        }
    }

    sealed class Osc
    {
        public int type; public readonly Auto freq = new Auto(4), amp = new Auto(4); public float vibRate, vibDepth, phase;
    }

    /// <summary>One-shot voice: sum(osc*amp) -> [osc filter] + noise -> [noise filter] * nAmp -> [tanh shaper] -> gain -> pan.</summary>
    sealed class Voice
    {
        public bool active; public long serial; public int delay, pos, len;
        public int nOsc; public readonly Osc[] osc = new Osc[MAX_OSC];
        public bool oscFilt, ofAuto; public int ofType; public float ofQ; public readonly Biquad of = new Biquad(); public readonly Auto ofFreq = new Auto(4);
        public int noise, noiseLen; public float brown; public readonly Auto nAmp = new Auto(32);
        public bool nFilt, nfAuto; public int nfType; public float nfQ; public readonly Biquad nf = new Biquad(); public readonly Auto nfFreq = new Auto(4);
        public bool shaper; public readonly Auto gain = new Auto(8); public float gl, gr;
        public Voice() { for (int i = 0; i < MAX_OSC; i++) osc[i] = new Osc(); }
    }
    static readonly Voice[] voices = new Voice[MAX_VOICES];
    static long serial;

    // ---- voice builders (call inside lock(sync)) ----
    static Voice NewVoice(float delay, float dur, bool panned, float pan)
    {
        Voice v = null; long oldest = long.MaxValue; int oi = 0;
        for (int i = 0; i < MAX_VOICES; i++)
        {
            if (!voices[i].active) { v = voices[i]; break; }
            if (voices[i].serial < oldest) { oldest = voices[i].serial; oi = i; }
        }
        if (v == null) v = voices[oi];   // steal the oldest
        v.serial = ++serial; v.delay = (int)(Math.Max(0f, delay) * sr); v.pos = 0; v.len = Math.Max(1, (int)(dur * sr));
        v.nOsc = 0; v.oscFilt = false; v.ofAuto = false; v.of.Reset(); v.ofFreq.Reset(0);
        v.noise = 0; v.noiseLen = int.MaxValue; v.brown = 0; v.nAmp.Reset(1f); v.nFilt = false; v.nfAuto = false; v.nf.Reset(); v.nfFreq.Reset(0);
        v.shaper = false; v.gain.Reset(1f);
        if (panned) { float x = (Mathf.Clamp(pan, -1f, 1f) + 1f) * 0.25f * Mathf.PI; v.gl = Mathf.Cos(x); v.gr = Mathf.Sin(x); } else { v.gl = 1f; v.gr = 1f; }
        v.active = true; return v;
    }
    static Osc AddOsc(Voice v, int type, float freq, float amp)
    {
        if (v.nOsc >= MAX_OSC) return v.osc[MAX_OSC - 1];
        var o = v.osc[v.nOsc++]; o.type = type; o.freq.Reset(freq); o.amp.Reset(amp); o.vibRate = 0; o.vibDepth = 0; o.phase = 0; return o;
    }
    static void SetNoise(Voice v, int kind, float amp, float dur) { v.noise = kind; v.nAmp.Reset(amp); v.noiseLen = dur > 0 ? (int)(dur * sr) : int.MaxValue; }
    static void NoiseFilter(Voice v, int type, float f, float q) { v.nFilt = true; v.nfType = type; v.nfQ = q; v.nfFreq.Reset(f); v.nf.Set(type, f, q, sr); }
    static void OscFilter(Voice v, int type, float f, float q) { v.oscFilt = true; v.ofType = type; v.ofQ = q; v.ofFreq.Reset(f); v.of.Set(type, f, q, sr); }
    /// <summary>JS env(g, t, a, peak, dec).</summary>
    static void Env(Auto g, float t, float a, float peak, float dec) { g.Set(t, 0.0001f).Exp(t + a, Math.Max(0.0002f, peak)).Exp(t + a + dec, 0.0001f); }

    // ================= lifecycle =================
    public static void init()
    {
        if (inited) return; inited = true;
        try
        {
            sr = AudioSettings.outputSampleRate; if (sr <= 0) sr = 48000; invSr = 1f / sr;
            rngM = (uint)(Environment.TickCount | 1);
            for (int i = 0; i < MAX_VOICES; i++) voices[i] = new Voice();
            InitLayers();
            host = new GameObject("QF_Audio"); UnityEngine.Object.DontDestroyOnLoad(host); host.hideFlags = HideFlags.HideInHierarchy;
            synthSrc = host.AddComponent<AudioSource>();
            synthSrc.clip = AudioClip.Create("qf_silence", sr, 1, sr, false); synthSrc.loop = true; synthSrc.spatialBlend = 0f; synthSrc.volume = 1f; synthSrc.priority = 0;
            synthSrc.playOnAwake = false; synthSrc.bypassEffects = false; synthSrc.bypassListenerEffects = true; synthSrc.bypassReverbZones = true;
            host.AddComponent<QFAudioSynth>();
            synthSrc.Play();
            for (int i = 0; i < 8; i++)
            {
                var g = new GameObject("clip" + i); g.transform.SetParent(host.transform, false);
                var s = g.AddComponent<AudioSource>(); s.playOnAwake = false; s.spatialBlend = 0f; s.loop = false; s.priority = 16; s.bypassReverbZones = true;
                var f = g.AddComponent<AudioLowPassFilter>(); f.enabled = false;
                slots.Add(new ClipSlot { src = s, lp = f, busyUntil = 0f });
            }
            ok = true;
        }
        catch (Exception e) { Debug.LogWarning("Audio init failed: " + e); ok = false; }
    }
    public static void resume() { if (!ok) return; paused = false; AudioListener.pause = false; if (synthSrc != null && !synthSrc.isPlaying) synthSrc.Play(); }
    public static void suspend() { if (!ok) return; paused = true; AudioListener.pause = true; }
    /// <summary>Stop every playing/scheduled voice clip and kill all one-shot synth voices (layers keep running).</summary>
    public static void stopAll()
    {
        if (!inited) return;
        for (int i = 0; i < slots.Count; i++) { if (slots[i].src != null) slots[i].src.Stop(); slots[i].busyUntil = 0f; }
        lock (sync) { for (int i = 0; i < MAX_VOICES; i++) if (voices[i] != null) voices[i].active = false; }
    }
    /// <summary>Game-thread tick (called by QFAudioSynth.Update).</summary>
    public static void Tick(float dt)
    {
        if (!ok || paused) return;
        if (dt > 0.1f) dt = 0.1f; clock += dt;
        if (chantSrc != null)
        {
            float tc = chantClipG.tc; chantVol += (chantClipG.target - chantVol) * (1f - Mathf.Exp(-dt / (tc < 0.001f ? 0.001f : tc)));
            chantClipG.cur = chantVol; chantSrc.volume = Mathf.Clamp01(chantVol);
        }
    }

    // ================= voice clips =================
    public static void loadClips()
    {
        if (clipsLoaded || !ok) return; clipsLoaded = true;
        var names = new List<string> { "name1", "name2", "cant", "back", "noenter", "nofollow", "nothuman", "nolook", "sizhu", "nopray", "noexit", "behind", "come1", "come2", "exit", "why", "fname", "arrived", "lookme", "rev1", "rev2", "rev3", "chant" };
        for (int i = 1; i <= 36; i++) names.Add("t" + i.ToString("00"));
        foreach (var n in names) { var c = Resources.Load<AudioClip>("Audio/" + n); if (c != null) { clips[n] = c; if (n == "chant") startChant(); } }
    }
    public static void startChant()
    {
        if (chantSrc != null || !ok || !clips.ContainsKey("chant")) return;
        var g = new GameObject("chant"); g.transform.SetParent(host.transform, false);
        chantSrc = g.AddComponent<AudioSource>(); chantSrc.clip = clips["chant"]; chantSrc.loop = true; chantSrc.spatialBlend = 0f; chantSrc.volume = 0f; chantSrc.priority = 8; chantSrc.playOnAwake = false;
        chantVol = 0f; chantSrc.Play();
    }
    /// <summary>JS A.clip(name, {vol, pan, lp, delay, rate}). Returns the playing time in seconds (0 if the clip is missing).</summary>
    public static float clip(string name, float vol = 0.8f, float pan = 0f, float lp = 0f, float delay = 0f, float rate = 1f)
    {
        if (!ok || name == null) return 0f; AudioClip b; if (!clips.TryGetValue(name, out b) || b == null) return 0f;
        if (rate <= 0f) rate = 1f; float dur = b.length / rate;
        ClipSlot s = null; float best = float.MaxValue;
        for (int i = 0; i < slots.Count; i++) { if (slots[i].busyUntil <= clock) { s = slots[i]; break; } if (slots[i].busyUntil < best) { best = slots[i].busyUntil; s = slots[i]; } }
        if (s == null) return dur;
        s.src.Stop(); s.src.clip = b; s.src.volume = Mathf.Clamp01(vol); s.src.panStereo = Mathf.Clamp(pan, -1f, 1f); s.src.pitch = rate;
        if (lp > 0f) { s.lp.enabled = true; s.lp.cutoffFrequency = Mathf.Clamp(lp, 200f, 22000f); } else s.lp.enabled = false;
        if (delay > 0f) s.src.PlayDelayed(delay); else s.src.Play();
        s.busyUntil = clock + Math.Max(0f, delay) + dur + 0.15f;
        return dur;
    }

    // ================= one-shots =================
    public static void step(int surf, bool run)
    {
        if (!ok) return;
        lock (sync)
        {
            var v = NewVoice(0, 0.2f, false, 0);
            float f = surf == 2 ? 700 + mr() * 300 : surf == 1 ? 1300 + mr() * 700 : 360 + mr() * 240, q = surf == 2 ? 2.2f : surf == 1 ? 1.1f : 0.9f;
            SetNoise(v, 1, 0f, 0.2f); NoiseFilter(v, BP, f, q);
            Env(v.nAmp, 0, 0.006f, (run ? 0.34f : 0.22f) * (surf == 1 ? 0.85f : 1f), surf == 2 ? 0.09f : 0.12f);
            if (surf == 2) { var o = AddOsc(v, SINE, 0, 0); o.freq.Set(0, 95 + mr() * 20).Exp(0.1f, 60); Env(o.amp, 0, 0.004f, run ? 0.3f : 0.2f, 0.14f); }
        }
    }
    static readonly float[][] BELL = { new[] { 1f, 1f, 7f }, new[] { 2.0f, 0.5f, 5f }, new[] { 2.76f, 0.38f, 4f }, new[] { 4.07f, 0.2f, 3f }, new[] { 5.4f, 0.13f, 2.4f }, new[] { 0.5f, 0.55f, 8f } };
    public static void bell(float pan, float vol)
    {
        if (!ok) return;
        lock (sync)
        {
            var v = NewVoice(0, 8.4f, true, pan); OscFilter(v, LP, 700 + vol * 5000, 1f);
            foreach (var p in BELL) { var o = AddOsc(v, SINE, 98 * p[0] * (1 + (mr() - 0.5f) * 0.004f), 0); Env(o.amp, 0, 0.008f, vol * p[1] * 0.5f, p[2]); }
        }
    }
    public static void knock(float pan, float vol, float dt = 0f, bool low = false)
    {
        if (!ok) return;
        lock (sync)
        {
            var v = NewVoice(dt, 0.32f, true, pan); float f0 = low ? 210 : 560;
            var o = AddOsc(v, SINE, 0, 0); o.freq.Set(0, f0).Exp(0.08f, f0 * 0.75f); Env(o.amp, 0, 0.002f, vol, low ? 0.2f : 0.12f);
            SetNoise(v, 1, 0f, 0.05f); NoiseFilter(v, BP, low ? 900 : 2600, 1f); Env(v.nAmp, 0, 0.001f, vol * 0.4f, 0.02f);
        }
    }
    public static void thunder(float vol)
    {
        if (!ok) return;
        lock (sync)
        {
            var v = NewVoice(0, 4.2f, false, 0); SetNoise(v, 2, 0f, 0); NoiseFilter(v, LP, 260, 1f); v.nfAuto = true; v.nfFreq.Set(0, 260).Exp(3.5f, 90); Env(v.nAmp, 0, 0.06f, vol, 3.6f);
            var c = NewVoice(0, 0.7f, false, 0); SetNoise(c, 1, 0f, 0.7f); NoiseFilter(c, BP, 1600, 1f); Env(c.nAmp, 0, 0.01f, vol * 0.3f, 0.5f);
        }
    }
    static readonly float[] STING = { 233, 247, 349, 370, 466 };
    public static void stinger()
    {
        if (!ok) return;
        lock (sync)
        {
            var v = NewVoice(0, 2.0f, false, 0); OscFilter(v, LP, 2600, 1f); v.gain.Reset(0); Env(v.gain, 0, 0.015f, 0.42f, 1.8f);
            foreach (var f in STING) { var o = AddOsc(v, SAW, 0, 1f); o.freq.Set(0, f).Exp(1.8f, f * 0.8f); }
            var n = NewVoice(0, 0.6f, false, 0); SetNoise(n, 1, 0f, 0.6f); NoiseFilter(n, HP, 2000, 1f); Env(n.nAmp, 0, 0.004f, 0.3f, 0.4f);
        }
    }
    public static void scream()
    {
        if (!ok) return;
        lock (sync)
        {
            var v = NewVoice(0, 1.5f, false, 0); v.shaper = true; v.gain.Reset(0); Env(v.gain, 0, 0.01f, 0.85f, 1.4f);
            AddOsc(v, SAW, 0, 0.3f).freq.Set(0, 880).Exp(1.2f, 110);
            AddOsc(v, SQUARE, 0, 0.3f).freq.Set(0, 660).Exp(1.2f, 90);
            AddOsc(v, SAW, 0, 0.3f).freq.Set(0, 1240).Exp(1.2f, 300);
            SetNoise(v, 1, 1f, 1.5f); NoiseFilter(v, BP, 3200, 2f); v.nfAuto = true; v.nfFreq.Set(0, 3200).Exp(1.2f, 500);
        }
    }
    public static void whisper(float pan, float vol)
    {
        if (!ok) return;
        lock (sync)
        {
            float tk = 0.05f; int n = 7 + (int)(mr() * 5);
            var v = NewVoice(0, 1f, true, pan); SetNoise(v, 1, 0.0001f, 0); NoiseFilter(v, BP, 2500 + mr() * 800, 1.8f);
            for (int k = 0; k < n; k++)
            {
                float d = 0.07f + mr() * 0.16f; v.nAmp.Target(tk, vol * (0.4f + mr() * 0.6f), 0.02f).Target(tk + d, 0.0001f, 0.03f); tk += d + 0.04f + mr() * 0.12f;
            }
            v.len = (int)((tk + 0.3f) * sr);
        }
    }
    public static void heart(float vol)
    {
        if (!ok) return;
        lock (sync)
        {
            var v = NewVoice(0, 0.52f, false, 0);
            for (int i = 0; i < 2; i++)
            {
                float d = i == 0 ? 0f : 0.24f; var o = AddOsc(v, SINE, 0, 0); o.freq.Set(d, 62).Exp(d + 0.12f, 40); Env(o.amp, d, 0.01f, vol * (i == 1 ? 0.7f : 1f), 0.16f);
            }
        }
    }
    public static void ignite()
    {
        if (!ok) return;
        lock (sync)
        {
            var v = NewVoice(0, 2.0f, false, 0);
            SetNoise(v, 1, 0f, 0.6f); NoiseFilter(v, LP, 200, 1f); v.nfAuto = true; v.nfFreq.Set(0, 200).Exp(0.35f, 2400); Env(v.nAmp, 0, 0.05f, 0.25f, 0.45f);
            var o = AddOsc(v, SINE, 523, 0); Env(o.amp, 0.1f, 0.01f, 0.08f, 1.6f);
        }
    }
    static readonly float[] CHIME = { 784, 1175, 1568 };
    public static void chime()
    {
        if (!ok) return;
        lock (sync)
        {
            var v = NewVoice(0, 1.8f, false, 0);
            for (int i = 0; i < 3; i++) { var o = AddOsc(v, SINE, CHIME[i], 0); Env(o.amp, i * 0.09f, 0.008f, 0.14f, 1.4f); }
        }
    }
    public static void breath(float pan = 0f)
    {
        if (!ok) return;
        lock (sync)
        {
            var v = NewVoice(0, 2.8f, true, pan); SetNoise(v, 1, 0f, 0); NoiseFilter(v, BP, 700, 1.2f); v.nfAuto = true; v.nfFreq.Set(0, 700).Lin(0.9f, 1300).Lin(2.2f, 600);
            v.nAmp.Set(0, 0.0001f).Exp(0.8f, 0.16f).Exp(1.1f, 0.02f).Exp(1.5f, 0.2f).Exp(2.6f, 0.0001f);
        }
    }
    static readonly float[][] HUM = { new[] { 392f, 0f, 1.5f }, new[] { 370f, 1.5f, 1.3f }, new[] { 330f, 2.8f, 2.4f } };
    public static void hum(float pan = 0f)
    {
        if (!ok) return;
        lock (sync)
        {
            var v = NewVoice(0, 5.4f, true, pan); OscFilter(v, LP, 900, 1f);
            foreach (var nt in HUM)
            {
                float f = nt[0], d = nt[1], l = nt[2];
                for (int i = 0; i < 2; i++)
                {
                    var o = AddOsc(v, i == 1 ? SINE : TRI, f * (i == 1 ? 2.01f : 1f), 0); o.vibRate = 5.2f; o.vibDepth = f * 0.012f;
                    o.amp.Set(d, 0.0001f).Exp(d + 0.25f, i == 1 ? 0.012f : 0.05f).Exp(d + l, 0.0001f);
                }
            }
        }
    }
    public static void stepsBehind(int n)
    {
        if (!ok) return;
        lock (sync)
        {
            for (int k = 0; k < n; k++)
            {
                var v = NewVoice(0.2f + k * 0.52f + mr() * 0.05f, 0.22f, false, 0); SetNoise(v, 1, 0f, 0.22f); NoiseFilter(v, BP, 300 + mr() * 120, 1f);
                Env(v.nAmp, 0, 0.008f, 0.12f + k * 0.012f, 0.13f);
            }
        }
    }
    public static void rumble()
    {
        if (!ok) return;
        lock (sync)
        {
            var v = NewVoice(0, 4.6f, false, 0); SetNoise(v, 2, 0f, 0); NoiseFilter(v, LP, 90, 1f); Env(v.nAmp, 0, 0.8f, 0.9f, 3.5f);
            var o = AddOsc(v, SINE, 36, 0); Env(o.amp, 0, 0.8f, 0.25f, 3f);
        }
    }

    // ================= continuous layers (audio thread) =================
    static readonly Biquad rainHP = new Biquad(), rainLPf = new Biquad(), rainLowLP = new Biquad(), windLP = new Biquad(), leafBP = new Biquad(), droneLP = new Biquad(),
        chantF1 = new Biquad(), chantF2 = new Biquad(), grindBP1 = new Biquad(), grindBP2 = new Biquad();
    static float brRainLow, brWind, brGrind, lastRainLP = -1f;
    static double tLayers;
    static readonly float[] dronePh = new float[4], chantPh = new float[3], chantVib = new float[3];
    static readonly float[] DRONE_F = { 55f, 55.4f, 82.6f, 61.7f }, CHANT_F = { 98f, 146.8f, 196.5f };
    static void InitLayers()
    {
        rainHP.Set(HP, 900, 1f, sr); rainLPf.Set(LP, 6500, 1f, sr); lastRainLP = 6500; rainLowLP.Set(LP, 520, 1f, sr); windLP.Set(LP, 380, 2f, sr); leafBP.Set(BP, 3200, 0.7f, sr);
        droneLP.Set(LP, 260, 1f, sr); chantF1.Set(BP, 650, 7f, sr); chantF2.Set(BP, 1100, 9f, sr); grindBP1.Set(BP, 210, 2.5f, sr); grindBP2.Set(BP, 900, 4f, sr);
        for (int i = 0; i < 3; i++) chantVib[i] = 4.4f + mr();
    }
    static void Smooth(Param p, float bt) { float tg = p.target, tc = p.tc; if (p.cur == tg) return; p.cur += (tg - p.cur) * (1f - (float)Math.Exp(-bt / (tc < 0.001f ? 0.001f : tc))); if (Math.Abs(p.cur - tg) < 1e-6f) p.cur = tg; }
    static bool On(Param p) { return p.cur > 1e-5f || p.target > 1e-5f; }

    static void RenderLayers(int frames)
    {
        const int SUB = 32; const double TAU = 6.283185307179586; float dt = invSr;
        for (int p0 = 0; p0 < frames; p0 += SUB)
        {
            int n = Math.Min(SUB, frames - p0); float bt = n * dt; double t = tLayers;
            Smooth(rainG, bt); Smooth(rainLP, bt); Smooth(rainLowG, bt); Smooth(windG, bt); Smooth(leafG, bt); Smooth(droneG, bt); Smooth(droneDis, bt); Smooth(chantG, bt); Smooth(grindG, bt); Smooth(grindPan, bt);
            bool rainOn = On(rainG), rainLowOn = On(rainLowG), windOn = On(windG), leafOn = On(leafG), droneOn = On(droneG), chantOn = On(chantG), grindOn = On(grindG);
            if (rainOn && Math.Abs(rainLP.cur - lastRainLP) > 1f) { lastRainLP = rainLP.cur; rainLPf.Set(LP, lastRainLP, 1f, sr); }
            if (windOn) windLP.Set(LP, 380f + 220f * (float)Math.Sin(TAU * 0.06 * t), 2f, sr);
            float leafAm = 0.5f + 0.45f * (float)Math.Sin(TAU * 0.4 * t);
            float chantAm = 0.6f + 0.4f * (float)Math.Sin(TAU * 1.6 * t);
            float cv0 = 0, cv1 = 0, cv2 = 0;
            if (chantOn)
            {
                chantF1.Set(BP, 650f + 180f * (float)Math.Sin(TAU * 0.35 * t), 7f, sr);
                cv0 = (CHANT_F[0] + 1.3f * (float)Math.Sin(TAU * chantVib[0] * t)) * dt; cv1 = (CHANT_F[1] + 1.3f * (float)Math.Sin(TAU * chantVib[1] * t)) * dt; cv2 = (CHANT_F[2] + 1.3f * (float)Math.Sin(TAU * chantVib[2] * t)) * dt;
            }
            float gx = (Math.Max(-1f, Math.Min(1f, grindPan.cur)) + 1f) * 0.25f * (float)Math.PI, gpl = (float)Math.Cos(gx), gpr = (float)Math.Sin(gx);
            float gRain = rainG.cur, gRainLow = rainLowG.cur, gWind = windG.cur, gLeaf = leafG.cur * leafAm, gDrone = droneG.cur, gDis = droneDis.cur, gChant = chantG.cur * chantAm, gGrind = grindG.cur;
            double sqPh = t * 7.0; sqPh -= Math.Floor(sqPh); float sq = (float)sqPh, sqInc = 7f * dt;
            for (int i = 0; i < n; i++)
            {
                float m = 0f;
                if (rainOn) m += rainLPf.Process(rainHP.Process(White())) * gRain;
                if (rainLowOn) { brRainLow = (brRainLow + 0.02f * White()) / 1.02f; m += rainLowLP.Process(brRainLow * 3.5f) * gRainLow; }
                if (windOn) { brWind = (brWind + 0.02f * White()) / 1.02f; m += windLP.Process(brWind * 3.5f) * gWind; }
                if (leafOn) m += leafBP.Process(White()) * gLeaf;
                if (droneOn)
                {
                    float x = 0f;
                    for (int k = 0; k < 4; k++) { float ph = dronePh[k] + DRONE_F[k] * dt; if (ph >= 1f) ph -= 1f; dronePh[k] = ph; x += (2f * ph - 1f) * (k == 3 ? gDis : 1f); }
                    m += droneLP.Process(x) * gDrone;
                }
                if (chantOn)
                {
                    float p0c = chantPh[0] + cv0; if (p0c >= 1f) p0c -= 1f; chantPh[0] = p0c;
                    float p1c = chantPh[1] + cv1; if (p1c >= 1f) p1c -= 1f; chantPh[1] = p1c;
                    float p2c = chantPh[2] + cv2; if (p2c >= 1f) p2c -= 1f; chantPh[2] = p2c;
                    float x = (2f * p0c - 1f) + (2f * p1c - 1f) + (2f * p2c - 1f);
                    m += (chantF1.Process(x) + chantF2.Process(x)) * gChant;
                }
                float l = m, r = m;
                if (grindOn)
                {
                    brGrind = (brGrind + 0.02f * White()) / 1.02f;
                    float g = grindBP1.Process(brGrind * 3.5f) + grindBP2.Process(White()) * 0.16f;
                    sq += sqInc; if (sq >= 1f) sq -= 1f;
                    g *= (0.65f + (sq < 0.5f ? 0.35f : -0.35f)) * gGrind; l += g * gpl; r += g * gpr;
                }
                bufL[p0 + i] = l; bufR[p0 + i] = r;
            }
            tLayers += bt;
        }
    }

    // ================= one-shot voices (audio thread) =================
    static float PolyBlep(float t, float dt)
    {
        if (dt <= 0f) return 0f;
        if (t < dt) { t /= dt; return t + t - t * t - 1f; }
        if (t > 1f - dt) { t = (t - 1f) / dt; return t * t + t + t + 1f; }
        return 0f;
    }
    static float Wave(int type, float ph, float dp)
    {
        switch (type)
        {
            case SAW: return 2f * ph - 1f - PolyBlep(ph, dp);
            case SQUARE: { float s = ph < 0.5f ? 1f : -1f; s += PolyBlep(ph, dp); float p2 = ph + 0.5f; if (p2 >= 1f) p2 -= 1f; s -= PolyBlep(p2, dp); return s; }
            case TRI: return 4f * Math.Abs(ph - 0.5f) - 1f;
            default: return (float)Math.Sin(6.283185307179586 * ph);
        }
    }
    static void RenderVoice(Voice v, int frames)
    {
        int i = 0; float dt = invSr;
        if (v.delay > 0) { int d = Math.Min(v.delay, frames); v.delay -= d; i = d; }
        for (; i < frames; i++)
        {
            if (v.pos >= v.len) { v.active = false; return; }
            float t = v.pos * dt, s = 0f;
            for (int o = 0; o < v.nOsc; o++)
            {
                Osc q = v.osc[o]; float f = q.freq.Next(t, dt);
                if (q.vibDepth != 0f) f += q.vibDepth * (float)Math.Sin(6.283185307179586 * q.vibRate * t);
                float a = q.amp.Next(t, dt), dp = f * dt, ph = q.phase + dp; if (ph >= 1f) ph -= (float)Math.Floor(ph); q.phase = ph;
                if (a > 1.5e-4f) s += Wave(q.type, ph, dp) * a;
            }
            if (v.oscFilt)
            {
                if (v.ofAuto) { float ff = v.ofFreq.Next(t, dt); if ((v.pos & 31) == 0) v.of.Set(v.ofType, ff, v.ofQ, sr); }
                s = v.of.Process(s);
            }
            if (v.noise != 0 && v.pos < v.noiseLen)
            {
                float w = White(); if (v.noise == 2) { v.brown = (v.brown + 0.02f * w) / 1.02f; w = v.brown * 3.5f; }
                if (v.nFilt)
                {
                    if (v.nfAuto) { float ff = v.nfFreq.Next(t, dt); if ((v.pos & 31) == 0) v.nf.Set(v.nfType, ff, v.nfQ, sr); }
                    w = v.nf.Process(w);
                }
                s += w * v.nAmp.Next(t, dt);
            }
            if (v.shaper) { float x = s < -1f ? -1f : s > 1f ? 1f : s; s = (float)Math.Tanh(x * 4f); }
            s *= v.gain.Next(t, dt);
            bufL[i] += s * v.gl; bufR[i] += s * v.gr;
            v.pos++;
        }
    }

    // ================= mix + compressor (audio thread) =================
    static float[] bufL = new float[4096], bufR = new float[4096];
    static float compEnv;
    /// <summary>Audio-thread entry point (called from QFAudioSynth.OnAudioFilterRead). Do not call from game code.</summary>
    public static void Render(float[] data, int channels)
    {
        if (!ok || paused || channels <= 0) { Array.Clear(data, 0, data.Length); return; }
        int frames = data.Length / channels;
        if (bufL.Length < frames) { bufL = new float[frames]; bufR = new float[frames]; }
        lock (sync)
        {
            RenderLayers(frames);
            for (int i = 0; i < MAX_VOICES; i++) { var v = voices[i]; if (v != null && v.active) RenderVoice(v, frames); }
        }
        // DynamicsCompressor approximation: threshold -16 dB, soft curve, attack 3 ms, release 250 ms, makeup ~ +3 dB; then a safety soft clip.
        const float T = 0.1585f, K = 2f, MAKEUP = 1.41f;
        float atk = 1f - (float)Math.Exp(-1.0 / (0.003 * sr)), rel = 1f - (float)Math.Exp(-1.0 / (0.25 * sr));
        float env = compEnv;
        for (int i = 0; i < frames; i++)
        {
            float l = bufL[i] * MASTER, r = bufR[i] * MASTER; float al = l < 0 ? -l : l, ar = r < 0 ? -r : r, pk = al > ar ? al : ar;
            env += (pk - env) * (pk > env ? atk : rel);
            float g = MAKEUP; if (env > T) g *= (T + (1f - (float)Math.Exp(-K * (env - T))) / K) / env;
            l *= g; r *= g;
            if (l > 0.9f) l = 0.9f + 0.1f * (float)Math.Tanh((l - 0.9f) * 10f); else if (l < -0.9f) l = -0.9f - 0.1f * (float)Math.Tanh((-l - 0.9f) * 10f);
            if (r > 0.9f) r = 0.9f + 0.1f * (float)Math.Tanh((r - 0.9f) * 10f); else if (r < -0.9f) r = -0.9f - 0.1f * (float)Math.Tanh((-r - 0.9f) * 10f);
            int j = i * channels;
            if (channels == 1) data[j] = (l + r) * 0.5f;
            else { data[j] = l; data[j + 1] = r; for (int c = 2; c < channels; c++) data[j + c] = 0f; }
        }
        if (env < 1e-9f) env = 0f; compEnv = env;
    }
}
