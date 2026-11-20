// Game: state, reset, messages, flow, environment, player, chasers, statues/ghost/pickups/gate, events, dying, main loop.
// Port of index.html lines 1567-1604, 1610-1611, 1639-1846 (minus HUD), 1901-1921. Everything in three.js coordinates.
using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class G
{
    // ================= game state =================
    public static Player player = new Player();
    public class DyingState { public float t; public Chaser c; public bool shown; }
    public static string state = "loading";
    public static float hour = 6.3f, day = 1, night = 0;
    public static bool finalEvent = false, wasNight = false;
    public static float shake = 0, stingerCD = 0, bellT = 4, knockT = 30, whisperT = 12, heartT = 0, bfsT = 0, hudT = 0;
    public static DyingState dying = null;
    public static float indoorF = 0, fogRange = 80, wet = 0, gateMsgT = 0, wellT = 6, fishT = 5;
    public static string lastZone = "forest"; public static Interior lastInside = null; public static string prevState = "playing";
    public static float titleT = 0;

    public static readonly HashSet<string> flags = new HashSet<string>();
    public static bool F(string k) { return flags.Contains(k); }
    public static void setF(string k) { flags.Add(k); }
    public static void clrF(string k) { flags.Remove(k); }

    public static readonly Dictionary<string, WXDef> WX = new Dictionary<string, WXDef>
    {
        { "mist", new WXDef { rain = 0, fog = 0.03f, label = "薄霧" } }, { "thick", new WXDef { rain = 0, fog = 0.046f, label = "濃霧" } },
        { "drizzle", new WXDef { rain = 0.38f, fog = 0.028f, label = "細雨" } }, { "storm", new WXDef { rain = 1, fog = 0.025f, label = "雷雨" } },
        { "still", new WXDef { rain = 0, fog = 0.021f, label = "霧散" } }
    };
    public static readonly Dictionary<string, string[]> WX_NEXT = new Dictionary<string, string[]>
    {
        { "mist", new[] { "thick", "drizzle", "still" } }, { "thick", new[] { "mist", "drizzle" } }, { "drizzle", new[] { "storm", "mist", "thick" } },
        { "storm", new[] { "drizzle", "mist" } }, { "still", new[] { "mist", "drizzle", "thick" } }
    };
    public static Weather weather = new Weather { name = "mist", rain = 0, fog = 0.03f, timer = 50 };
    public static Lightning lightning = new Lightning { t = 9, flash = 0, next = 8, thunderIn = -1, dist = 0 };
    public static readonly Color FOG_DAY = C("#8f9892"), FOG_NIGHT = C("#070a0f"), FOG_DUSK = C("#6e4b43"), FOG_FLASH = C("#bcc6d8"), FOG_FINAL = C("#2a0c0a"), FOG_IN = C("#0b0c0c");
    public static readonly Color SKY_DAY = new Color(0.80f, 0.86f, 0.90f), SKY_NIGHT = new Color(0.10f, 0.14f, 0.22f), GND_DAY = new Color(0.46f, 0.44f, 0.38f), GND_NIGHT = new Color(0.03f, 0.035f, 0.04f);
    public static readonly Color EYE_OFF = C(0x0a0706), EYE_ON = C(0xff2408);
    static Color envLerp(Color a, Color b, float t) { return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f); }
    static Color envMul(Color a, float k) { return new Color(a.r * k, a.g * k, a.b * k, 1f); }

    public static bool flashOn() { return player.flash && player.battery > 0; }
    public static Interior interiorAt(float x, float z)
    {
        for (int i = 0; i < INTERIORS.Count; i++) { var h = INTERIORS[i]; if (x > h.minx && x < h.maxx && z > h.minz && z < h.maxz) return h; }
        return null;
    }

    // ---------- laterTimers (setTimeout) ----------
    class LaterTimer { public float t; public Action a; }
    static readonly List<LaterTimer> laterTimers = new List<LaterTimer>();
    /// <summary>setTimeout(a, sec*1000): runs while state=="playing"; cleared by resetGame.</summary>
    public static void later(float sec, Action a) { laterTimers.Add(new LaterTimer { t = sec, a = a }); }
    static void updateLater(float dt)
    {
        for (int i = laterTimers.Count - 1; i >= 0; i--)
        {
            var tm = laterTimers[i]; tm.t -= dt;
            if (tm.t <= 0) { laterTimers.RemoveAt(i); try { tm.a(); } catch (Exception e) { Debug.LogException(e); } }
        }
    }

    public static void resetGame()
    {
        player.x = START.x; player.z = START.y; player.yaw = 0; player.pitch = -0.03f; player.vx = 0; player.vz = 0; player.stamina = 100; player.battery = 100; player.sanity = 100;
        player.flash = false; player.safe = true; player.bob = 0; player.bobAmp = 0; player.stepAcc = 0; player.exhausted = false; player.deaths = 0; player.nights = 0; player.elapsed = 0;
        player.zone = "forest"; player.inside = null; player.danger = 0; player.keys = 0; player.charms = 0; player.eye = EYE; player.grace = 0;
        player.y = groundH(player.x, player.z); player.lastLantern = LANTERNS.Count > 0 ? LANTERNS[0] : null;
        hour = 6.3f; finalEvent = false; wasNight = false; flags.Clear(); lastZone = "forest"; lastInside = null; wet = 0; laterTimers.Clear();
        for (int i = 0; i < LANTERNS.Count; i++) setLantern(LANTERNS[i], i == 0);
        foreach (var s in STELES) s.read = false;
        foreach (var s in STATUES)
        {
            s.msgDone = false; if (!s.hot0Set) { s.hot0 = s.hot; s.hot0Set = true; } else setHot(s, s.hot0);
            s.g.rotY = s.yaw; s.curYaw = s.yaw;
        }
        foreach (var c in CHASERS)
        {
            if (!c.dormant0Set) { c.dormant0 = c.dormant; c.dormant0Set = true; }
            c.dormant = c.dormant0; c.x = c.hx; c.z = c.hz; c.g.setPos(c.x, c.g.py, c.z); if (c.hall == null) c.g.posY = groundH(c.x, c.z);
            c.g.rotY = c.hyaw; c.moved = 0; setHot(c, false);
        }
        foreach (var p in PICKUPS) { p.taken = false; p.g.visible = true; }
        foreach (var H in HALLS) foreach (var L in H.lamps) setLamp(L, true);
        if (DAIBUTSU != null && DAIBUTSU.tears != null) DAIBUTSU.tears.visible = false;
        if (FOURF != null) { FOURF.spin = 0; FOURF.g.rotY = FOURF.yaw; }
        if (GATEOBJ != null)
        {
            GATEOBJ.open = 0; GATEOBJ.opening = false; GATEOBJ.col.off = false;
            for (int d = 0; d < GATEOBJ.doors.Length; d++) { GATEOBJ.doors[d].rotY = 0; for (int k = 0; k < 3; k++) GATEOBJ.locks[d, k].visible = true; }
        }
        weather.name = "mist"; weather.rain = 0; weather.fog = 0.03f; weather.timer = 50;
        if (ghost != null) { ghost.root.visible = false; ghost.active = false; ghost.cool = 25; }
        bellT = 4; knockT = 40; whisperT = 15; msgQ.Clear(); updateInv(); resetHalluc();
    }

    // ================= messages =================
    public class Msg { public string t; public float dur; public string cls; }
    public static readonly List<Msg> msgQ = new List<Msg>();
    public static Msg msgCur = null; public static float msgT = 0, msgGap = 0;
    /// <summary>What the UI shows: text, css-like class ("", "warn", "voice") and whether the line is visible.</summary>
    public static string msgText = ""; public static string msgCls = ""; public static bool msgOn = false;
    public static void showMsg(string t, float dur = 4.2f, string cls = "")
    {
        if (restoring) return;
        var m = new Msg { t = t, dur = dur > 0 ? dur : 4.2f, cls = cls ?? "" };
        if (cls == "voice")
        {
            int i = 0; while (i < msgQ.Count && msgQ[i].cls == "voice") i++; msgQ.Insert(i, m);
            if (msgCur != null && msgCur.cls != "voice") { msgT = min(msgT, 0.05f); }
            msgGap = min(msgGap, 0.15f);
        }
        else msgQ.Add(m);
        if (msgQ.Count > 5) msgQ.RemoveAt(msgQ.Count - 1);
    }
    public static void updateMsg(float dt)
    {
        if (msgCur != null) { msgT -= dt; if (msgT <= 0) { msgOn = false; msgCur = null; msgGap = 0.6f; } }
        else if (msgGap > 0) msgGap -= dt;
        else if (msgQ.Count > 0) { msgCur = msgQ[0]; msgQ.RemoveAt(0); msgText = msgCur.t; msgCls = msgCur.cls; msgOn = true; msgT = msgCur.dur; }
    }
    /// <summary>JS wrote the key/charm counters into the DOM here; the UI reads player.keys / player.charms directly.</summary>
    public static void updateInv() { }

    // ================= input helpers =================
    public static void lookBy(float dx, float dy, float sens) { player.yaw -= dx * sens; player.pitch = clamp(player.pitch - dy * sens, -1.35f, 1.35f); }
    public static void toggleFlash()
    {
        if (player.battery <= 0 && !player.flash) { showMsg("手電筒沒電了。去找燈籠。", 3, "warn"); return; }
        player.flash = !player.flash;
    }

    // ================= flow =================
    public static void pause()
    {
        if (state != "playing") return; state = "paused"; UI.show("pauseOv", true); A.suspend(); UI.lockCursor(false); UI.releaseKeys();
    }
    public static void resume() { state = "playing"; UI.show("pauseOv", false); A.resume(); UI.lockCursor(true); }
    public static void startGame()
    {
        A.init(); A.resume(); A.loadClips(); A.stopAll();
        {
            string v = (UI.nameValue ?? "").Trim(); if (v.Length > 8) v = v.Substring(0, 8);
            PNAME = v.Length > 0 ? v : "玄燁"; try { PlayerPrefs.SetString("qf_name", PNAME); PlayerPrefs.Save(); } catch (Exception) { }
        }
        bool cont = continueRun && Save.hasSave && Save.data != null && Save.data.seed == SEED; continueRun = false;
        if (cont) applySave(Save.data); else resetGame();
        wasSafe = false;
        UI.show("title", false); UI.show("hud", true); if (UI.usingTouch) { UI.show("touch", true); }
        state = "playing"; UI.lockCursor(true);
        if (cont) showMsg("燈籠還在燒。你在上次點亮的燈籠旁醒來。（銅鑰 " + player.keys + " / 3）", 4.5f);
        else
        {
            showMsg("你在潮濕的落葉上醒來。四周全是霧。", 4.5f); showMsg("山門在北邊，被三道銅鎖鎖著。三把鑰匙藏在山裡三座殿裡——地圖上標紅的那三座。", 6f);
            showMsg("身邊的石燈籠還亮著。地上那圈光以內，祂們進不來。", 5);
        }
    }
    public static void die(Chaser c)
    {
        if (state != "playing") return; noteDeath(c); visStop(); state = "dying"; player.deaths++; dying = new DyingState { t = 0, c = c, shown = false }; A.stopAll(); A.scream(); msgQ.Clear(); msgCur = null; msgOn = false; msgCls = "";
        if (c.eyes != null)
        {
            foreach (var m in c.eyes) { m.mat = eyeMatBlack; m.mulScale(1.9f); }
            if (c.sprs != null) foreach (var s in c.sprs) { s.mat = eyeSprHot; s.mulScale(0.42f); }
        }
        UI.lockCursor(false); UI.releaseKeys();
    }
    public static void sendHome(Chaser c) { c.x = c.hx; c.z = c.hz; c.g.setPos(c.x, c.hall != null ? c.g.py : groundH(c.x, c.z), c.z); c.moved = 0; }
    public static void respawn()
    {
        var c = dying != null ? dying.c : null;
        if (c != null)
        {
            if (c.eyes != null) { foreach (var m in c.eyes) m.mulScale(1 / 1.9f); if (c.sprs != null) foreach (var s in c.sprs) s.mulScale(1 / 0.42f); }
            setHot(c, c.hall != null && !c.dormant); dying = null;
        }
        var L = player.lastLantern ?? LANTERNS[0]; float px = L.x, pz = L.z + 2.4f; resolve(ref px, ref pz, 0.5f); resolve(ref px, ref pz, 0.5f);
        player.x = px; player.z = pz; player.yaw = 0; player.pitch = -0.03f; player.vx = 0; player.vz = 0; player.stamina = 100; player.sanity = max(player.sanity, 60); player.safe = true; player.inside = null;
        player.y = groundH(px, pz);
        foreach (var j in CHASERS) { if (hypot(j.x - px, j.z - pz) < 45 || j.hall != null) sendHome(j); }
        if (PHANTOM != null) { phantomHide(); PHANTOM.cool = RR(40, 70); }
        HL.trap = null; visStop(); VIS.cool = 30;
        player.grace = 2.5f; A.stopAll();
        UI.setFx("redfx", 0); UI.setFx("blackfx", 0); UI.show("dead", false); state = "playing"; A.resume(); UI.lockCursor(true); showMsg("燈籠還在燒。你又醒了過來。", 3.5f);
    }
    public static void win()
    {
        state = "won"; UI.lockCursor(false); Save.clear(); msgQ.Clear(); msgCur = null; msgOn = false; int t = floor(player.elapsed);
        UI.setWinStats((t / 60) + ":" + (t % 60).ToString("00"), player.nights, player.deaths);
        UI.show("hud", false); UI.show("touch", false); UI.show("win", true); A.bell(0, 0.9f);
    }
    public static void useCharm()
    {
        player.grace = 3; player.charms--; updateInv(); A.bell(0, 0.9f); A.chime(); shake = 0.5f; lightning.t = 0;
        foreach (var j in CHASERS) { if (!j.dormant && hypot(j.x - player.x, j.z - player.z) < 45) sendHome(j); }
        showMsg("護身符燒成了灰。祂們退回去了——三秒內快離開。", 4.5f, "warn");
    }

    // ================= environment =================
    public static void updateEnv(float dt, bool advance)
    {
        if (advance) { hour += dt / HOUR_SEC; if (hour >= 24) hour -= 24; }
        uTime += dt;
        float sunE = sin((hour - 6) / 12 * PI); day = smooth(-0.12f, 0.22f, sunE); night = 1 - day; float dusk = max(0, 1 - abs(sunE) / 0.3f) * (hour > 12 ? 1 : 0.55f);
        weather.timer -= dt;
        if (weather.timer <= 0 && advance)
        {
            weather.name = pickR(WX_NEXT[weather.name]); weather.timer = RR(40, 75);
            if (weather.name == "storm") showMsg("雨下大了。雷聲在山谷裡滾。", 3.5f); if (weather.name == "thick") showMsg("霧變濃了。", 2.5f);
        }
        var Wd = WX[weather.name]; weather.rain = lerp(weather.rain, Wd.rain, 1 - exp(-dt / 6)); weather.fog = lerp(weather.fog, Wd.fog, 1 - exp(-dt / 8));
        wet = clamp(wet + (weather.rain > 0.15f ? dt * 0.06f * weather.rain : -dt * 0.012f), 0, 1); uWet = wet;
        lightning.t += dt;
        if (weather.rain > 0.65f && advance) { lightning.next -= dt; if (lightning.next <= 0) { lightning.next = RR(6, 16); lightning.t = 0; lightning.thunderIn = RR(0.4f, 2.2f); lightning.dist = lightning.thunderIn; } }
        if (lightning.thunderIn > 0) { lightning.thunderIn -= dt; if (lightning.thunderIn <= 0) A.thunder(clamp(1 - lightning.dist / 3, 0.35f, 1) * (player.inside != null ? 0.6f : 1)); }
        float lt = lightning.t; lightning.flash = lt < 0.07f ? 1 : lt < 0.14f ? 0.08f : lt < 0.26f ? 0.85f : max(0, 0.85f - (lt - 0.26f) * 3);
        indoorF = lerp(indoorF, player.inside != null ? 1 : 0, 1 - exp(-dt * 3));
        float dens = weather.fog * (1 + 0.3f * night) * (1 - 0.35f * lightning.flash) * (finalEvent ? 1.08f : 1) * lerp(1, 0.5f, indoorF);
        fogDensity = dens; fogRange = clamp(2.35f / dens, 48, 150);
        Color fc = envLerp(envLerp(FOG_DAY, FOG_NIGHT, night), FOG_DUSK, dusk * 0.5f); fc = envMul(fc, 1 - 0.22f * weather.rain * day);
        if (finalEvent) fc = envLerp(fc, FOG_FINAL, 0.35f); fc = envLerp(fc, FOG_FLASH, lightning.flash * 0.8f); fc = envLerp(fc, FOG_IN, indoorF * 0.55f); fogC = fc;
        hemi.color = envLerp(SKY_DAY, SKY_NIGHT, night); hemi.groundColor = envLerp(GND_DAY, GND_NIGHT, night);
        hemi.intensity = (0.13f + 1.7f * day * (1 - 0.3f * weather.rain)) * (finalEvent ? 0.85f : 1) + lightning.flash * 2.6f;
        float ng = smooth(0.3f, 0.85f, night);
        if (eyeMatNight != null) setColor(eyeMatNight, envLerp(EYE_OFF, EYE_ON, ng)); if (eyeSprNight != null) setOpacity(eyeSprNight, ng * 0.8f);
        if (finalEvent) { if (eyeMatNight != null) setColor(eyeMatNight, EYE_ON); if (eyeSprNight != null) setOpacity(eyeSprNight, 0.85f); }
        // rain (the drops are advanced by rain.step(dt) at the end of frame(), after the visions had their say)
        if (rain != null)
        {
            float rshow = 1 - indoorF; int n = floor(rain.N * clamp(weather.rain, 0, 1)); rain.count = n;
            rain.opacity = (0.34f * weather.rain * (0.6f + 0.4f * day) + lightning.flash * 0.3f) * rshow; rain.visible = rshow > 0.05f && n > 0;
        }
        if (moon != null)
        {
            moon.opacity = smooth(0.55f, 0.95f, night) * 0.6f * (1 - weather.rain * 0.7f) * (1 - indoorF); moon.color = C(player.nights >= 1 ? 0xd8584a : 0xdfe6f0);
            moon.setPos(camera.px - 55, camera.py + 85, camera.pz - 110);
        }
        // lamps: nearest two lit sources get real lights
        Lamp a = null, b = null; float da = 1e9f, db = 1e9f; float cx = camera.px, cz = camera.pz;
        for (int i = 0; i < LAMPS.Count; i++)
        {
            var L = LAMPS[i]; if (!L.lit) continue; float d = (L.x - cx) * (L.x - cx) + (L.z - cz) * (L.z - cz);
            if (d < da) { b = a; db = da; a = L; da = d; } else if (d < db) { b = L; db = d; }
            L.fl += dt * 12; if (L.spMat != null) setOpacity(L.spMat, (L.indoor ? 0.75f : 0.5f + 0.3f * night) * (0.9f + sin(L.fl) * 0.06f + sin(L.fl * 2.7f) * 0.05f));
        }
        envLampLight(lampA, a, da); envLampLight(lampB, b, db);
        if (exitGlowMat != null && GATEOBJ != null) { float gdist = hypot(cx - GATE.x, cz - GATE.y); setOpacity(exitGlowMat, GATEOBJ.open * clamp(1 - gdist / 70, 0, 1) * 0.9f); }
    }
    static void envLampLight(PLight lamp, Lamp L, float d2)
    {
        if (L != null && d2 < 60 * 60)
        {
            lamp.setPos(L.x, L.y + 0.05f, L.z); lamp.color = L.color; lamp.distance = L.range;
            lamp.intensity = L.power * (L.indoor ? 1.5f : 0.5f + 1.25f * night) * (0.9f + sin(L.fl * 1.3f) * 0.08f);
        }
        else lamp.intensity = 0;
    }

    // ================= player =================
    public static void updatePlayer(float dt)
    {
        float fx = 0, sx = 0;
        if (UI.key("KeyW") || UI.key("ArrowUp")) fx += 1; if (UI.key("KeyS") || UI.key("ArrowDown")) fx -= 1;
        if (UI.key("KeyD") || UI.key("ArrowRight")) sx += 1; if (UI.key("KeyA") || UI.key("ArrowLeft")) sx -= 1;
        fx += UI.joy.y; sx += UI.joy.x;
        float len = hypot(fx, sx); if (len > 1) { fx /= len; sx /= len; }
        bool crouch = player.eye < 1.3f;
        bool wantRun = (UI.key("ShiftLeft") || UI.key("ShiftRight") || UI.touchRun) && len > 0.2f && !player.exhausted && !crouch;
        float spd = (wantRun ? 7.4f : 4.0f) * (player.sanity < 15 ? 0.9f : 1) * (crouch ? 0.5f : 1); float sn = sin(player.yaw), cs = cos(player.yaw);
        float tx = (-sn * fx + cs * sx) * spd, tz = (-cs * fx - sn * sx) * spd; float k = 1 - exp(-10 * dt); player.vx = lerp(player.vx, tx, k); player.vz = lerp(player.vz, tz, k);
        float px = player.x + player.vx * dt, pz = player.z + player.vz * dt; resolve(ref px, ref pz, 0.42f); resolve(ref px, ref pz, 0.42f);
        px = clamp(px, -XB + 0.6f, XB - 0.6f); pz = clamp(pz, ZN + 0.6f, ZS - 0.6f);
        float moved = hypot(px - player.x, pz - player.z); player.x = px; player.z = pz;
        if (wantRun && moved > 0.01f) { player.stamina -= 19 * dt; if (player.stamina <= 0) { player.stamina = 0; player.exhausted = true; } }
        else { player.stamina = min(100, player.stamina + (player.exhausted ? 9 : 14) * dt); if (player.stamina > 30) player.exhausted = false; }
        player.inside = interiorAt(player.x, player.z);
        player.bob += moved * (wantRun ? 1.75f : 2.2f); player.bobAmp = lerp(player.bobAmp, len > 0.15f ? 1 : 0, 1 - exp(-8 * dt));
        player.stepAcc += moved;
        if (player.stepAcc > (wantRun ? 2.1f : 1.6f)) { player.stepAcc = 0; A.step(player.inside != null && player.inside != KOFUN ? 2 : (wet > 0.3f ? 1 : 0), wantRun); }
        float gy = groundH(player.x, player.z); player.y = lerp(player.y, gy, 1 - exp(-14 * dt));
        float eyeT = EYE;
        if (KOFUN != null)
        {
            if (player.inside == KOFUN) eyeT = min(EYE, KOFUN.ceilAt(player.x) - 0.32f);
            else if (abs(player.z - KOFUN.z) < 3 && player.x > KOFUN.entX - 1.2f && player.x <= KOFUN.minx) eyeT = min(EYE, KOFUN.ceilAt(KOFUN.minx) - 0.32f);
        }
        player.eye = lerp(player.eye, eyeT, 1 - exp(-9 * dt));
        float sk = shake > 0 ? shake : 0; shake = max(0, shake - dt * 1.6f);
        camera.setPos(player.x, player.y + player.eye + sin(player.bob) * 0.05f * player.bobAmp, player.z);
        camera.setRot(player.pitch + (rnd() - 0.5f) * sk * 0.05f, player.yaw + (rnd() - 0.5f) * sk * 0.05f, sin(player.bob * 0.5f) * 0.006f * player.bobAmp);
        if (flashOn())
        {
            player.battery = max(0, player.battery - 0.42f * dt);
            if (player.battery <= 0) { player.flash = false; showMsg("手電筒沒電了。", 3, "warn"); }
            else if (player.battery < 20 && !F("lowBat")) { setF("lowBat"); showMsg("手電筒快沒電了。找燈籠。", 3.5f, "warn"); }
        }
        if (player.battery > 40) clrF("lowBat");
    }

    // ================= chasers =================
    public static bool inView(float x, float y, float z, float m) { Vector3 v = camera.project(x, y, z); return v.z < 1 && v.z > -1 && abs(v.x) < m && abs(v.y) < 1.05f; }
    public static bool inTorch(float x, float y, float z, float range)
    {
        if (!flashOn() || torch.intensity < 1f) return false; Vector3 fw = camera.dir(); float dx = x - camera.px, dy = y - camera.py, dz = z - camera.pz, L = hypot(dx, dy, dz);
        return L < range && (dx * fw.x + dy * fw.y + dz * fw.z) / L > 0.885f;
    }
    public static bool isSeen(Chaser c, float dist)
    {
        float y0 = c.g.py; if (!inView(c.x, y0 + c.headY, c.z, 0.97f) && !inView(c.x, y0 + c.h * 0.45f, c.z, 0.97f)) return false;
        bool lit = false;
        if (c.hall == null) { float fogVis = 1.6f / max(0.01f, fogDensity); if (day > 0.35f && dist < fogVis * min(1, day * 1.6f)) lit = true; if (!lit && lightning.flash > 0.3f && dist < 55) lit = true; }
        if (!lit && dist < 4) lit = true;
        if (!lit && inTorch(c.x, y0 + c.h * 0.6f, c.z, 33)) lit = true;
        if (!lit) { for (int i = 0; i < LAMPS.Count; i++) { var L = LAMPS[i]; if (L.lit && (c.x - L.x) * (c.x - L.x) + (c.z - L.z) * (c.z - L.z) < (L.indoor ? 49 : 169)) { lit = true; break; } } }
        if (!lit) return false; return !blocked(camera.px, camera.pz, c.x, c.z);
    }
    public static void updateChasers(float dt)
    {
        if (player.grace > 0) player.grace -= dt;
        float base_ = lerp(1.5f, 3.2f, night) * (finalEvent ? 1.1f : 1); bool insane = player.sanity <= 0.5f; float nearest = 1e9f, gd = 1e9f, gpan = 0; bool flick = false;
        float rx = cos(player.yaw), rz = -sin(player.yaw);
        bfsT -= dt;
        if (bfsT <= 0) { bfsT = 0.35f; foreach (var mm in new[] { m1, m2 }) { if (mm == null) continue; mm.cellOf(player.x, player.z, out int pc0, out int pr0); if (mm.inside(pc0, pr0)) mm.bfs(pc0, pr0); } }
        for (int ji = 0; ji < CHASERS.Count; ji++)
        {
            var j = CHASERS[ji]; if (j.dormant) continue; float dx = player.x - j.x, dz = player.z - j.z, dist = hypot(dx, dz);
            bool active = j.zone == player.zone && dist < (j.hall != null ? 60 : 38 + 34 * night + (finalEvent ? 30 : 0));
            j.seen = dist < 85 ? isSeen(j, dist) : false;
            if (j.seen)
            {
                if (j.moved > 3 && dist < 15 && stingerCD <= 0) { A.stinger(); stingerCD = 6; player.sanity -= 7; shake = 0.7f; if (!F("sting")) { setF("sting"); showMsg("……那尊石僧，剛剛不在那裡。", 4, "warn"); } }
                j.moved = 0;
            }
            float move = 0;
            if (active && !(VIS.hold > 0))
            {
                if (!j.seen) move = (j.hall != null ? lerp(2.3f, 2.9f, night) : base_) * (j.zone == "final" && finalEvent ? 1.15f : 1) * (j.kind == "ascetic" ? 1.15f : 1);
                else if (insane) move = 1.1f; else if (finalEvent && j.zone == "final") move = 1.25f;
            }
            if (move > 0)
            {
                float tx = player.x, tz = player.z;
                if (j.maze != null)
                {
                    var m = j.maze; m.cellOf(j.x, j.z, out int c, out int r); m.cellOf(player.x, player.z, out int pc, out int pr);
                    if (m.inside(c, r) && !(c == pc && r == pr))
                    {
                        int best = -1, bd = m.dist[m.idx(c, r)]; if (bd < 0) bd = 1000000000;
                        for (int d = 0; d < 4; d++) { if (!m.open(c, r, d)) continue; int v = m.dist[m.idx(c + DC[d], r + DR[d])]; if (v >= 0 && v < bd) { bd = v; best = d; } }
                        if (best >= 0) { float[] cc = m.center(c, r), nc = m.center(c + DC[best], r + DR[best]); tx = DC[best] == 0 ? cc[0] : nc[0]; tz = DR[best] == 0 ? cc[1] : nc[1]; }
                        else { tx = j.x; tz = j.z; }
                    }
                }
                float mx = tx - j.x, mz = tz - j.z; float ml = hypot(mx, mz);
                if (ml > 0.05f)
                {
                    mx /= ml; mz /= ml;
                    if (j.maze == null)
                    {
                        if (j.stuck > 0.8f) { j.side = rnd() < 0.5f ? -1 : 1; j.sideT = 1.5f; j.stuck = 0; }
                        if (j.sideT > 0) { j.sideT -= dt; float sx = -mz * j.side, sz = mx * j.side; mx = mx * 0.3f + sx; mz = mz * 0.3f + sz; float l2 = hypot(mx, mz); mx /= l2; mz /= l2; }
                    }
                    float step = min(move * dt, ml); float qx = j.x + mx * step, qz = j.z + mz * step; resolve(ref qx, ref qz, j.r);
                    if (j.hall == null) for (int li = 0; li < LANTERNS.Count; li++)
                        {
                            var L = LANTERNS[li]; if (!L.lit) continue; float lx = qx - L.x, lz = qz - L.z, ld = hypot(lx, lz);
                            if (ld < SAFE_R + 0.3f) { float ldd = ld == 0 ? 1 : ld; qx = L.x + lx / ldd * (SAFE_R + 0.3f); qz = L.z + lz / ldd * (SAFE_R + 0.3f); }
                        }
                    if (j.hall != null) { var h = j.hall; qx = clamp(qx, h.minx + 0.45f, h.maxx - 0.45f); qz = clamp(qz, h.minz + 0.45f, h.maxz - 0.45f); }
                    else if (j.maze != null) { var m = j.maze; qx = clamp(qx, m.x0 + 0.8f, m.x0 + m.cols * m.cell - 0.8f); qz = clamp(qz, m.zN + 0.8f, m.zS - 0.8f); }
                    else { var zc = ZR[j.zone]; qz = clamp(qz, zc[0], zc[1]); qx = clamp(qx, -XB + 3, XB - 3); if (interiorAt(qx, qz) != null) { qx = j.x; qz = j.z; } }
                    float act = hypot(qx - j.x, qz - j.z); if (act < step * 0.3f) j.stuck += dt; else j.stuck = max(0, j.stuck - dt);
                    j.x = qx; j.z = qz; j.moved += act; j.g.setPos(j.x, j.hall != null ? j.g.py : groundH(j.x, j.z), j.z);
                    if (act > 0.001f) { if (!j.seen && dist < gd) { gd = dist; float dd = dist == 0 ? 1 : dist; gpan = (-dx / dd) * rx + (-dz / dd) * rz; } if (dist < 9) flick = true; }
                }
            }
            if (!j.seen && active) j.g.rotY = atan2(dx, dz) + j.faceYaw;
            if (active && dist < nearest) nearest = dist;
            if (dist < 1.12f && !player.safe && active && !(player.grace > 0)) { if (player.charms > 0) { useCharm(); return; } die(j); return; }
        }
        for (int a = 0; a < CHASERS.Count; a++)
        {
            var A1 = CHASERS[a]; if (A1.dormant) continue;
            for (int b = a + 1; b < CHASERS.Count; b++)
            {
                var B1 = CHASERS[b]; if (B1.dormant || B1.zone != A1.zone) continue; float ex = B1.x - A1.x, ez = B1.z - A1.z, ed = hypot(ex, ez);
                if (ed < 0.9f && ed > 1e-4f) { float push = (0.9f - ed) / 2; A1.x -= ex / ed * push; A1.z -= ez / ed * push; B1.x += ex / ed * push; B1.z += ez / ed * push; }
            }
        }
        player.danger = clamp(1 - (nearest - 2) / 14, 0, 1);
        if (A.ok) { A.set(A.grindG, gd < 24 ? clamp(1 - gd / 24, 0, 1) * 0.42f : 0, 0.12f); A.set(A.grindPan, clamp(gpan, -1, 1), 0.1f); }
        float want = flashOn() ? (flick && rnd() < 0.3f ? 0.3f : 2.6f) * (player.battery < 12 ? (rnd() < 0.15f ? 0.2f : 1) : 1) : 0; torch.intensity = want;
    }

    // ================= statues, ghost, pickups, gate =================
    public static void updateStatues(float dt)
    {
        float ng = 1e9f;
        for (int i = 0; i < STATUES.Count; i++)
        {
            var s = STATUES[i]; float dx = player.x - s.x, dz = player.z - s.z, d = hypot(dx, dz); if (s.giant && d < ng) ng = d;
            if (s.msg != null && !s.msgDone && d < s.msgR) { s.msgDone = true; showMsg(s.msg, 4.5f); }
            if (s.track && d < 95 && s.g.visible)
            {
                bool seen = inView(s.x, s.y + s.height * 0.6f, s.z, 1.05f) && d < fogRange;
                if (!seen || finalEvent) { float tgt = atan2(dx, dz) + s.faceYaw; float diff = wrapA(tgt - s.g.rotY); float sp = s.turn * (finalEvent ? 1.6f : 1); s.g.rotY = s.g.rotY + clamp(diff, -sp * dt, sp * dt); }
            }
        }
        player.nearGiant = ng;
        if (FOURF != null && player.inside != null && player.inside.id == "t6")
        {
            FOURF.cool -= dt; bool seen = inView(FOURF.x, FOURF.y + FOURF.height * 0.5f, FOURF.z, 1.0f);
            if (!seen && FOURF.cool <= 0)
            {
                FOURF.g.rotY = FOURF.g.rotY + PI / 2; FOURF.cool = 1.2f; FOURF.spin++; A.knock(0, 0.25f, 0, true);
                if (FOURF.spin == 2 && !F("ff")) { setF("ff"); showMsg("祭壇上那顆頭有四張臉。你每次回頭，朝著你的都是另一張。", 5, "warn"); }
            }
        }
    }
    public static void updateGhost(float dt)
    {
        if (ghost == null) return; float fwx = -sin(player.yaw), fwz = -cos(player.yaw);
        if (!ghost.active)
        {
            if (player.sanity < 35 && !player.safe && player.inside == null) ghost.cool -= dt;
            if (ghost.cool <= 0)
            {
                int side = rnd() < 0.5f ? -1 : 1; float a = player.yaw + side * RR(0.55f, 0.75f), d = RR(8, 14);
                float x = player.x - sin(a) * d, z = player.z - cos(a) * d; ghost.root.setPos(x, groundH(x, z), z); ghost.root.rotY = atan2(player.x - x, player.z - z) + 0.873f; ghost.root.visible = true; ghost.active = true; ghost.life = 7;
            }
        }
        else
        {
            ghost.life -= dt; float gx = ghost.root.px - player.x, gz = ghost.root.pz - player.z, gl = hypot(gx, gz); float dot = (gx * fwx + gz * fwz) / gl;
            if (dot > 0.96f || ghost.life <= 0) { ghost.root.visible = false; ghost.active = false; ghost.cool = RR(18, 36); if (dot > 0.96f) { A.whisper(0, 0.5f); player.sanity -= 3; } }
        }
    }
    public static void updatePickups(float dt)
    {
        float t = uTime;
        for (int i = 0; i < PICKUPS.Count; i++)
        {
            var p = PICKUPS[i]; if (p.taken) continue; p.g.rotY = t * 1.2f; p.g.posY = p.y + sin(t * 2 + p.x) * 0.05f;
            if (hypot(p.x - player.x, p.z - player.z) < 1.5f && abs(p.y - (player.y + 1)) < 2.2f)
            {
                p.taken = true; p.g.visible = false; A.chime();
                if (p.type == "key") { player.keys++; showMsg(player.keys >= 3 ? "第三把銅鑰。往北走到山門前，三道鎖會自己掉下來。" : "你拿到了一把銅鑰。（" + player.keys + " / 3）", 4.5f); }
                else { player.charms++; showMsg("一張護身符。被抓到的那一刻會自動燒掉，替你擋一次，給你三秒逃開。", 5f); }
                updateInv(); if (p.onTake != null) p.onTake();
            }
        }
    }
    public static void updateGate(float dt)
    {
        var Gt = GATEOBJ; if (Gt == null) return; float d = hypot(player.x - GATE.x, player.z - GATE.y); gateMsgT -= dt;
        for (int k = 0; k < 3; k++) { bool v = player.keys <= k; for (int dr = 0; dr < Gt.doors.Length; dr++) Gt.locks[dr, k].visible = v && !Gt.opening; }
        if (!Gt.opening && d < 9 && player.z > GATE.y)
        {
            if (player.keys >= 3) { Gt.opening = true; A.rumble(); A.bell(0, 1); shake = 0.6f; showMsg("三道銅鎖同時掉了下來。山門開了。", 4.5f); }
            else if (gateMsgT <= 0) { gateMsgT = 14; showMsg("山門被三道銅鎖鎖著。還差 " + (3 - player.keys) + " 把銅鑰——在寺廟裡。", 4.5f, "warn"); }
        }
        if (Gt.opening && Gt.open < 1) { Gt.open = min(1, Gt.open + dt / 3.5f); float a = smooth(0, 1, Gt.open) * 1.75f; Gt.doors[0].rotY = a; Gt.doors[1].rotY = -a; if (Gt.open > 0.45f) Gt.col.off = true; }
    }

    // ================= events =================
    public static readonly Dictionary<string, string> ZONE_MSG = new Dictionary<string, string>
    {
        { "maze1", "石壁迷宮。牆上嵌著鬼面瓦，每一張都在笑。" }, { "field", "千佛坡。霧裡坐著比樹還高的佛。" }, { "maze2", "竹林迷陣。風一吹，竹子就像有人在走。" }, { "final", "後山。山門就在最北邊。" }
    };
    public static readonly Dictionary<string, string> IN_MSG = new Dictionary<string, string>
    {
        { "t1", "地藏堂。成堆的石地藏，圍著紅色的圍兜。這裡很安靜。" }, { "t2", "本堂。兩側坐著石僧，供桌上有東西在發亮。" }, { "hut", "一間廢屋。佛壇還在，供品早就爛了。" },
        { "t3", "大佛殿。手電筒往上照——照不到祂的臉。" }, { "t4", "千手殿。牆上掛滿鬼面，眼洞裡有光。這裡的石僧，不用等天黑。" }, { "t6", "閻魔殿。牆上貼滿了符。祭壇上有一顆頭。" },
        { "kofun", "石室。只能彎著腰進去。最裡面站著什麼。" }
    };
    public static void updateEvents(float dt)
    {
        var ins = player.inside; player.zone = ins != null ? ins.id : zoneOfZ(player.z);
        if (ins == null) { string zz = zoneOfZ(player.z); if (zz != lastZone) { lastZone = zz; if (ZONE_MSG.ContainsKey(zz) && !F("z_" + zz)) { setF("z_" + zz); showMsg(ZONE_MSG[zz], 4.5f); } } }
        if (ins != lastInside) { lastInside = ins; if (ins != null && IN_MSG.ContainsKey(ins.id) && !F("in_" + ins.id)) { setF("in_" + ins.id); showMsg(IN_MSG[ins.id], 5); } }
        // lanterns
        Lantern nl = null; float nd = 1e9f;
        for (int i = 0; i < LANTERNS.Count; i++)
        {
            var L = LANTERNS[i]; float d = hypot(L.x - player.x, L.z - player.z);
            if (!L.lit && d < 3.4f) { setLantern(L, true); A.ignite(); player.lastLantern = L; showMsg(F("lit") ? "石燈籠亮了。" : "石燈籠亮了，已存檔。被抓會在這裡醒來，拿到的鑰匙不會掉；下次打開遊戲也從這裡繼續。", F("lit") ? 3 : 6); setF("lit"); }
            if (L.lit && d < nd) { nd = d; nl = L; }
        }
        player.safe = nl != null && nd < SAFE_R; if (player.safe) { player.battery = min(100, player.battery + 18 * dt); player.lastLantern = nl; }
        // sanity
        float ds = 0;
        if (player.safe) ds += 9;
        else
        {
            if (day > 0.6f && ins == null) ds += 0.7f; ds -= night * 0.5f; if ((night > 0.5f || ins != null) && !flashOn()) ds -= 0.7f; if (player.nearGiant < 26) ds -= 0.4f; ds -= player.danger * 1.4f; if (finalEvent) ds -= 0.6f;
            if (ins != null && ins.id != "t1") ds -= 0.25f; if (ins != null && ins.id == "t1") ds += 2.5f;
        }
        player.sanity = clamp(player.sanity + ds * dt, 0, 100);
        if (player.sanity < 30 && !F("lowSan")) { setF("lowSan"); showMsg("耳邊開始有人在念經。心神快撐不住了——找燈籠。", 4.5f, "warn"); }
        if (player.sanity > 55) clrF("lowSan");
        if (player.sanity <= 0.5f && !F("zeroSan")) { setF("zeroSan"); showMsg("你盯著祂們……祂們還是在動。", 4, "warn"); }
        if (player.sanity > 20) clrF("zeroSan");
        if (night > 0.5f && !wasNight) { wasNight = true; showMsg(UI.usingTouch ? "天黑了。點右下的「燈」打開手電筒。" : "天黑了。按 F 打開手電筒。", 4.5f, "warn"); }
        if (night < 0.5f && wasNight) { wasNight = false; player.nights++; showMsg("天亮了。你撐過了第 " + player.nights + " 夜。", 4); }
        if (hour > 16.8f && hour < 17 && !F("dusk")) { setF("dusk"); showMsg("天色開始暗了。", 3); }
        if (hour < 16) clrF("dusk");
        if (ins != null && ins.id != "t1" && !flashOn() && !F("dark")) { setF("dark"); showMsg(UI.usingTouch ? "裡面很暗。點「燈」打開手電筒。" : "裡面很暗。按 F 打開手電筒。", 4); }
        for (int i = 0; i < STELES.Count; i++) { var s = STELES[i]; if (!s.read && hypot(s.x - player.x, s.z - player.z) < 3.8f) { s.read = true; showMsg("碑上刻著：「" + s.text.Replace(" ", "，") + "」", 4.5f); } }
        if (!finalEvent && player.z < -263 && abs(player.x) < 12 && !player.safe)
        {
            finalEvent = true; A.bell(0, 1); A.rumble(); shake = 0.8f; showMsg("所有的佛，同時轉過頭來。", 3.5f, "warn"); showMsg(player.keys >= 3 ? "跑。" : "跑——但你的鑰匙還不夠。", 3, "warn");
        }
        if (finalEvent && player.z > -240) finalEvent = false;
        if (GATEOBJ != null && GATEOBJ.open > 0.6f && player.z < GATE.y - 1.6f && abs(player.x) < 4.4f) { win(); return; }
        // sounds
        float gx = GATE.x - player.x, gz = GATE.y - player.z, gd = hypot(gx, gz); float rx = cos(player.yaw), rz = -sin(player.yaw); float gpan = (gx * rx + gz * rz) / (gd == 0 ? 1 : gd);
        bellT -= dt; if (bellT <= 0) { bellT = finalEvent ? 7 : 18; A.bell(gpan * 0.85f, clamp(1 - gd / 700, 0.14f, 1) * (ins != null ? 0.5f : 1)); }
        if (night > 0.5f && !player.safe && ins == null)
        {
            knockT -= dt; if (knockT <= 0) { knockT = RR(25, 50); float pan = RR(-1, 1); int n = 3 + floor(rnd() * 4); for (int k = 0; k < n; k++) A.knock(pan, 0.22f, k * 0.42f); }
        }
        if (WELL != null)
        {
            float wd = hypot(WELL.x - player.x, WELL.z - player.z);
            if (wd < 16)
            {
                wellT -= dt;
                if (wellT <= 0) { wellT = RR(2.5f, 6); float pan = ((WELL.x - player.x) * rx + (WELL.z - player.z) * rz) / (wd == 0 ? 1 : wd); int n = 2 + floor(rnd() * 3); for (int k = 0; k < n; k++) A.knock(pan, 0.3f * clamp(1 - wd / 16, 0.2f, 1), k * 0.5f, true); }
            }
        }
        if (ins != null && ins.id != "kofun") { fishT -= dt; if (fishT <= 0) { fishT = RR(0.9f, 1.1f) * (ins.id == "t1" ? 1.4f : 0.8f); A.knock(RR(-0.3f, 0.3f), 0.07f, 0, true); } }
        if (player.sanity < 40 && !player.safe) { whisperT -= dt; if (whisperT <= 0) { whisperT = RR(9, 20); A.whisper(RR(-1, 1), 0.35f + (1 - player.sanity / 40) * 0.3f); } }
        float danger = player.danger; heartT -= dt;
        if ((danger > 0.08f || player.sanity < 20) && heartT <= 0) { heartT = lerp(1.15f, 0.42f, max(danger, (20 - player.sanity) / 20 * 0.6f)); A.heart(0.35f + danger * 0.5f); }
        stingerCD -= dt;
        if (A.ok)
        {
            bool zr = player.zone == "maze2"; float mf = 1 - indoorF * 0.7f;
            A.set(A.rainG, weather.rain * 0.3f * mf); A.set(A.rainLP, lerp(6500, 900, indoorF)); A.set(A.rainLowG, weather.rain * 0.25f * (1 - indoorF * 0.3f));
            A.set(A.windG, (0.07f + (1 - weather.rain) * 0.05f + (finalEvent ? 0.05f : 0)) * mf);
            A.set(A.leafG, zr ? 0.07f : 0.012f * mf); A.set(A.droneG, 0.025f + night * 0.04f + danger * 0.05f + (finalEvent ? 0.05f : 0) + indoorF * 0.035f);
            A.set(A.droneDis, night * 0.8f + (finalEvent ? 0.6f : 0) + indoorF * 0.5f);
            float cl = pow(1 - player.sanity / 100, 2) * 0.14f + (finalEvent ? 0.05f : 0) + (ins != null && ins.id != "t1" && ins.id != "hut" ? 0.022f : 0); A.set(A.chantG, cl * 0.5f, 0.8f);
            {
                float near = 0; for (int i = 0; i < DEADB.Count; i++) { var s = DEADB[i]; near = max(near, clamp(1 - hypot(s.x - player.x, s.z - player.z) / 60, 0, 1)); }
                if (NICHEPOS.HasValue) near = max(near, clamp(1 - hypot(NICHEPOS.Value.x - player.x, NICHEPOS.Value.y - player.z) / 55, 0, 1));
                A.set(A.chantClipG, clamp(cl * 2.4f + near * 0.22f + VIS.chant * 0.3f + (ins != null && (ins.id == "t3" || ins.id == "t6" || ins.id == "t4") ? 0.12f : 0), 0, 0.6f), 0.9f);
            }
        }
    }

    // ================= dying =================
    public static void updateDying(float dt)
    {
        var d = dying; var j = d.c; d.t += dt;
        float hy = j.g.py + j.headY; float dx = j.x - camera.px, dz = j.z - camera.pz; float dist = hypot(dx, dz);
        if (dist > 0.75f) { float m = min(dist - 0.75f, dt * 9); j.x -= dx / dist * m; j.z -= dz / dist * m; j.g.setPos(j.x, j.g.py, j.z); }
        j.g.rotY = atan2(camera.px - j.x, camera.pz - j.z) + j.faceYaw; dx = j.x - camera.px; dz = j.z - camera.pz;
        float ty = atan2(-dx, -dz), tp = atan2(hy - camera.py, hypot(dx, dz)); float k = min(1, dt * 16); player.yaw += wrapA(ty - player.yaw) * k; player.pitch = lerp(player.pitch, tp, k);
        float sk = max(0, 1 - d.t) * 0.08f; camera.setRot(player.pitch + (rnd() - 0.5f) * sk, player.yaw + (rnd() - 0.5f) * sk, 0); camera.fov = lerp(72, 46, min(1, d.t * 3));
        torch.intensity = 2.2f; hemi.intensity = max(hemi.intensity, 0.25f);
        UI.setFx("redfx", d.t < 0.25f ? d.t * 2.4f : max(0.45f, 0.7f - (d.t - 0.25f) * 0.3f));
        if (d.t > 1.6f && !d.shown) { d.shown = true; UI.setFx("blackfx", 1); }
        if (d.t > 2.5f && state == "dying")
        {
            state = "dead"; camera.fov = 72;
            UI.setDeadSub(deadSubText()); UI.show("dead", true); A.suspend();
        }
    }

    // ================= main loop =================
    /// <summary>One frame of game logic (JS frame()). Rendering itself is Unity's; Boot calls renderApply() and UI.update() afterwards.</summary>
    public static void frame(float dt)
    {
        if (dt > 0.05f) dt = 0.05f; if (dt < 0) dt = 0;
        if (state == "loading") return;
        if (state == "playing")
        {
            player.elapsed += dt; updateLater(dt); updatePlayer(dt); updateEvents(dt);
            if (state != "playing") return;
            fixTick(dt);
            updateEnv(dt, true); updateStatues(dt); updateChasers(dt);
            if (state == "playing") { updateGhost(dt); updatePickups(dt); updateGate(dt); updateHalluc(dt); }
            updateMsg(dt);
        }
        else if (state == "dying") { updateEnv(dt, false); updateDying(dt); }
        else if (state == "title")
        {
            titleT += dt; camera.setPos(START.x, groundH(START.x, START.y) + EYE, START.y); camera.setRot(0.02f, sin(titleT * 0.08f) * 0.5f, 0); updateEnv(dt, false);
        }
        else return;
        if (rain != null) rain.step(dt);
        beamTick();
        cullPass();
    }

    // ================= boot =================
    /// <summary>JS boot() minus asset loading (Boot.cs calls loadAll first): build the world and go to the title screen.</summary>
    public static void boot()
    {
        buildWorld(); buildLanternRings(); buildBeam();
        resetGame(); state = "title";
    }
}
