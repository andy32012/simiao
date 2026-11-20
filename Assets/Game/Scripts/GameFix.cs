// Fix pass on top of the faithful port: persistent save at lanterns, death explanations, first-time hints,
// visible lantern safe radius, flashlight beam in the fog. Everything in three.js coordinates except the beam (camera-local).
using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class G
{
    /// <summary>While true, showMsg is silent (used when replaying pickup effects from a save).</summary>
    public static bool restoring;
    /// <summary>Set by the title screen: true = continue from the save, false = new run.</summary>
    public static bool continueRun;
    static bool wasSafe; static string deathWhy = "";

    // ================= save =================
    public static void saveNow()
    {
        if (LANTERNS.Count == 0) return;
        var d = new SaveData
        {
            seed = SEED, nights = player.nights, deaths = player.deaths, charms = player.charms, keys = player.keys,
            lantern = Mathf.Max(0, LANTERNS.IndexOf(player.lastLantern)), hour = hour, elapsed = player.elapsed,
            battery = player.battery, sanity = player.sanity, stamina = player.stamina, wet = wet, weather = weather.name, name = PNAME, wasNight = wasNight,
            lanternLit = new bool[LANTERNS.Count], pickupTaken = new bool[PICKUPS.Count], steleRead = new bool[STELES.Count]
        };
        for (int i = 0; i < LANTERNS.Count; i++) d.lanternLit[i] = LANTERNS[i].lit;
        for (int i = 0; i < PICKUPS.Count; i++) d.pickupTaken[i] = PICKUPS[i].taken;
        for (int i = 0; i < STELES.Count; i++) d.steleRead[i] = STELES[i].read;
        d.flags = new string[flags.Count]; flags.CopyTo(d.flags);
        Save.write(d);
    }

    /// <summary>Rebuild the run state from a save: same world seed, taken pickups (with their temple effects replayed silently), lit lanterns, time of day.</summary>
    public static void applySave(SaveData d)
    {
        resetGame();
        restoring = true; bool aok = A.ok; A.ok = false;
        try
        {
            int keys = 0;
            for (int i = 0; i < PICKUPS.Count; i++)
            {
                if (d.pickupTaken == null || i >= d.pickupTaken.Length || !d.pickupTaken[i]) continue;
                var p = PICKUPS[i]; p.taken = true; p.g.visible = false; if (p.type == "key") keys++;
                if (p.onTake != null) { try { p.onTake(); } catch (Exception e) { Debug.LogException(e); } }
            }
            player.keys = keys; player.charms = Mathf.Max(0, d.charms);
            for (int i = 0; i < LANTERNS.Count; i++) setLantern(LANTERNS[i], i == 0 || (d.lanternLit != null && i < d.lanternLit.Length && d.lanternLit[i]));
            for (int i = 0; i < STELES.Count; i++) STELES[i].read = d.steleRead != null && i < d.steleRead.Length && d.steleRead[i];
            flags.Clear(); if (d.flags != null) foreach (var f in d.flags) flags.Add(f);
            hour = d.hour; player.nights = d.nights; player.deaths = d.deaths; player.elapsed = d.elapsed; wasNight = d.wasNight; wet = d.wet;
            string wn = d.weather != null && WX.ContainsKey(d.weather) ? d.weather : "mist"; weather.name = wn; weather.fog = WX[wn].fog; weather.rain = WX[wn].rain; weather.timer = 40;
            var L = LANTERNS[Mathf.Clamp(d.lantern, 0, LANTERNS.Count - 1)]; setLantern(L, true); player.lastLantern = L;
            float px = L.x, pz = L.z + 2.4f; resolve(ref px, ref pz, 0.5f); resolve(ref px, ref pz, 0.5f);
            player.x = px; player.z = pz; player.y = groundH(px, pz); player.yaw = 0; player.pitch = -0.03f;
            player.sanity = Mathf.Max(d.sanity, 60); player.battery = 100; player.stamina = 100; player.safe = true;
            lastZone = zoneOfZ(pz); lastInside = null;
        }
        finally { A.ok = aok; restoring = false; }
        msgQ.Clear(); msgCur = null; msgOn = false; shake = 0; stingerCD = 0;
    }

    // ================= death explanations =================
    public static string deathReason(Chaser c)
    {
        if (finalEvent && c.zone == "final") return "山門前，祂們連被看著都會走。這一段只能跑，不要停。";
        if (player.sanity <= 0.5f) return "心神歸零了。那時候就算盯著，祂們也還是會走——先回燈籠旁把心神養回來。";
        bool dark = night > 0.5f || player.inside != null;
        if (dark && player.battery <= 0) return "手電筒沒電了。黑暗裡你看不見祂；看不見，祂就能動。";
        if (dark && !player.flash) return "你沒開手電筒。夜裡和殿裡，只有被燈照到的才算「看見」。";
        if (c.hall != null) return "殿裡的石僧在你背後。你轉身之前，祂已經走到了。";
        return "祂是從你沒看著的方向來的。走路時，記得回頭看。";
    }
    public static void noteDeath(Chaser c) { deathWhy = deathReason(c); QFLog.info("被抓（第 " + (player.deaths + 1) + " 次）：" + deathWhy + " · " + (player.inside != null ? player.inside.id : player.zone) + " (" + player.x.ToString("0") + "," + player.z.ToString("0") + ")"); }
    public static string deadSubText()
    {
        return deathWhy + "\n" + (player.deaths > 2 ? "祂們越來越熟悉你了。" : "") + "最後點亮的那盞燈籠還在燒；拿到的鑰匙都還在。";
    }

    // ================= lantern safe radius =================
    static readonly List<Obj> lanternRings = new List<Obj>(); static Material ringMat;
    public static void buildLanternRings()
    {
        ringMat = basicMat(C(0xffb468), true, glowTex, true, 0f, true, false, true);
        foreach (var L in LANTERNS)
        {
            var mb = new MB(); const int SEG = 96; float r0 = SAFE_R - 0.6f, r1 = SAFE_R + 0.35f;
            for (int i = 0; i < SEG; i++)
            {
                float a0 = i / (float)SEG * TAU, a1 = (i + 1) / (float)SEG * TAU;
                Func<float, float, float[]> P = (a, r) => { float x = L.x + Mathf.Cos(a) * r, z = L.z + Mathf.Sin(a) * r; return v3(x, groundH(x, z) + 0.1f, z); };
                mb.quad(P(a0, r1), P(a1, r1), P(a1, r0), P(a0, r0), 0.5f, 0f, 0.5f, 1f);   // glow profile across the band: soft edges
            }
            var o = mkMesh(mb.geo(), ringMat, false, false, "safeRing"); o.visible = false; lanternRings.Add(o);
        }
    }

    // ================= flashlight glow in the fog (soft halo in front of the lens) =================
    static GameObject beamGo; static Material beamMat;
    public static void buildBeam()
    {
        beamMat = glowMat(C(0xfff0d8), 0f, false);
        var o = mkSprite(beamMat, "torchGlow"); o.tr.SetParent(camera.tr, false);
        o.tr.localPosition = new Vector3(0.05f, -0.07f, 1.8f); o.tr.localScale = Vector3.one * 3.4f; beamGo = o.go; beamGo.SetActive(false);
    }

    // ================= per-frame extras (called from frame while playing) =================
    public static void fixTick(float dt)
    {
        // save whenever the player steps into a lantern's light
        if (player.safe && !wasSafe && !AutoTest.active) saveNow();
        wasSafe = player.safe;

        if (player.exhausted && !F("hintExh")) { setF("hintExh"); showMsg("跑不動了。等體力回到三成，就能再跑。", 4); }
        if (player.sanity < 75 && !player.safe && !F("hintSan"))
        {
            setF("hintSan");
            showMsg("心神在往下掉。黑暗、靠近巨佛、被追都會讓它下降；越低越容易看見不存在的東西，歸零時盯著祂們也沒用。回燈籠旁可以回復。", 6.5f, "warn");
        }
        if (!F("hintPick") && player.inside != null)
        {
            for (int i = 0; i < PICKUPS.Count; i++)
            {
                var p = PICKUPS[i]; if (p.taken || p.hall != player.inside) continue;
                if (hypot(p.x - player.x, p.z - player.z) < 7) { setF("hintPick"); showMsg("供桌上有東西在發亮。走過去就會撿起來。", 4); break; }
            }
        }

        // safe rings: visible around lit lanterns, gently breathing
        if (ringMat != null)
        {
            setOpacity(ringMat, (0.28f + 0.08f * Mathf.Sin(uTime * 1.7f)) * (0.5f + 0.5f * night));
            for (int i = 0; i < lanternRings.Count && i < LANTERNS.Count; i++)
            {
                var L = LANTERNS[i]; lanternRings[i].visible = L.lit && hypot(L.x - player.x, L.z - player.z) < 70;
            }
        }
    }

    /// <summary>Beam opacity follows the real flashlight intensity and the fog thickness. Called every frame from frame().</summary>
    public static void beamTick()
    {
        if (beamMat == null) return;
        float k = flashOn() && (state == "playing" || state == "dying") ? Mathf.Clamp01(torch.intensity / 2.6f) : 0f;
        float a = k * Mathf.Clamp(0.035f + fogDensity * 1.6f, 0.05f, 0.13f) * (1 - 0.5f * indoorF) * (0.3f + 0.7f * night);
        setOpacity(beamMat, a); beamGo.SetActive(a > 0.001f);
    }
}
