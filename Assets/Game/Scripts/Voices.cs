// Voices & hallucinations (index.html lines 1255-1335): the voices that call your name, the beckoning phantom, eerie sounds.
// All coordinates are three.js space. Vector2 "sources" are (x, z).
using System;
using System.Collections.Generic;
using UnityEngine;

public class HLTrap { public float yaw, t; }
public class HLState { public float vCool = 18, tick = 0, eerieT = 35, pd = 0; public HLTrap trap; public Vector2? lastSrc; }
public struct VoicePos { public float pan, vol, lp; }

public static partial class G
{
    // ================= voices & hallucinations =================
    public static string PNAME = "玄燁";
    public static readonly Dictionary<string, float> DUR = new Dictionary<string, float> {
        {"name1",5.44f},{"name2",4.89f},{"cant",6.08f},{"back",5.28f},{"noenter",5.62f},{"nofollow",5.98f},{"nothuman",6.06f},{"nolook",5.71f},{"sizhu",5.36f},{"nopray",5.7f},{"noexit",6.29f},{"behind",5.61f},
        {"come1",3.63f},{"come2",3.49f},{"exit",4.38f},{"why",4.15f},{"fname",3.29f},{"arrived",2.06f},{"lookme",2.24f},
        {"t01",5.26f},{"t02",8.22f},{"t03",6.57f},{"t04",5.87f},{"t05",5.98f},{"t06",6.11f},{"t07",7.63f},{"t08",7.69f},{"t09",4.86f},{"t10",8.0f},{"t11",6.15f},{"t12",8.82f},
        {"t13",6.19f},{"t14",6.07f},{"t15",8.15f},{"t16",6.83f},{"t17",4.62f},{"t18",7.42f},{"t19",6.64f},{"t20",7.17f},{"t21",5.94f},{"t22",7.01f},{"t23",7.26f},{"t24",5.79f},
        {"t25",5.32f},{"t26",7.44f},{"t27",5.8f},{"t28",9.0f},{"t29",7.2f},{"t30",7.23f},{"t31",5.84f},{"t32",11.12f},{"t33",6.02f},{"t34",5.03f},{"t35",8.94f},{"t36",5.05f} };
    public static HLState HL = new HLState();

    public static VoicePos voicePos(float x, float z)
    {
        float dx = x - player.x, dz = z - player.z, d = hypot(dx, dz); if (d == 0) d = 1;
        float rx = cos(player.yaw), rz = -sin(player.yaw), fx = -sin(player.yaw), fz = -cos(player.yaw);
        float pan = clamp((dx * rx + dz * rz) / d, -1, 1) * 0.9f, front = (dx * fx + dz * fz) / d;
        return new VoicePos { pan = pan, vol = clamp(1.05f - d / 110f, 0.4f, 1), lp = front < -0.2f ? 2400 : (d > 45 ? 3000 : 0) };
    }
    /// <summary>The web build used speechSynthesis for custom names. Not available here -> false (falls back to 「施主……」).</summary>
    public static bool speakName() { return false; }

