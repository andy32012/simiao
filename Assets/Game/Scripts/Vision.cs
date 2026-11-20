// Visions: things the eyes make up (index.html lines 1337-1565).
// Ring of buddhas, face in your face, phantom hall, procession, peripheral figure, total darkness, red rain, face in the sky, heads at your feet.
// All coordinates are three.js space.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class VisAfter { public float t, life; public bool chain; }
public class VisFace { public VisSt st; public float t; public bool chain, done; }
public class VisFeet { public float x, z; public bool seen; }
public class VisSky { public Obj g; public List<Material> mats = new List<Material>(); public Material sm; public bool on; public float a, life; }
public class VisEyes { public Obj p; public Obj[] spr; public Material m; public int N; }
public class VisHall { public Obj g; public float[][] LP; }
public class VisRingSlot { public VisSt st; public float x, z, h; public bool put; }
public class VisSideSlot { public VisSt st; public float sx, lz; public bool moved; }
public class VisProcSlot { public VisSt st; public float off, ph; }
/// <summary>One running vision episode (JS `e`). k = ring | temple | proc | peri | dark | linger | red. Union of all per-kind fields.</summary>
public class VisEp
{
    public string k; public bool abort, done;
    public float t, life;
    // ring (JS e.st -> stage)
    public int stage; public float cx, cz, yaw0, awayT, hb; public bool away, forced; public List<VisRingSlot> slots;
    // temple (JS e.side -> sideL)
    public float yaw, ox, oz, oy, c, s, fish; public List<VisSideSlot> sideL; public VisSt end; public bool said, on; public List<float[]> lw;
    // procession
    public float px, pz, lat, s0, stopT; public bool stop; public List<VisProcSlot> list;
    // peripheral / linger (JS e.st -> st)
    public int n, side; public float wait, stayT; public bool stay; public VisSt st;
    // dark
    public float dur;
}
public class VisState
{
    public float sway; public List<VisSt> pool = new List<VisSt>(), heads = new List<VisSt>();
    public VisEp ep; public float cool = 38, hold, blink, blinkDur = 0.24f; public Action blinkFn; public float blinkOp, dark, red; public bool redDirty;
    public float roof, chant, upT; public bool upRoll, dnRoll; public float skyCool = 25, feetCool = 30; public int n; public string last = "";
    public VisFace face; public VisSky sky; public VisFeet feet; public VisEyes eyes; public VisHall hall; public VisAfter after;
}

public static partial class G
{
    // ================= visions: things the eyes make up =================
    public static VisState VIS = new VisState();
    public static readonly Color FOG_TEMPLE = C("#1c0c08"), FOG_RED = C("#3a0705"), RAIN_BASE = C(0xaab3b8), RAIN_RED = C(0xb01208);
    public static float HFOV() { return Mathf.Atan(0.7265f * camera.aspect); }
    public static void visSan(float x) { if (player.sanity > 12) player.sanity = max(12, player.sanity - x); }
    static Texture2D visTex(string n) { Texture2D t; T.TryGetValue(n, out t); return t; }

