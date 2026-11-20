// Asset loading (index.html lines 464-489): fills G.T, G.MODELS, G.PH, G.CREDITS from Assets/Resources.
// Source files are produced by tools/convert_assets.py:
//   Resources/Tex/<name>.png, Resources/ModelTex/<name>.png (normal maps end in _n), Resources/Mesh/<file>.bytes ("QFM1"),
//   Resources/assets_manifest.json, Resources/Audio/*.
// Meshes stay in three.js/glTF space inside the files; G.meshFromArrays converts (and we flip v because glTF UVs are image-space).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public static partial class G
{
    public static readonly string[] TEXN_ALL = { "floor_d", "floor_n", "moss_d", "moss_n", "mud_d", "mud_n", "stone_d", "stone_n", "wall_d", "wall_n", "roof_d", "roof_n",
        "wood_d", "wood_n", "crag_d", "crag_n", "rocka_d", "rocka_n", "rockb_d", "rockb_n", "cliffa_d", "cliffa_n", "cliffb_d", "cliffb_n",
        "stump_d", "stump_n", "log_d", "log_n", "branch_d", "fern", "grass", "bamboo", "fir", "fira_d", "firb_d", "firk_d", "cliffc_d" };
    public static readonly string[] PHN = { "phantom", "rock_moss_set_01", "rock_moss_set_02", "rock_face_01", "rock_face_02", "tree_stump_01", "dead_tree_trunk", "dry_branches_medium_01", "fir", "cliffc" };
    /// <summary>Compute tangents for every model mesh (not only the ones that ship a normal map). Turn on if detail normal maps get added later.</summary>
    public static bool MODEL_TANGENTS = false;
    public static bool assetsLoaded = false;

    /// <summary>Synchronous load of everything. progress(fraction 0..1, text). Throws with the asset name if something is missing.</summary>
    public static void loadAll(Action<float, string> progress = null)
    {
        var it = loadAllSteps(progress); while (it.MoveNext()) { }
    }

    /// <summary>Same as loadAll but as an iterator (one asset per step) so a coroutine can spread it over frames: `var it = G.loadAllSteps(cb); while (it.MoveNext()) yield return null;`</summary>
    public static IEnumerator loadAllSteps(Action<float, string> progress = null)
    {
        T.Clear(); MODELS.Clear(); PH.Clear(); CREDITS.Clear(); assetsLoaded = false;
        var mt = Resources.Load<TextAsset>("assets_manifest"); if (mt == null) throw new Exception("assets.json");
        var man = MiniJson.Parse(mt.text) as Dictionary<string, object>; if (man == null) throw new Exception("assets.json");
        var models = (Dictionary<string, object>)man["models"];
        var names = new List<string>(models.Keys);
        int total = TEXN_ALL.Length + PHN.Length + names.Count, done = 0;
        Action tick = () => { done++; if (progress != null) progress(done / (float)total, "正在把佛像搬上山…… " + done + " / " + total); };

        foreach (var n in TEXN_ALL)
        {
            var t = Resources.Load<Texture2D>("Tex/" + n); if (t == null) throw new Exception(n);
            T[n] = t; tick(); yield return null;
        }
        foreach (var n in PHN)
        {
            foreach (var rp in readMeshFile("ph_" + n))
                PH[n + "." + rp.key] = new PHPart { geo = buildMesh(rp, "ph_" + n + "." + rp.key, true) };
            tick(); yield return null;
        }
        foreach (var n in names)
        {
            var mm = (Dictionary<string, object>)models[n];
            var M = new ModelInfo { name = n, d = MiniJson.Floats(mm["d"]), t = mm.ContainsKey("t") ? (int)(double)mm["t"] : 0 };
            if (mm.ContainsKey("eyes"))
            {
                var e = (Dictionary<string, object>)mm["eyes"]; var E = new EyesInfo();
                E.eyes = MiniJson.Floats2(e["eyes"]);
                if (e.ContainsKey("tears")) { var tl = (List<object>)e["tears"]; E.tears = new float[tl.Count][][]; for (int i = 0; i < tl.Count; i++) E.tears[i] = MiniJson.Floats2(tl[i]); }
                E.r = e.ContainsKey("r") ? (float)(double)e["r"] : 0.01f;
                E.faceYaw = e.ContainsKey("faceYaw") ? (float)(double)e["faceYaw"] : 0f;
                M.eyes = E;
            }
            foreach (var rp in readMeshFile(n))
            {
                var part = new ModelPart { unlit = rp.unlit };
                bool hasN = !string.IsNullOrEmpty(rp.ntex);
                part.geo = buildMesh(rp, n + "_" + rp.key, hasN || MODEL_TANGENTS);
                if (!string.IsNullOrEmpty(rp.tex)) { part.map = Resources.Load<Texture2D>("ModelTex/" + rp.tex); if (part.map == null) throw new Exception(rp.tex); }
                if (hasN) { part.normalMap = Resources.Load<Texture2D>("ModelTex/" + rp.ntex); if (part.normalMap == null) Debug.LogWarning("missing normal map " + rp.ntex); }
                M.parts.Add(part);
            }
            MODELS[n] = M; tick(); yield return null;
        }
        if (man.ContainsKey("credits"))
            foreach (var c in (List<object>)man["credits"]) { var l = (List<object>)c; var s = new string[l.Count]; for (int i = 0; i < s.Length; i++) s[i] = Convert.ToString(l[i], CultureInfo.InvariantCulture); CREDITS.Add(s); }
        assetsLoaded = true;
    }

    // ---------- QFM1 reader ----------
    class RawPart { public string key, tex, ntex; public bool unlit, hasUV, dbl; public Vector3[] pos, nor; public Vector2[] uv; public int[] tris; }

    static string rstr(byte[] b, ref int o) { int l = b[o] | (b[o + 1] << 8); o += 2; string s = Encoding.UTF8.GetString(b, o, l); o += l; return s; }

    static List<RawPart> readMeshFile(string file)
    {
        var ta = Resources.Load<TextAsset>("Mesh/" + file); if (ta == null) throw new Exception(file);
        byte[] b = ta.bytes; if (b.Length < 8 || b[0] != 'Q' || b[1] != 'F' || b[2] != 'M' || b[3] != '1') throw new Exception(file + " (bad mesh file)");
        int n = BitConverter.ToInt32(b, 4), o = 8; var list = new List<RawPart>(n);
        for (int pi = 0; pi < n; pi++)
        {
            int start = o; var p = new RawPart();
            p.key = rstr(b, ref o); p.tex = rstr(b, ref o); p.ntex = rstr(b, ref o);
            int flags = b[o++]; p.unlit = (flags & 1) != 0; p.hasUV = (flags & 2) != 0; p.dbl = (flags & 4) != 0; bool big = (flags & 8) != 0;
            int vc = BitConverter.ToInt32(b, o); o += 4; int ic = BitConverter.ToInt32(b, o); o += 4;
            var pf = new float[vc * 3]; Buffer.BlockCopy(b, o, pf, 0, vc * 12); o += vc * 12;
            p.pos = new Vector3[vc]; p.nor = new Vector3[vc];
            for (int i = 0; i < vc; i++)
            {
                p.pos[i] = new Vector3(pf[i * 3], pf[i * 3 + 1], pf[i * 3 + 2]);
                p.nor[i] = new Vector3((sbyte)b[o + i * 4] / 127f, (sbyte)b[o + i * 4 + 1] / 127f, (sbyte)b[o + i * 4 + 2] / 127f);
            }
            o += vc * 4;
            if (p.hasUV)
            {
                var uf = new float[vc * 2]; Buffer.BlockCopy(b, o, uf, 0, vc * 8); o += vc * 8;
                p.uv = new Vector2[vc]; for (int i = 0; i < vc; i++) p.uv[i] = new Vector2(uf[i * 2], 1f - uf[i * 2 + 1]);   // glTF v is top-down
            }
            p.tris = new int[ic];
            if (big) { Buffer.BlockCopy(b, o, p.tris, 0, ic * 4); o += ic * 4; }
            else { var us = new ushort[ic]; Buffer.BlockCopy(b, o, us, 0, ic * 2); o += ic * 2; for (int i = 0; i < ic; i++) p.tris[i] = us[i]; }
            o += (4 - ((o - start) & 3)) & 3;
            list.Add(p);
        }
        return list;
    }

    static Mesh buildMesh(RawPart p, string name, bool tangents)
    {
        var m = meshFromArrays(p.pos, p.nor, p.uv, p.tris, null, tangents && p.uv != null);
        m.name = name; return m;
    }
}

