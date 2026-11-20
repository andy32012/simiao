// Render layer: the only place (with Core.MB) that knows about Unity space.
// Wraps GameObjects (Obj), camera, lights, fog, rain, moon, materials, instanced scatter and distance culling.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>three.js Object3D stand-in. Holds a LOCAL transform in three.js space and mirrors it to a Unity GameObject.</summary>
public class Obj
{
    public GameObject go; public Transform tr; public MeshRenderer mr;
    public float px, py, pz, rx, ry, rz, sx = 1, sy = 1, sz = 1;
    public Obj(string name = "obj") { go = new GameObject(name); tr = go.transform; if (G.root != null) tr.SetParent(G.root, false); }
    public Obj apply() { tr.localPosition = new Vector3(px, py, -pz); tr.localRotation = G.Q3(rx, ry, rz); tr.localScale = new Vector3(sx, sy, sz); return this; }
    public Obj setPos(float x, float y, float z) { px = x; py = y; pz = z; tr.localPosition = new Vector3(x, y, -z); return this; }
    /// <summary>Euler angles in three.js order YXZ (rotation.set(rx, ry, rz, 'YXZ')).</summary>
    public Obj setRot(float x, float y, float z) { rx = x; ry = y; rz = z; tr.localRotation = G.Q3(rx, ry, rz); return this; }
    public float rotY { get { return ry; } set { ry = value; tr.localRotation = G.Q3(rx, ry, rz); } }
    public float rotX { get { return rx; } set { rx = value; tr.localRotation = G.Q3(rx, ry, rz); } }
    public float rotZ { get { return rz; } set { rz = value; tr.localRotation = G.Q3(rx, ry, rz); } }
    public float posY { get { return py; } set { py = value; tr.localPosition = new Vector3(px, py, -pz); } }
    public Obj setScale(float s) { sx = sy = sz = s; tr.localScale = new Vector3(s, s, s); return this; }
    public Obj setScale(float x, float y, float z) { sx = x; sy = y; sz = z; tr.localScale = new Vector3(x, y, z); return this; }
    public Obj mulScale(float k) { return setScale(sx * k, sy * k, sz * k); }
    public bool visible { get { return go.activeSelf; } set { if (go.activeSelf != value) go.SetActive(value); } }
    public Obj add(Obj child) { child.tr.SetParent(tr, false); return this; }
    /// <summary>Shared material of this object's MeshRenderer (null for groups).</summary>
    public Material mat { get { return mr != null ? mr.sharedMaterial : null; } set { if (mr != null) mr.sharedMaterial = value; } }
    /// <summary>Set the material on every MeshRenderer below this object (JS traverse(o=>o.material=...)).</summary>
    public void setMatAll(Material m) { foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = m; }
    public void setShadows(bool cast, bool recv) { foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) { r.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = recv; } }
}

/// <summary>Camera wrapper (three.js PerspectiveCamera, rotation order YXZ).</summary>
public class CamW
{
    public Camera cam; public Transform tr; public float px, py, pz, rx, ry, rz;
    public void setPos(float x, float y, float z) { px = x; py = y; pz = z; tr.position = new Vector3(x, y, -z); }
    public void setRot(float x, float y, float z) { rx = x; ry = y; rz = z; tr.rotation = G.Q3(rx, ry, rz); }
    /// <summary>Re-apply after changing rx/ry/rz fields directly (JS camera.rotation.z += ...).</summary>
    public void apply() { tr.position = new Vector3(px, py, -pz); tr.rotation = G.Q3(rx, ry, rz); }
    public float fov { get { return cam.fieldOfView; } set { cam.fieldOfView = value; } }
    public float aspect { get { return cam.aspect; } }
    /// <summary>Like THREE.Vector3.project(camera): returns NDC x,y (-1..1 on screen). z is 0 when the point is between the near and far planes, 2 otherwise.</summary>
    public Vector3 project(float x, float y, float z)
    {
        Vector3 v = cam.WorldToViewportPoint(new Vector3(x, y, -z));
        bool ok = v.z > cam.nearClipPlane && v.z < cam.farClipPlane;
        return new Vector3(v.x * 2 - 1, v.y * 2 - 1, ok ? 0f : 2f);
    }
    /// <summary>camera.getWorldDirection(): unit forward vector in three.js space.</summary>
    public Vector3 dir() { Vector3 f = tr.forward; return new Vector3(f.x, f.y, -f.z); }
}