    public static void visInit()
    {
        var STAND = new HashSet<string> { "amitabha", "gandhara", "elevenhead", "arhat", "sekijin", "jizoped" };
        Action<string, List<VisSt>> mk = (name, arr) =>
        {
            ModelInfo M; if (!MODELS.TryGetValue(name, out M) || M == null) return;
            var g = buildModel(name, new SO { cast = false, moss = 0.6f, gray = 0.3f, tint = new[] { 0.45f, 0.44f, 0.42f }, tintKey = "vis" }); g.visible = false;
            var st = new VisSt { name = name, g = g, M = M, stand = STAND.Contains(name), faceYaw = M.eyes != null ? M.eyes.faceYaw : 0, wr = M.d[0] * 0.5f, x = 0, y = 0, z = 0, h = 1, on = false };
            if (M.eyes != null) addEyes(st, M, new SO { hot = true });
            arr.Add(st);
        };
        foreach (var nm in new[] { "mossbuddha", "ironbuddha", "bluebuddha", "armless", "amitabha", "gandhara", "elevenhead", "arhat", "arhat", "sekijin", "sekijin", "jizoped", "monk", "naga" }) mk(nm, VIS.pool);
        foreach (var nm in new[] { "head_a", "head_b", "head_a", "head_b", "head_a", "head_b", "head_a" }) mk(nm, VIS.heads);
        // the face above the trees
        {
            ModelInfo M; MODELS.TryGetValue("head_b", out M);
            if (M != null && M.eyes != null)
            {
                var S = new VisSky(); var g = mkGroup("visSky");
                foreach (var p in M.parts) { var mt = basicMat(Color.white, false, p.map, true, 0f, false, false); S.mats.Add(mt); var m = mkMesh(p.geo, mt, false, false); g.add(m); }
                var sm = glowMat(C(0xff2408), 0f, false);
                foreach (var e in M.eyes.eyes) { var s = mkSprite(sm); s.setPos(e[0], e[1], e[2] + M.eyes.r * 1.3f); s.setScale(M.eyes.r * 7); g.add(s); }
                g.setScale(30); g.visible = false; S.g = g; S.sm = sm; S.on = false; S.a = 0; S.life = 0; VIS.sky = S;
            }
        }
        // eyes in the dark
        {
            int N = 46; var p = mkGroup("visEyes"); var m = glowMat(C(0xff2a0c), 0.9f, false); var spr = new Obj[N * 2];
            for (int i = 0; i < N * 2; i++) { var s = mkSprite(m); s.setScale(0.36f); s.visible = false; p.add(s); spr[i] = s; }
            p.visible = false; VIS.eyes = new VisEyes { p = p, spr = spr, m = m, N = N };
        }
        // a hall that is not there
        {
            var g = mkGroup("visHall"); MB P = new MB(), D = new MB(), O = new MB(), L = new MB();
            foreach (float z in new float[] { 4, -2, -8, -14, -20, -26, -32 })
            {
                foreach (int sx in new[] { -1, 1 }) P.cyl(sx * 4.2f, -4.5f, z, 0.36f, 10.6f, 10, 2);
                D.box(0, 5.75f, z, 9.4f, 0.5f, 0.42f, 2);
                for (int k = 0; k < 3; k++)
                {
                    float x = RR(-3.6f, 3.6f), hh = RR(0.8f, 1.15f), w = hh * 0.13f, u0 = floor(rnd() * 4) / 4f;
                    O.quad(v3(x - w, 5.5f - hh, z + 0.24f), v3(x + w, 5.5f - hh, z + 0.24f), v3(x + w, 5.5f, z + 0.24f), v3(x - w, 5.5f, z + 0.24f), u0, 0, u0 + 0.25f, 1);
                }
            }
            foreach (int sx in new[] { -1, 1 }) D.box(sx * 4.2f, 6.1f, -14, 0.42f, 0.42f, 37, 2);
            D.quad(v3(-6.8f, 6.35f, -35), v3(6.8f, 6.35f, -35), v3(6.8f, 6.35f, 6), v3(-6.8f, 6.35f, 6), 0, 0, 5, 15);
            D.quad(v3(-6.6f, -4.5f, 6), v3(-6.6f, -4.5f, -35), v3(-6.6f, 6.35f, -35), v3(-6.6f, 6.35f, 6), 0, 0, 15, 4);
            D.quad(v3(6.6f, -4.5f, -35), v3(6.6f, -4.5f, 6), v3(6.6f, 6.35f, 6), v3(6.6f, 6.35f, -35), 0, 0, 15, 4);
            D.quad(v3(6.6f, -4.5f, 6), v3(-6.6f, -4.5f, 6), v3(-6.6f, 6.35f, 6), v3(6.6f, 6.35f, 6), 0, 0, 5, 4);
            D.quad(v3(-6.6f, -4.5f, -35), v3(6.6f, -4.5f, -35), v3(6.6f, 6.35f, -35), v3(-6.6f, 6.35f, -35), 0, 0, 5, 4);
            float[][] LP = { new[] { -2.4f, 4.5f, -4f }, new[] { 2.4f, 4.5f, -13f }, new[] { -2.4f, 4.5f, -22f }, new[] { 2.6f, 4.3f, -28f } };
            var sm = glowMat(C(0xff4a1c), 0.85f, true);
            foreach (var q in LP) { L.box(q[0], q[1], q[2], 0.34f, 0.5f, 0.34f, 1); L.box(q[0], q[1] + 0.75f, q[2], 0.03f, 1, 0.03f, 1); var s = mkSprite(sm); s.setPos(q[0], q[1], q[2]); s.setScale(3.2f); g.add(s); }
            Action<MB, Material> add = (mb, mat) => { if (!mb.empty) g.add(mkMesh(mb.geo(), mat, false, true)); };
            add(P, stdMat(visTex("wood_d"), visTex("wood_n"), new W { moss = 0, tint = new[] { 0.55f, 0.17f, 0.12f } }));
            add(D, stdMat(visTex("wood_d"), null, new W { moss = 0, tint = new[] { 0.2f, 0.165f, 0.145f } }, true));
            Material om = null; if (HM != null) HM.TryGetValue("ofuda", out om);
            if (om == null) om = litMat(Color.white, 1f, 0f, ofudaTex(), null, 1f, true);
            add(O, om); add(L, basicMat(C(0xb8321a)));
            g.visible = false; VIS.hall = new VisHall { g = g, LP = LP };
        }
    }

    // ---------- helpers
    static void visEyes(VisSt st, bool on) { if (st.eyes == null) return; foreach (var m in st.eyes) m.visible = on; if (st.sprs != null) foreach (var s in st.sprs) s.visible = on; }
    static void visPut(VisSt st, float x, float z, float h, float sink = 0f, float? y = null, float? yaw = null, float pitch = 0f, bool eyes = true)
    {
        st.x = x; st.z = z; st.h = h; st.y = (y.HasValue ? y.Value : groundH(x, z)) - sink * h; st.g.setPos(x, st.y, z); st.g.setScale(h);
        st.g.setRot(pitch, (yaw.HasValue ? yaw.Value : atan2(player.x - x, player.z - z)) + st.faceYaw, 0); st.g.visible = true; st.on = true; visEyes(st, eyes);
    }
    static void visOff(VisSt st) { st.g.visible = false; st.on = false; }
    static bool visSeenXY(float x, float y, float z, float h, float wr, float ym = 0f)
    {
        float d = hypot(x - camera.px, z - camera.pz); if (d == 0) d = 1; float m = 1.03f + (h * wr / d) / (0.7265f * camera.aspect); if (ym == 0) ym = 1.03f;
        foreach (float f in new[] { 0.5f, 0.96f, 0.04f }) { Vector3 v = camera.project(x, y + h * f, z); if (v.z < 1 && abs(v.x) < m && abs(v.y) < ym) return true; }
        return false;
    }
    static bool visSeen(VisSt st, float ym = 0f) { return visSeenXY(st.x, st.y, st.z, st.h, st.wr, ym); }
    static void visTurn(VisSt st, float k, float dt) { float tgt = atan2(player.x - st.x, player.z - st.z) + st.faceYaw; st.g.rotY += wrapA(tgt - st.g.rotY) * min(1, dt * k); }
    static bool slotOK(float x, float z) { return abs(x) < XB - 4 && z > ZN + 6 && z < ZS - 4 && interiorAt(x, z) == null && canPlace(x, z, 0.8f); }
    static void setBlink(float o) { VIS.blinkOp = o; UI.setFx("blink", o); }
    static void visBlink(Action fn, float dur = 0.24f)
    {
        if (VIS.blinkFn != null) { var f = VIS.blinkFn; VIS.blinkFn = null; f(); }
        VIS.blinkDur = dur > 0 ? dur : 0.24f; VIS.blink = VIS.blinkDur; VIS.blinkFn = fn;
    }
    static void visClear()
    {
        foreach (var s in VIS.pool) visOff(s); foreach (var s in VIS.heads) visOff(s); VIS.face = null; VIS.after = null; VIS.feet = null; VIS.dark = 0; VIS.red = 0; VIS.roof = 0;
        if (VIS.hall != null) VIS.hall.g.visible = false; if (VIS.eyes != null) VIS.eyes.p.visible = false; if (VIS.sky != null) { VIS.sky.on = false; VIS.sky.a = 0; VIS.sky.g.visible = false; }
    }
    static void visEnd(float? cool = null) { var e = VIS.ep; VIS.ep = null; VIS.cool = cool.HasValue ? cool.Value : RR(32, 56); if (e != null) { VIS.last = e.k; VIS.n++; } }
    public static void visStop()
    {
        visClear(); VIS.sway = 0; VIS.ep = null; VIS.blink = 0; VIS.blinkFn = null; VIS.hold = 0; setBlink(0);
        if (rain != null) rain.color = RAIN_BASE; VIS.redDirty = false;
        if (camera.cam != null && camera.fov != 72) { camera.fov = 72; }
    }
    public static void resetVision() { visStop(); VIS.cool = 38; VIS.n = 0; VIS.last = ""; VIS.skyCool = 25; VIS.feetCool = 30; VIS.chant = 0; }
    static VisSt visShift(List<VisSt> l) { if (l.Count == 0) return null; var s = l[0]; l.RemoveAt(0); return s; }