/// <summary>Minimal JSON reader: objects -> Dictionary&lt;string,object&gt; (insertion ordered), arrays -> List&lt;object&gt;, numbers -> double, strings, bool, null.</summary>
public static class MiniJson
{
    public static object Parse(string s) { int i = 0; var v = Val(s, ref i); return v; }
    public static float[] Floats(object o) { var l = (List<object>)o; var a = new float[l.Count]; for (int i = 0; i < a.Length; i++) a[i] = (float)(double)l[i]; return a; }
    public static float[][] Floats2(object o) { var l = (List<object>)o; var a = new float[l.Count][]; for (int i = 0; i < a.Length; i++) a[i] = Floats(l[i]); return a; }

    static void WS(string s, ref int i) { while (i < s.Length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t')) i++; }
    static object Val(string s, ref int i)
    {
        WS(s, ref i); if (i >= s.Length) throw new Exception("json: unexpected end");
        char c = s[i];
        if (c == '{')
        {
            var d = new Dictionary<string, object>(); i++; WS(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                WS(s, ref i); string k = Str(s, ref i); WS(s, ref i); if (s[i] != ':') throw new Exception("json: ':' expected at " + i); i++;
                d[k] = Val(s, ref i); WS(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new Exception("json: ',' or '}' expected at " + i);
            }
        }
        if (c == '[')
        {
            var l = new List<object>(); i++; WS(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Val(s, ref i)); WS(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return l; }
                throw new Exception("json: ',' or ']' expected at " + i);
            }
        }
        if (c == '"') return Str(s, ref i);
        if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
        int st = i; while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
        if (i == st) throw new Exception("json: unexpected '" + c + "' at " + i);
        return double.Parse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture);
    }
    static string Str(string s, ref int i)
    {
        if (s[i] != '"') throw new Exception("json: string expected at " + i);
        i++; var sb = new StringBuilder();
        while (s[i] != '"')
        {
            char c = s[i++];
            if (c != '\\') { sb.Append(c); continue; }
            c = s[i++];
            switch (c)
            {
                case 'n': sb.Append('\n'); break; case 't': sb.Append('\t'); break; case 'r': sb.Append('\r'); break;
                case 'b': sb.Append('\b'); break; case 'f': sb.Append('\f'); break;
                case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                default: sb.Append(c); break;
            }
        }
        i++; return sb.ToString();
    }
}
