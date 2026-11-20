// Core: utilities, layout constants, terrain, maze, collision hash, mesh builder.  (index.html lines 260-385, 491-516)
// Everything here works in three.js coordinates. Conversion to Unity happens only in MB.geo()/meshFromArrays()/Obj/Scatter.
using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class G
{
    // ================= utilities =================
    public const float PI = Mathf.PI, TAU = Mathf.PI * 2f, HP = Mathf.PI / 2f;
    public static float clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }
    public static float lerp(float a, float b, float t) { return a + (b - a) * t; }
    public static float smooth(float e0, float e1, float x) { float t = (x - e0) / (e1 - e0); t = t < 0 ? 0 : t > 1 ? 1 : t; return t * t * (3 - 2 * t); }
    public static float wrapA(float a) { while (a > PI) a -= TAU; while (a < -PI) a += TAU; return a; }
    public static float hypot(float a, float b) { return Mathf.Sqrt(a * a + b * b); }
    public static float hypot(float a, float b, float c) { return Mathf.Sqrt(a * a + b * b + c * c); }
    public static float sin(float a) { return Mathf.Sin(a); }
    public static float cos(float a) { return Mathf.Cos(a); }
    public static float atan2(float y, float x) { return Mathf.Atan2(y, x); }
    public static float abs(float a) { return a < 0 ? -a : a; }
    public static float min(float a, float b) { return a < b ? a : b; }
    public static float max(float a, float b) { return a > b ? a : b; }
    public static float pow(float a, float b) { return Mathf.Pow(a, b); }
    public static float exp(float a) { return Mathf.Exp(a); }
    public static int floor(float a) { return Mathf.FloorToInt(a); }
    public static int round(float a) { return Mathf.FloorToInt(a + 0.5f); }   // JS Math.round
    public static int ceil(float a) { return Mathf.CeilToInt(a); }

    public static Func<float> mulberry32(int seed)
    {
        uint a = unchecked((uint)seed);
        return () =>
        {
            unchecked
            {
                a += 0x6D2B79F5u; uint t = a;
                t = (t ^ (t >> 15)) * (1u | t);
                t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
                return Mathf.Min((float)((t ^ (t >> 14)) / 4294967296.0), 0.99999994f);
            }
        };
    }
    public static int SEED;
    public static Func<float> rand = mulberry32(1);
    /// <summary>Re-seed the deterministic world generator (call before buildWorld).</summary>
    public static void seedWorld(int seed) { SEED = seed; rand = mulberry32(seed); }
    /// <summary>Seeded range (JS R).</summary>
    public static float R(float a, float b) { return a + (b - a) * rand(); }
    /// <summary>Math.random()</summary>
    public static float rnd() { return Mathf.Min(UnityEngine.Random.value, 0.99999994f); }
    /// <summary>Unseeded range (JS RR).</summary>
    public static float RR(float a, float b) { return a + (b - a) * rnd(); }
    public static T pick<T>(IList<T> arr) { return arr[floor(rand() * arr.Count)]; }
    public static T pickR<T>(IList<T> arr) { return arr[floor(rnd() * arr.Count)]; }
    public static float NOX = 0, NOZ = 0;
    public static float hash2(int ix, int iz)
    {
        unchecked
        {
            uint h = (uint)(ix * 374761393 + iz * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
            return (float)(h / 4294967296.0);
        }
    }
    public static float vnoise(float x, float z)
    {
        int ix = floor(x), iz = floor(z); float fx = x - ix, fz = z - iz; float ux = fx * fx * (3 - 2 * fx), uz = fz * fz * (3 - 2 * fz);
        float a = hash2(ix, iz), b = hash2(ix + 1, iz), c = hash2(ix, iz + 1), d = hash2(ix + 1, iz + 1);
        return (a + (b - a) * ux + (c - a) * uz + (a - b - c + d) * ux * uz) * 2 - 1;
    }
    public static float[] v3(float x, float y, float z) { return new[] { x, y, z }; }
    public static float[] v2(float x, float z) { return new[] { x, z }; }
    /// <summary>Hex colour -> Color holding the raw channel values. The original used these as LINEAR values, and all QF shaders take them as linear vectors.</summary>
    public static Color C(int hex, float a = 1f) { return new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, a); }
    public static Color C(string css) { return C(Convert.ToInt32(css.TrimStart('#'), 16)); }
    public static Color C(float[] rgb) { return new Color(rgb[0], rgb[1], rgb[2], 1f); }

    // ================= layout constants =================
    public const float HOUR_SEC = 13, SAFE_R = 7, EYE = 1.62f;
    public const float XB = 146, ZS = 336, ZN = -336;
    public static readonly Vector2 START = new Vector2(0, 320);   // (x, z)
    public static readonly Vector2 GATE = new Vector2(0, -322);   // (x, z)
    public static readonly int[] DC = { 0, 1, 0, -1 }, DR = { 1, 0, -1, 0 };
    public class MazeCfg { public int cols, rows; public float cell, x0, zS, T, H, braid, zN, hw; }
    public static readonly MazeCfg M1C = new MazeCfg { cols = 26, rows = 11, cell = 8, x0 = -104, zS = 160, T = 1.4f, H = 7.6f, braid = 0.08f, zN = 160 - 11 * 8, hw = 104 };
    public static readonly MazeCfg M2C = new MazeCfg { cols = 20, rows = 6, cell = 10, x0 = -100, zS = -104, T = 1.7f, H = 11, braid = 0.1f, zN = -104 - 6 * 10, hw = 100 };
    public static readonly List<Pad> PADS = new List<Pad>();
    public static float mazeFlat(float z)
    {
        float a = smooth(M1C.zS + 8, M1C.zS, z) * smooth(M1C.zN - 8, M1C.zN, z);
        float b = smooth(M2C.zS + 8, M2C.zS, z) * smooth(M2C.zN - 8, M2C.zN, z);
        float c = smooth(-262, -276, z) * 0.92f;
        return max(a, max(b, c));
    }
    public static float baseH(float x, float z)
    {
        float b = (ZS - z) * 0.045f;
        float n = vnoise(x * 0.03f + NOX, z * 0.03f + NOZ) * 3.1f + vnoise(x * 0.11f + NOZ, z * 0.11f + NOX) * 0.7f;
        n *= 1 - 0.9f * mazeFlat(z);
        return b + n;
    }
    public static float terrainH(float x, float z)
    {
        float h = baseH(x, z);
        for (int i = 0; i < PADS.Count; i++)
        {
            var p = PADS[i]; float dx = max(abs(x - p.x) - p.hx, 0), dz = max(abs(z - p.z) - p.hz, 0);
            if (dx < p.m && dz < p.m) { float d = hypot(dx, dz); if (d < p.m) h = lerp(h, p.h + (p.bump != null ? p.bump(x, z) : 0), 1 - smooth(0, p.m, d)); }
        }
        float ax = abs(x);
        h += smooth(XB - 14, XB + 6, ax) * 10 + smooth(ZS - 8, ZS + 8, z) * 9 + smooth(ZN + 10, ZN - 6, z) * 9;
        h += smooth(M1C.hw - 1, M1C.hw + 9, ax) * smooth(M1C.zS + 10, M1C.zS - 2, z) * smooth(M1C.zN - 10, M1C.zN + 2, z) * 8;
        h += smooth(M2C.hw - 1, M2C.hw + 9, ax) * smooth(M2C.zS + 10, M2C.zS - 2, z) * smooth(M2C.zN - 10, M2C.zN + 2, z) * 8;
        return h;
    }
    /// <summary>Walkable floors. NOTE: JS used FLOORS.unshift() for steps -> use FLOORS.Insert(0, ...).</summary>
    public static readonly List<FloorRect> FLOORS = new List<FloorRect>();
    public static float groundH(float x, float z)
    {
        for (int i = 0; i < FLOORS.Count; i++) { var f = FLOORS[i]; if (x >= f.minx && x <= f.maxx && z >= f.minz && z <= f.maxz) return f.fn(x, z); }
        return terrainH(x, z);
    }

    // ================= collision hash =================
    const float CELL = 6;
    static readonly Dictionary<int, List<ColCircle>> circB = new Dictionary<int, List<ColCircle>>();
    static readonly Dictionary<int, List<ColRect>> rectB = new Dictionary<int, List<ColRect>>();
    static int STAMP = 1;
    static int ckey(int ix, int iz) { return (ix + 600) * 4096 + (iz + 600); }
    public static void clearCollision() { circB.Clear(); rectB.Clear(); }
    public static ColCircle addCircle(float x, float z, float r, bool occ = false)
    {
        var o = new ColCircle { x = x, z = z, r = r, occ = occ };
        int a = floor((x - r) / CELL), b = floor((x + r) / CELL), c = floor((z - r) / CELL), d = floor((z + r) / CELL);
        for (int ix = a; ix <= b; ix++) for (int iz = c; iz <= d; iz++) { int k = ckey(ix, iz); if (!circB.TryGetValue(k, out var l)) { l = new List<ColCircle>(); circB[k] = l; } l.Add(o); }
        return o;
    }
    public static ColRect addRect(float minx, float maxx, float minz, float maxz, bool occ = true)
    {
        if (minx > maxx) { float t = minx; minx = maxx; maxx = t; }
        if (minz > maxz) { float t = minz; minz = maxz; maxz = t; }
        var o = new ColRect { minx = minx, maxx = maxx, minz = minz, maxz = maxz, occ = occ };
        int a = floor(minx / CELL), b = floor(maxx / CELL), c = floor(minz / CELL), d = floor(maxz / CELL);
        for (int ix = a; ix <= b; ix++) for (int iz = c; iz <= d; iz++) { int k = ckey(ix, iz); if (!rectB.TryGetValue(k, out var l)) { l = new List<ColRect>(); rectB[k] = l; } l.Add(o); }
        return o;
    }
    /// <summary>Push the point (x,z) with radius r out of all colliders (JS resolve(p,r) where p={x,z}).</summary>
    public static void resolve(ref float px, ref float pz, float r)
    {
        STAMP++;
        int a = floor((px - r) / CELL), b = floor((px + r) / CELL), c = floor((pz - r) / CELL), d = floor((pz + r) / CELL);
        for (int ix = a; ix <= b; ix++) for (int iz = c; iz <= d; iz++)
            {
                int k = ckey(ix, iz);
                if (circB.TryGetValue(k, out var cl)) foreach (var o in cl)
                    {
                        if (o.s == STAMP || o.off) continue; o.s = STAMP; float dx = px - o.x, dz = pz - o.z, m = r + o.r, d2 = dx * dx + dz * dz;
                        if (d2 < m * m) { if (d2 < 1e-8f) { px = o.x + m; } else { float dd = Mathf.Sqrt(d2); px = o.x + dx / dd * m; pz = o.z + dz / dd * m; } }
                    }
                if (rectB.TryGetValue(k, out var rl)) foreach (var o in rl)
                    {
                        if (o.s == STAMP || o.off) continue; o.s = STAMP; float qx = clamp(px, o.minx, o.maxx), qz = clamp(pz, o.minz, o.maxz); float dx = px - qx, dz = pz - qz, d2 = dx * dx + dz * dz;
                        if (d2 < r * r)
                        {
                            if (d2 > 1e-8f) { float dd = Mathf.Sqrt(d2); px = qx + dx / dd * r; pz = qz + dz / dd * r; }
                            else
                            {
                                float l = px - o.minx, rr = o.maxx - px, t = pz - o.minz, bb = o.maxz - pz, mn = min(min(l, rr), min(t, bb));
                                if (mn == l) px = o.minx - r; else if (mn == rr) px = o.maxx + r; else if (mn == t) pz = o.minz - r; else pz = o.maxz + r;
                            }
                        }
                    }
            }
    }
    public static bool canPlace(float x, float z, float r)
    {
        STAMP++;
        int a = floor((x - r - 14) / CELL), b = floor((x + r + 14) / CELL), c = floor((z - r - 14) / CELL), d = floor((z + r + 14) / CELL);
        for (int ix = a; ix <= b; ix++) for (int iz = c; iz <= d; iz++)
            {
                int k = ckey(ix, iz);
                if (circB.TryGetValue(k, out var cl)) foreach (var o in cl) { if (o.s == STAMP) continue; o.s = STAMP; float m = o.r + r; if ((x - o.x) * (x - o.x) + (z - o.z) * (z - o.z) < m * m) return false; }
                if (rectB.TryGetValue(k, out var rl)) foreach (var o in rl) { if (o.s == STAMP) continue; o.s = STAMP; float qx = clamp(x, o.minx, o.maxx), qz = clamp(z, o.minz, o.maxz); if ((x - qx) * (x - qx) + (z - qz) * (z - qz) < r * r) return false; }
            }
        return true;
    }
    /// <summary>Line of sight test on the ground plane: true if an occluder lies between a and b.</summary>
    public static bool blocked(float ax, float az, float bx, float bz)
    {
        float dx = bx - ax, dz = bz - az, L = hypot(dx, dz); int n = ceil(L / 0.9f);
        for (int i = 1; i < n; i++)
        {
            float x = ax + dx * i / n, z = az + dz * i / n; int k = ckey(floor(x / CELL), floor(z / CELL));
            if (rectB.TryGetValue(k, out var rl)) foreach (var o in rl) if (o.occ && !o.off && x > o.minx && x < o.maxx && z > o.minz && z < o.maxz) return true;
            if (circB.TryGetValue(k, out var cl)) foreach (var o in cl) if (o.occ && !o.off && (x - o.x) * (x - o.x) + (z - o.z) * (z - o.z) < o.r * o.r * 0.75f) return true;
        }
        return false;
    }

    // ================= three -> unity conversion helpers =================
    /// <summary>three.js position -> Unity position.</summary>
    public static Vector3 U3(float x, float y, float z) { return new Vector3(x, y, -z); }
    /// <summary>three.js Euler (order YXZ) -> Unity quaternion (mirrored on Z).</summary>
    public static Quaternion Q3(float rx, float ry, float rz)
    {
        float c1 = Mathf.Cos(rx / 2), c2 = Mathf.Cos(ry / 2), c3 = Mathf.Cos(rz / 2), s1 = Mathf.Sin(rx / 2), s2 = Mathf.Sin(ry / 2), s3 = Mathf.Sin(rz / 2);
        float x = s1 * c2 * c3 + c1 * s2 * s3, y = c1 * s2 * c3 - s1 * c2 * s3, z = c1 * c2 * s3 - s1 * s2 * c3, w = c1 * c2 * c3 + s1 * s2 * s3;
        return new Quaternion(-x, -y, z, w);
    }
    /// <summary>Compose a transform matrix in three.js space (JS mtx()). Pass the result to SC.add / multiply with other three-space matrices.</summary>
    public static Matrix4x4 mtx(float x, float y, float z, float ry, float s, float rx = 0, float rz = 0, float sy = float.NaN)
    {
        float c1 = Mathf.Cos(rx / 2), c2 = Mathf.Cos(ry / 2), c3 = Mathf.Cos(rz / 2), s1 = Mathf.Sin(rx / 2), s2 = Mathf.Sin(ry / 2), s3 = Mathf.Sin(rz / 2);
        var q = new Quaternion(s1 * c2 * c3 + c1 * s2 * s3, c1 * s2 * c3 - s1 * c2 * s3, c1 * c2 * s3 - s1 * s2 * c3, c1 * c2 * c3 + s1 * s2 * s3);
        return Matrix4x4.TRS(new Vector3(x, y, z), q, new Vector3(s, float.IsNaN(sy) ? s : sy, s));
    }
    /// <summary>three-space matrix -> Unity-space matrix (S*M*S with S = diag(1,1,-1)).</summary>
    public static Matrix4x4 toUnity(Matrix4x4 m)
    {
        m.m02 = -m.m02; m.m12 = -m.m12; m.m20 = -m.m20; m.m21 = -m.m21; m.m23 = -m.m23; m.m32 = -m.m32; return m;
    }

    /// <summary>Build a Unity mesh from three-space arrays. tris use three.js (CCW) winding. nor may be null (computed). colors optional.</summary>
    public static Mesh meshFromArrays(Vector3[] pos, Vector3[] nor, Vector2[] uv, int[] tris, Color[] colors = null, bool tangents = true)
    {
        int n = pos.Length; var p = new Vector3[n];
        for (int i = 0; i < n; i++) p[i] = new Vector3(pos[i].x, pos[i].y, -pos[i].z);
        int[] t;
        if (tris == null) { t = new int[n]; for (int i = 0; i < n; i += 3) { t[i] = i; t[i + 1] = i + 2; t[i + 2] = i + 1; } }
        else { t = new int[tris.Length]; for (int i = 0; i < tris.Length; i += 3) { t[i] = tris[i]; t[i + 1] = tris[i + 2]; t[i + 2] = tris[i + 1]; } }
        var m = new Mesh(); if (n > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = p; if (uv != null) m.uv = uv; if (colors != null) m.colors = colors; m.triangles = t;
        if (nor != null) { var nn = new Vector3[n]; for (int i = 0; i < n; i++) nn[i] = new Vector3(nor[i].x, nor[i].y, -nor[i].z).normalized; m.normals = nn; }
        else m.RecalculateNormals();
        if (tangents && uv != null) m.RecalculateTangents();
        m.RecalculateBounds(); return m;
    }

    // ---- primitive geometries (three.js equivalents, all centred like three.js) ----
    /// <summary>BoxGeometry. uvUnit>0 tiles the UVs by size/uvUnit (replaces the JS boxUV helper); 0 keeps 0..1 per face.</summary>
    public static Mesh boxGeo(float sx, float sy, float sz, float uvUnit = 0)
    {
        var b = new MB();
        if (uvUnit > 0) b.box(0, 0, 0, sx, sy, sz, uvUnit);
        else
        {
            float x0 = -sx / 2, x1 = sx / 2, y0 = -sy / 2, y1 = sy / 2, z0 = -sz / 2, z1 = sz / 2;
            b.quad(v3(x0, y0, z1), v3(x1, y0, z1), v3(x1, y1, z1), v3(x0, y1, z1)); b.quad(v3(x1, y0, z0), v3(x0, y0, z0), v3(x0, y1, z0), v3(x1, y1, z0));
            b.quad(v3(x1, y0, z1), v3(x1, y0, z0), v3(x1, y1, z0), v3(x1, y1, z1)); b.quad(v3(x0, y0, z0), v3(x0, y0, z1), v3(x0, y1, z1), v3(x0, y1, z0));
            b.quad(v3(x0, y1, z1), v3(x1, y1, z1), v3(x1, y1, z0), v3(x0, y1, z0)); b.quad(v3(x0, y0, z0), v3(x1, y0, z0), v3(x1, y0, z1), v3(x0, y0, z1));
        }
        return b.geo();
    }
    /// <summary>PlaneGeometry(w,h): in the XY plane, facing +Z (three space), uv 0..1. (u0..u1 lets you pick a horizontal slice of the texture.)</summary>
    public static Mesh planeGeo(float w, float h, float u0 = 0, float u1 = 1)
    {
        var b = new MB(); b.quad(v3(-w / 2, -h / 2, 0), v3(w / 2, -h / 2, 0), v3(w / 2, h / 2, 0), v3(-w / 2, h / 2, 0), u0, 0, u1, 1); return b.geo();
    }
    public static Mesh sphereGeo(float r, int seg = 10, int rings = 8)
    {
        var b = new MB();
        for (int j = 0; j < rings; j++) for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * TAU, a1 = (i + 1) / (float)seg * TAU, p0 = j / (float)rings * PI, p1 = (j + 1) / (float)rings * PI;
                Func<float, float, float[]> P = (a, p) => v3(r * Mathf.Sin(p) * Mathf.Cos(a), r * Mathf.Cos(p), r * Mathf.Sin(p) * Mathf.Sin(a));
                b.quad(P(a1, p1), P(a0, p1), P(a0, p0), P(a1, p0));
            }
        var g = b.geo(false, true); return g;
    }
    /// <summary>CylinderGeometry(rTop, rBottom, h, seg) centred on the origin, optional caps.</summary>
    public static Mesh cylGeo(float rTop, float rBottom, float h, int seg = 8, bool open = false, float y0 = float.NaN)
    {
        var b = new MB(); float yb = float.IsNaN(y0) ? -h / 2 : y0; b.cyl(0, yb, 0, rBottom, h, seg, 1, rTop);
        if (!open) for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * TAU, a1 = (i + 1) / (float)seg * TAU;
                b.tri(v3(0, yb + h, 0), v3(Mathf.Cos(a1) * rTop, yb + h, Mathf.Sin(a1) * rTop), v3(Mathf.Cos(a0) * rTop, yb + h, Mathf.Sin(a0) * rTop));
                b.tri(v3(0, yb, 0), v3(Mathf.Cos(a0) * rBottom, yb, Mathf.Sin(a0) * rBottom), v3(Mathf.Cos(a1) * rBottom, yb, Mathf.Sin(a1) * rBottom));
            }
        return b.geo();
    }
    /// <summary>TorusGeometry(R, tube, radialSeg, tubularSeg) lying in the XY plane.</summary>
    public static Mesh torusGeo(float R_, float tube, int rs = 8, int ts = 20)
    {
        var b = new MB();
        Func<float, float, float[]> P = (u, v) => v3((R_ + tube * Mathf.Cos(v)) * Mathf.Cos(u), (R_ + tube * Mathf.Cos(v)) * Mathf.Sin(u), tube * Mathf.Sin(v));
        for (int i = 0; i < ts; i++) for (int j = 0; j < rs; j++)
            {
                float u0 = i / (float)ts * TAU, u1 = (i + 1) / (float)ts * TAU, w0 = j / (float)rs * TAU, w1 = (j + 1) / (float)rs * TAU;
                b.quad(P(u0, w0), P(u1, w0), P(u1, w1), P(u0, w1));
            }
        return b.geo(false, true);
    }
    /// <summary>LatheGeometry: profile points (radius, y) revolved around Y. Double sided use is expected.</summary>
    public static Mesh latheGeo(float[][] pts, int seg = 20)
    {
        var b = new MB();
        for (int i = 0; i < seg; i++) for (int j = 0; j < pts.Length - 1; j++)
            {
                float a0 = i / (float)seg * TAU, a1 = (i + 1) / (float)seg * TAU; float[] p0 = pts[j], p1 = pts[j + 1];
                b.quad(v3(Mathf.Sin(a0) * p0[0], p0[1], Mathf.Cos(a0) * p0[0]), v3(Mathf.Sin(a1) * p0[0], p0[1], Mathf.Cos(a1) * p0[0]), v3(Mathf.Sin(a1) * p1[0], p1[1], Mathf.Cos(a1) * p1[0]), v3(Mathf.Sin(a0) * p1[0], p1[1], Mathf.Cos(a0) * p1[0]));
            }
        return b.geo(false, true);
    }
}