    // ---------- 圍佛: one in the path, then all around; they go when you stop looking
    static bool startRing()
    {
        float fa = player.yaw + PI; var pool = VIS.pool.Where(s => !s.on).ToList(); var big = pool.Where(s => !s.stand).ToList(); var tall = pool.Where(s => s.stand).ToList();
        var e = new VisEp { k = "ring", stage = 0, t = 0, cx = player.x, cz = player.z, yaw0 = player.yaw, slots = new List<VisRingSlot>(), away = false, awayT = 0, life = 34, hb = 0 };
        for (int i = 0; i < 10; i++)
        {
            float a = fa + i / 10f * TAU + (i != 0 ? RR(-0.13f, 0.13f) : 0); float[] ok = null;
            float[] ds = i != 0 ? new[] { RR(5.4f, 7.2f), 6.4f, 8f, 4.8f } : new[] { 6.6f, 7.6f, 5.6f, 8.6f };
            foreach (float d in ds) { float x = player.x + sin(a) * d, z = player.z + cos(a) * d; if (slotOK(x, z)) { ok = new[] { x, z }; break; } }
            if (ok == null) { if (i == 0) return false; continue; }
            VisSt st = i % 2 == 0 ? (visShift(big) ?? visShift(tall)) : (visShift(tall) ?? visShift(big)); if (st == null) break;
            e.slots.Add(new VisRingSlot { st = st, x = ok[0], z = ok[1], h = i == 0 ? 3.7f : (st.stand ? RR(2.7f, 3.5f) : RR(2.4f, 3.2f)), put = false });
        }
        if (e.slots.Count < 5) return false; VIS.ep = e;
        visBlink(() => { if (VIS.ep != e) return; var s = e.slots[0]; visPut(s.st, s.x, s.z, s.h, sink: 0.03f); s.put = true; A.knock(0, 0.34f, 0, true); }, 0.2f);
        return true;
    }
    static void updRing(VisEp e, float dt)
    {
        e.t += dt; VIS.hold = 0.4f; float pit = abs(player.pitch);
        if (e.stage == 0)
        {
            if (!e.slots[0].put) return;
            if (pit > 0.6f) { e.away = true; e.stage = 2; } else if (e.t > 2.8f || (e.t > 0.5f && abs(wrapA(player.yaw - e.yaw0)) > 0.45f)) { e.stage = 1; e.t = 0; }
        }
        if (e.stage >= 1 && !e.away)
        {
            foreach (var s in e.slots) if (!s.put && !visSeenXY(s.x, groundH(s.x, s.z), s.z, s.h, s.st.wr)) { visPut(s.st, s.x, s.z, s.h, sink: 0.03f); s.put = true; }
            if (e.stage == 1 && e.t > 3)
            {
                e.stage = 2; var rest = e.slots.Where(s => !s.put).ToList();
                if (rest.Count > 0) visBlink(() => { if (VIS.ep != e || e.away) return; foreach (var s in rest) if (!s.put) { visPut(s.st, s.x, s.z, s.h, sink: 0.03f); s.put = true; } }, 0.18f);
                A.bell(0, 0.5f);
                if (!F("vRing")) showMsg("四周都是佛。不要看祂們——抬頭看天，或低頭看地。", 5.5f, "warn"); else if (HL.vCool < 40) { say((Vector2?)null, new[] { "NAME" }); HL.vCool = 45; }
                setF("vRing");
            }
            if (pit > 0.6f || e.life <= 0 || hypot(player.x - e.cx, player.z - e.cz) > 11) e.away = true;
        }
        e.life -= dt; if (e.stage >= 1) { visSan(1.2f * dt); e.hb -= dt; if (e.hb <= 0) { e.hb = 0.75f; A.heart(0.55f); } }
        int left = 0;
        foreach (var s in e.slots)
        {
            var st = s.st; if (!s.put || !st.on) continue; float d = hypot(st.x - player.x, st.z - player.z);
            if (d < st.h * st.wr * 0.7f + 0.5f) { visOff(st); A.whisper(0, 0.45f); visSan(3); continue; }
            bool seen = visSeen(st, e.away ? 0.7f : 0f);
            if (e.away) { if (!seen) { visOff(st); continue; } }
            else if (!seen)
            {
                visTurn(st, 4, dt);
                if (e.stage >= 1 && d > 3) { float k = 0.5f * dt / d; st.x += (player.x - st.x) * k; st.z += (player.z - st.z) * k; st.y = groundH(st.x, st.z) - 0.03f * st.h; st.g.setPos(st.x, st.y, st.z); }
            }
            left++;
        }
        if (e.stage == 2 && left == 0 && !e.away && VIS.blinkFn == null) e.away = true;
        if (e.away)
        {
            e.awayT += dt; foreach (var s in e.slots) s.put = true;
            if (left > 0 && e.awayT > 7 && !e.forced) { e.forced = true; visBlink(() => { foreach (var s in e.slots) visOff(s.st); }, 0.2f); }
            if (left == 0)
            {
                bool first = !F("vFace"); setF("vRing"); visEnd(RR(40, 60));
                if (first || rnd() < 0.6f) { setF("vFace"); VIS.after = new VisAfter { t = RR(0.35f, 0.8f), life = 14, chain = first || rnd() < 0.4f }; } else showMsg("……都不見了。", 2.6f);
            }
        }
    }

