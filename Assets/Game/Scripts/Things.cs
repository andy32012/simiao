// Things: lamps, statues, steles, world-state declarations, path helpers, lanterns, chasers, pickups.
// Port of index.html lines 536-582, 604-608, 717-768. Everything in three.js coordinates.
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>findSpot options.</summary>
public class FO { public int tries = 200; public float padGap = 3; public float? pathGap; public float pathMax; public float gap = 1; public float lampGap = 6; }
/// <summary>makeChaser options.</summary>
public class CO { public string zone; public Maze maze; public Interior hall; public bool dormant; public float? yaw; }
public class ChaserKind { public string model; public float h, r; }
public class GateObj { public Obj g; public Obj[] doors; public Obj[,] locks; /*[door,k]*/ public ColRect col; public float open; public bool opening; public float y0; }
public class GhostObj { public Obj root; public bool active; public float cool, life, flashT; }
public class PhantomObj { public Obj root, arm; public Material mat; public bool active; public float cool, x, z, life, callT, warnT, fade, gone, dx, dz; public int hops, calls; }

public static partial class G
{
    // ================= lamps (lanterns, candles) =================
    public static readonly List<Lamp> LAMPS = new List<Lamp>();   // {x,y,z,lit,sp,power,range,flick}
    public static Lamp addLamp(float x, float y, float z, LO o = null)
    {
        thingsInit();
        if (o == null) o = new LO();
        float size = (o.size.HasValue && o.size.Value != 0) ? o.size.Value : 3.6f;
        var spMat = glowMat(C((o.color.HasValue && o.color.Value != 0) ? o.color.Value : 0xffa448), 1f, true);
        var sp = mkSprite(spMat, "lampGlow"); sp.setScale(size); sp.setPos(x, y, z); sp.visible = false;
        var L = new Lamp
        {
            x = x, y = y, z = z, lit = false, sp = sp, spMat = spMat,
            power = (o.power.HasValue && o.power.Value != 0) ? o.power.Value : 1f,
            range = (o.range.HasValue && o.range.Value != 0) ? o.range.Value : 17f,
            fl = rnd() * 10, color = C((o.color.HasValue && o.color.Value != 0) ? o.color.Value : 0xffa04a),
            indoor = o.indoor ?? false, baseSize = size
        };
        LAMPS.Add(L); return L;
    }
    public static void setLamp(Lamp L, bool on) { L.lit = on; L.sp.visible = on; }

