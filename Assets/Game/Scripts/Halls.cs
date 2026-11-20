// Roofs & halls. Port of index.html lines 636-715. Everything in three.js coordinates.
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A walk-in temple hall (JS object H returned by buildHall).</summary>
public class Hall : Interior
{
    public HallDef def; public Obj g;
    public float w, d, rot, baseY, P, wallH, ceil;
    public List<Lamp> lamps = new List<Lamp>();
    public float ridgeRise, ridgeRl; public bool ridgeSwap;   // JS H.ridge = {rise, rl, swap}; rise is local Y above baseY
    public Statue bud; public List<Chaser> sleep;
    public const float th = 0.36f;
    float cs, sn;
    public Hall() { isHall = true; }
    public void setRot(float r) { rot = r; cs = Mathf.Cos(r); sn = Mathf.Sin(r); }
    /// <summary>Hall-local (lx, lz) -> world {x, z}.</summary>
    public float[] W(float lx, float lz) { return new[] { x + lx * cs + lz * sn, z - lx * sn + lz * cs }; }
    /// <summary>World-space axis aligned rect {minx, maxx, minz, maxz} of a hall-local rect.</summary>
    public float[] rectW(float lx0, float lx1, float lz0, float lz1) { var a = W(lx0, lz0); var b = W(lx1, lz1); return new[] { Mathf.Min(a[0], b[0]), Mathf.Max(a[0], b[0]), Mathf.Min(a[1], b[1]), Mathf.Max(a[1], b[1]) }; }
    public float yaw(float ly) { return rot + ly; }
    /// <summary>Place a statue at hall-local (lx, lz). o.dy lifts it above the floor; o.yaw is relative to the hall.</summary>
    public Statue statue(string name, float lx, float lz, SO o = null)
    {
        var p = W(lx, lz); o = o != null ? o.clone() : new SO();
        o.indoor = true; if (!o.y.HasValue) o.y = baseY + P + o.dy;
        o.yaw = rot + o.yaw; return G.placeStatue(name, p[0], p[1], o);
    }
    /// <summary>Lit candle / lamp at hall-local (lx, lz), ly above the floor.</summary>
    public Lamp lamp(float lx, float ly, float lz, LO o = null)
    {
        var p = W(lx, lz); var q = new LO { indoor = true, size = 1.6f, power = 0.75f, range = 12 };
        if (o != null) { if (o.color.HasValue) q.color = o.color; if (o.size.HasValue) q.size = o.size; if (o.power.HasValue) q.power = o.power; if (o.range.HasValue) q.range = o.range; if (o.indoor.HasValue) q.indoor = o.indoor; }
        var L = G.addLamp(p[0], baseY + P + ly, p[1], q); G.setLamp(L, true); lamps.Add(L); return L;
    }
    /// <summary>Box (table / altar) standing on the floor at hall-local (lx, lz).</summary>
    public Obj box(float lx, float lz, float sx, float sy, float sz, string matKey = "woodIn", bool col = true)
    {
        var M = G.hallMats(); var b = new MB(); b.box(0, sy / 2, 0, sx, sy, sz, 1.8f);
        var m = G.mkMesh(b.geo(), M[string.IsNullOrEmpty(matKey) ? "woodIn" : matKey], !G.LOW, true, "hallBox"); var p = W(lx, lz); m.setPos(p[0], baseY + P, p[1]); m.rotY = rot;
        G.cullAdd(m, p[0], baseY, p[1], Mathf.Max(sx, sz) + 2, 1f);
        if (col) { var r = rectW(lx - sx / 2, lx + sx / 2, lz - sz / 2, lz + sz / 2); G.addRect(r[0], r[1], r[2], r[3], sy > 1.6f); }
        return m;
    }
    /// <summary>Paste n paper talismans on the inner walls.</summary>
    public void ofuda(int n)
    {
        var M = G.hallMats(); var og = new MB(); float hw = w / 2, hd = d / 2;
        for (int i = 0; i < n; i++)
        {
            int side = G.floor(G.rand() * 3); float hh = G.R(0.5f, 0.75f), ww = hh * 0.26f, yy = P + G.R(0.9f, Mathf.Min(3.2f, wallH - 0.8f)); float u0 = G.floor(G.rand() * 4) / 4f, u1 = u0 + 0.25f; float tl = G.R(-0.15f, 0.15f);
            if (side == 0) { float lz = -hd + th / 2 + 0.04f, lx = G.R(-hw + 0.6f, hw - 0.6f); og.quad(G.v3(lx - ww, yy, lz), G.v3(lx + ww, yy + tl * ww, lz), G.v3(lx + ww, yy + hh + tl * ww, lz), G.v3(lx - ww, yy + hh, lz), u0, 0, u1, 1); }
            else
            {
                int sg = side == 1 ? -1 : 1; float lx = sg * (hw - th / 2 - 0.04f), lz = G.R(-hd + 0.6f, hd - 0.6f);
                if (sg < 0) og.quad(G.v3(lx, yy, lz + ww), G.v3(lx, yy + tl * ww, lz - ww), G.v3(lx, yy + hh + tl * ww, lz - ww), G.v3(lx, yy + hh, lz + ww), u0, 0, u1, 1);
                else og.quad(G.v3(lx, yy, lz - ww), G.v3(lx, yy + tl * ww, lz + ww), G.v3(lx, yy + hh + tl * ww, lz + ww), G.v3(lx, yy + hh, lz - ww), u0, 0, u1, 1);
            }
        }
        if (og.empty) return;
        var m = G.mkMesh(og.geo(), M["ofuda"], false, true, "ofuda"); g.add(m);
    }
}