    // ---------- 貼臉: a face filling the view
    static bool startFace(bool chain)
    {
        var st = VIS.pool.Find(s => !s.on && s.name == "mossbuddha") ?? VIS.pool.Find(s => !s.on && !s.stand && s.M.eyes != null);
        if (st == null || st.M.eyes == null || player.inside != null || player.safe) return false;
        float h = 4.6f; var E = st.M.eyes.eyes; float ex = (E[0][0] + E[1][0]) / 2 * h, ey = E[0][1] * h, ez = (E[0][2] + E[1][2]) / 2 * h;
        float yaw = player.yaw, ry = yaw + st.faceYaw, c = cos(ry), sn = sin(ry), cp = cos(player.pitch), D = 0.74f;
        float tx = camera.px - sin(yaw) * D * cp, tz = camera.pz - cos(yaw) * D * cp, ty = camera.py + sin(player.pitch) * D - 0.02f;
        st.h = h; st.x = tx - (ex * c + ez * sn); st.z = tz - (-ex * sn + ez * c); st.y = ty - ey; st.g.setPos(st.x, st.y, st.z); st.g.setScale(h); st.g.setRot(0, ry, 0); st.g.visible = true; st.on = true; visEyes(st, true);
        VIS.face = new VisFace { st = st, t = 0.85f, chain = chain, done = false }; A.stinger(); stingerCD = 6; shake = 1; visSan(6); return true;
    }

    // ---------- 幻寺: a hall grows around you
    static bool hallFit(float yaw)
    {
        float c = cos(yaw), s = sin(yaw), y0 = groundH(player.x, player.z); string zn = zoneOfZ(player.z);
        foreach (float lz in new float[] { 5, 0, -8, -16, -24, -33 }) foreach (float lx in new[] { -5.6f, 0f, 5.6f })
            {
                float x = player.x + lx * c + lz * s, z = player.z - lx * s + lz * c;
                if (abs(x) > XB - 9 || z < ZN + 12 || z > ZS - 8 || interiorAt(x, z) != null || inPad(x, z, 2) || inMazeBand(z, 2) || zoneOfZ(z) != zn) return false;
                float dy = groundH(x, z) - y0; if (dy > 2.1f || dy < -2.6f) return false;
            }
        return true;
    }
    static bool startTemple(bool inBlink)
    {
        var H = VIS.hall; if (H == null || VIS.ep != null) return false; float? yawN = null;
        foreach (float d in new[] { 0f, 0.4f, -0.4f, 0.8f, -0.8f, 1.3f, -1.3f, 2f, -2f, PI }) { if (hallFit(player.yaw + d)) { yawN = player.yaw + d; break; } }
        if (!yawN.HasValue) return false; float yaw = yawN.Value;
        var pool = VIS.pool.Where(s => !s.on).ToList(); var endSt = pool.Find(s => s.name == "ironbuddha") ?? pool.Find(s => !s.stand); if (endSt == null) return false;
        var rest = pool.Where(s => s != endSt).ToList();
        var e = new VisEp { k = "temple", t = 0, yaw = yaw, ox = player.x, oz = player.z, oy = groundH(player.x, player.z), c = cos(yaw), s = sin(yaw), sideL = new List<VisSideSlot>(), end = endSt, fish = 0, done = false, said = false, on = false, lw = new List<float[]>() };
        Func<float, float, float[]> Wl = (lx, lz) => new[] { e.ox + lx * e.c + lz * e.s, e.oz - lx * e.s + lz * e.c };
        Action go = () =>
        {
            if (VIS.ep != e) return; H.g.setPos(e.ox, e.oy, e.oz); H.g.rotY = yaw; H.g.visible = true; VIS.roof = 1; e.on = true;
            int k = 0;
            foreach (float lz in new float[] { -5, -11, -17, -23 }) foreach (int sx in new[] { -1, 1 })
                {
                    if (k >= rest.Count) break; var st = rest[k++]; float[] p = Wl(sx * 3, lz);
                    visPut(st, p[0], p[1], st.stand ? RR(2.3f, 2.8f) : RR(2f, 2.5f), sink: 0.02f); e.sideL.Add(new VisSideSlot { st = st, sx = sx, lz = lz, moved = false });
                }
            float[] pe = Wl(0, -31.2f); visPut(endSt, pe[0], pe[1], 5.7f, sink: 0.03f, yaw: atan2(e.ox - pe[0], e.oz - pe[1]));
            foreach (var q in H.LP) { float[] w = Wl(q[0], q[2]); e.lw.Add(new[] { w[0], e.oy + q[1], w[1] }); }
            A.bell(0, 0.8f); if (!F("vTemple")) { setF("vTemple"); showMsg("……這裡，什麼時候有一座殿？", 4.5f, "warn"); }
        };
        VIS.ep = e; if (inBlink) go(); else visBlink(go, 0.3f); return true;
    }
    static void templeOff(VisEp e) { VIS.hall.g.visible = false; VIS.roof = 0; foreach (var q in e.sideL) visOff(q.st); visOff(e.end); }
    static void updTemple(VisEp e, float dt)
    {
        if (!e.on) return; e.t += dt; VIS.hold = 0.4f; float dx = player.x - e.ox, dz = player.z - e.oz, lx = dx * e.c - dz * e.s, lz = dx * e.s + dz * e.c;
        visSan(0.7f * dt); e.fish -= dt; if (e.fish <= 0) { e.fish = 0.85f; A.knock(RR(-0.3f, 0.3f), 0.09f, 0, true); }
        foreach (var q in e.sideL)
        {
            var st = q.st; if (!st.on) continue; bool seen = visSeen(st);
            if (!seen && !q.moved && q.lz > lz + 2.5f)
            {   // the ones behind you step into the aisle
                float x = e.ox + q.sx * 0.85f * e.c + q.lz * e.s, z = e.oz - q.sx * 0.85f * e.s + q.lz * e.c; st.x = x; st.z = z; st.y = groundH(x, z) - 0.02f * st.h; st.g.setPos(x, st.y, z); q.moved = true;
            }
            if (!seen) visTurn(st, 5, dt);
        }
        if (e.done) return; bool out_ = abs(lx) > 6.3f || lz > 5.6f || lz < -34.4f;
        if (!e.said && hypot(e.end.x - player.x, e.end.z - player.z) < 9.5f) { e.said = true; A.clip("arrived", vol: 1f); showMsg("「你來了。」", 3, "voice"); shake = 0.4f; e.t = max(e.t, 27.4f); }
        if (out_ || e.t > 29)
        {
            e.done = true; if (out_) { A.whisper(0, 0.5f); visSan(4); }
            visBlink(() => { if (VIS.ep != e) return; templeOff(e); visEnd(RR(45, 70)); if (!F("vTempleEnd")) { setF("vTempleEnd"); showMsg("殿不見了。你還站在林子裡。", 4); } }, 0.3f);
        }
    }