/// <summary>Point / spot light wrapper. color is a LINEAR colour (as in the original), intensity/distance use the original three.js numbers.</summary>
public class PLight
{
    public Light l; public float K = 1f; Color _c = Color.white; float _i;
    public void setPos(float x, float y, float z) { l.transform.position = new Vector3(x, y, -z); }
    public Color color { get { return _c; } set { _c = value; l.color = new Color(Mathf.Max(0, value.r), Mathf.Max(0, value.g), Mathf.Max(0, value.b)).gamma; } }
    public void setRGB(float r, float g, float b) { color = new Color(r, g, b); }
    public float distance { get { return l.range; } set { l.range = value; } }
    public float intensity { get { return _i; } set { _i = value; l.intensity = value * K; l.enabled = value > 0.0005f; } }
}

public class Hemi { public Color color = Color.white, groundColor = Color.gray; public float intensity = 1f; }

public class RainFx
{
    public int N; public bool visible; public Color color = G.C(0xaab3b8); public float opacity; public int count;
    Mesh mesh; Vector3[] verts; float[] data; GameObject go; Material mat;
    public RainFx(int n)
    {
        N = n; data = new float[n * 4]; verts = new Vector3[n * 2];
        for (int i = 0; i < n; i++) { data[i * 4] = G.RR(-22, 22); data[i * 4 + 1] = G.RR(-8, 16); data[i * 4 + 2] = G.RR(-22, 22); data[i * 4 + 3] = G.RR(16, 24); }
        mesh = new Mesh(); mesh.MarkDynamic(); mesh.vertices = verts; var idx = new int[n * 2]; for (int i = 0; i < idx.Length; i++) idx[i] = i;
        mesh.SetIndices(idx, MeshTopology.Lines, 0); mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000);
        go = new GameObject("rain"); go.transform.SetParent(G.root, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
        mat = new Material(G.sh("QF/Rain")); var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        go.SetActive(false);
    }
    /// <summary>Advance the drops (count = active drops) and upload. Call every frame after setting count/opacity/visible/color.</summary>
    public void step(float dt)
    {
        bool show = visible && count > 0 && opacity > 0.003f;
        if (go.activeSelf != show) go.SetActive(show);
        if (!show) return;
        int n = Mathf.Min(count, N);
        for (int i = 0; i < N; i++)
        {
            int j = i * 2;
            if (i >= n) { verts[j] = verts[j + 1] = Vector3.zero; continue; }
            data[i * 4 + 1] -= data[i * 4 + 3] * dt;
            if (data[i * 4 + 1] < -8) { data[i * 4 + 1] += 24; data[i * 4] = G.RR(-22, 22); data[i * 4 + 2] = G.RR(-22, 22); }
            float x = data[i * 4], y = data[i * 4 + 1], z = data[i * 4 + 2];
            verts[j] = new Vector3(x + 0.12f, y + 0.65f, -z); verts[j + 1] = new Vector3(x, y, -z);
        }
        mesh.vertices = verts;
        go.transform.position = G.camera.tr.position;
        mat.SetVector("_Color", new Vector4(color.r, color.g, color.b, Mathf.Clamp01(opacity)));
    }
}

public class MoonFx
{
    public Obj o; public Material mat; public float opacity; public Color color = G.C(0xdfe6f0);
    public void setPos(float x, float y, float z) { o.setPos(x, y, z); }
    public void apply() { bool v = opacity > 0.003f; o.visible = v; if (v) mat.SetVector("_Color", new Vector4(color.r, color.g, color.b, opacity)); }
}

/// <summary>Definition passed to SC.add (JS {geo, mat, cell, cast, recv, r, yc, k, far}).</summary>
public class ScDef { public Mesh geo; public Material mat; public float cell = 40, r = 3, yc = 0, k = 1, far = 0; public bool cast, recv; }