public static partial class G
{
    // ================= roofs & halls =================
    /// <summary>Curved hip roof into mb. Returns the ridge half length and whether x/z were swapped.</summary>
    public static (float rl, bool swap) hipRoof(MB mb, float ex, float ez, float y0, float rise, float lift, float tile)
    {
        bool swap = ez > ex; if (swap) { float t = ex; ex = ez; ez = t; }
        float rl = Mathf.Max(0.5f, ex - ez * 0.9f); const int NU = 8, NV = 5;
        Func<float, float, float, float[]> P = (x, y, z) => swap ? v3(z, y, x) : v3(x, y, z);
        Action<float[], float[], float[], float[]> face = (e0, e1, r0, r1) =>
        {
            float L = hypot(e1[0] - e0[0], e1[1] - e0[1]), S = hypot(rise, ez);
            Func<float, float, float[]> pt = (u, t) =>
            {
                float x = lerp(lerp(e0[0], e1[0], u), lerp(r0[0], r1[0], u), t), z = lerp(lerp(e0[1], e1[1], u), lerp(r0[1], r1[1], u), t); float c = abs(u * 2 - 1);
                return P(x, y0 + rise * pow(t, 1.3f) + lift * c * c * c * (1 - t) * (1 - t), z);
            };
            for (int i = 0; i < NU; i++) for (int j = 0; j < NV; j++)
                {
                    float u0 = i / (float)NU, u1 = (i + 1) / (float)NU, t0 = j / (float)NV, t1 = (j + 1) / (float)NV; float[] a = pt(u0, t0), b = pt(u1, t0), c = pt(u1, t1), d = pt(u0, t1);
                    if (swap) mb.quad(b, a, d, c, u1 * L / tile, t0 * S / tile, u0 * L / tile, t1 * S / tile); else mb.quad(a, b, c, d, u0 * L / tile, t0 * S / tile, u1 * L / tile, t1 * S / tile);
                }
        };
        face(v2(-ex, ez), v2(ex, ez), v2(-rl, 0), v2(rl, 0)); face(v2(ex, -ez), v2(-ex, -ez), v2(rl, 0), v2(-rl, 0)); face(v2(ex, ez), v2(ex, -ez), v2(rl, 0), v2(rl, 0)); face(v2(-ex, -ez), v2(-ex, ez), v2(-rl, 0), v2(-rl, 0));
        return (rl, swap);
    }