    // ---------- 行列: stone figures walking across the path
    static bool startProc()
    {
        var pool = VIS.pool.Where(s => !s.on && s.stand).ToList(); if (pool.Count < 5) return false;
        float fa = player.yaw + PI, dir = rnd() < 0.5f ? 1 : -1, D = RR(11, 14); string zn = zoneOfZ(player.z);
        float cx = player.x + sin(fa) * D, cz = player.z + cos(fa) * D, px = cos(fa) * dir, pz = -sin(fa) * dir, lat = D * Mathf.Tan(HFOV()) + 3.5f;
        foreach (float t in new[] { -lat, -lat / 2, 0f, lat / 2, lat })
        {
            float x = cx + px * t, z = cz + pz * t;
            if (abs(x) > XB - 5 || z < ZN + 8 || z > ZS - 5 || interiorAt(x, z) != null || inPad(x, z, 1) || inMazeBand(z, 2) || zoneOfZ(z) != zn) return false;
        }
        var e = new VisEp { k = "proc", t = 0, cx = cx, cz = cz, px = px, pz = pz, lat = lat, s0 = -lat, list = new List<VisProcSlot>(), stop = false, stopT = 0, fish = 0, done = false };
        int n = Mathf.Min(7, pool.Count);
        for (int i = 0; i < n; i++)
        {
            var st = pool[i]; float t = e.s0 - i * 2.7f, x = cx + px * t, z = cz + pz * t;
            visPut(st, x, z, RR(2.4f, 2.9f), yaw: atan2(px, pz), eyes: night > 0.5f); e.list.Add(new VisProcSlot { st = st, off = -i * 2.7f, ph = rnd() * 6 });
        }
        VIS.ep = e; return true;
    }
    static void updProc(VisEp e, float dt)
    {
        e.t += dt; float near = 1e9f; int any = 0; if (!e.stop) e.s0 += 1.7f * dt;
        foreach (var q in e.list)
        {
            var st = q.st; if (!st.on) continue; any++; float t = e.s0 + q.off;
            if (!e.stop)
            {
                float x = e.cx + e.px * t, z = e.cz + e.pz * t; st.x = x; st.z = z; st.y = groundH(x, z) + abs(sin(uTime * 2.7f + q.ph)) * 0.035f; st.g.setPos(x, st.y, z);
                if ((t > e.lat + 2 || e.t > 60) && !visSeen(st)) { visOff(st); continue; }
            }
            else visTurn(st, 5, dt);
            float d = hypot(st.x - player.x, st.z - player.z); if (d < near) near = d;
        }
        if (!e.stop)
        {
            e.fish -= dt;
            if (e.fish <= 0 && any > 0) { e.fish = 0.95f; var lead = e.list.Find(q => q.st.on); if (lead != null) { var v = voicePos(lead.st.x, lead.st.z); A.knock(v.pan, 0.11f * v.vol, 0, true); } }
            if (!F("vProc") && e.list[0].st.on && visSeen(e.list[0].st)) { setF("vProc"); showMsg("霧裡有一列石像，在走。", 4); }
            if (near < 7.5f)
            {
                e.stop = true; e.stopT = 1.8f; foreach (var q in e.list) visEyes(q.st, true); A.knock(0, 0.32f, 0, true); A.whisper(0, 0.5f); shake = 0.3f; visSan(5);
                if (!F("vProcStop")) { setF("vProcStop"); showMsg("祂們停下來了。全部轉過頭。", 3.2f, "warn"); }
            }
            else if (any == 0) visEnd();
        }
        else { VIS.hold = 0.4f; e.stopT -= dt; if (e.stopT <= 0 && !e.done) { e.done = true; visBlink(() => { if (VIS.ep != e) return; foreach (var q in e.list) visOff(q.st); visEnd(); }, 0.24f); } }
    }