public class Scatter
{
    class Bucket { public ScDef def; public List<Matrix4x4> list = new List<Matrix4x4>(); }
    readonly Dictionary<string, Bucket> b = new Dictionary<string, Bucket>();
    /// <summary>Queue one instance. m is a THREE-SPACE matrix (from G.mtx(...), optionally multiplied with other three-space matrices).</summary>
    public void add(string keyName, ScDef def, Matrix4x4 m)
    {
        float cs = def.cell > 0 ? def.cell : 40; int cx = Mathf.FloorToInt(m.m03 / cs), cz = Mathf.FloorToInt(m.m23 / cs); string k = keyName + "|" + cx + "|" + cz;
        if (!b.TryGetValue(k, out var e)) { e = new Bucket { def = def }; b[k] = e; }
        e.list.Add(m);
    }
    public void build()
    {
        foreach (var e in b.Values)
        {
            int n = e.list.Count; var d = e.def; if (n == 0 || d.geo == null || d.mat == null) continue;
            d.mat.enableInstancing = true;
            float sx = 0, sy = 0, sz = 0; for (int i = 0; i < n; i++) { sx += e.list[i].m03; sy += e.list[i].m13; sz += e.list[i].m23; }
            sx /= n; sy /= n; sz /= n; float r = 0; for (int i = 0; i < n; i++) r = Mathf.Max(r, G.hypot(e.list[i].m03 - sx, e.list[i].m23 - sz));
            var ba = new G.Batch { mesh = d.geo, mat = d.mat, cast = d.cast, recv = d.recv };
            for (int i = 0; i < n; i += 1023) { int c = Mathf.Min(1023, n - i); var arr = new Matrix4x4[c]; for (int j = 0; j < c; j++) arr[j] = G.toUnity(e.list[i + j]); ba.chunks.Add(arr); }
            float rad = r + (d.r > 0 ? d.r : 3);
            ba.bounds = new Bounds(new Vector3(sx, sy + d.yc, -sz), Vector3.one * (rad * 2 + 60));
            G.BATCHES.Add(ba);
            G.CULL.Add(new G.CullItem { batch = ba, x = sx, y = sy + d.yc, z = sz, r = rad, far = d.far, k = d.k > 0 ? d.k : 1, fr = true });
        }
        b.Clear();
    }
}

public static partial class G
{
    // ---------- quality (the web build had low/mid/high; the Unity build is always "high") ----------
    public static bool LOW = false, MID = false, LITE = false; public static float DENS = 1f;

    // ---------- scene roots ----------
    public static Transform root;          // parent of every world object
    public static CamW camera = new CamW();
    public static PLight torch = new PLight(), lampA = new PLight(), lampB = new PLight();
    public static Hemi hemi = new Hemi();
    /// <summary>Exp2 fog like THREE.FogExp2: fogC is a LINEAR colour, fogDensity the three.js density.</summary>
    public static Color fogC = C(0x8c968e); public static float fogDensity = 0.03f;
    public static RainFx rain; public static MoonFx moon;
    public static float uTime, uWet;       // U.uTime.value / U.uWet.value
    public static Font FONT;               // dynamic CJK serif font
    public static Texture2D glowTex, noiseTexture, moonTex;

    // ---------- assets (filled by loadAll in Assets.cs) ----------
    public static readonly Dictionary<string, Texture2D> T = new Dictionary<string, Texture2D>();
    public static readonly Dictionary<string, ModelInfo> MODELS = new Dictionary<string, ModelInfo>();
    public static readonly Dictionary<string, PHPart> PH = new Dictionary<string, PHPart>();
    public static readonly List<string[]> CREDITS = new List<string[]>();   // [title, author, license]

    // light unit conversion (three.js r128 legacy lights -> URP), tuned by eye
    public static float K_SPOT = 40f, K_POINT = 9f;

    static readonly Dictionary<string, Shader> shCache = new Dictionary<string, Shader>();
    public static Shader sh(string name)
    {
        if (shCache.TryGetValue(name, out var s) && s != null) return s;
        s = Shader.Find(name);
        if (s == null) { Debug.LogError("Shader not found: " + name); s = Shader.Find("Universal Render Pipeline/Unlit"); }
        shCache[name] = s; return s;
    }