    // ================= statues =================
    public static readonly List<Statue> STATUES = new List<Statue>();
    public static Mesh eyeGeo;
    public static Material eyeMatBlack, eyeMatNight, eyeMatHot, eyeSprNight, eyeSprHot, tearMat, paleTearMat;
    /// <summary>Create the shared eye / tear materials. Idempotent; called automatically by the factories below, but safe to call from boot.</summary>
    public static void thingsInit()
    {
        if (eyeGeo != null && eyeMatNight != null) return;
        eyeGeo = sphereGeo(1f, 10, 8);
        eyeMatBlack = basicMat(C(0x000000), false);
        eyeMatNight = basicMat(C(0x000000), true);
        eyeMatHot = basicMat(C(0xff2a10), true);
        eyeSprNight = glowMat(C(0xff2408), 0f, true);
        eyeSprHot = glowMat(C(0xff3010), 0.9f, true);
        tearMat = basicMat(C(0x2a0503), true, null, false, 1f, true);
        paleTearMat = litMat(C(0xe2dbd0), 1f, 0f, null, null, 1f, true);
    }
    static string numKey(float v) { return v.ToString("R", System.Globalization.CultureInfo.InvariantCulture); }
    public static Material modelMat(string name, ModelPart part, SO o)
    {
        if (o == null) o = new SO();
        string kk = (o.indoor ? "i" : "o") + (o.moss.HasValue ? numKey(o.moss.Value) : "") + (o.gray != 0 ? numKey(o.gray) : "") + (o.tintKey ?? "");
        if (!part.mats.TryGetValue(kk, out var m) || m == null)
        {
            var w = new W { moss = o.moss.HasValue ? o.moss.Value : (o.indoor ? 0.12f : 0.42f), gray = o.gray, tint = o.tint != null ? o.tint : new float[] { 1, 1, 1 }, amb = o.indoor ? 0.16f : 1f };
            // JS Object.assign(..., o.w||{}): the only override object ever passed sets tint2 / t2 / t2y (dead buddha heads)
            if (o.wx != null) { w.tint2 = o.wx.tint2; w.t2 = o.wx.t2; w.t2y = o.wx.t2y; }
            m = stdMat(part.map, part.normalMap, w);
            part.mats[kk] = m;
        }
        return m;
    }
    public static Obj buildModel(string name, SO o = null)
    {
        if (o == null) o = new SO();
        var M = MODELS[name]; var g = mkGroup(name);
        for (int i = 0; i < M.parts.Count; i++)
        {
            var p = M.parts[i];
            var m = mkMesh(p.geo, modelMat(name, p, o), !LOW && o.cast, true, name + "_" + i);
            g.add(m);
        }
        return g;
    }
    public static void addEyes(EyeHolder st, ModelInfo M, SO o = null)
    {
        thingsInit();
        if (o == null) o = new SO();
        var E = M.eyes; st.eyes = new List<Obj>(); st.sprs = new List<Obj>(); bool hot = o.hot;
        for (int i = 0; i < E.eyes.Length; i++)
        {
            var e = E.eyes[i];
            var m = mkMesh(eyeGeo, hot ? eyeMatHot : eyeMatNight, false, false, "eye"); m.setPos(e[0], e[1], e[2]); m.setScale(E.r * (o.eyeScale != 0 ? o.eyeScale : 1f)); st.g.add(m); st.eyes.Add(m);
            var s = mkSprite(hot ? eyeSprHot : eyeSprNight, "eyeGlow"); s.setPos(e[0], e[1], e[2] + E.r * 1.2f); s.setScale(E.r * 9); st.g.add(s); st.sprs.Add(s);
        }
        st.eyeY = E.eyes[0][1];
    }
    public static void setHot(EyeHolder st, bool hot)
    {
        if (st == null || st.eyes == null) return; st.hot = hot;
        foreach (var m in st.eyes) m.mat = hot ? eyeMatHot : eyeMatNight;
        foreach (var s in st.sprs) s.mat = hot ? eyeSprHot : eyeSprNight;
    }
    public static void addTears(Statue st, ModelInfo M, SO o = null)
    {
        thingsInit();
        if (o == null) o = new SO();
        var E = M.eyes; if (E == null || E.tears == null) return; var mb = new MB(); float w = E.r * (o.tearW != 0 ? o.tearW : 0.55f);
        if (o.strands)
        {
            float[] SX = { -0.75f, 0f, 0.7f };
            for (int k = 0; k < E.tears.Length; k++)
            {
                var tr = E.tears[k]; var e0 = E.eyes[Mathf.Min(k, E.eyes.Length - 1)];
                foreach (float sx in SX)
                {
                    int n = Mathf.Min(tr.Length, round((sx == 0 ? 8 : 5) + rand() * 2)); float[] prev = v3(e0[0] + sx * E.r, e0[1] - E.r * 0.35f, e0[2]);
                    for (int i = 0; i < n; i++)
                    {
                        float[] q = v3(tr[i][0] + sx * E.r * (1 + i * 0.03f), tr[i][1], tr[i][2]); float w0 = E.r * 0.34f * (1 - i / (float)n * 0.75f), w1 = E.r * 0.34f * (1 - (i + 1) / (float)n * 0.75f), zo = E.r * 0.14f;
                        mb.quad(v3(prev[0] - w0, prev[1], prev[2] + zo), v3(prev[0] + w0, prev[1], prev[2] + zo), v3(q[0] + w1, q[1], q[2] + zo), v3(q[0] - w1, q[1], q[2] + zo)); prev = q;
                    }
                }
            }
            if (mb.empty) return;
            var ms = mkMesh(mb.geo(), o.tearMat != null ? o.tearMat : tearMat, false, false, "tears"); ms.visible = false; st.g.add(ms); st.tears = ms; return;
        }
        for (int k = 0; k < E.tears.Length; k++)
        {
            var tr = E.tears[k]; if (tr.Length < 2) continue; float[] prev = E.eyes[Mathf.Min(k, E.eyes.Length - 1)];
            for (int i = 0; i < tr.Length; i++)
            {
                var q = tr[i]; float ww = w * (1 - i / (float)tr.Length * 0.6f);
                mb.quad(v3(prev[0] - ww, prev[1], prev[2] + E.r * 0.25f), v3(prev[0] + ww, prev[1], prev[2] + E.r * 0.25f), v3(q[0] + ww, q[1], q[2] + E.r * 0.25f), v3(q[0] - ww, q[1], q[2] + E.r * 0.25f)); prev = q;
            }
        }
        if (mb.empty) return;
        var m = mkMesh(mb.geo(), o.tearMat != null ? o.tearMat : tearMat, false, false, "tears"); m.visible = false; st.g.add(m); st.tears = m;
    }
    static float soScale(SO o) { return o.s != 0 ? o.s : o.h != 0 ? o.h : o.w != 0 ? o.w : 1f; }
    public static Statue placeStatue(string name, float x, float z, SO o = null)
    {
        thingsInit();
        if (o == null) o = new SO();
        if (!MODELS.TryGetValue(name, out var M) || M == null) { Debug.LogWarning("no model " + name); return null; }
        float s = soScale(o); var g = buildModel(name, o); float y = (o.y.HasValue ? o.y.Value : groundH(x, z)) - o.sink * s;
        g.setPos(x, y, z); g.setRot(o.pitch, o.yaw, o.roll); g.setScale(s);
        var st = new Statue
        {
            name = name, g = g, x = x, z = z, y = y, s = s, yaw = o.yaw, track = o.track, turn = o.turn != 0 ? o.turn : 0.7f, msg = o.msg,
            msgR = o.msgR != 0 ? o.msgR : Mathf.Max(14, s * 1.6f), msgDone = false, giant = s >= 7, hot = o.hot, height = M.d[2] * s,
            faceYaw = M.eyes != null ? M.eyes.faceYaw : 0f, indoor = o.indoor, curYaw = o.yaw
        };
        if (M.eyes != null && o.eyes) { addEyes(st, M, o); if (o.tears) addTears(st, M, o); }
        if (!(o.col.HasValue && o.col.Value == 0)) { float r = ((o.col.HasValue && o.col.Value != 0) ? o.col.Value : Mathf.Max(M.d[0], M.d[1]) * 0.4f) * s; st.col = addCircle(x, z, r, s > 3.5f); }
        cullAdd(g, x, y, z, Mathf.Max(M.d[0], M.d[2]) * s * 0.7f + 2, o.k != 0 ? o.k : 1.25f);
        STATUES.Add(st); return st;
    }
    public static void placeInst(string name, float x, float z, SO o = null)
    {
        if (o == null) o = new SO();
        if (!MODELS.TryGetValue(name, out var M) || M == null) { Debug.LogWarning("no model " + name); return; }
        float s = soScale(o); float y = (o.y.HasValue ? o.y.Value : groundH(x, z)) - o.sink * s;
        var pm = mtx(x, y, z, o.yaw, s, o.pitch, o.roll);
        for (int i = 0; i < M.parts.Count; i++)
        {
            var p = M.parts[i];
            SC.add(name + i + (o.indoor ? "i" : "") + (o.tintKey ?? ""), new ScDef { geo = p.geo, mat = modelMat(name, p, o), cell = o.cell != 0 ? o.cell : 40, cast = true, recv = true, r = s * 1.2f, k = o.k != 0 ? o.k : 1, far = o.far }, pm);
        }
        if (!(o.col.HasValue && o.col.Value == 0)) addCircle(x, z, ((o.col.HasValue && o.col.Value != 0) ? o.col.Value : Mathf.Max(M.d[0], M.d[1]) * 0.4f) * s, false);
    }