// ================= maze =================
public class Maze
{
    public int cols, rows, n, entC, exitC; public float cell, x0, zS, zN, T, H, braid, hw;
    public byte[] wallN, wallE; public short[] dist;
    static readonly int[] DC = G.DC, DR = G.DR;
    public Maze(G.MazeCfg c)
    {
        cols = c.cols; rows = c.rows; cell = c.cell; x0 = c.x0; zS = c.zS; zN = c.zN; T = c.T; H = c.H; braid = c.braid; hw = c.hw;
        n = cols * rows; wallN = new byte[n]; wallE = new byte[n]; dist = new short[n];
        for (int i = 0; i < n; i++) { wallN[i] = 1; wallE[i] = 1; dist[i] = -1; }
        gen();
    }
    public int idx(int c, int r) { return r * cols + c; }
    public bool inside(int c, int r) { return c >= 0 && r >= 0 && c < cols && r < rows; }
    /// <summary>Cell (c, r) containing world point (x, z).</summary>
    public void cellOf(float x, float z, out int c, out int r) { c = Mathf.FloorToInt((x - x0) / cell); r = Mathf.FloorToInt((zS - z) / cell); }
    /// <summary>World centre (x, z) of a cell.</summary>
    public void center(int c, int r, out float x, out float z) { x = x0 + (c + 0.5f) * cell; z = zS - (r + 0.5f) * cell; }
    public float[] center(int c, int r) { return new[] { x0 + (c + 0.5f) * cell, zS - (r + 0.5f) * cell }; }
    public bool open(int c, int r, int d)
    {
        if (d == 0) return r < rows - 1 && wallN[idx(c, r)] == 0;
        if (d == 1) return c < cols - 1 && wallE[idx(c, r)] == 0;
        if (d == 2) return r > 0 && wallN[idx(c, r - 1)] == 0;
        return c > 0 && wallE[idx(c - 1, r)] == 0;
    }
    void setOpen(int c, int r, int d) { if (d == 0) wallN[idx(c, r)] = 0; else if (d == 1) wallE[idx(c, r)] = 0; else if (d == 2) wallN[idx(c, r - 1)] = 0; else wallE[idx(c - 1, r)] = 0; }
    void gen()
    {
        var vis = new byte[n]; var stack = new List<int[]>(); var s0 = new[] { G.floor(G.rand() * cols), G.floor(G.rand() * rows) }; stack.Add(s0); vis[idx(s0[0], s0[1])] = 1;
        var opts = new List<int>();
        while (stack.Count > 0)
        {
            int c = stack[stack.Count - 1][0], r = stack[stack.Count - 1][1]; opts.Clear();
            for (int d = 0; d < 4; d++) { int nc = c + DC[d], nr = r + DR[d]; if (inside(nc, nr) && vis[idx(nc, nr)] == 0) opts.Add(d); }
            if (opts.Count == 0) { stack.RemoveAt(stack.Count - 1); continue; }
            int dd = opts[G.floor(G.rand() * opts.Count)]; setOpen(c, r, dd); int c2 = c + DC[dd], r2 = r + DR[dd]; vis[idx(c2, r2)] = 1; stack.Add(new[] { c2, r2 });
        }
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++)
            {
                if (r < rows - 1 && wallN[idx(c, r)] != 0 && G.rand() < braid) wallN[idx(c, r)] = 0;
                if (c < cols - 1 && wallE[idx(c, r)] != 0 && G.rand() < braid) wallE[idx(c, r)] = 0;
            }
        entC = 3 + G.floor(G.rand() * (cols - 6));
        do { exitC = 3 + G.floor(G.rand() * (cols - 6)); } while (Mathf.Abs(exitC - entC) < cols * 0.3f);
    }
    public void bfs(int c0, int r0)
    {
        var D = dist; for (int i = 0; i < n; i++) D[i] = -1; if (!inside(c0, r0)) return;
        var q = new int[n]; int h = 0, t = 0; q[t++] = idx(c0, r0); D[q[0]] = 0;
        while (h < t) { int i = q[h++]; int c = i % cols, r = i / cols; for (int d = 0; d < 4; d++) { if (!open(c, r, d)) continue; int j = idx(c + DC[d], r + DR[d]); if (D[j] < 0) { D[j] = (short)(D[i] + 1); q[t++] = j; } } }
    }
    /// <summary>List of cells [c, r] from (c0,r0) to (c1,r1).</summary>
    public List<int[]> path(int c0, int r0, int c1, int r1)
    {
        bfs(c1, r1); var o = new List<int[]> { new[] { c0, r0 } }; int c = c0, r = r0, g = 0;
        while ((c != c1 || r != r1) && g++ < n)
        {
            int best = -1, bd = 1000000000; for (int d = 0; d < 4; d++) { if (!open(c, r, d)) continue; int v = dist[idx(c + DC[d], r + DR[d])]; if (v >= 0 && v < bd) { bd = v; best = d; } }
            if (best < 0) break; c += DC[best]; r += DR[best]; o.Add(new[] { c, r });
        }
        return o;
    }
    public int degree(int c, int r) { int k = 0; for (int d = 0; d < 4; d++) if (open(c, r, d)) k++; if (r == 0 && c == entC) k++; if (r == rows - 1 && c == exitC) k++; return k; }
    public struct Seg { public float x, z; public bool h; }
    public List<Seg> segments()
    {
        var s = new List<Seg>();
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++)
            {
                float cx = x0 + (c + 0.5f) * cell;
                bool north = r < rows - 1 ? wallN[idx(c, r)] != 0 : (c != exitC);
                if (north) s.Add(new Seg { x = cx, z = zS - (r + 1) * cell, h = true });
                if (r == 0 && c != entC) s.Add(new Seg { x = cx, z = zS, h = true });
                bool east = c < cols - 1 ? wallE[idx(c, r)] != 0 : true;
                if (east) s.Add(new Seg { x = x0 + (c + 1) * cell, z = zS - (r + 0.5f) * cell, h = false });
                if (c == 0) s.Add(new Seg { x = x0, z = zS - (r + 0.5f) * cell, h = false });
            }
        return s;
    }
}