    /// <summary>Create camera, lights, fx, shared textures. Called once by Boot before loadAll/buildWorld.</summary>
    public static void renderInit()
    {
        var rgo = new GameObject("World"); root = rgo.transform;
        var cgo = new GameObject("Main Camera"); cgo.tag = "MainCamera"; var cam = cgo.AddComponent<Camera>();
        cam.fieldOfView = 72; cam.nearClipPlane = 0.07f; cam.farClipPlane = 220f; cam.clearFlags = CameraClearFlags.SolidColor; cam.allowHDR = true; cam.allowMSAA = true;
        cgo.AddComponent<AudioListener>();
        camera.cam = cam; camera.tr = cgo.transform;
        var ucd = cgo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        ucd.renderPostProcessing = true; ucd.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        // flashlight: SpotLight(0xfff0d8, 0, 46, 0.5, 0.6, 1.4) at (0.22,-0.2,0.05) aiming at (0,-0.05,-1) in camera space
        var tgo = new GameObject("torch"); tgo.transform.SetParent(cgo.transform, false); tgo.transform.localPosition = new Vector3(0.22f, -0.2f, 0.12f);
        tgo.transform.localRotation = Quaternion.LookRotation(new Vector3(-0.22f, 0.15f, 1.05f));
        var tl = tgo.AddComponent<Light>(); tl.type = LightType.Spot; tl.range = 46; tl.spotAngle = 2 * 0.5f * Mathf.Rad2Deg; tl.innerSpotAngle = tl.spotAngle * 0.4f;
        tl.shadows = LightShadows.Soft; tl.shadowStrength = 1f; tl.shadowBias = 0.02f; tl.shadowNormalBias = 0.3f; tl.shadowNearPlane = 0.3f;
        torch.l = tl; torch.K = K_SPOT; torch.color = C(0xfff0d8); torch.intensity = 0;
        foreach (var pl in new[] { lampA, lampB })
        {
            var g = new GameObject("lamp"); var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 17; l.shadows = LightShadows.Soft; l.shadowStrength = 0.9f; l.shadowBias = 0.03f; l.shadowNormalBias = 0.4f;
            pl.l = l; pl.K = K_POINT; pl.color = C(0xffa04a); pl.intensity = 0;
        }
        RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.black; RenderSettings.skybox = null;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.reflectionIntensity = 0f;

        FONT = Font.CreateDynamicFontFromOSFont(new[] { "Noto Serif TC", "Noto Serif CJK TC", "Source Han Serif TC", "PMingLiU", "MingLiU", "Songti TC", "PingFang TC", "Heiti TC", "Microsoft JhengHei", "Arial Unicode MS" }, 32);
        glowTex = makeGlowTex(); noiseTexture = makeNoiseTex(); moonTex = makeMoonTex();
        Shader.SetGlobalTexture("_QFNoise", noiseTexture);
        rain = new RainFx(3400);
        moon = new MoonFx(); moon.mat = glowMat(C(0xdfe6f0), 0f, false, moonTex, false); moon.o = mkSprite(moon.mat); moon.o.setScale(26); moon.o.visible = false;
    }