    public static readonly List<Hall> HALLS = new List<Hall>();
    public static readonly List<Interior> INTERIORS = new List<Interior>();
    public static Dictionary<string, Material> HM = null;   // shared hall materials
    public static Texture2D ofudaTexture = null;            // HM.ofuda.map
    public static Dictionary<string, Material> hallMats()
    {
        if (HM != null) return HM;
        Texture2D wood_d = thTex("wood_d"), wood_n = thTex("wood_n");
        ofudaTexture = ofudaTex();
        HM = new Dictionary<string, Material>
        {
            { "woodOut", stdMat(wood_d, wood_n, new W { moss = 0.3f, tint = new[] { 0.5f, 0.4f, 0.34f } }) },
            { "woodIn", stdMat(wood_d, wood_n, new W { moss = 0, tint = new[] { 0.5f, 0.42f, 0.36f }, amb = 0.15f }) },
            { "pilOut", stdMat(wood_d, wood_n, new W { moss = 0.22f, tint = new[] { 0.5f, 0.2f, 0.15f } }) },
            { "pilIn", stdMat(wood_d, wood_n, new W { moss = 0, tint = new[] { 0.5f, 0.2f, 0.15f }, amb = 0.15f }) },
            { "floorIn", stdMat(wood_d, wood_n, new W { moss = 0, tint = new[] { 0.62f, 0.56f, 0.5f }, amb = 0.15f }) },
            { "ceilIn", stdMat(wood_d, null, new W { moss = 0, tint = new[] { 0.22f, 0.19f, 0.17f }, amb = 0.12f }) },
            { "soffit", stdMat(wood_d, null, new W { moss = 0.1f, tint = new[] { 0.3f, 0.25f, 0.22f } }) },
            { "plaster", stdMat(thTex("mud_d"), null, new W { moss = 0.34f, gray = 0.85f, tint = new[] { 1.7f, 1.64f, 1.5f } }) },
            { "roof", stdMat(thTex("roof_d"), thTex("roof_n"), new W { moss = 0.5f, gray = 0.35f, tint = new[] { 0.55f, 0.56f, 0.58f } }, true) },
            { "stone", stdMat(thTex("wall_d"), thTex("wall_n"), new W { moss = 0.5f }) },
            { "stoneIn", stdMat(thTex("wall_d"), thTex("wall_n"), new W { moss = 0, amb = 0.16f }) },
            { "ofuda", stdMat(ofudaTexture, null, new W { moss = 0, amb = 0.3f }, true, 1f) }
        };
        return HM;
    }