    // ---------- 餘光: at the edge of your eye, gone when you look
    static bool startPeri() { VIS.ep = new VisEp { k = "peri", n = 0, wait = 0.05f, st = null, side = rnd() < 0.5f ? 1 : -1, life = 0, stay = false, stayT = 0 }; return true; }
    static readonly float[] PERI_D = { 8.5f, 6.3f, 4.4f };
    static bool periPlace(VisEp e)
    {
        var pool = VIS.pool.Where(s => !s.on && s.stand).ToList(); if (pool.Count == 0) return false; var st = pickR(pool); float hf = HFOV(), fa = player.yaw + PI;
        for (int k = 0; k < 8; k++)
        {
            float a = fa - e.side * hf * RR(0.72f, 0.86f), d = PERI_D[Mathf.Clamp(e.n, 0, 2)] * RR(0.9f, 1.1f), x = player.x + sin(a) * d, z = player.z + cos(a) * d;
            if (slotOK(x, z) && !blocked(player.x, player.z, x, z)) { visPut(st, x, z, RR(2.6f, 3.2f), sink: 0.02f); e.st = st; e.life = 11; return true; }
            if (k == 3) e.side = -e.side;
        }
        return false;
    }
    static void updPeri(VisEp e, float dt)
    {
        if (e.st == null) { e.wait -= dt; if (e.wait <= 0) { if (e.n >= 3 || abs(player.pitch) > 0.5f || !periPlace(e)) visEnd(); } return; }
        var st = e.st; float dx = st.x - player.x, dz = st.z - player.z, d = hypot(dx, dz); if (d == 0) d = 1; float fx = -sin(player.yaw), fz = -cos(player.yaw);
        float ang = Mathf.Acos(clamp((dx * fx + dz * fz) / d, -1, 1)); bool look = ang < HFOV() * 0.33f && abs(player.pitch) < 0.5f;
        e.life -= dt; Action next = () => { visOff(st); e.st = null; e.n++; e.side = -e.side; e.wait = RR(1.1f, 2.4f); };
        if (e.n < 2)
        {
            if (look || d < 2.6f) { var v = voicePos(st.x, st.z); A.whisper(v.pan, 0.35f); visSan(2); next(); } else if (e.life <= 0 && !visSeen(st)) next();
        }
        else
        {
            if (!visSeen(st)) visTurn(st, 5, dt);
            if (look && !e.stay) { e.stay = true; A.knock(0, 0.3f, 0, true); if (!F("vPeri")) { setF("vPeri"); showMsg("這一尊……沒有消失。", 3.5f, "warn"); } }
            if (e.stay)
            {
                e.stayT += dt; VIS.hold = 0.4f;
                if (!visSeen(st) || d < 1.6f) { visOff(st); e.st = null; e.n = 3; e.wait = 0.05f; say(behindPoint(5), new[] { "behind" }, vol: 1.1f); A.stepsBehind(4); visSan(4); }
                else if (e.stayT > 9 && !e.done) { e.done = true; visBlink(() => { visOff(st); if (VIS.ep == e) visEnd(); }, 0.2f); }
            }
            else if ((e.life <= 0 && !visSeen(st)) || d < 1.6f) { visOff(st); visEnd(); }
        }
    }

    // ---------- 全黑: every light goes out, and they are all looking
    static bool startDark()
    {
        var E = VIS.eyes; if (E == null) return false; VIS.ep = new VisEp { k = "dark", t = 0, n = 0, hb = 0, dur = 6.4f };
        for (int i = 0; i < E.N; i++)
        {
            float a = rnd() * TAU, d = RR(4.5f, 21), x = player.x + sin(a) * d, z = player.z + cos(a) * d, y = groundH(x, z) + RR(1.3f, 4.6f), k = 0.1f * (1 + d / 22f), ox = cos(a) * k, oz = -sin(a) * k;
            E.spr[i * 2].setPos(x - ox, y, z - oz); E.spr[i * 2 + 1].setPos(x + ox, y, z + oz); E.spr[i * 2].visible = false; E.spr[i * 2 + 1].visible = false;
        }
        E.p.visible = true; A.knock(0, 0.36f, 0, true); if (!F("vDark")) { setF("vDark"); showMsg("光，全部熄了。", 3, "warn"); }
        return true;
    }
    static void updDark(VisEp e, float dt)
    {
        e.t += dt; VIS.hold = 0.4f; var E = VIS.eyes; VIS.dark = min(1, e.t / 0.18f);
        if (e.t > 1.1f)
        {
            int n = Mathf.Min(E.N, floor((e.t - 1.1f) * 13));
            if (n != e.n) { e.n = n; for (int i = 0; i < E.N * 2; i++) E.spr[i].visible = i < n * 2; if (n % 6 == 1) A.whisper(RR(-1, 1), 0.3f); }
        }
        setOpacity(E.m, 0.8f + sin(uTime * 21) * 0.12f); e.hb -= dt; if (e.hb <= 0) { e.hb = 0.55f; A.heart(0.7f); }
        visSan(1 * dt);
        if (e.t >= e.dur)
        {
            E.p.visible = false; VIS.dark = 0; A.bell(0, 0.6f); shake = 0.4f;
            // when the light comes back, one of them is standing right there
            VisSt st = rnd() < 0.6f ? VIS.pool.Find(s => !s.on && s.stand) : null; float fa = player.yaw + PI, x = player.x + sin(fa) * 2.3f, z = player.z + cos(fa) * 2.3f;
            if (st != null && abs(player.pitch) < 0.4f && slotOK(x, z)) { visPut(st, x, z, 2.5f, sink: 0.02f); A.stinger(); stingerCD = 6; VIS.ep = new VisEp { k = "linger", st = st, life = 5, done = false }; VIS.last = "dark"; VIS.n++; }
            else visEnd();
        }
    }
    static void updLinger(VisEp e, float dt)
    {
        e.life -= dt; VIS.hold = 0.4f; var st = e.st;
        if (!visSeen(st) || hypot(st.x - player.x, st.z - player.z) < 1.1f) { visOff(st); visEnd(); }
        else if (e.life <= 0 && !e.done) { e.done = true; visBlink(() => { visOff(st); if (VIS.ep == e) visEnd(); }, 0.2f); }
    }

    // ---------- 紅雨
    static bool startRed() { if (weather.rain < 0.2f) return false; VIS.ep = new VisEp { k = "red", t = 0 }; if (!F("vRed")) { setF("vRed"); showMsg("雨……是紅的。", 3.5f, "warn"); } return true; }
    static void updRed(VisEp e, float dt) { e.t += dt; VIS.red = clamp(min(e.t / 2, (16 - e.t) / 2), 0, 1); if (e.t >= 16 || weather.rain < 0.1f) { VIS.red = 0; visEnd(RR(18, 30)); } }