    // parts: array of clip keys; 'NAME' / 'FNAME' are replaced by the player's name
    public static readonly Dictionary<string, string> LINE = new Dictionary<string, string> {
        {"cant","那裡，不能去。"},{"back","回去。"},{"noenter","不要進去。"},{"nofollow","不要跟他走。"},{"nothuman","那個，不是人。"},{"nolook","不要回頭。"},{"nopray","不要拜我。"},{"noexit","你走不出去的。"},{"behind","在你後面。"},
        {"come1","過來這邊。"},{"come2","來……過來。"},{"exit","這邊，才是出口。"},{"why","為什麼不過來？"},{"arrived","你來了。"},{"lookme","看著我。"},
        {"t01","你最近好嗎？"},{"t02","你為什麼，突然出現在這裡？"},{"t03","你覺得，你活得了嗎？"},{"t04","你終於來了。"},{"t05","我等你，很久了。"},{"t06","你還記得我嗎？"},{"t07","你媽媽，知道你在這裡嗎？"},{"t08","天黑以前，你走不掉的。"},
        {"t09","你在找什麼？"},{"t10","你後面那個，是誰？"},{"t11","不要數我們。"},{"t12","上一個進來的人，還在這裡。"},{"t13","你的臉，借我看一下。"},{"t14","留下來吧。"},{"t15","這裡很安靜，不好嗎？"},{"t16","你已經走了三圈了。"},
        {"t17","你怕嗎？"},{"t18","你聽得見我，對不對？"},{"t19","他們，都沒有回去。"},{"t20","你的名字，我記住了。"},{"t21","今天晚上，睡在這裡吧。"},{"t22","不要相信那盞燈。"},{"t23","你剛剛，踩到我了。"},{"t24","我看見你了。"},
        {"t25","你為什麼不笑？"},{"t26","你身上，有活人的味道。"},{"t27","再走近一點。"},{"t28","你的眼睛很好看。給我。"},{"t29","外面，已經沒有人在等你了。"},{"t30","你以為，你是第一次來嗎？"},{"t31","噓。祂醒了。"},
        {"t32","我們一共一千尊。現在，多一尊。"},{"t33","你累了吧。坐下來。"},{"t34","別哭。"},{"t35","你死過一次了，記得嗎？"},{"t36","跑什麼？"} };
    public static readonly List<string> TALK = makeTalk();
    static List<string> makeTalk() { var l = new List<string>(); for (int i = 1; i <= 36; i++) l.Add("t" + i.ToString("00")); return l; }
    public static List<string> talkLeft = new List<string>();
    public static string talkLine()
    {
        if (talkLeft.Count == 0) talkLeft = new List<string>(TALK);
        for (int n = 0; n < 12; n++)
        {
            int i = floor(rnd() * talkLeft.Count); string k = talkLeft[i];
            if ((k == "t35" && player.deaths == 0) || (k == "t36" && hypot(player.vx, player.vz) < 5.5f) || (k == "t08" && night > 0.4f) || (k == "t21" && night < 0.5f) || (k == "t16" && player.elapsed < 240)) continue;
            talkLeft.RemoveAt(i); return k;
        }
        return "t24";
    }
    public static void talk() { string k = talkLine(); say(buddhaSrc(), rnd() < 0.3f ? new[] { "NAME", k } : new[] { k }); }