// ================= mesh builder (JS class MB) =================
/// <summary>Non-indexed triangle soup in three.js space. pos/nor/uv are public so callers can post-process normals like the JS did.</summary>
public class MB
{
    public List<float> pos = new List<float>(), nor = new List<float>(), uv = new List<float>();
    public void tri(float[] a, float[] b, float[] c)
    {
        float ux = b[0] - a[0], uy = b[1] - a[1], uz = b[2] - a[2], vx = c[0] - a[0], vy = c[1] - a[1], vz = c[2] - a[2];
        float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx; float l = Mathf.Sqrt(nx * nx + ny * ny + nz * nz); if (l == 0) l = 1; nx /= l; ny /= l; nz /= l;
        pos.Add(a[0]); pos.Add(a[1]); pos.Add(a[2]); pos.Add(b[0]); pos.Add(b[1]); pos.Add(b[2]); pos.Add(c[0]); pos.Add(c[1]); pos.Add(c[2]);
        for (int i = 0; i < 3; i++) { nor.Add(nx); nor.Add(ny); nor.Add(nz); }
        uv.Add(0); uv.Add(0); uv.Add(1); uv.Add(0); uv.Add(1); uv.Add(1);
    }
    public void quad(float[] a, float[] b, float[] c, float[] d, float u0 = 0, float v0 = 0, float u1 = 1, float v1 = 1)
    {
        float ux = b[0] - a[0], uy = b[1] - a[1], uz = b[2] - a[2], vx = d[0] - a[0], vy = d[1] - a[1], vz = d[2] - a[2];
        float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx; float l = Mathf.Sqrt(nx * nx + ny * ny + nz * nz); if (l == 0) l = 1; nx /= l; ny /= l; nz /= l;
        pos.Add(a[0]); pos.Add(a[1]); pos.Add(a[2]); pos.Add(b[0]); pos.Add(b[1]); pos.Add(b[2]); pos.Add(c[0]); pos.Add(c[1]); pos.Add(c[2]);
        pos.Add(a[0]); pos.Add(a[1]); pos.Add(a[2]); pos.Add(c[0]); pos.Add(c[1]); pos.Add(c[2]); pos.Add(d[0]); pos.Add(d[1]); pos.Add(d[2]);
        for (int i = 0; i < 6; i++) { nor.Add(nx); nor.Add(ny); nor.Add(nz); }
        uv.Add(u0); uv.Add(v0); uv.Add(u1); uv.Add(v0); uv.Add(u1); uv.Add(v1); uv.Add(u0); uv.Add(v0); uv.Add(u1); uv.Add(v1); uv.Add(u0); uv.Add(v1);
    }
    public void box(float cx, float cy, float cz, float sx, float sy, float sz, float t = 2)
    {
        float x0 = cx - sx / 2, x1 = cx + sx / 2, y0 = cy - sy / 2, y1 = cy + sy / 2, z0 = cz - sz / 2, z1 = cz + sz / 2;
        quad(G.v3(x0, y0, z1), G.v3(x1, y0, z1), G.v3(x1, y1, z1), G.v3(x0, y1, z1), 0, 0, sx / t, sy / t);
        quad(G.v3(x1, y0, z0), G.v3(x0, y0, z0), G.v3(x0, y1, z0), G.v3(x1, y1, z0), 0, 0, sx / t, sy / t);
        quad(G.v3(x1, y0, z1), G.v3(x1, y0, z0), G.v3(x1, y1, z0), G.v3(x1, y1, z1), 0, 0, sz / t, sy / t);
        quad(G.v3(x0, y0, z0), G.v3(x0, y0, z1), G.v3(x0, y1, z1), G.v3(x0, y1, z0), 0, 0, sz / t, sy / t);
        quad(G.v3(x0, y1, z1), G.v3(x1, y1, z1), G.v3(x1, y1, z0), G.v3(x0, y1, z0), 0, 0, sx / t, sz / t);
        quad(G.v3(x0, y0, z0), G.v3(x1, y0, z0), G.v3(x1, y0, z1), G.v3(x0, y0, z1), 0, 0, sx / t, sz / t);
    }
    public void cyl(float cx, float y0, float cz, float r, float h, int seg = 10, float t = 2, float r2 = float.NaN)
    {
        float rt = float.IsNaN(r2) ? r : r2;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i / (float)seg * G.TAU, a1 = (i + 1) / (float)seg * G.TAU;
            Func<float, float, float, float[]> p = (a, y, rr) => G.v3(cx + Mathf.Cos(a) * rr, y, cz + Mathf.Sin(a) * rr);
            quad(p(a1, y0, r), p(a0, y0, r), p(a0, y0 + h, rt), p(a1, y0 + h, rt), (i + 1) / (float)seg * G.TAU * r / t, 0, i / (float)seg * G.TAU * r / t, h / t);
        }
    }
    public bool empty { get { return pos.Count == 0; } }
    public int vcount { get { return pos.Count / 3; } }
    /// <summary>Build the Unity mesh. flipV: use for UVs given in image space (v down) or for textures the JS loaded with flipY=false.
    /// smoothN: recompute smooth normals instead of using the stored ones.</summary>
    public Mesh geo(bool flipV = false, bool smoothN = false)
    {
        int n = pos.Count / 3; var p = new Vector3[n]; var nn = new Vector3[n]; var u = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            p[i] = new Vector3(pos[i * 3], pos[i * 3 + 1], pos[i * 3 + 2]); nn[i] = new Vector3(nor[i * 3], nor[i * 3 + 1], nor[i * 3 + 2]);
            u[i] = new Vector2(uv[i * 2], flipV ? 1 - uv[i * 2 + 1] : uv[i * 2 + 1]);
        }
        if (smoothN)
        {   // weld by position, average normals
            var map = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < n; i++) { Vector3 a; map.TryGetValue(p[i], out a); map[p[i]] = a + nn[i]; }
            for (int i = 0; i < n; i++) nn[i] = map[p[i]].normalized;
        }
        return G.meshFromArrays(p, nn, u, null);
    }
}