    // ================= steles =================
    public static readonly List<Stele> STELES = new List<Stele>();
    public static Material steleSideMat = null;
    static Mesh steleSideGeo, steleFrontGeo;
    static Texture2D thTex(string n) { return T.TryGetValue(n, out var t) ? t : null; }
    public static void makeStele(float x, float z, float yaw, string text)
    {
        if (steleSideMat == null) steleSideMat = stdMat(thTex("wall_d"), null, new W { moss = 0.5f, gray = 0.3f });
        if (steleSideGeo == null)
        {   // BoxGeometry(1.1, 2.4, 0.3): five faces with the side material, the +Z face separately for the inscription
            float x0 = -0.55f, x1 = 0.55f, y0 = -1.2f, y1 = 1.2f, z0 = -0.15f, z1 = 0.15f;
            var sb = new MB();
            sb.quad(v3(x1, y0, z0), v3(x0, y0, z0), v3(x0, y1, z0), v3(x1, y1, z0));   // -Z
            sb.quad(v3(x1, y0, z1), v3(x1, y0, z0), v3(x1, y1, z0), v3(x1, y1, z1));   // +X
            sb.quad(v3(x0, y0, z0), v3(x0, y0, z1), v3(x0, y1, z1), v3(x0, y1, z0));   // -X
            sb.quad(v3(x0, y1, z1), v3(x1, y1, z1), v3(x1, y1, z0), v3(x0, y1, z0));   // +Y
            sb.quad(v3(x0, y0, z0), v3(x1, y0, z0), v3(x1, y0, z1), v3(x0, y0, z1));   // -Y
            steleSideGeo = sb.geo();
            var fb = new MB(); fb.quad(v3(x0, y0, z1), v3(x1, y0, z1), v3(x1, y1, z1), v3(x0, y1, z1));   // +Z, uv 0..1
            steleFrontGeo = fb.geo();
        }
        var front = stdMat(steleTex(text), null, new W { moss = 0.34f });
        var m = mkGroup("stele");
        m.add(mkMesh(steleSideGeo, steleSideMat, !LOW, true, "steleSides"));
        m.add(mkMesh(steleFrontGeo, front, !LOW, true, "steleFront"));
        float y = groundH(x, z); m.setPos(x, y + 1.05f, z); m.setRot(0, yaw, R(-0.05f, 0.05f));
        addCircle(x, z, 0.75f); cullAdd(m, x, y, z, 3, 0.8f); STELES.Add(new Stele { x = x, z = z, text = text, read = false });
    }