    static readonly string[] HALL_MB_KEYS = { "plaster", "woodOut", "woodIn", "pilOut", "pilIn", "floorIn", "ceilIn", "soffit", "roof", "stone" };
    public static Hall buildHall(HallDef def)
    {
        var M = hallMats(); float x = def.x, z = def.z, w = def.w, d = def.d; float rot = def.rot, wallH = def.wallH != 0 ? def.wallH : 4f, P = 0.5f, th = Hall.th, ov = def.ov != 0 ? def.ov : 1.7f;
        float padY = terrainH(x, z); float baseY = padY;
        var H = new Hall { def = def, x = x, z = z, w = w, d = d, baseY = baseY, P = P, floorY = baseY + P, wallH = wallH, id = def.id, name = def.name ?? "", ceil = wallH };
        H.setRot(rot);
        Func<float, float, float[]> W = H.W; Func<float, float, float, float, float[]> rectW = H.rectW;
        var mb = new Dictionary<string, MB>(); foreach (var k in HALL_MB_KEYS) mb[k] = new MB();
        MB mbPlaster = mb["plaster"], mbWoodOut = mb["woodOut"], mbWoodIn = mb["woodIn"], mbPilOut = mb["pilOut"], mbPilIn = mb["pilIn"], mbFloorIn = mb["floorIn"], mbCeilIn = mb["ceilIn"], mbSoffit = mb["soffit"], mbRoof = mb["roof"], mbStone = mb["stone"];
        float hw = w / 2, hd = d / 2; float doorW = def.doorW != 0 ? def.doorW : 2.6f, doorH = Mathf.Min(def.doorH != 0 ? def.doorH : 3f, wallH - 0.5f);
        // platform & steps
        float pe = 1.3f; mbStone.box(0, P / 2, 0, w + pe * 2, P, d + pe * 2, 2.5f);
        var fr = rectW(-hw - pe, hw + pe, -hd - pe, hd + pe); FLOORS.Add(new FloorRect { minx = fr[0], maxx = fr[1], minz = fr[2], maxz = fr[3], fn = (px, pz) => baseY + P });
        Action<float, float, float> steps = (lz0, dir, sw) =>
        {
            for (int i = 0; i < 3; i++) { mbStone.box(0, P * (2 - i) / 3f / 2f, lz0 + dir * (0.25f + i * 0.5f), sw, P * (2 - i) / 3f + 0.001f, 0.5f, 2.5f); }
            var r = rectW(-sw / 2, sw / 2, lz0, lz0 + dir * 1.6f); var a = W(0, lz0); var b = W(0, lz0 + dir * 1.6f);
            FLOORS.Insert(0, new FloorRect
            {
                minx = r[0], maxx = r[1], minz = r[2], maxz = r[3],
                fn = (px, pz) => { float t = clamp(((px - a[0]) * (b[0] - a[0]) + (pz - a[1]) * (b[1] - a[1])) / 2.56f, 0, 1); return baseY + P * (1 - t); }
            });
        };
        steps(hd + pe, 1, Mathf.Max(doorW + 1.6f, 4)); if (def.backDoor != 0) steps(-hd - pe, -1, def.backDoor + 1.6f);
        // walls: outer box + inner lining
        Action<float, float, float, float, float, float> wallX = (lz, x0, x1, y0, y1, inw) =>
        {
            if (x1 - x0 < 0.05f) return; mbWoodOut.box((x0 + x1) / 2, P + (y0 + y1) / 2, lz, x1 - x0, y1 - y0, th, 2.2f);
            float zi = lz + inw * (th / 2 + 0.015f);
            if (inw > 0) mbWoodIn.quad(v3(x0, P + y0, zi), v3(x1, P + y0, zi), v3(x1, P + y1, zi), v3(x0, P + y1, zi), x0 / 2.2f, y0 / 2.2f, x1 / 2.2f, y1 / 2.2f);
            else mbWoodIn.quad(v3(x1, P + y0, zi), v3(x0, P + y0, zi), v3(x0, P + y1, zi), v3(x1, P + y1, zi), x1 / 2.2f, y0 / 2.2f, x0 / 2.2f, y1 / 2.2f);
            if (y0 == 0)
            {
                var r = rectW(x0, x1, lz - th / 2, lz + th / 2); addRect(r[0], r[1], r[2], r[3], true);
                if (y1 > 2.6f && x1 - x0 > 0.9f)
                {
                    float zo = lz - inw * (th / 2 + 0.03f); mbPlaster.box((x0 + x1) / 2, P + (1.2f + y1 - 0.8f) / 2, zo, x1 - x0 - 0.3f, y1 - 2.0f, 0.06f, 3);
                    mbPilOut.box((x0 + x1) / 2, P + 1.08f, zo, x1 - x0, 0.2f, 0.16f, 1.6f); mbPilOut.box((x0 + x1) / 2, P + y1 - 0.68f, zo, x1 - x0, 0.22f, 0.16f, 1.6f);
                }
            }
        };
        Action<float, float, float, float, float, float> wallZ = (lx, z0, z1, y0, y1, inw) =>
        {
            mbWoodOut.box(lx, P + (y0 + y1) / 2, (z0 + z1) / 2, th, y1 - y0, z1 - z0, 2.2f);
            float xi = lx + inw * (th / 2 + 0.015f);
            if (inw > 0) mbWoodIn.quad(v3(xi, P + y0, z1), v3(xi, P + y0, z0), v3(xi, P + y1, z0), v3(xi, P + y1, z1), z1 / 2.2f, y0 / 2.2f, z0 / 2.2f, y1 / 2.2f);
            else mbWoodIn.quad(v3(xi, P + y0, z0), v3(xi, P + y0, z1), v3(xi, P + y1, z1), v3(xi, P + y1, z0), z0 / 2.2f, y0 / 2.2f, z1 / 2.2f, y1 / 2.2f);
            var r = rectW(lx - th / 2, lx + th / 2, z0, z1); addRect(r[0], r[1], r[2], r[3], true);
            if (y1 > 2.6f)
            {
                float xo = lx - inw * (th / 2 + 0.03f); mbPlaster.box(xo, P + (1.2f + y1 - 0.8f) / 2, (z0 + z1) / 2, 0.06f, y1 - 2.0f, z1 - z0 - 0.3f, 3);
                mbPilOut.box(xo, P + 1.08f, (z0 + z1) / 2, 0.16f, 0.2f, z1 - z0, 1.6f); mbPilOut.box(xo, P + y1 - 0.68f, (z0 + z1) / 2, 0.16f, 0.22f, z1 - z0, 1.6f);
            }
        };
        wallZ(-hw, -hd, hd, 0, wallH, 1); wallZ(hw, -hd, hd, 0, wallH, -1);
        wallX(hd, -hw, -doorW / 2, 0, wallH, -1); wallX(hd, doorW / 2, hw, 0, wallH, -1); wallX(hd, -doorW / 2, doorW / 2, doorH, wallH, -1);
        if (def.backDoor != 0) { float bw = def.backDoor, bh = Mathf.Min(2.8f, wallH - 0.5f); wallX(-hd, -hw, -bw / 2, 0, wallH, 1); wallX(-hd, bw / 2, hw, 0, wallH, 1); wallX(-hd, -bw / 2, bw / 2, bh, wallH, 1); }
        else wallX(-hd, -hw, hw, 0, wallH, 1);
        // beams & pillars
        float pr = def.pr != 0 ? def.pr : 0.24f;
        Action<float, float, bool> pil = (lx, lz, inside) =>
        {
            (inside ? mbPilIn : mbPilOut).cyl(lx, P, lz, pr, wallH, 10, 1.6f);
            if (inside) { var p = W(lx, lz); addCircle(p[0], p[1], pr + 0.05f, true); }
        };
        int nx = Mathf.Max(2, round(w / 4)), nz = Mathf.Max(2, round(d / 4));
        for (int i = 0; i <= nx; i++) { float lx = -hw + w * i / nx; if (abs(lx) > doorW / 2 + 0.2f || i == 0 || i == nx) pil(lx, hd + 0.05f, false); pil(lx, -hd - 0.05f, false); }
        for (int j = 1; j < nz; j++) { float lz = -hd + d * j / nz; pil(-hw - 0.05f, lz, false); pil(hw + 0.05f, lz, false); }
        pil(-doorW / 2 - 0.3f, hd + 0.05f, false); pil(doorW / 2 + 0.3f, hd + 0.05f, false);
        mbPilOut.box(0, P + wallH - 0.2f, hd + 0.12f, w + 0.6f, 0.4f, 0.3f, 1.6f); mbPilOut.box(0, P + doorH + 0.2f, hd + 0.14f, doorW + 1.4f, 0.34f, 0.26f, 1.6f);
        if (def.ipil != null) foreach (var ip in def.ipil) pil(ip[0], ip[1], true);
        // floor, ceiling, soffit, roof
        mbFloorIn.quad(v3(-hw, P + 0.012f, hd), v3(hw, P + 0.012f, hd), v3(hw, P + 0.012f, -hd), v3(-hw, P + 0.012f, -hd), 0, 0, w / 2.4f, d / 2.4f);
        float cy = P + wallH; mbCeilIn.quad(v3(-hw, cy - 0.02f, -hd), v3(hw, cy - 0.02f, -hd), v3(hw, cy - 0.02f, hd), v3(-hw, cy - 0.02f, hd), 0, 0, w / 3, d / 3);
        float ex = hw + ov, ez = hd + ov; mbSoffit.quad(v3(-ex, cy + 0.02f, -ez), v3(ex, cy + 0.02f, -ez), v3(ex, cy + 0.02f, ez), v3(-ex, cy + 0.02f, ez), 0, 0, ex, ez);
        float rise = def.rise != 0 ? def.rise : Mathf.Min(w, d) * 0.42f; var rf = hipRoof(mbRoof, ex, ez, cy + 0.04f, rise, def.lift.HasValue ? def.lift.Value : 0.7f, 1.5f);
        // ridge beam
        if (rf.swap) mbPilOut.box(0, cy + rise + 0.15f, 0, 0.5f, 0.5f, rf.rl * 2 + 0.6f, 1.6f); else mbPilOut.box(0, cy + rise + 0.15f, 0, rf.rl * 2 + 0.6f, 0.5f, 0.5f, 1.6f);
        var g = mkGroup("hall_" + def.id); g.setPos(x, baseY, z); g.rotY = rot;
        foreach (var k in HALL_MB_KEYS)
        {
            if (mb[k].empty) continue;
            var m = mkMesh(mb[k].geo(), M[k], !LOW && (k == "woodOut" || k == "pilIn" || k == "pilOut"), true, k); g.add(m);
        }
        // name plaque
        if (!string.IsNullOrEmpty(def.name))
        {
            float pw = Mathf.Min(3.4f, doorW * 0.9f);
            var pm = mkMesh(planeGeo(pw, pw / 3), litMat(Color.white, 0.8f, 0f, plaqueTex(def.name)), false, true, "plaque"); pm.setPos(0, P + doorH + 0.95f, hd + 0.3f); g.add(pm);
        }
        cullAdd(g, x, baseY, z, hypot(w, d) * 0.6f + rise + 4, 1.3f);
        var ir = rectW(-hw + th / 2, hw - th / 2, -hd + th / 2, hd - th / 2);
        H.g = g; H.minx = ir[0]; H.maxx = ir[1]; H.minz = ir[2]; H.maxz = ir[3];
        H.ridgeRise = cy + rise; H.ridgeRl = rf.rl; H.ridgeSwap = rf.swap;
        HALLS.Add(H); INTERIORS.Add(H); return H;
    }
}