    static Texture2D makeGlowTex()
    {
        int S = 64; var t = new Texture2D(S, S, TextureFormat.RGBA32, true, true); var px = new Color32[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float d = hypot(x + 0.5f - 32, y + 0.5f - 32) / 32f; float a = d < 0.25f ? lerp(1f, 0.5f, d / 0.25f) : d < 1 ? lerp(0.5f, 0f, (d - 0.25f) / 0.75f) : 0f;
                px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        t.SetPixels32(px); t.wrapMode = TextureWrapMode.Clamp; t.Apply(true); return t;
    }
    static Texture2D makeMoonTex()
    {
        int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, true, true); var px = new Color32[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float r = hypot(x + 0.5f - 64, y + 0.5f - 64); float u = clamp((r - 10) / 54f, 0, 1);
                float a = u < 0.42f ? lerp(1f, 0.95f, u / 0.42f) : u < 0.5f ? lerp(0.95f, 0.25f, (u - 0.42f) / 0.08f) : lerp(0.25f, 0f, (u - 0.5f) / 0.5f);
                px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        t.SetPixels32(px); t.wrapMode = TextureWrapMode.Clamp; t.Apply(true); return t;
    }
    static Texture2D makeNoiseTex()
    {
        int S = 256; var t = new Texture2D(S, S, TextureFormat.RGBA32, true, true); var px = new Color32[S * S];
        Func<int, int, Func<int, int, float>> lat = (n, seed) =>
        {
            var a = new float[n * n]; var r = mulberry32(seed); for (int i = 0; i < a.Length; i++) a[i] = r();
            return (x, y) =>
            {
                float fx = x * n / (float)S, fy = y * n / (float)S; int ix = floor(fx), iy = floor(fy); float tx = fx - ix, ty = fy - iy; float sx = tx * tx * (3 - 2 * tx), sy = ty * ty * (3 - 2 * ty);
                Func<int, int, float> v = (i, j) => a[((j % n + n) % n) * n + ((i % n + n) % n)];
                return lerp(lerp(v(ix, iy), v(ix + 1, iy), sx), lerp(v(ix, iy + 1), v(ix + 1, iy + 1), sx), sy);
            };
        };
        var a1 = lat(8, 11); var a2 = lat(32, 12); var b1 = lat(16, 21); var b2 = lat(64, 22); var c1 = lat(64, 31);
        var cs = new float[S]; var rr = mulberry32(41); for (int i = 0; i < S; i++) cs[i] = rr();
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                px[y * S + x] = new Color32((byte)(255 * clamp(a1(x, y) * 0.65f + a2(x, y) * 0.35f, 0, 1)), (byte)(255 * clamp(b1(x, y) * 0.6f + b2(x, y) * 0.4f, 0, 1)),
                    (byte)(255 * clamp(cs[x] * 0.6f + cs[(x + 3) % S] * 0.25f + c1(x, y) * 0.25f, 0, 1)), 255);
            }
        t.SetPixels32(px); t.wrapMode = TextureWrapMode.Repeat; t.filterMode = FilterMode.Bilinear; t.Apply(true); return t;
    }