    // ---------- 天上的臉 (look up) / 腳邊的頭 (look down)
    static void skyOn()
    {
        var S = VIS.sky; float fa = player.yaw + PI, x = player.x + sin(fa) * 38, z = player.z + cos(fa) * 38;
        S.g.setPos(x, player.y + 20, z); S.g.setRot(0.85f, atan2(player.x - x, player.z - z), 0); S.g.visible = true; S.on = true; S.a = 0; S.life = 9;
        A.rumble(); if (!F("vSky") || rnd() < 0.5f) { A.clip("lookme", vol: 0.9f, delay: 0.9f); showMsg("「看著我。」", 2.8f, "voice"); }
        setF("vSky");
    }
    static void updSky(float dt)
    {
        var S = VIS.sky; if (S == null) return; VIS.skyCool -= dt; bool up = player.pitch > 0.5f && player.inside == null;
        if (up)
        {
            VIS.upT += dt;
            if (!VIS.upRoll && VIS.upT > 0.3f)
            {
                VIS.upRoll = true; bool ring = VIS.ep != null && VIS.ep.k == "ring";
                if (!S.on && VIS.skyCool <= 0 && VIS.dark < 0.1f && VIS.roof < 0.5f && !player.safe && rnd() < (ring ? 0.6f : 0.45f)) skyOn();
            }
        }
        else { VIS.upT = 0; VIS.upRoll = false; }
        if (!S.on) return; S.life -= dt; bool want = player.pitch > 0.34f && player.inside == null && S.life > 0 && VIS.roof < 0.5f; S.a = clamp(S.a + (want ? dt / 1.3f : -dt / 0.45f), 0, 1);
        if (S.a <= 0 && !want) { S.on = false; S.g.visible = false; VIS.skyCool = RR(55, 95); return; }
        float ck = lerp(0.5f, 1.1f, night);
        foreach (var m in S.mats) { setOpacity(m, S.a * lerp(0.8f, 0.92f, night)); setRGB(m, fogC.r * ck + 0.36f * night, fogC.g * ck + 0.25f * night, fogC.b * ck + 0.23f * night); }
        setOpacity(S.sm, S.a * lerp(0.55f, 0.95f, night)); if (S.a > 0.5f) visSan(1.5f * dt);
    }
    static void feetOn()
    {
        int n = VIS.heads.Count; float a0 = rnd() * TAU;
        for (int i = 0; i < n; i++) { var h = VIS.heads[i]; float a = a0 + i / (float)n * TAU + RR(-0.2f, 0.2f), d = RR(0.72f, 1.05f), x = player.x + sin(a) * d, z = player.z + cos(a) * d; visPut(h, x, z, RR(0.5f, 0.65f), sink: 0.35f, pitch: -0.8f); }
        VIS.feet = new VisFeet { x = player.x, z = player.z, seen = false };
    }
    static void updFeet(float dt)
    {
        VIS.feetCool -= dt; var Ft = VIS.feet;
        if (Ft == null)
        {
            if (player.pitch < -0.1f)
            {
                if (!VIS.dnRoll)
                {
                    VIS.dnRoll = true; bool ring = VIS.ep != null && VIS.ep.k == "ring";
                    if (VIS.feetCool <= 0 && player.inside == null && !player.safe && VIS.roof < 0.5f && VIS.dark < 0.1f && VIS.heads.Count > 0 && rnd() < (ring ? 0.55f : 0.4f)) feetOn();
                }
            }
            else VIS.dnRoll = false;
            return;
        }
        float d = hypot(player.x - Ft.x, player.z - Ft.z); bool look = player.pitch < -0.55f && d < 2.2f;
        if (look && !Ft.seen) { Ft.seen = true; A.whisper(0, 0.5f); shake = 0.35f; if (!F("vFeet")) { setF("vFeet"); showMsg("腳邊……全是臉。", 3.5f, "warn"); } }
        if (Ft.seen && look) visSan(1.5f * dt);
        if (((player.pitch > -0.08f || d > 3.2f) && !VIS.heads.Any(h => h.on && visSeen(h))) || d > 10) { foreach (var h in VIS.heads) visOff(h); VIS.feet = null; VIS.feetCool = Ft.seen ? RR(50, 90) : RR(8, 16); }
    }