    // ================= world state =================
    public static Maze m1, m2;
    public static List<PathObj> paths = new List<PathObj>();
    public static Material groundMat = null;
    public static Obj exitGlow = null; public static Material exitGlowMat = null;
    public static GhostObj ghost = null;
    public static readonly List<Lantern> LANTERNS = new List<Lantern>();
    public static readonly List<Chaser> CHASERS = new List<Chaser>();
    public static readonly List<Pickup> PICKUPS = new List<Pickup>();
    public static readonly List<Temple> TEMPLES = new List<Temple>();
    public static Kofun KOFUN = null; public static GateObj GATEOBJ = null; public static Statue FOURF = null, DAIBUTSU = null, WELL = null, BLEEDER = null; public static PhantomObj PHANTOM = null;
    /// <summary>千佛崖 position as (x, z) in a Vector2 (y = z), null until built.</summary>
    public static Vector2? NICHEPOS = null;
    public static Action<float, float, float> cliffFn = null;   // cliff(x, z, yaw)
    public static readonly List<Landmark> LANDMARKS = new List<Landmark>();
    public static readonly List<HeadTop> HEADTOPS = new List<HeadTop>();
    public static readonly List<Statue> DEADB = new List<Statue>();
    public static string zoneOfZ(float z) { return z > M1C.zS ? "forest" : z > M1C.zN ? "maze1" : z > M2C.zS ? "field" : z > M2C.zN ? "maze2" : "final"; }
    static Dictionary<string, float[]> _ZR;
    /// <summary>Z ranges of the three open zones: ZR["forest"|"field"|"final"] = {zMin, zMax}.</summary>
    public static Dictionary<string, float[]> ZR
    {
        get
        {
            if (_ZR == null) _ZR = new Dictionary<string, float[]> { { "forest", new[] { M1C.zS + 2, ZS - 6 } }, { "field", new[] { M2C.zS + 2, M1C.zN - 2 } }, { "final", new[] { ZN + 8, M2C.zN - 2 } } };
            return _ZR;
        }
    }