    // ---------- object factories ----------
    public static Obj mkGroup(string name = "group") { return new Obj(name); }
    /// <summary>new THREE.Mesh(geo, mat).</summary>
    public static Obj mkMesh(Mesh m, Material mat, bool cast = true, bool recv = true, string name = "mesh")
    {
        var o = new Obj(name); o.go.AddComponent<MeshFilter>().sharedMesh = m; o.mr = o.go.AddComponent<MeshRenderer>(); o.mr.sharedMaterial = mat;
        o.mr.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off; o.mr.receiveShadows = recv; return o;
    }
    static Mesh quadMesh;
    /// <summary>new THREE.Sprite(mat): a camera-facing unit quad; setScale(s) gives its world size. mat must come from glowMat().</summary>
    public static Obj mkSprite(Material mat, string name = "sprite")
    {
        if (quadMesh == null) { quadMesh = planeGeo(1, 1); quadMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f); }
        return mkMesh(quadMesh, mat, false, false, name);
    }

    // ---------- materials ----------
    static Vector4 V4(Color c, float a) { return new Vector4(c.r, c.g, c.b, a); }
    static Vector4 V4(float[] c) { return new Vector4(c[0], c[1], c[2], 1); }
    /// <summary>MeshStandardMaterial + weatherize(): lit, mossy, wet. w==null -> defaults (moss 0.4).</summary>
    public static Material stdMat(Texture map, Texture normalMap, W w = null, bool doubleSided = false, float roughness = 0.93f, float metalness = 0f)
    {
        var m = new Material(sh("QF/Weather")); if (w == null) w = new W();
        if (map != null) m.SetTexture("_MainTex", map);
        if (normalMap != null) { m.SetTexture("_BumpMap", normalMap); m.EnableKeyword("_NORMALMAP"); }
        m.SetFloat("_Roughness", roughness); m.SetFloat("_Metallic", metalness);
        m.SetVector("_Tint", V4(w.tint)); m.SetVector("_Tint2", V4(w.tint2)); m.SetFloat("_T2", w.t2); m.SetVector("_T2Y", new Vector4(w.t2y[0], w.t2y[1], 0, 0));
        m.SetFloat("_Moss", w.moss); m.SetFloat("_Gray", w.gray); m.SetFloat("_Amb", w.amb);
        m.SetFloat("_Cull", doubleSided ? 0 : 2); m.enableInstancing = true; return m;
    }
    /// <summary>Plain lit material without weathering (MeshStandardMaterial({color, roughness, metalness, map, emissive})). Colours are linear.</summary>
    public static Material litMat(Color color, float roughness = 0.9f, float metalness = 0f, Texture map = null, Color? emissive = null, float emissiveIntensity = 1f, bool doubleSided = false)
    {
        var m = stdMat(map, null, new W { moss = 0 }, doubleSided, roughness, metalness);
        m.SetVector("_BaseColor", V4(color, 1));
        if (emissive.HasValue) m.SetVector("_Emission", V4(emissive.Value * emissiveIntensity, 1));
        return m;
    }
    /// <summary>Alpha-tested, double sided, wind-swayed foliage (JS MeshStandardMaterial{alphaTest} + swayMat(mat, amp, byY, gain)).</summary>
    public static Material foliageMat(Texture map, float alphaTest, Color color, float swayAmp, bool swayByY = false, float gain = 1f, float roughness = 0.9f)
    {
        var m = new Material(sh("QF/Foliage")); m.SetTexture("_MainTex", map); m.SetFloat("_Cutoff", alphaTest); m.SetVector("_Color", V4(color, 1));
        m.SetFloat("_SwayAmp", swayAmp); m.SetFloat("_SwayByY", swayByY ? 1 : 0); m.SetFloat("_Gain", gain); m.SetFloat("_Roughness", roughness); m.enableInstancing = true; return m;
    }
    /// <summary>MeshBasicMaterial. color linear; opacity &lt; 1 or transparent -> alpha blended; additive -> SRC_ALPHA, ONE.</summary>
    public static Material basicMat(Color color, bool fog = true, Texture map = null, bool transparent = false, float opacity = 1f, bool doubleSided = false, bool depthWrite = true, bool additive = false)
    {
        var m = new Material(sh("QF/Unlit")); if (map != null) m.SetTexture("_MainTex", map);
        m.SetVector("_Color", V4(color, opacity)); m.SetFloat("_Fog", fog ? 1 : 0); m.SetFloat("_Cull", doubleSided ? 0 : 2);
        if (transparent || additive)
        {
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha)); m.SetFloat("_ZWrite", depthWrite ? 1 : 0);
            m.SetFloat("_Additive", additive ? 1 : 0); m.renderQueue = (int)RenderQueue.Transparent; m.SetOverrideTag("RenderType", "Transparent");
        }
        else { m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.Zero); m.SetFloat("_ZWrite", 1); }
        m.enableInstancing = true; return m;
    }
    /// <summary>Additive (or alpha) camera-facing glow for mkSprite (SpriteMaterial{map:glowTex, AdditiveBlending}).</summary>
    public static Material glowMat(Color color, float opacity = 1f, bool fog = true, Texture map = null, bool additive = true)
    {
        var m = new Material(sh("QF/Glow")); m.SetTexture("_MainTex", map != null ? map : glowTex); m.SetVector("_Color", V4(color, opacity)); m.SetFloat("_Fog", fog ? 1 : 0);
        m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha)); m.SetFloat("_Additive", additive ? 1 : 0);
        m.renderQueue = (int)RenderQueue.Transparent + 10; return m;
    }
    /// <summary>material.opacity = a (QF/Unlit, QF/Glow).</summary>
    public static void setOpacity(Material m, float a) { var c = m.GetVector("_Color"); c.w = a; m.SetVector("_Color", c); }
    public static float getOpacity(Material m) { return m.GetVector("_Color").w; }
    /// <summary>material.color = c (keeps opacity). Works for QF/Unlit, QF/Glow, QF/Foliage.</summary>
    public static void setColor(Material m, Color c) { var v = m.GetVector("_Color"); m.SetVector("_Color", new Vector4(c.r, c.g, c.b, v.w)); }
    public static void setRGB(Material m, float r, float g, float b) { var v = m.GetVector("_Color"); m.SetVector("_Color", new Vector4(r, g, b, v.w)); }
    public static Color getColor(Material m) { var v = m.GetVector("_Color"); return new Color(v.x, v.y, v.z, 1); }

    // ---------- instanced scatter + culling ----------
    public class Batch { public Mesh mesh; public Material mat; public bool cast, recv, visible = true; public List<Matrix4x4[]> chunks = new List<Matrix4x4[]>(); public Bounds bounds; }
    public class CullItem { public Obj o; public Batch batch; public float x, y, z, r, k = 1, far; public bool fr; }
    public static readonly List<Batch> BATCHES = new List<Batch>();
    public static readonly List<CullItem> CULL = new List<CullItem>();
    public static readonly Scatter SC = new Scatter();
    /// <summary>Register an object for fog-distance culling (JS cullAdd(o,x,y,z,r,{k,far,fr})).</summary>
    public static void cullAdd(Obj o, float x, float y, float z, float r, float k = 1f, float far = 0f, bool fr = false) { CULL.Add(new CullItem { o = o, x = x, y = y, z = z, r = r, k = k, far = far, fr = fr }); }

    static readonly Plane[] frPlanes = new Plane[6];
    /// <summary>JS cullPass(): hide everything beyond the fog. Uses G.fogRange (Game.cs) and G.CHASERS.</summary>
    public static void cullPass()
    {
        GeometryUtility.CalculateFrustumPlanes(camera.cam, frPlanes); float cx = camera.px, cz = camera.pz;
        for (int i = 0; i < CULL.Count; i++)
        {
            var c = CULL[i]; float d = hypot(c.x - cx, c.z - cz) - c.r; bool vis = d < (c.far > 0 ? min(c.far, fogRange) : fogRange * c.k);
            if (vis && c.fr) { var ctr = new Vector3(c.x, c.y, -c.z); float rad = c.r + 5; for (int p = 0; p < 6; p++) if (frPlanes[p].GetDistanceToPoint(ctr) < -rad) { vis = false; break; } }
            if (c.batch != null) c.batch.visible = vis; else if (c.o != null) c.o.visible = vis;
        }
        for (int i = 0; i < CHASERS.Count; i++) { var c = CHASERS[i]; float d = hypot(c.x - cx, c.z - cz); c.g.visible = d < fogRange * 1.1f; }
    }

    /// <summary>Push fog/ambient/time globals to the shaders and draw the instanced batches. Called every frame by Boot after G.frame().</summary>
    public static void renderApply()
    {
        Shader.SetGlobalVector("_QFFogColor", new Vector4(fogC.r, fogC.g, fogC.b, 1)); Shader.SetGlobalFloat("_QFFogDensity", fogDensity);
        Shader.SetGlobalVector("_QFHemiSky", new Vector4(hemi.color.r, hemi.color.g, hemi.color.b, 0) * hemi.intensity);
        Shader.SetGlobalVector("_QFHemiGround", new Vector4(hemi.groundColor.r, hemi.groundColor.g, hemi.groundColor.b, 0) * hemi.intensity);
        Shader.SetGlobalFloat("_QFTime", uTime); Shader.SetGlobalFloat("_QFWet", uWet);
        camera.cam.backgroundColor = new Color(Mathf.Max(0, fogC.r), Mathf.Max(0, fogC.g), Mathf.Max(0, fogC.b), 1).gamma;
        if (moon != null) moon.apply();
        for (int i = 0; i < BATCHES.Count; i++)
        {
            var b = BATCHES[i]; if (!b.visible) continue;
            var rp = new RenderParams(b.mat) { worldBounds = b.bounds, shadowCastingMode = b.cast ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows = b.recv };
            for (int c = 0; c < b.chunks.Count; c++) Graphics.RenderMeshInstanced(rp, b.mesh, 0, b.chunks[c]);
        }
    }
}