    // ---------- director
    static bool visCan() { return player.inside == null && !player.safe && !inMazeBand(player.z, 3) && !(PHANTOM != null && PHANTOM.active) && HL.trap == null && !finalEvent && !(player.danger > 0.05f) && !(player.grace > 0); }
    static string visPick()
    {
        if (!F("vRing")) return "ring";
        string[] ks = { "ring", "peri", "proc", "temple", "dark", "red" };
        float[] w = { 2.4f, 2.4f, 2.2f, 2.2f, night > 0.4f ? 2.6f : 1.3f, weather.rain > 0.25f ? 1.2f : 0f };
        for (int i = 0; i < ks.Length; i++) if (ks[i] == VIS.last) w[i] *= 0.2f;
        float tot = 0; for (int i = 0; i < w.Length; i++) tot += w[i]; float r = rnd() * tot;
        for (int i = 0; i < ks.Length; i++) { r -= w[i]; if (r <= 0) return ks[i]; }
        return "peri";
    }
    public static bool visTry(string k = null)
    {
        if (VIS.ep != null) return false; if (string.IsNullOrEmpty(k)) k = visPick(); bool ok;
        switch (k) { case "ring": ok = startRing(); break; case "temple": ok = startTemple(false); break; case "proc": ok = startProc(); break; case "peri": ok = startPeri(); break; case "dark": ok = startDark(); break; case "red": ok = startRed(); break; default: ok = false; break; }
        if (!ok) VIS.cool = 5; return ok;
    }
    static void visEnv(float dt)
    {
        var e = VIS.ep;
        VIS.chant = lerp(VIS.chant, e != null && (e.k == "ring" || e.k == "temple" || e.k == "proc") ? 1 : 0, 1 - exp(-dt * 1.4f));
        if (VIS.roof > 0.01f)
        {
            fogDensity *= 1.3f; fogC = Color.Lerp(fogC, FOG_TEMPLE, 0.82f); hemi.intensity *= 0.3f; rain.visible = false; moon.opacity = 0;
            if (e != null && e.lw != null && e.lw.Count > 0)
            {
                float[] a = null, b = null; float da = 1e9f, db = 1e9f;
                foreach (var q in e.lw) { float d = (q[0] - player.x) * (q[0] - player.x) + (q[2] - player.z) * (q[2] - player.z); if (d < da) { b = a; db = da; a = q; da = d; } else if (d < db) { b = q; db = d; } }
                float fl = 0.9f + sin(uTime * 9) * 0.07f;
                for (int i = 0; i < 2; i++)
                {
                    PLight lamp = i == 0 ? lampA : lampB; float[] q = i == 0 ? a : b; if (q == null) continue;
                    lamp.setPos(q[0], q[1] - 0.25f, q[2]); lamp.setRGB(1, 0.3f, 0.12f); lamp.distance = 17; lamp.intensity = (0.9f + 1.1f * night) * fl;
                }
            }
        }
        if (VIS.red > 0.01f) { fogC = Color.Lerp(fogC, FOG_RED, 0.5f * VIS.red); rain.color = Color.Lerp(RAIN_BASE, RAIN_RED, VIS.red); rain.opacity = min(0.9f, rain.opacity * (1 + 1.3f * VIS.red)); VIS.redDirty = true; }
        else if (VIS.redDirty) { rain.color = RAIN_BASE; VIS.redDirty = false; }
        if (VIS.dark > 0.01f)
        {
            float k = 1 - VIS.dark; fogC = new Color(fogC.r * k, fogC.g * k, fogC.b * k, 1); hemi.intensity *= lerp(1, 0.02f, VIS.dark); torch.intensity *= k; lampA.intensity *= k; lampB.intensity *= k;
            fogDensity *= 1 + 0.4f * VIS.dark; moon.opacity *= k; rain.opacity *= k;
            foreach (var L in LAMPS) if (L.lit && L.spMat != null) setOpacity(L.spMat, getOpacity(L.spMat) * k);
        }
        if (VIS.face != null) { lampB.setPos(camera.px, camera.py + 0.3f, camera.pz); lampB.setRGB(0.8f, 0.78f, 0.7f); lampB.distance = 5; lampB.intensity = 0.5f + 1.6f * night; }
        // rain was already stepped by updateEnv this frame; push the vision's colour / opacity / visibility changes through
        if (rain != null && (VIS.roof > 0.01f || VIS.red > 0.01f || VIS.dark > 0.01f || VIS.redDirty)) rain.step(0f);
    }
    public static void updateVision(float dt)
    {
        if (VIS.blink > 0)
        {
            VIS.blink -= dt; float p = 1 - max(0, VIS.blink) / VIS.blinkDur;
            if (p >= 0.42f && VIS.blinkFn != null) { var f = VIS.blinkFn; VIS.blinkFn = null; f(); }
            setBlink(p < 0.3f ? p / 0.3f : p < 0.55f ? 1 : max(0, 1 - (p - 0.55f) / 0.45f));
        }
        else if (VIS.blinkOp != 0) setBlink(0);
        if (VIS.hold > 0) VIS.hold -= dt;
        var e = VIS.ep;
        if (e != null)
        {
            if ((player.inside != null || player.safe) && e.k != "red")
            {
                if (!e.abort)
                {
                    e.abort = true;
                    visBlink(() => { if (VIS.ep == e) { visClear(); visEnd(RR(22, 36)); if (player.safe && !F("vSafe")) { setF("vSafe"); showMsg("燈火一照，那些東西就散了。", 3.5f); } } }, 0.2f);
                }
            }
            else if (!e.abort)
            {
                switch (e.k)
                {
                    case "ring": updRing(e, dt); break; case "temple": updTemple(e, dt); break; case "proc": updProc(e, dt); break; case "peri": updPeri(e, dt); break;
                    case "dark": updDark(e, dt); break; case "linger": updLinger(e, dt); break; case "red": updRed(e, dt); break;
                }
            }
        }
        else if (VIS.face == null && VIS.after == null && visCan()) { VIS.cool -= dt * (night > 0.5f ? 1.35f : 1) * (player.sanity < 50 ? 1.3f : 1); if (VIS.cool <= 0) visTry(); }
        if (VIS.after != null)
        {
            var a = VIS.after; a.life -= dt;
            if (player.inside != null || player.safe || a.life <= 0) VIS.after = null;
            else if (abs(player.pitch) < 0.22f) { a.t -= dt; if (a.t <= 0) { VIS.after = null; startFace(a.chain); } }
        }
        if (VIS.face != null)
        {
            var Fc = VIS.face; VIS.hold = 0.4f; Fc.t -= dt;
            if (Fc.t <= 0 && !Fc.done) { Fc.done = true; visBlink(() => { visOff(Fc.st); VIS.face = null; if (Fc.chain && VIS.ep == null && !player.safe && player.inside == null) startTemple(true); }, 0.26f); }
        }
        updSky(dt); updFeet(dt);
        VIS.sway = lerp(VIS.sway, (e != null && e.k != "red" && e.k != "proc" && e.k != "peri") || VIS.face != null || VIS.after != null ? 1 : 0, 1 - exp(-dt * 1.2f));
        if (VIS.sway > 0.01f) { float t = uTime; camera.rz += (sin(t * 0.83f) * 0.032f + sin(t * 2.1f) * 0.008f) * VIS.sway; camera.rx += sin(t * 0.61f) * 0.008f * VIS.sway; camera.apply(); }
        float ft = VIS.face != null ? 63 : 72; if (abs(camera.fov - ft) > 0.05f) { camera.fov = lerp(camera.fov, ft, 1 - exp(-dt * 16)); }
        visEnv(dt);
    }
}