    public static float pathDist(float x, float z)
    {
        float best = 1e9f;
        for (int pi = 0; pi < paths.Count; pi++)
        {
            var p = paths[pi]; if (x < p.bb[0] - 30 || x > p.bb[1] + 30 || z < p.bb[2] - 30 || z > p.bb[3] + 30) continue; var P = p.pts;
            for (int i = 1; i < P.Count; i++)
            {
                float[] a = P[i - 1], b = P[i]; float vx = b[0] - a[0], vz = b[1] - a[1], wx = x - a[0], wz = z - a[1];
                float den = vx * vx + vz * vz; if (den == 0) den = 1;
                float t = clamp((wx * vx + wz * vz) / den, 0, 1); float dx = wx - vx * t, dz = wz - vz * t; float d = dx * dx + dz * dz; if (d < best) best = d;
            }
        }
        return Mathf.Sqrt(best);
    }
    public static PathObj addPath(List<float[]> pts)
    {
        float[] bb = { 1e9f, -1e9f, 1e9f, -1e9f };
        foreach (var p in pts) { bb[0] = min(bb[0], p[0]); bb[1] = max(bb[1], p[0]); bb[2] = min(bb[2], p[1]); bb[3] = max(bb[3], p[1]); }
        var o = new PathObj { pts = pts, bb = bb }; paths.Add(o); return o;
    }
    public static List<float[]> wiggle(float[] a, float[] b, int n, float amp)
    {
        var pts = new List<float[]>(); float ph = R(0, TAU), k = R(1.4f, 2.4f); float dx = b[0] - a[0], dz = b[1] - a[1], L = hypot(dx, dz); if (L == 0) L = 1; float px = -dz / L, pz = dx / L;
        for (int i = 0; i <= n; i++) { float t = i / (float)n, s = sin(t * PI * k + ph) * amp * sin(t * PI); pts.Add(v2(lerp(a[0], b[0], t) + px * s, lerp(a[1], b[1], t) + pz * s)); }
        return pts;
    }
    /// <summary>Point at fraction f of the polyline's length: {x, z, dirX, dirZ}.</summary>
    public static float[] pointAlong(List<float[]> P, float f)
    {
        float L = 0; var seg = new List<float>();
        for (int i = 1; i < P.Count; i++) { float l = hypot(P[i][0] - P[i - 1][0], P[i][1] - P[i - 1][1]); seg.Add(l); L += l; }
        float target = L * f;
        for (int i = 1; i < P.Count; i++)
        {
            if (target <= seg[i - 1])
            {
                float t = target / seg[i - 1]; float dx = P[i][0] - P[i - 1][0], dz = P[i][1] - P[i - 1][1], l = hypot(dx, dz); if (l == 0) l = 1;
                return new[] { lerp(P[i - 1][0], P[i][0], t), lerp(P[i - 1][1], P[i][1], t), dx / l, dz / l };
            }
            target -= seg[i - 1];
        }
        var q = P[P.Count - 1]; return new[] { q[0], q[1], 0f, -1f };
    }
    public static float[] nearestOn(List<float[]> P, float x, float z)
    {
        float best = 1e9f; float[] bp = P[0];
        for (int i = 1; i < P.Count; i++)
        {
            float[] a = P[i - 1], b = P[i]; float vx = b[0] - a[0], vz = b[1] - a[1]; float den = vx * vx + vz * vz; if (den == 0) den = 1;
            float t = clamp(((x - a[0]) * vx + (z - a[1]) * vz) / den, 0, 1); float qx = a[0] + vx * t, qz = a[1] + vz * t; float d = (x - qx) * (x - qx) + (z - qz) * (z - qz); if (d < best) { best = d; bp = v2(qx, qz); }
        }
        return bp;
    }
    public static bool inPad(float x, float z, float m) { for (int i = 0; i < PADS.Count; i++) { var p = PADS[i]; if (abs(x - p.x) < p.hx + m && abs(z - p.z) < p.hz + m) return true; } return false; }
    public static bool inMazeBand(float z, float m) { return (z < M1C.zS + m && z > M1C.zN - m) || (z < M2C.zS + m && z > M2C.zN - m); }
    /// <summary>Random free spot {x, z} in the given ranges, or null.</summary>
    public static float[] findSpot(float[] xr, float[] zr, float r, FO o = null)
    {
        if (o == null) o = new FO();
        int tries = o.tries != 0 ? o.tries : 200;
        for (int t = 0; t < tries; t++)
        {
            float x = R(xr[0], xr[1]), z = R(zr[0], zr[1]);
            if (inMazeBand(z, r + 2) || inPad(x, z, r + (o.padGap != 0 ? o.padGap : 3))) continue;
            if (pathDist(x, z) < r + (o.pathGap.HasValue ? o.pathGap.Value : 4)) continue;
            if (o.pathMax != 0 && pathDist(x, z) > o.pathMax) continue;
            if (hypot(x - START.x, z - START.y) < r + 14) continue;
            if (!canPlace(x, z, r + (o.gap != 0 ? o.gap : 1))) continue;
            bool nearLamp = false; float lg = r + (o.lampGap != 0 ? o.lampGap : 6);
            for (int i = 0; i < LANTERNS.Count; i++) if (hypot(LANTERNS[i].x - x, LANTERNS[i].z - z) < lg) { nearLamp = true; break; }
            if (nearLamp) continue;
            return new[] { x, z };
        }
        return null;
    }
    public static float faceTo(float x, float z, float tx, float tz) { return atan2(tx - x, tz - z); }