    /// <summary>Speak a sequence of clips from a world position (src = (x, z); null = inside your head). Returns the total time used.</summary>
    public static float say(Vector2? src, string[] parts, float vol = 1f, bool female = false, float delay = 0f)
    {
        VoicePos pos = src.HasValue ? voicePos(src.Value.x, src.Value.y) : new VoicePos { pan = 0, vol = 0.95f, lp = 0 };
        float t = delay;
        foreach (string k in parts)
        {
            string clip = k, text; LINE.TryGetValue(k, out text); float tail = female ? 2.0f : 3.2f;
            if (k == "NAME" || k == "FNAME")
            {
                text = PNAME + "……";
                if (PNAME == "玄燁") clip = k == "FNAME" ? "fname" : (rnd() < 0.5f ? "name1" : "name2");
                else { clip = null; if (!speakName()) { clip = "sizhu"; text = "施主……"; } }
            }
            if (clip != null) { A.clip(clip, vol: pos.vol * vol, pan: pos.pan, lp: pos.lp, delay: t); }
            float dur; if (clip == null || !DUR.TryGetValue(clip, out dur)) dur = 4.5f;
            float dry = max(1.1f, dur - tail); showMsg("「" + text + "」", max(2.2f, dry + 1.1f), "voice"); t += dry + 0.9f;
        }
        HL.lastSrc = src; return t;
    }
    public static Statue nearestGiant(float maxD = 90f)
    {
        Statue best = null; float bd = maxD > 0 ? maxD : 90f;
        foreach (var s in STATUES) { if (!s.giant || s.indoor) continue; float d = hypot(s.x - player.x, s.z - player.z); if (d < bd && d > 6) { bd = d; best = s; } }
        return best;
    }
    public static Vector2 behindPoint(float d) { float a = player.yaw + RR(-0.7f, 0.7f); return new Vector2(player.x + sin(a) * d, player.z + cos(a) * d); }
    public static Vector2 buddhaSrc() { var g = nearestGiant(90); return g != null ? new Vector2(g.x, g.z) : behindPoint(30); }
    public static void flashGhost()
    {
        if (ghost == null) return; float fx = -sin(player.yaw), fz = -cos(player.yaw); float x = player.x + fx * 1.7f, z = player.z + fz * 1.7f;
        ghost.root.setPos(x, groundH(x, z) - 0.25f, z); ghost.root.rotY = atan2(player.x - x, player.z - z) + 0.873f;
        ghost.root.visible = true; ghost.active = false; ghost.flashT = 0.5f; A.stinger(); A.clip("lookme", vol: 1f); showMsg("「看著我。」", 2.6f, "voice"); player.sanity = max(0, player.sanity - 12); shake = 0.9f;
    }
    public static void phantomHide() { var P = PHANTOM; if (P == null) return; P.active = false; P.root.visible = false; setOpacity(P.mat, 0); }
    static Vector2? phantomSpot(Func<float[]> gen, bool los)
    {
        for (int t = 0; t < 14; t++)
        {
            float[] q = gen(); float x = q[0], z = q[1];
            if (abs(x) > XB - 8 || z < ZN + 8 || z > ZS - 8) continue; if (zoneOfZ(z) != zoneOfZ(player.z) || inMazeBand(z, 3)) continue; if (inPad(x, z, 3) || interiorAt(x, z) != null) continue; if (!canPlace(x, z, 0.9f)) continue;
            if (los && blocked(player.x, player.z, x, z)) continue;
            bool nearLit = false; foreach (var L in LANTERNS) if (L.lit && hypot(L.x - x, L.z - z) < 12) { nearLit = true; break; }
            if (nearLit) continue;
            return new Vector2(x, z);
        }
        return null;
    }
    static void phantomPlace(float x, float z) { var P = PHANTOM; P.x = x; P.z = z; P.root.setPos(x, groundH(x, z) - 0.04f, z); P.root.rotY = atan2(player.x - x, player.z - z); P.root.visible = true; P.fade = 0; }
    public static void updatePhantom(float dt)
    {
        var P = PHANTOM; if (P == null) return; bool out_ = player.inside == null && !inMazeBand(player.z, 1);
        if (!P.active)
        {
            if (out_ && !player.safe && !finalEvent && VIS.ep == null && VIS.face == null) P.cool -= dt * (night > 0.5f ? 1.4f : 1) * (player.sanity < 60 ? 1.4f : 1);
            if (P.cool <= 0)
            {
                var s = phantomSpot(() => { float a = player.yaw + RR(-0.5f, 0.5f), d = RR(19, 26); return new[] { player.x - sin(a) * d, player.z - cos(a) * d }; }, true);
                if (!s.HasValue) { P.cool = 6; return; }
                float sx = s.Value.x, sz = s.Value.y;
                phantomPlace(sx, sz); P.active = true; P.hops = 0; P.life = 30; P.callT = 1.4f; P.calls = 0; P.warnT = (!F("ph1") || rnd() < 0.65f) ? RR(3.5f, 5) : -1; P.gone = 0;
                float l = hypot(sx - player.x, sz - player.z); if (l == 0) l = 1; P.dx = (sx - player.x) / l; P.dz = (sz - player.z) / l;
                if (!F("ph1")) { setF("ph1"); showMsg("霧裡站著一個人。他在對你招手。", 4); }
            }
            return;
        }
        if (!out_ || player.safe) { phantomHide(); P.cool = RR(40, 70); return; }
        if (P.gone > 0) { P.gone -= dt; if (P.gone <= 0) { P.root.visible = true; P.fade = 0; } return; }
        P.fade = min(1, P.fade + dt * 0.9f); float pale = smooth(0.45f, 0.85f, night);
        setRGB(P.mat, 0.016f + pale * 0.5f, 0.016f + pale * 0.55f, 0.016f + pale * 0.62f); setOpacity(P.mat, P.fade * lerp(0.9f, 0.5f, pale));
        P.root.rotY += wrapA(atan2(player.x - P.x, player.z - P.z) - P.root.rotY) * min(1, dt * 3); P.arm.rotX = sin(uTime * 3.1f) * 0.4f - 0.05f;
        float dist = hypot(P.x - player.x, P.z - player.z); P.life -= dt; if (inView(P.x, P.root.py + 1.4f, P.z, 0.9f)) player.sanity = max(0, player.sanity - 0.9f * dt);
        P.callT -= dt;
        if (P.callT <= 0 && P.calls < 3)
        {
            P.calls++; P.callT = RR(6.5f, 9); string k = P.calls == 1 ? (rnd() < 0.5f ? "come1" : "come2") : pickR(new[] { "come1", "come2", "exit" });
            say(new Vector2(P.x, P.z), P.calls == 2 && PNAME == "玄燁" && rnd() < 0.5f ? new[] { "FNAME", k } : new[] { k }, female: true);
        }
        if (P.warnT > 0) { P.warnT -= dt; if (P.warnT <= 0 && HL.vCool < 25) { say(buddhaSrc(), new[] { "NAME", rnd() < 0.5f ? "nofollow" : "nothuman" }); HL.vCool = 40; } }
        if (dist < 7.5f)
        {
            if (P.hops < 2)
            {
                float ba = atan2(P.dx, P.dz);
                var s = phantomSpot(() => { float a = ba + RR(-0.55f, 0.55f), d = RR(15, 23); return new[] { P.x + sin(a) * d, P.z + cos(a) * d }; }, false);
                if (s.HasValue)
                {
                    float sx = s.Value.x, sz = s.Value.y;
                    P.hops++; P.root.visible = false; P.gone = 0.9f; P.x = sx; P.z = sz; P.root.setPos(sx, groundH(sx, sz) - 0.04f, sz); P.life = 26; P.callT = 1.6f; P.calls = Mathf.Min(P.calls, 1); A.whisper(P.dx * 0.3f, 0.4f); return;
                }
            }
            // the end of the trail
            phantomHide(); P.cool = RR(100, 160); A.clip("arrived", vol: 1f); showMsg("「你來了。」", 3, "voice"); if (!F("phLesson")) { setF("phLesson"); showMsg("……招手的人把你帶到了祂們面前。下次聽到「不要跟他走」，就別跟。", 5.5f, "warn"); } A.stinger(); shake = 0.8f; player.sanity = max(0, player.sanity - 18); stingerCD = 6;
            Chaser best = null; float bd = 1e9f;
            foreach (var j in CHASERS) { if (j.hall != null || j.maze != null || j.dormant || j.zone != player.zone) continue; float dd = hypot(j.x - player.x, j.z - player.z); if (dd < bd) { bd = dd; best = j; } }
            if (best != null)
            {
                Vector2 b = behindPoint(5.5f);
                if (canPlace(b.x, b.y, 0.6f) && interiorAt(b.x, b.y) == null) { best.x = b.x; best.z = b.y; best.g.setPos(b.x, groundH(b.x, b.y), b.y); best.moved = 0; best.g.rotY = atan2(player.x - b.x, player.z - b.y) + best.faceYaw; }
            }
            return;
        }
        if (dist > 48 || P.life <= 0) { if (dist < 60 && rnd() < 0.6f) say(new Vector2(P.x, P.z), new[] { "why" }, female: true); phantomHide(); P.cool = RR(60, 110); }
    }
    public static void updateHalluc(float dt)
    {
        bool out_ = player.inside == null; HL.vCool -= dt;
        if (ghost != null && ghost.flashT > 0) { ghost.flashT -= dt; if (ghost.flashT <= 0) ghost.root.visible = false; }
        if (HL.trap != null) { HL.trap.t -= dt; if (abs(wrapA(player.yaw - HL.trap.yaw)) > 2.1f) { HL.trap = null; flashGhost(); } else if (HL.trap.t <= 0) HL.trap = null; }
        updatePhantom(dt);
        HL.tick -= dt;
        if (HL.tick <= 0)
        {
            HL.tick = 0.5f; float fx = -sin(player.yaw), fz = -cos(player.yaw);
            foreach (var s in DEADB) { bool v = night > 0.45f || finalEvent; if (s.eyes != null) foreach (var m in s.eyes) m.visible = v; if (s.sprs != null) foreach (var sp in s.sprs) sp.visible = v; }
            // the dead buddha speaks first
            for (int i = 0; i < DEADB.Count; i++)
            {
                var s = DEADB[i];
                if (!F("db" + i) && hypot(s.x - player.x, s.z - player.z) < 38) { setF("db" + i); say(new Vector2(s.x, s.z), new[] { "NAME", "nopray" }, vol: 1.1f); HL.vCool = 40; }
            }
            if (NICHEPOS.HasValue && !F("nc") && hypot(NICHEPOS.Value.x - player.x, NICHEPOS.Value.y - player.z) < 30)
            {
                setF("nc"); for (int k = 0; k < 4; k++) later(k * 0.7f, () => { if (state == "playing") A.whisper(RR(-1, 1), 0.45f); });
                A.clip("rev3", vol: 0.4f, pan: -0.4f);
            }
            if (out_)
            {
                foreach (var t in TEMPLES)
                {
                    var hi = t.H; if (!t.key || hi == null || !hi.isHall || F("ne" + hi.id)) continue; var h = (Hall)hi;
                    float[] dp = h.W(0, h.d / 2 + 5);
                    if (hypot(dp[0] - player.x, dp[1] - player.z) < 8)
                    {
                        bool taken = false; foreach (var p in PICKUPS) if (p.hall == hi && p.taken) { taken = true; break; }
                        if (!taken) { setF("ne" + h.id); if (h.id != "t3") { say(behindPoint(24), new[] { "noenter" }); HL.vCool = 35; } }
                    }
                }
                if (player.zone == "final" && !F("nx") && player.z < -150) { setF("nx"); say(buddhaSrc(), new[] { "NAME", "noexit" }); HL.vCool = 45; }
                if (HL.vCool <= 0 && !player.safe)
                {
                    // a stone monk waiting ahead, unseen
                    Chaser ahead = null;
                    foreach (var j in CHASERS)
                    {
                        if (j.dormant || j.hall != null || j.zone != player.zone || j.seen) continue; float dx = j.x - player.x, dz = j.z - player.z, d = hypot(dx, dz);
                        if (d < 26 && d > 7 && (dx * fx + dz * fz) / d > 0.55f) { ahead = j; break; }
                    }
                    bool inMaze = inMazeBand(player.z, 0); HL.pd = inMaze ? 0 : pathDist(player.x, player.z);
                    if (ahead != null) { say(buddhaSrc(), rnd() < 0.7f ? new[] { "back" } : new[] { "NAME", "cant" }); HL.vCool = RR(40, 60); }
                    else if (HL.pd > 30 && hypot(player.vx, player.vz) > 1.5f && rnd() < 0.02f) { if (rnd() < 0.25f) say(buddhaSrc(), new[] { "NAME", "cant" }); else talk(); HL.vCool = RR(30, 50); }
                    else if (night > 0.6f && rnd() < 0.06f)
                    {
                        float r = rnd();
                        if (r < 0.62f) talk();
                        else if (r < 0.82f) { say(behindPoint(8), new[] { "nolook" }, vol: 1.1f); HL.trap = new HLTrap { yaw = player.yaw, t = 7 }; }
                        else { say(behindPoint(6), new[] { "behind" }, vol: 1.1f); A.stepsBehind(4); }
                        HL.vCool = RR(24, 42);
                    }
                    else if (rnd() < 0.05f) { talk(); HL.vCool = RR(26, 46); }
                }
            }
        }
        // things that should not be there
        if (out_ && !player.safe)
        {
            HL.eerieT -= dt * (night > 0.5f ? 1.5f : 1) * (player.sanity < 50 ? 1.4f : 1);
            if (HL.eerieT <= 0)
            {
                HL.eerieT = RR(24, 46); int r = floor(rnd() * 6);
                if (r == 0) A.clip(pickR(new[] { "rev1", "rev2", "rev3" }), vol: 0.38f, pan: RR(-1, 1), lp: 3200);
                else if (r == 1) A.stepsBehind(4 + floor(rnd() * 3)); else if (r == 2) A.breath(RR(-0.6f, 0.6f)); else if (r == 3) A.hum(RR(-0.9f, 0.9f));
                else if (r == 4) A.knock(RR(-1, 1), 0.2f, 0, true); else A.clip("rev2", vol: 0.3f, pan: RR(-1, 1), rate: 0.8f);
            }
        }
        updateVision(dt);
    }
    public static void resetHalluc()
    {
        HL.vCool = 18; HL.eerieT = 35; HL.trap = null; HL.tick = 0;
        if (PHANTOM != null) { phantomHide(); PHANTOM.cool = RR(40, 60); }
        if (ghost != null) ghost.flashT = 0;
        resetVision();
    }
}