    static Material lanternCoreMat; static Mesh lanternCoreGeo;
    public static Lantern makeLantern(float x, float z, string kind = null)
    {
        string name = !string.IsNullOrEmpty(kind) ? kind : (LANTERNS.Count % 2 != 0 ? "lantern_b" : "lantern_a"); float h = 2.15f; resolve(ref x, ref z, 0.7f);
        var st = placeStatue(name, x, z, new SO { h = h, yaw = R(0, TAU), moss = 0.5f, col = 0.36f, sink = 0.02f, k = 1.1f, cast = false }); float fy = st.y + h * (name == "lantern_a" ? 0.672f : 0.682f);
        var L = addLamp(x, fy, z, new LO { size = 4.2f, power = 1.25f, range = 18 });
        if (lanternCoreMat == null) lanternCoreMat = basicMat(C(0xffc070), true);
        if (lanternCoreGeo == null) lanternCoreGeo = boxGeo(0.2f, 0.2f, 0.2f);
        var core = mkMesh(lanternCoreGeo, lanternCoreMat, false, false, "lanternCore"); core.setPos(x, fy, z); core.visible = false;
        var o = new Lantern { x = x, z = z, y = st.y, lit = false, L = L, core = core }; LANTERNS.Add(o); return o;
    }
    public static void setLantern(Lantern o, bool on) { o.lit = on; setLamp(o.L, on); o.core.visible = on; }

    // ----- chasers
    public static readonly Dictionary<string, ChaserKind> CK = new Dictionary<string, ChaserKind>
    {
        { "arhat", new ChaserKind { model = "arhat", h = 1.78f, r = 0.4f } },
        { "monk", new ChaserKind { model = "monk", h = 1.25f, r = 0.5f } },
        { "ascetic", new ChaserKind { model = "ascetic", h = 1.95f, r = 0.4f } }
    };
    public static Chaser makeChaser(string kind, float x, float z, CO o = null)
    {
        thingsInit();
        if (o == null) o = new CO();
        var K = CK[kind]; var M = MODELS[K.model]; var g = buildModel(K.model, new SO { indoor = o.hall != null, moss = o.hall != null ? 0.08f : 0.3f, cast = true });
        g.setScale(K.h); g.setPos(x, groundH(x, z), z); g.rotY = (o.yaw.HasValue && o.yaw.Value != 0) ? o.yaw.Value : R(0, TAU);
        var c = new Chaser
        {
            kind = kind, g = g, x = x, z = z, hx = x, hz = z, hyaw = g.rotY, zone = o.zone, maze = o.maze, hall = o.hall, dormant = o.dormant, seen = false, moved = 0, stuck = 0, sideT = 0, side = 1, h = K.h, r = K.r,
            faceYaw = M.eyes != null ? M.eyes.faceYaw : 0f, headY = (M.eyes != null ? M.eyes.eyes[0][1] : 0.88f) * K.h, vis = true
        };
        if (M.eyes != null) { addEyes(c, M, new SO { eyeScale = 1.25f }); if (o.hall != null) setHot(c, false); }
        CHASERS.Add(c); return c;
    }

    // ----- pickups
    static Material keyMat; static Mesh keyRingGeo, keyShaftGeo, keyBitGeo, charmGeo; static Material charmMat;
    public static Pickup makePickup(string type, float x, float y, float z, Action onTake = null, Interior hall = null)
    {
        thingsInit();
        var g = mkGroup("pickup_" + type); g.setPos(x, y, z);
        if (type == "key")
        {
            if (keyMat == null)
            {
                keyMat = litMat(C(0xc9a04a), 0.35f, 0.9f, null, C(0x5a3c08), 0.9f);
                keyRingGeo = torusGeo(0.07f, 0.018f, 8, 20); keyShaftGeo = cylGeo(0.014f, 0.014f, 0.2f, 8); keyBitGeo = boxGeo(0.06f, 0.018f, 0.014f);
            }
            var ring = mkMesh(keyRingGeo, keyMat, false, false, "keyRing"); ring.setPos(0, 0.11f, 0); g.add(ring);
            var sh_ = mkMesh(keyShaftGeo, keyMat, false, false, "keyShaft"); sh_.setPos(0, -0.06f, 0); g.add(sh_);
            foreach (float dy in new[] { -0.13f, -0.08f }) { var b = mkMesh(keyBitGeo, keyMat, false, false, "keyBit"); b.setPos(0.035f, dy, 0); g.add(b); }
        }
        else
        {
            if (charmMat == null)
            {
                hallMats();
                charmMat = basicMat(Color.white, true, ofudaTexture, false, 1f, true);
                charmGeo = planeGeo(0.11f, 0.3f, 0.25f, 0.5f);
            }
            g.add(mkMesh(charmGeo, charmMat, false, false, "charm"));
        }
        var sp = mkSprite(glowMat(C(type == "key" ? 0xffc060 : 0xfff0c0), 0.55f, true), "pickupGlow"); sp.setScale(type == "key" ? 1.3f : 0.9f); g.add(sp);
        var p = new Pickup { type = type, x = x, y = y, z = z, g = g, taken = false, onTake = onTake, hall = hall, id = PICKUPS.Count }; PICKUPS.Add(p); return p;
    }
}
