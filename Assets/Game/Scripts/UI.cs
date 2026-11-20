// UI: the whole interface built from code with uGUI (port of the HTML/CSS in index.html 1-250, input 1606-1637, HUD/map/credits 1848-1899).
// Works for touch (iPad, landscape, safe area) and keyboard + mouse (Windows). All sizes are in reference units (1280x720, match 0.5).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UI
{
    // ================= palette (sRGB UI colours) =================
    static Color H(int hex, float a = 1f) { return new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, a); }
    static Color A(Color c, float a) { return new Color(c.r, c.g, c.b, a); }
    static readonly Color INK = H(0x07080a), BONE = H(0xe4ddcc), DIM = H(0xa39d8f), MOSS = H(0x8aa35c), LANTERN = H(0xf0ad4e), BLOOD = H(0xc0302a);
    static readonly Color BTN_BG = new Color(10 / 255f, 12 / 255f, 12 / 255f, 0.55f), CLEAR = new Color(0, 0, 0, 0);

    // ================= public state =================
    public static Joy joy = new Joy();
    public static bool touchRun, usingTouch;
    public static float mouseSens = 1f, saturation = 1f, contrast = 1f;
    public static string nameValue { get { return nameInput != null ? nameInput.text : ""; } }

    // ================= internals =================
    static Font font;
    static Canvas canvas; static RectTransform canvasRT;
    static readonly Dictionary<string, GameObject> panels = new Dictionary<string, GameObject>();
    static readonly Dictionary<string, Graphic> fx = new Dictionary<string, Graphic>();
    static readonly List<RectTransform> safeRects = new List<RectTransform>();
    static readonly List<RectTransform> blockRects = new List<RectTransform>();
    static Texture2D ovTex, winTex; static Sprite circSp, ringSp, arrowSp, triSp;
    static RawImage grainImg; static Graphic blackImg; static float blackCur, blackTarget, grainT;
    // hud
    static Text clockT, phaseT, deathsT, invT, msgT; static Outline msgOutline; static RectTransform barsRT, compassRT, msgSafe;
    static Image fSta, fBat, fSan; static Text[] cmpT; static Image[] cmpI;
    static float hudT, msgA; static string msgShown = "", msgShownCls = "";
    // touch
    static RectTransform touchRoot, joyBase, knob, tRunRT, tLightRT; static Image tRunFill, tRunRing, tLightFill, tLightRing; static GameObject tHint;
    static bool touchWanted, runOn, lightOn;
    enum Role { None, Joy, Look, Run }
    static readonly Dictionary<int, Role> roles = new Dictionary<int, Role>(); static readonly List<int> tmpIds = new List<int>();
    static int lookId = -1; static Vector2 lookPos;
    // title / overlays
    static InputField nameInput; static Button startBtn, newRunBtn; static Text startTxt, loadTxt, deadSubT, sTimeT, sNightsT, sDeathsT; static RectTransform loadFill, titleCard, titleSafe;
    static RectTransform credCol, credList, credContent, credSafe; static bool credBuilt;
    // map
    static RectTransform mapSafe, mapPanel, mapStatic, mapDyn, mapBtnRT; static float ms = 0.5f, mapStaticMs = -1; static int mapStaticN = -1;
    // input
    static readonly Dictionary<string, KeyCode> keyMap = new Dictionary<string, KeyCode> {
        {"KeyW", KeyCode.W}, {"KeyA", KeyCode.A}, {"KeyS", KeyCode.S}, {"KeyD", KeyCode.D}, {"KeyF", KeyCode.F}, {"KeyM", KeyCode.M}, {"KeyP", KeyCode.P},
        {"ArrowUp", KeyCode.UpArrow}, {"ArrowDown", KeyCode.DownArrow}, {"ArrowLeft", KeyCode.LeftArrow}, {"ArrowRight", KeyCode.RightArrow},
        {"ShiftLeft", KeyCode.LeftShift}, {"ShiftRight", KeyCode.RightShift}, {"Escape", KeyCode.Escape}, {"Space", KeyCode.Space} };
    static readonly HashSet<KeyCode> suppressed = new HashSet<KeyCode>();
    static bool wasLocked, hasAxes = true, axesChecked; const float MOUSE_PX = 15f;
    static int lastW, lastH, settle = 6; static Rect lastSafe;

    // ================= small factories =================
    static RectTransform rt(string name, Transform parent) { var go = new GameObject(name, typeof(RectTransform)); var r = (RectTransform)go.transform; r.SetParent(parent, false); return r; }
    static RectTransform fill(RectTransform r, float l = 0, float b = 0, float rgt = 0, float t = 0) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rgt, -t); return r; }
    /// <summary>Anchor point (ax,ay), pivot (px,py), position and size.</summary>
    static RectTransform place(RectTransform r, float ax, float ay, float px, float py, float x, float y, float w, float h)
    { r.anchorMin = r.anchorMax = new Vector2(ax, ay); r.pivot = new Vector2(px, py); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); return r; }
    static Text txt(Transform p, string s, int size, Color c, TextAnchor a = TextAnchor.UpperLeft, bool bold = false, bool wrap = false, string name = "t")
    {
        var r = rt(name, p); var t = r.gameObject.AddComponent<Text>(); t.font = font; t.text = s; t.fontSize = size; t.color = c; t.alignment = a; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false; t.supportRichText = true; return t;
    }
    static Image img(Transform p, Color c, Sprite s = null, bool ray = false, string name = "i") { var r = rt(name, p); var im = r.gameObject.AddComponent<Image>(); im.sprite = s; im.color = c; im.raycastTarget = ray; return im; }
    static Shadow shade(Graphic g, float a = 0.9f, float d = 1f) { var s = g.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, a); s.effectDistance = new Vector2(0, -d); return s; }
    static LayoutElement le(Component c, float pw = -1, float ph = -1, float fw = -1)
    { var e = c.gameObject.GetComponent<LayoutElement>(); if (e == null) e = c.gameObject.AddComponent<LayoutElement>(); if (pw >= 0) { e.preferredWidth = pw; e.minWidth = pw; } if (ph >= 0) { e.preferredHeight = ph; e.minHeight = ph; } if (fw >= 0) e.flexibleWidth = fw; return e; }
    static void edge(RectTransform p, Color c, Vector2 amin, Vector2 amax, Vector2 omin, Vector2 omax)
    { var im = img(p, c, null, false, "edge"); var r = im.rectTransform; r.anchorMin = amin; r.anchorMax = amax; r.offsetMin = omin; r.offsetMax = omax; im.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; }
    /// <summary>1px style border made of four thin images (works on translucent backgrounds).</summary>
    static void frame(RectTransform r, Color c, float th = 1f)
    {
        edge(r, c, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -th), Vector2.zero); edge(r, c, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, th));
        edge(r, c, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(th, 0)); edge(r, c, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-th, 0), Vector2.zero);
    }
    static T group<T>(Component c, float spacing, TextAnchor align, int padX = 0, int padY = 0) where T : HorizontalOrVerticalLayoutGroup
    {
        var g = c.gameObject.AddComponent<T>(); g.spacing = spacing; g.childAlignment = align; g.padding = new RectOffset(padX, padX, padY, padY);
        g.childControlWidth = g.childControlHeight = true; g.childForceExpandWidth = g.childForceExpandHeight = false; return g;
    }
    static Button button(Transform p, string label, int size, Color textCol, Color bg, Color border, int padX, int padY, Action onClick, bool bold = false)
    {
        var r = rt("btn", p); var im = r.gameObject.AddComponent<Image>(); im.color = bg; im.raycastTarget = true;
        var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = im; var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
        var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = Color.white; cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1); cb.selectedColor = Color.white; cb.disabledColor = new Color(1, 1, 1, 0.5f); cb.fadeDuration = 0.08f; b.colors = cb;
        if (onClick != null) b.onClick.AddListener(() => { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); onClick(); });
        if (border.a > 0) frame(r, border, 1f);
        group<HorizontalLayoutGroup>(r, 0, TextAnchor.MiddleCenter, padX, padY);
        txt(r, label, size, textCol, TextAnchor.MiddleCenter, bold, false, "label");
        return b;
    }
    /// <summary>The big call-to-action button (.cta).</summary>
    static Button cta(Transform p, string label, Action onClick, bool dark = false)
    {
        var b = dark ? button(p, spaced(label), 19, H(0x1b1a17), new Color(0, 0, 0, 0.04f), H(0x1b1a17), 34, 13, onClick, true)
                     : button(p, spaced(label), 19, H(0xfff4e2), A(LANTERN, 0.12f), LANTERN, 34, 13, onClick, true);
        return b;
    }
    static string spaced(string s) { return string.Join(" ", s.ToCharArray()); }
    static void clearChildren(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject); }

    // ================= textures =================
    static Texture2D tex(int w, int h, Func<int, int, Color> f, bool repeat = false)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false); var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x, y);
        t.SetPixels(px); t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; t.Apply(); return t;
    }
    /// <summary>CSS radial-gradient(ellipse at center ...): t = 0 at the centre, 1 at the corners.</summary>
    static Texture2D radial(Color c0, Color c1, float t0, float t1, int S = 128)
    {
        return tex(S, S, (x, y) => { float u = (x + 0.5f) / S * 2 - 1, v = (y + 0.5f) / S * 2 - 1; float t = Mathf.Sqrt(u * u + v * v) / 1.41421356f; return Color.Lerp(c0, c1, Mathf.Clamp01((t - t0) / (t1 - t0))); });
    }
    static Texture2D circleTex(int S, float ringPx)
    {
        return tex(S, S, (x, y) =>
        {
            float d = Mathf.Sqrt((x + 0.5f - S / 2f) * (x + 0.5f - S / 2f) + (y + 0.5f - S / 2f) * (y + 0.5f - S / 2f)); float R = S / 2f - 1f;
            float a = Mathf.Clamp01(R - d + 0.5f); if (ringPx > 0) a *= Mathf.Clamp01(d - (R - ringPx) + 0.5f); return new Color(1, 1, 1, a);
        });
    }
    /// <summary>Rasterise a polygon given in canvas coordinates (y down), centred, covering [-ext, ext].</summary>
    static Texture2D polyTex(float[][] p, int S, float ext, Color fillC, Color lineC, float linePx)
    {
        Func<float, float, bool> ins = (x, y) =>
        {
            bool c = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if (((p[i][1] > y) != (p[j][1] > y)) && (x < (p[j][0] - p[i][0]) * (y - p[i][1]) / (p[j][1] - p[i][1]) + p[i][0])) c = !c;
            return c;
        };
        return tex(S, S, (px, py) =>
        {
            float x = ((px + 0.5f) / S * 2 - 1) * ext, y = -((py + 0.5f) / S * 2 - 1) * ext;
            if (ins(x, y)) return fillC;
            if (linePx > 0) { float d = linePx * ext * 2 / S; for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4; if (ins(x + Mathf.Cos(a) * d, y + Mathf.Sin(a) * d)) return lineC; } }
            return new Color(fillC.r, fillC.g, fillC.b, 0);
        });
    }
    static Sprite sprite(Texture2D t) { return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f); }

    // ================= init =================
    public static void init()
    {
        if (canvasRT != null) return;
        font = G.FONT != null ? G.FONT : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var cgo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1280, 720);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; sc.matchWidthOrHeight = 0.5f;
        canvasRT = (RectTransform)cgo.transform;
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        ovTex = radial(new Color(12 / 255f, 14 / 255f, 14 / 255f, 0.45f), new Color(4 / 255f, 5 / 255f, 6 / 255f, 0.92f), 0f, 0.75f);
        winTex = radial(new Color(250 / 255f, 246 / 255f, 236 / 255f, 0.92f), new Color(196 / 255f, 190 / 255f, 176 / 255f, 0.96f), 0f, 1f);
        circSp = sprite(circleTex(128, 0)); ringSp = sprite(circleTex(128, 2.2f));
        arrowSp = sprite(polyTex(new[] { new[] { 0f, -14f }, new[] { 9f, 10f }, new[] { 0f, 5f }, new[] { -9f, 10f } }, 64, 16f, H(0x0c1a3a), Color.white, 1.6f));
        triSp = sprite(polyTex(new[] { new[] { 0f, -9f }, new[] { 8f, 6f }, new[] { -8f, 6f } }, 32, 10f, H(0x3a2f1c), CLEAR, 0));

        // ---- fullscreen fx, in the JS z-order ----
        addFx("vignette", radial(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.9f), 0.42f, 1f), 0.5f);
        addFx("danger", radial(new Color(110 / 255f, 0, 0, 0), new Color(150 / 255f, 12 / 255f, 6 / 255f, 0.8f), 0.38f, 1f), 0f);
        var gt = tex(180, 180, (x, y) => { float v = UnityEngine.Random.value; return new Color(v, v, v, UnityEngine.Random.value * 90f / 255f); }, true);
        grainImg = (RawImage)addFx("grain", gt, 0.07f);
        addFx("flashfx", Texture2D.whiteTexture, 0f);
        addFx("blink", Texture2D.whiteTexture, 0f, Color.black);
        addFx("redfx", radial(new Color(170 / 255f, 10 / 255f, 0, 0.3f), new Color(60 / 255f, 0, 0, 0.92f), 0f, 1f), 0f);

        buildHud(); buildMsg(); buildTouch();
        blackImg = addFx("blackfx", Texture2D.whiteTexture, 0f, Color.black); fx.Remove("blackfx");
        buildTitle(); buildPause(); buildMap(); buildCred(); buildDead(); buildWin();

        show("hud", false); show("title", true);
        if (Input.touchSupported && !Input.mousePresent) usingTouch = true;
        relayout();
    }
    static Graphic addFx(string id, Texture t, float alpha, Color? tint = null)
    {
        var r = rt(id, canvasRT); fill(r); var ri = r.gameObject.AddComponent<RawImage>(); ri.texture = t; ri.raycastTarget = false; var c = tint ?? Color.white; c.a = alpha; ri.color = c;
        ri.enabled = alpha > 0.003f; fx[id] = ri; return ri;
    }
    static RectTransform safeChild(RectTransform root) { var s = rt("safe", root); safeRects.Add(s); return s; }

    // ---------- HUD (index.html 149-161) ----------
    static void buildHud()
    {
        var root = rt("hud", canvasRT); fill(root); panels["hud"] = root.gameObject; var s = safeChild(root);
        clockT = txt(s, "06:00", 28, BONE, TextAnchor.UpperLeft, true); place(clockT.rectTransform, 0, 1, 0, 1, 18, -12, 220, 36); shade(clockT, 0.95f);
        phaseT = txt(s, "黎明 · 薄霧", 13, DIM); place(phaseT.rectTransform, 0, 1, 0, 1, 18, -50, 460, 20); shade(phaseT, 0.95f);
        // compass
        compassRT = rt("compass", s); place(compassRT, 0.5f, 1, 0.5f, 1, 0, -12, 300, 32);
        cmpT = new Text[12]; cmpI = new Image[12];
        for (int i = 0; i < 12; i++)
        {
            cmpI[i] = img(compassRT, BONE); place(cmpI[i].rectTransform, 0, 1, 0.5f, 1, 0, 0, 1, 6); cmpI[i].enabled = false;
            cmpT[i] = txt(compassRT, "北", 16, BONE, TextAnchor.MiddleCenter, true); place(cmpT[i].rectTransform, 0, 1, 0.5f, 0.5f, 0, -17, 24, 24); shade(cmpT[i]); cmpT[i].enabled = false;
        }
        var ptr = img(compassRT, LANTERN); place(ptr.rectTransform, 0.5f, 1, 0.5f, 0.5f, 0, -3, 6, 6); ptr.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        // top right
        var bp = button(s, "暫停", 13, BONE, BTN_BG, A(BONE, 0.35f), 0, 0, () => { if (G.state == "playing") G.pause(); }); var bpr = (RectTransform)bp.transform; place(bpr, 1, 1, 1, 1, -18, -12, 64, 34); blockRects.Add(bpr);
        var bm = button(s, "地圖", 13, BONE, BTN_BG, A(BONE, 0.35f), 0, 0, () => openMap()); var bmr = (RectTransform)bm.transform; place(bmr, 1, 1, 1, 1, -92, -12, 64, 34); blockRects.Add(bmr);
        deathsT = txt(s, "第 1 天", 13, DIM, TextAnchor.MiddleRight); place(deathsT.rectTransform, 1, 1, 1, 1, -168, -12, 340, 34); shade(deathsT, 0.95f);
        invT = txt(s, "", 15, BONE, TextAnchor.UpperRight); place(invT.rectTransform, 1, 1, 1, 1, -18, -54, 380, 24); shade(invT, 0.95f);
        // bars
        barsRT = rt("bars", s); place(barsRT, 0, 0, 0, 0, 18, 20, 180, 56);
        fSta = bar("體力", 0, BONE); fBat = bar("電量", 1, LANTERN); fSan = bar("心神", 2, MOSS);
        refreshInv();
    }
    static Image bar(string label, int i, Color c)
    {
        var l = txt(barsRT, label, 12, DIM, TextAnchor.MiddleLeft); place(l.rectTransform, 0, 1, 0, 1, 0, -i * 20, 40, 16); shade(l, 0.95f);
        var tr = img(barsRT, A(BONE, 0.16f)); place(tr.rectTransform, 0, 1, 0, 1, 42, -i * 20 - 6, 130, 4);
        var f = img(tr.transform, c); fill(f.rectTransform); return f;
    }
    static void refreshInv() { if (invT != null) invT.text = "銅鑰 <color=#f0ad4e><b>" + G.player.keys + "</b></color>/3 · 護身符 <color=#f0ad4e><b>" + G.player.charms + "</b></color>"; }

    // ---------- message line (#msg) ----------
    static void buildMsg()
    {
        var root = rt("msg", canvasRT); fill(root); msgSafe = safeChild(root);
        msgT = txt(msgSafe, "", 20, BONE, TextAnchor.LowerCenter, false, true); place(msgT.rectTransform, 0.5f, 0.2f, 0.5f, 0, 0, 0, 680, 80);
        msgOutline = msgT.gameObject.AddComponent<Outline>(); msgOutline.effectColor = new Color(0, 0, 0, 0.85f); msgOutline.effectDistance = new Vector2(1.2f, 1.2f);
        shade(msgT, 0.9f, 2f); msgT.color = A(BONE, 0);
    }

    // ---------- touch controls (index.html 164-169) ----------
    static void buildTouch()
    {
        touchRoot = rt("touch", canvasRT); fill(touchRoot); panels["touch"] = touchRoot.gameObject; var s = safeChild(touchRoot);
        joyBase = rt("joy", touchRoot); place(joyBase, 0, 0, 0.5f, 0.5f, 0, 0, 124, 124);
        fill(img(joyBase, new Color(10 / 255f, 12 / 255f, 12 / 255f, 0.25f), circSp).rectTransform); fill(img(joyBase, A(BONE, 0.3f), ringSp).rectTransform);
        knob = img(joyBase, A(BONE, 0.32f), circSp).rectTransform; place(knob, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 50, 50);
        joyBase.gameObject.SetActive(false);
        var h = txt(s, "左邊拖曳走路 · 右邊拖曳轉頭", 12, A(BONE, 0.45f), TextAnchor.LowerLeft); place(h.rectTransform, 0, 0, 0, 0, 26, 30, 360, 20); tHint = h.gameObject;
        tRunRT = touchBtn(s, "跑", -26, 120, out tRunFill, out tRunRing); tLightRT = touchBtn(s, "燈", -110, 40, out tLightFill, out tLightRing);
        touchRoot.gameObject.SetActive(false);
    }
    static RectTransform touchBtn(RectTransform p, string label, float x, float y, out Image fillI, out Image ringI)
    {
        var r = rt("tbtn", p); place(r, 1, 0, 1, 0, x, y, 76, 76);
        fillI = img(r, new Color(10 / 255f, 12 / 255f, 12 / 255f, 0.45f), circSp); fill(fillI.rectTransform); ringI = img(r, A(BONE, 0.42f), ringSp); fill(ringI.rectTransform);
        var t = txt(r, label, 20, BONE, TextAnchor.MiddleCenter, true); fill(t.rectTransform); return r;
    }
    static void setTouchBtn(Image fillI, Image ringI, bool on)
    { fillI.color = on ? A(LANTERN, 0.3f) : new Color(10 / 255f, 12 / 255f, 12 / 255f, 0.45f); ringI.color = on ? LANTERN : A(BONE, 0.42f); }

    // ---------- overlays ----------
    static RectTransform overlay(string id, Texture bgTex, Color tint)
    {
        var root = rt(id, canvasRT); fill(root); var bg = root.gameObject.AddComponent<RawImage>(); bg.texture = bgTex; bg.color = tint; bg.raycastTarget = true;
        panels[id] = root.gameObject; var s = safeChild(root); root.gameObject.SetActive(false); return s;
    }
    static RectTransform column(Transform p, float width, float spacing, TextAnchor align)
    {
        var r = rt("col", p); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(width, 0);
        group<VerticalLayoutGroup>(r, spacing, align); var f = r.gameObject.AddComponent<ContentSizeFitter>(); f.verticalFit = ContentSizeFitter.FitMode.PreferredSize; return r;
    }
    static Text big(Transform p, string s, Color c) { var t = txt(p, s, 46, c, TextAnchor.MiddleCenter, true, true); t.lineSpacing = 1.1f; return t; }
    static Text sub(Transform p, string s, Color c, int size = 15) { var t = txt(p, s, size, c, TextAnchor.MiddleCenter, false, true); t.lineSpacing = 1.45f; return t; }
    static void line(Transform p) { var l = img(p, A(BONE, 0.14f)); le(l, -1, 1, 1); }

    // title (index.html 173-204)
    static void buildTitle()
    {
        titleSafe = overlay("title", ovTex, Color.white);
        titleCard = rt("card", titleSafe); titleCard.anchorMin = titleCard.anchorMax = titleCard.pivot = new Vector2(0.5f, 0.5f);
        group<HorizontalLayoutGroup>(titleCard, 48, TextAnchor.UpperLeft); var fit = titleCard.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        // masthead: vertical title + seal
        var mast = rt("mast", titleCard); le(mast, 150, 540);
        var vt = txt(mast, "霧\n鎖\n千\n佛\n山", 84, BONE, TextAnchor.UpperCenter, true); place(vt.rectTransform, 0, 1, 0, 1, 0, 0, 104, 540); vt.lineSpacing = 0.98f; shade(vt, 0.9f, 3f);
        var seal = img(mast, H(0x9b2018)); place(seal.rectTransform, 0, 1, 0, 1, 112, -10, 32, 64); frame(seal.rectTransform, H(0xb8352a), 2f);
        var st = txt(seal.transform, "出\n山", 17, H(0xf6e7da), TextAnchor.MiddleCenter, true); fill(st.rectTransform); st.lineSpacing = 0.95f;
        // text column
        var col = rt("col", titleCard); le(col, 500); group<VerticalLayoutGroup>(col, 13, TextAnchor.UpperLeft);
        var intro = txt(col, "你在潮濕的落葉上醒來。四周全是霧，霧裡站著佛——很多很多的佛。山門被三道銅鎖鎖著，三把鑰匙藏在山裡的殿堂中。\n<color=#ffffff><b>找齊三把銅鑰，走出這座山。</b></color>", 16, BONE, TextAnchor.UpperLeft, false, true); intro.lineSpacing = 1.45f;
        var rules = rt("rules", col); group<VerticalLayoutGroup>(rules, 7, TextAnchor.UpperLeft);
        string E0 = "<color=#e4ddcc>", E1 = "</color>";
        foreach (var s in new[] {
            "石僧像" + E0 + "只有在你沒看著祂們的時候才會動" + E1 + "——盯住就停，移開視線就走。天黑後更快；夜裡和殿裡要" + E0 + "手電筒照到" + E1 + "才算看見。",
            "山裡有" + E0 + "地藏堂、本堂、大佛殿、千手殿、閻魔殿、石室、廢屋" + E1 + "，都能走進去。三把" + E0 + "銅鑰" + E1 + "藏在其中三座殿裡（地圖上標紅），其他地方有護身符。走過去就會撿起來。",
            E0 + "護身符" + E1 + "會在被抓到的那一刻自動燒掉，替你擋一次。走近" + E0 + "石燈籠" + E1 + "會點亮並存檔、充電、回復心神；地上那圈光以內，祂們進不來。",
            E0 + "體力" + E1 + "用完要等回到三成才能再跑。" + E0 + "心神" + E1 + "越低越容易看見幻覺，歸零時盯著石僧也擋不住。",
            "深山裡會有聲音" + E0 + "叫你的名字" + E1 + "，霧裡會有人對你招手。有的在救你，有的不是——留意有沒有別的聲音在警告你。",
            E0 + "眼睛看到的不一定是真的。" + E1 + "突然被一圈佛圍住是幻覺：抬頭看天或低頭看地，再看回來就會散。對追你的石僧，這招沒用。",
            "迷路就看" + E0 + "地圖" + E1 + "。鐘聲只告訴你山門在北邊；要走的路，跟著石板路和路邊的石碑。" })
        {
            var row = rt("li", rules); group<HorizontalLayoutGroup>(row, 8, TextAnchor.UpperLeft);
            var mk = rt("mk", row); le(mk, 14, 20); var d = img(mk, LANTERN); place(d.rectTransform, 0.5f, 1, 0.5f, 0.5f, 0, -11, 6, 6); d.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            var t = txt(row, s, 14, DIM, TextAnchor.UpperLeft, false, true); t.lineSpacing = 1.3f; le(t, -1, -1, 1);
        }
        line(col);
        foreach (var kv in new[] { new[] { "電腦", "W A S D 走 · 滑鼠轉頭 · Shift 跑 · F 手電筒 · M 地圖 · Esc 暫停" }, new[] { "平板", "左半邊拖曳走路 · 右半邊拖曳轉頭 · 右下「跑」「燈」 · 右上「地圖」「暫停」" } })
        {
            var row = rt("keys", col); group<HorizontalLayoutGroup>(row, 14, TextAnchor.UpperLeft);
            var dt = txt(row, kv[0], 13, BONE); le(dt, 40);
            var dd = txt(row, kv[1], 13, DIM, TextAnchor.UpperLeft, false, true); dd.lineSpacing = 1.25f; le(dd, -1, -1, 1);
        }
        line(col);
        // name row
        var nrow = rt("namerow", col); group<HorizontalLayoutGroup>(nrow, 10, TextAnchor.MiddleLeft);
        txt(nrow, "你的名字", 12, DIM, TextAnchor.MiddleLeft);
        var ir = rt("name", nrow); var ibg = ir.gameObject.AddComponent<Image>(); ibg.color = BTN_BG; ibg.raycastTarget = true; le(ir, 150, 34); frame(ir, A(BONE, 0.35f), 1f);
        var it = txt(ir, "", 16, BONE, TextAnchor.MiddleLeft); fill(it.rectTransform, 10, 2, 10, 2); it.supportRichText = false;
        nameInput = ir.gameObject.AddComponent<InputField>(); nameInput.textComponent = it; nameInput.targetGraphic = ibg; nameInput.characterLimit = 8; nameInput.lineType = InputField.LineType.SingleLine;
        nameInput.customCaretColor = true; nameInput.caretColor = BONE; nameInput.selectionColor = A(LANTERN, 0.35f);
        string saved = PlayerPrefs.GetString("qf_name", "玄燁"); nameInput.text = string.IsNullOrEmpty(saved) ? "玄燁" : saved;
        txt(nrow, "山裡的東西會這樣叫你", 12, DIM, TextAnchor.MiddleLeft);
        // loading bar
        var track = img(col, A(BONE, 0.16f)); le(track, 300, 3);
        var lf = img(track.transform, LANTERN); loadFill = lf.rectTransform; loadFill.anchorMin = Vector2.zero; loadFill.anchorMax = new Vector2(0, 1); loadFill.offsetMin = loadFill.offsetMax = Vector2.zero;
        loadTxt = txt(col, "正在把佛像搬上山……", 12, DIM);
        // foot
        var foot = rt("foot", col); group<HorizontalLayoutGroup>(foot, 16, TextAnchor.MiddleLeft);
        startBtn = cta(foot, "起霧中", () =>
        {
            string v = (nameInput.text ?? "").Trim(); PlayerPrefs.SetString("qf_name", v.Length > 0 ? v : "玄燁");
            G.continueRun = Save.hasSave; G.startGame();
        });
        startTxt = startBtn.GetComponentInChildren<Text>(); startBtn.interactable = false; startTxt.color = A(H(0xfff4e2), 0.5f);
        newRunBtn = button(foot, "<color=#a39d8f>重新開始</color>", 12, DIM, CLEAR, CLEAR, 2, 6, () => { G.continueRun = false; Save.clear(); G.startGame(); });
        newRunBtn.gameObject.SetActive(false);
        txt(foot, "建議戴耳機", 12, DIM, TextAnchor.MiddleLeft);
        var lb = button(foot, "<color=#a39d8f>模型出處</color>", 12, DIM, CLEAR, CLEAR, 2, 6, () => openCred());
        var ul = img(lb.transform, A(DIM, 0.7f)); var ur = ul.rectTransform; ur.anchorMin = new Vector2(0, 0); ur.anchorMax = new Vector2(1, 0); ur.offsetMin = new Vector2(2, 3); ur.offsetMax = new Vector2(-2, 4); ul.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
    }
    static void buildPause()
    {
        var s = overlay("pauseOv", ovTex, Color.white); var c = column(s, 560, 16, TextAnchor.MiddleCenter);
        big(c, "暫停", BONE); sub(c, "霧還在。祂們也還在。\n電腦：點「繼續」後滑鼠會鎖進畫面，按 Esc 再暫停。\n平板：點右上「暫停」暫停，點「繼續」回到遊戲。", DIM);
        cta(c, "繼續", () => G.resume());
    }
    static void buildDead()
    {
        var s = overlay("dead", ovTex, Color.white); var c = column(s, 560, 16, TextAnchor.MiddleCenter);
        var b = big(c, "你被留在了千佛山", H(0xe4a097)); var gl = b.gameObject.AddComponent<Outline>(); gl.effectColor = new Color(160 / 255f, 10 / 255f, 0, 0.35f); gl.effectDistance = new Vector2(2, 2);
        deadSubT = sub(c, "最後點亮的那盞燈籠，還在燒。", DIM);
        cta(c, "在燈籠旁醒來", () => G.respawn());
    }
    static void buildWin()
    {
        var s = overlay("win", winTex, Color.white); var c = column(s, 560, 16, TextAnchor.MiddleCenter);
        big(c, "你走出了千佛山", H(0x1b1a17)); sub(c, "身後的鐘聲停了。\n……但你確定，從山門走出來的，只有你一個嗎？", H(0x4b473f));
        var row = rt("stats", c); group<HorizontalLayoutGroup>(row, 36, TextAnchor.MiddleCenter);
        sTimeT = stat(row, "0:00", "用時"); sNightsT = stat(row, "0", "撐過的夜"); sDeathsT = stat(row, "0", "被抓次數");
        cta(c, "再入一次山", () => { show("win", false); G.startGame(); }, true);
    }
    static Text stat(Transform row, string v, string label)
    {
        var c = rt("stat", row); group<VerticalLayoutGroup>(c, 2, TextAnchor.MiddleCenter);
        var t = txt(c, v, 26, H(0x1b1a17), TextAnchor.MiddleCenter, true); le(t, 110, 38); var lt = txt(c, label, 12, H(0x4b473f), TextAnchor.MiddleCenter); le(lt, 110, 18); return t;
    }
    static void buildCred()
    {
        credSafe = overlay("credOv", ovTex, Color.white); credCol = column(credSafe, 720, 14, TextAnchor.UpperLeft);
        txt(credCol, "模 型 出 處", 22, BONE, TextAnchor.UpperLeft, true);
        var p = txt(credCol, "佛像與寺廟物件是真實文物與場景的 3D 掃描，依各作者的授權使用（CC0 或 CC BY 4.0），為了遊戲做過減面與風化處理。樹皮、地面、岩石等材質來自 Poly Haven（CC0）。人聲用 Kokoro 語音模型（Apache 2.0）合成後再加工。", 13, DIM, TextAnchor.UpperLeft, false, true); p.lineSpacing = 1.35f;
        line(credCol);
        credList = rt("list", credCol); le(credList, -1, 300, 1); var hit = credList.gameObject.AddComponent<Image>(); hit.color = CLEAR; hit.raycastTarget = true;
        var sr = credList.gameObject.AddComponent<ScrollRect>(); var vp = rt("vp", credList); fill(vp); vp.gameObject.AddComponent<RectMask2D>();
        credContent = rt("content", vp); credContent.anchorMin = new Vector2(0, 1); credContent.anchorMax = new Vector2(1, 1); credContent.pivot = new Vector2(0.5f, 1); credContent.offsetMin = credContent.offsetMax = Vector2.zero;
        group<VerticalLayoutGroup>(credContent, 4, TextAnchor.UpperLeft); var f = credContent.gameObject.AddComponent<ContentSizeFitter>(); f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.viewport = vp; sr.content = credContent; sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 30; sr.movementType = ScrollRect.MovementType.Clamped;
        line(credCol);
        cta(credCol, "關閉", () => show("credOv", false));
    }
    static void openCred()
    {
        if (!credBuilt && G.CREDITS.Count > 0)
        {
            credBuilt = true;
            foreach (var c in G.CREDITS)
            {
                string a = c.Length > 1 ? c[1] : "", l = c.Length > 2 ? c[2] : "";
                var t = txt(credContent, "<color=#e4ddcc><b>" + c[0] + "</b></color> — " + a + "（" + l + "）· Sketchfab", 12, DIM, TextAnchor.UpperLeft, false, true); t.lineSpacing = 1.2f;
            }
            txt(credContent, "Fir twigs, rocks, ferns, grass, bark, ground and stone textures — Poly Haven (CC0)", 12, DIM, TextAnchor.UpperLeft, false, true);
        }
        show("credOv", true); settle = 2;
    }

    // ---------- map (index.html 1871-1894) ----------
    static void buildMap()
    {
        var root = rt("mapOv", canvasRT); fill(root); var bg = root.gameObject.AddComponent<Image>(); bg.color = new Color(4 / 255f, 5 / 255f, 6 / 255f, 0.86f); bg.raycastTarget = true;
        panels["mapOv"] = root.gameObject; mapSafe = safeChild(root);
        var pn = img(mapSafe, H(0xcbbd99), null, false, "mapcv"); mapPanel = pn.rectTransform; place(mapPanel, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 320, 580);
        var streaks = rt("streaks", mapPanel); fill(streaks);
        for (int i = 0; i < 160; i++)
        {
            var s = img(streaks, new Color(90 / 255f, 70 / 255f, 30 / 255f, UnityEngine.Random.value * 0.08f)); var r = s.rectTransform;
            float x = UnityEngine.Random.value, y = UnityEngine.Random.value; r.anchorMin = new Vector2(x, y); r.anchorMax = new Vector2(Mathf.Min(1, x + 0.004f + UnityEngine.Random.value * 0.04f), Mathf.Min(1, y + 0.0015f)); r.offsetMin = r.offsetMax = Vector2.zero;
        }
        mapStatic = rt("static", mapPanel); fill(mapStatic); mapDyn = rt("dyn", mapPanel); fill(mapDyn);
        frame(mapPanel, H(0x3a2f1c), 1f);
        var b = cta(mapSafe, "收起", () => closeMap()); mapBtnRT = (RectTransform)b.transform; place(mapBtnRT, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 150, 50);
        root.gameObject.SetActive(false);
    }
    // canvas-pixel helpers (origin top-left of the 640x1160 sheet, y down)
    static Image mrect(RectTransform p, float x, float y, float w, float h, Color c, Sprite s = null) { var im = img(p, c, s); place(im.rectTransform, 0, 1, 0, 1, x * ms, -y * ms, w * ms, h * ms); return im; }
    static Image mdot(RectTransform p, float x, float y, float w, float h, Color c, Sprite s) { var im = img(p, c, s); place(im.rectTransform, 0, 1, 0.5f, 0.5f, x * ms, -y * ms, w * ms, h * ms); return im; }
    static void mline(RectTransform p, float x1, float y1, float x2, float y2, float w, Color c)
    {
        float dx = x2 - x1, dy = y2 - y1, L = Mathf.Sqrt(dx * dx + dy * dy); if (L < 0.01f) return;
        var im = img(p, c); var r = im.rectTransform; place(r, 0, 1, 0, 0.5f, x1 * ms, -y1 * ms, L * ms, Mathf.Max(1f, w * ms)); r.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
    }
    static Text mtext(RectTransform p, string s, float x, float y, float px, Color c, float hAlign = 0.5f, bool bold = false)
    {
        var a = hAlign < 0.25f ? TextAnchor.MiddleLeft : hAlign > 0.75f ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
        var t = txt(p, s, Mathf.Max(11, Mathf.RoundToInt(px * ms * 1.25f)), c, a, bold); place(t.rectTransform, 0, 1, hAlign, 0.5f, x * ms, -y * ms, 420, 30); return t;
    }
    public static void drawMap()
    {
        if (mapPanel == null) return; Canvas.ForceUpdateCanvases();
        Rect sr = mapSafe.rect; float mapH = Mathf.Min(sr.height * 0.82f, sr.width * 0.92f * 1160f / 640f); ms = mapH / 1160f;
        float total = mapH + 12 + 50; mapPanel.sizeDelta = new Vector2(640 * ms, mapH); mapPanel.anchoredPosition = new Vector2(0, total / 2 - mapH / 2); mapBtnRT.anchoredPosition = new Vector2(0, -total / 2 + 25);
        const float Wc = 640, Hc = 1160, pad = 30; float XB = G.XB, ZS = G.ZS, ZN = G.ZN;
        float sc = Mathf.Min((Wc - pad * 2) / (2 * XB), (Hc - pad * 2) / (ZS - ZN));
        Func<float, float> X = x => Wc / 2 + x * sc; Func<float, float> Y = z => Hc / 2 + (z - (ZS + ZN) / 2) * sc;
        Color ink = H(0x1c150a);
        if (mapStaticN != G.paths.Count || Mathf.Abs(ms - mapStaticMs) > 1e-4f)
        {
            mapStaticN = G.paths.Count; mapStaticMs = ms; clearChildren(mapStatic); var p = mapStatic;
            // border
            Color bc = H(0x4a3a20); float bx = X(-XB), by = Y(ZN), bw = 2 * XB * sc, bh = (ZS - ZN) * sc;
            mrect(p, bx, by, bw, 2, bc); mrect(p, bx, by + bh - 2, bw, 2, bc); mrect(p, bx, by, 2, bh, bc); mrect(p, bx + bw - 2, by, 2, bh, bc);
            // maze bands
            foreach (var mz in new[] { G.M1C, G.M2C })
            {
                string label = mz == G.M1C ? "石 壁 迷 宮" : "竹 林 迷 陣";
                mrect(p, X(-mz.hw), Y(mz.zN), 2 * mz.hw * sc, (mz.zS - mz.zN) * sc, new Color(74 / 255f, 58 / 255f, 32 / 255f, 0.2f));
                for (float x = -mz.hw; x < mz.hw; x += 7) mline(p, X(x), Y(mz.zN), X(Mathf.Min(mz.hw, x + (mz.zS - mz.zN) * 0.5f)), Y(mz.zS), 1, new Color(74 / 255f, 58 / 255f, 32 / 255f, 0.55f));
                Color side = new Color(74 / 255f, 58 / 255f, 32 / 255f, 0.6f);
                mrect(p, X(-XB), Y(mz.zN), (XB - mz.hw) * sc, (mz.zS - mz.zN) * sc, side); mrect(p, X(mz.hw), Y(mz.zN), (XB - mz.hw) * sc, (mz.zS - mz.zN) * sc, side);
                mtext(p, label, X(0), Y((mz.zS + mz.zN) / 2), 24, H(0x2c2212), 0.5f, true);
            }
            // paths: dashes [6,5], width 2.4
            Color pc = new Color(60 / 255f, 44 / 255f, 20 / 255f, 0.75f);
            foreach (var path in G.paths)
            {
                var P = path.pts; float carry = 0;
                for (int i = 1; i < P.Count; i++)
                {
                    float x1 = X(P[i - 1][0]), y1 = Y(P[i - 1][1]), x2 = X(P[i][0]), y2 = Y(P[i][1]); float dx = x2 - x1, dy = y2 - y1, L = Mathf.Sqrt(dx * dx + dy * dy); if (L < 1e-4f) continue;
                    float t = carry;
                    while (t < L) { float e = Mathf.Min(L, t + 6); mline(p, x1 + dx * t / L, y1 + dy * t / L, x1 + dx * e / L, y1 + dy * e / L, 2.4f, pc); t += 11; }
                    carry = t - L;
                }
            }
            // landmarks
            foreach (var l in G.LANDMARKS) { float x = X(l.x), y = Y(l.z); mdot(p, x, y - 1.5f, 20, 20, Color.white, triSp); mtext(p, l.name, x, y - 17, 18, ink, 0.5f, true); }
            // gate + area labels
            mrect(p, X(-9), Y(G.GATE.y) - 3, 18 * sc + 4, 5, H(0x8a1f16)); mtext(p, "山　門", X(0), Y(G.GATE.y) - 15, 20, ink, 0.5f, true);
            mtext(p, "千佛坡", X(-62), Y(-74), 16, ink); mtext(p, "入山口", X(34), Y(G.START.y) + 5, 16, ink); mtext(p, "後山", X(70), Y(-205), 16, ink);
            mtext(p, "北 ↑", 12, 24, 20, ink, 0f, true); mtext(p, "紅＝藏鑰　橘點＝點亮的燈籠", Wc - 12, Hc - 12, 15, ink, 1f);
        }
        clearChildren(mapDyn); var q = mapDyn;
        foreach (var t in G.TEMPLES)
        {
            var h = t.H; if (h == null) continue; float x = X(h.x), y = Y(h.z); bool got = false;
            if (t.key) foreach (var pk in G.PICKUPS) if (pk.type == "key" && pk.hall == h && pk.taken) { got = true; break; }
            mrect(q, x - 9.5f, y - 9.5f, 19, 19, ink); mrect(q, x - 8, y - 8, 16, 16, t.key ? (got ? H(0x6f6a55) : H(0x8a1f16)) : H(0x3a2f1c));
            string nm = string.IsNullOrEmpty(h.name) ? (t.label ?? "") : h.name;
            mtext(q, nm + (t.key ? (got ? "（已取）" : "（鑰）") : ""), x, y - 19, 18, ink, 0.5f, true);
        }
        foreach (var L in G.LANTERNS) { float d = L.lit ? 10 : 6; mdot(q, X(L.x), Y(L.z), d, d, L.lit ? H(0xd9831f) : new Color(60 / 255f, 44 / 255f, 20 / 255f, 0.5f), circSp); }
        var ar = mdot(q, X(G.player.x), Y(G.player.z), 46, 46, Color.white, arrowSp); ar.rectTransform.localRotation = Quaternion.Euler(0, 0, G.player.yaw * Mathf.Rad2Deg);
    }
    public static void openMap()
    {
        if (G.state != "playing") return; G.state = "map"; show("mapOv", true); drawMap(); lockCursor(false); releaseKeys();
    }
    public static void closeMap()
    {
        if (G.state != "map") return; show("mapOv", false); G.state = "playing"; lockCursor(true);
    }

    // ================= public API =================
    public static void show(string id, bool on)
    {
        if (id == "touch") { touchWanted = on; return; }
        GameObject go; if (!panels.TryGetValue(id, out go)) return;
        if (go.activeSelf != on) go.SetActive(on);
        if (on && (id == "title" || id == "credOv")) settle = 3;
        if (on && id == "hud") { hudT = 0; refreshInv(); }
    }
    public static void setFx(string id, float opacity)
    {
        if (id == "blackfx") { blackTarget = Mathf.Clamp01(opacity); return; }
        Graphic g; if (fx.TryGetValue(id, out g)) applyFx(g, opacity);
    }
    static void applyFx(Graphic g, float o) { o = Mathf.Clamp01(o); var c = g.color; if (c.a != o) { c.a = o; g.color = c; } bool on = o > 0.003f; if (g.enabled != on) g.enabled = on; }
    public static void setDeadSub(string s) { if (deadSubT != null) deadSubT.text = s; }
    public static void setWinStats(string time, int nights, int deaths) { if (sTimeT == null) return; sTimeT.text = time; sNightsT.text = nights.ToString(); sDeathsT.text = deaths.ToString(); }
    public static void setLoad(float fraction, string text, bool ready)
    {
        if (loadFill == null) return; loadFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1);
        if (text != null) loadTxt.text = text;
        startBtn.interactable = ready; startTxt.text = spaced(ready ? (Save.hasSave ? "繼續" : "入山") : "起霧中"); if (newRunBtn != null) newRunBtn.gameObject.SetActive(ready && Save.hasSave); startTxt.color = A(H(0xfff4e2), ready ? 1f : 0.5f);
    }
    public static void setFilter(float s, float c) { saturation = s; contrast = c; }
    /// <summary>JS keys[code]. False after releaseKeys() until the key is released and pressed again.</summary>
    public static bool key(string code)
    {
        KeyCode kc;
        if (!keyMap.TryGetValue(code, out kc)) { kc = KeyCode.None; KeyCode p; if (code.StartsWith("Key") && Enum.TryParse(code.Substring(3), out p)) kc = p; keyMap[code] = kc; }
        return kc != KeyCode.None && Input.GetKey(kc) && !suppressed.Contains(kc);
    }
    public static void releaseKeys()
    {
        foreach (var kv in keyMap) if (kv.Value != KeyCode.None && Input.GetKey(kv.Value)) suppressed.Add(kv.Value);
        clearTouch();
    }
    public static void lockCursor(bool on)
    {
        if (usingTouch) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return; }
        Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !on; if (!on) wasLocked = false;
    }

    // ================= per-frame =================
    public static void update(float dt)
    {
        if (canvasRT == null) return;
        if (Screen.width != lastW || Screen.height != lastH || Screen.safeArea != lastSafe || settle > 0) { if (settle > 0) settle--; relayout(); }
        string st = G.state ?? "";
        // touch detection
        if (Input.touchCount > 0 && !usingTouch) { usingTouch = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; placeBars(); }
        else if (usingTouch && Input.touchCount == 0 && Input.mousePresent && (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D))) { usingTouch = false; placeBars(); }
        if (usingTouch && st == "playing") touchWanted = true;
        // focus lost -> pause (blur / visibilitychange)
        if (!Application.isFocused && st == "playing") { releaseKeys(); G.pause(); st = G.state ?? ""; }
        pollKeys(st); pollMouse(); pollTouch();
        st = G.state ?? "";
        // touch layer
        bool tl = touchWanted && usingTouch; if (touchRoot.gameObject.activeSelf != tl) touchRoot.gameObject.SetActive(tl);
        if (tl) { if (runOn != touchRun) { runOn = touchRun; setTouchBtn(tRunFill, tRunRing, runOn); } bool fl = G.player.flash; if (lightOn != fl) { lightOn = fl; setTouchBtn(tLightFill, tLightRing, fl); } }
        if (st == "playing") updateHUD(dt);
        updateMsg(dt);
        // blackfx: CSS transition .9s
        if (blackCur != blackTarget) { blackCur = Mathf.MoveTowards(blackCur, blackTarget, dt / 0.9f); applyFx(blackImg, blackCur); }
        // grain jitter (~9 steps / s)
        grainT -= dt; if (grainT <= 0 && grainImg.enabled) { grainT = 0.11f; var r = grainImg.uvRect; r.x = UnityEngine.Random.value; r.y = UnityEngine.Random.value; grainImg.uvRect = r; }
    }

    static void relayout()
    {
        lastW = Screen.width; lastH = Screen.height; lastSafe = Screen.safeArea; Canvas.ForceUpdateCanvases();
        Rect sa = Screen.safeArea; float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
        Vector2 mn = new Vector2(Mathf.Clamp01(sa.xMin / sw), Mathf.Clamp01(sa.yMin / sh)), mx = new Vector2(Mathf.Clamp01(sa.xMax / sw), Mathf.Clamp01(sa.yMax / sh));
        if (mx.x - mn.x < 0.1f || mx.y - mn.y < 0.1f) { mn = Vector2.zero; mx = Vector2.one; }
        foreach (var r in safeRects) { r.anchorMin = mn; r.anchorMax = mx; r.offsetMin = r.offsetMax = Vector2.zero; }
        Canvas.ForceUpdateCanvases();
        Vector2 cs = canvasRT.rect.size; Vector2 ss = new Vector2(cs.x * (mx.x - mn.x), cs.y * (mx.y - mn.y));
        if (grainImg != null) { var u = grainImg.uvRect; u.width = cs.x / 180f; u.height = cs.y / 180f; grainImg.uvRect = u; }
        if (msgT != null) msgT.rectTransform.sizeDelta = new Vector2(Mathf.Min(680, ss.x * 0.84f), 80);
        placeBars();
        if (titleCard != null && titleCard.gameObject.activeInHierarchy)
        {
            titleCard.localScale = Vector3.one; LayoutRebuilder.ForceRebuildLayoutImmediate(titleCard); Vector2 sz = titleCard.rect.size;
            float s = Mathf.Min(1.2f, Mathf.Min((ss.x - 48) / Mathf.Max(1, sz.x), (ss.y - 36) / Mathf.Max(1, sz.y))); titleCard.localScale = new Vector3(s, s, 1);
        }
        if (credCol != null)
        {
            credCol.sizeDelta = new Vector2(Mathf.Min(720, ss.x - 40), credCol.sizeDelta.y); var e = credList.GetComponent<LayoutElement>(); float lh = Mathf.Max(120, ss.y * 0.5f); e.preferredHeight = lh; e.minHeight = lh;
        }
        GameObject mo; if (panels.TryGetValue("mapOv", out mo) && mo.activeSelf) drawMap();
    }
    static void placeBars()
    {
        if (barsRT == null) return;
        if (usingTouch) place(barsRT, 0, 1, 0, 1, 18, -86, 180, 56); else place(barsRT, 0, 0, 0, 0, 18, 20, 180, 56);
    }

    // ---------- keyboard ----------
    static void pollKeys(string st)
    {
        if (suppressed.Count > 0) suppressed.RemoveWhere(kc => !Input.GetKey(kc));
        if (st == "playing")
        {
            if (Input.GetKeyDown(KeyCode.F)) G.toggleFlash();
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape)) G.pause();
            else if (Input.GetKeyDown(KeyCode.M)) openMap();
        }
        else if (st == "map") { if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Escape)) closeMap(); }
        else if (st == "paused") { if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) G.resume(); }
    }

    // ---------- mouse ----------
    static void pollMouse()
    {
        if (usingTouch) { wasLocked = false; return; }
        if (!axesChecked) { axesChecked = true; try { Input.GetAxisRaw("Mouse X"); Input.GetAxisRaw("Mouse Y"); } catch (Exception) { hasAxes = false; } }
        bool locked = Cursor.lockState == CursorLockMode.Locked; bool playing = G.state == "playing";
        if (playing)
        {
            if (locked)
            {
                if (hasAxes)
                {
                    float dx = Input.GetAxisRaw("Mouse X") * MOUSE_PX * mouseSens, dy = -Input.GetAxisRaw("Mouse Y") * MOUSE_PX * mouseSens;
                    if (dx != 0 || dy != 0) G.lookBy(dx, dy, 0.0022f);
                }
            }
            else if (wasLocked) G.pause();   // the lock was taken away (pointerlockchange)
            else if (Input.GetMouseButtonDown(0) && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) lockCursor(true);
        }
        else if (locked) lockCursor(false);
        wasLocked = G.state == "playing" && Cursor.lockState == CursorLockMode.Locked;
    }

    // ---------- touch: every finger keeps its own role until it lifts ----------
    static bool hitCircle(RectTransform r, Vector2 screenPos, float scale)
    {
        Vector2 lp; if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(r, screenPos, null, out lp)) return false;
        Vector2 c = r.rect.center; return (lp - c).magnitude <= r.rect.width * 0.5f * scale;
    }
    static bool overBlock(Vector2 screenPos)
    {
        foreach (var r in blockRects) if (r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, screenPos, null)) return true;
        return false;
    }
    static void endFinger(int id)
    {
        Role r; if (!roles.TryGetValue(id, out r)) return; roles.Remove(id);
        if (r == Role.Joy) { joy.id = -1; joy.x = joy.y = 0; if (joyBase != null) joyBase.gameObject.SetActive(false); }
        else if (r == Role.Look) lookId = -1;
    }
    static void clearTouch()
    {
        roles.Clear(); joy.id = -1; joy.x = joy.y = 0; lookId = -1; touchRun = false; if (joyBase != null) joyBase.gameObject.SetActive(false);
    }
    static void pollTouch()
    {
        if (G.state != "playing") { if (roles.Count > 0 || joy.id >= 0 || touchRun) clearTouch(); return; }
        int n = Input.touchCount;
        // forget fingers that disappeared without an Ended event
        if (roles.Count > 0)
        {
            tmpIds.Clear(); foreach (var id in roles.Keys) tmpIds.Add(id);
            foreach (var id in tmpIds) { bool found = false; for (int i = 0; i < n; i++) if (Input.GetTouch(i).fingerId == id) { found = true; break; } if (!found) endFinger(id); }
        }
        float k = canvasRT.rect.width / Mathf.Max(1, Screen.width); bool run = false;
        for (int i = 0; i < n; i++)
        {
            Touch t = Input.GetTouch(i); int id = t.fingerId; Role r;
            if (t.phase == TouchPhase.Began)
            {
                endFinger(id); r = Role.None;
                if (hitCircle(tRunRT, t.position, 1.25f)) r = Role.Run;
                else if (hitCircle(tLightRT, t.position, 1.25f)) G.toggleFlash();
                else if (overBlock(t.position)) { }
                else if (t.position.x < Screen.width * 0.45f && joy.id < 0)
                {
                    r = Role.Joy; joy.id = id; joy.ox = t.position.x; joy.oy = t.position.y; joy.x = joy.y = 0;
                    joyBase.position = new Vector3(t.position.x, t.position.y, 0); knob.anchoredPosition = Vector2.zero; joyBase.gameObject.SetActive(true); if (tHint != null && tHint.activeSelf) tHint.SetActive(false);
                }
                else if (lookId < 0) { r = Role.Look; lookId = id; lookPos = t.position; }
                roles[id] = r;
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { endFinger(id); continue; }
            else if (roles.TryGetValue(id, out r))
            {
                if (r == Role.Joy)
                {
                    float dx = (t.position.x - joy.ox) * k, dy = (t.position.y - joy.oy) * k; float L = Mathf.Sqrt(dx * dx + dy * dy); const float mxr = 56f;
                    if (L > mxr) { dx *= mxr / L; dy *= mxr / L; }
                    joy.x = dx / mxr; joy.y = dy / mxr; knob.anchoredPosition = new Vector2(dx, dy);
                }
                else if (r == Role.Look)
                {
                    Vector2 d = (t.position - lookPos) * k; lookPos = t.position;
                    if (d.x != 0 || d.y != 0) G.lookBy(d.x, -d.y, 0.0048f);
                }
            }
            if (roles.TryGetValue(id, out r) && r == Role.Run) run = true;
        }
        touchRun = run;
    }

    // ---------- HUD (updateHUD + drawCompass) ----------
    static string PH_(float h) { return h < 5 ? "夜深" : h < 7 ? "黎明" : h < 11 ? "早晨" : h < 14 ? "正午" : h < 17 ? "午後" : h < 19 ? "黃昏" : h < 23 ? "夜" : "子夜"; }
    static void drawCompass()
    {
        float heading = ((-G.player.yaw * Mathf.Rad2Deg) % 360 + 360) % 360, ppd = 300f / 130f; int start = Mathf.CeilToInt((heading - 65) / 15f) * 15; int k = 0;
        for (int d = start; d <= heading + 65 && k < cmpT.Length; d += 15, k++)
        {
            float x = 150 + (d - heading) * ppd; int dd = ((d % 360) + 360) % 360; float fade = Mathf.Clamp01(1 - Mathf.Abs(x - 150) / 150f);
            if (dd % 90 == 0)
            {
                cmpI[k].enabled = false; cmpT[k].enabled = true; cmpT[k].text = dd == 0 ? "北" : dd == 90 ? "東" : dd == 180 ? "南" : "西";
                cmpT[k].color = A(BONE, 0.25f + 0.75f * fade); cmpT[k].rectTransform.anchoredPosition = new Vector2(x, -17);
            }
            else
            {
                cmpT[k].enabled = false; cmpI[k].enabled = true; bool mid = dd % 45 == 0; var r = cmpI[k].rectTransform;
                r.anchoredPosition = new Vector2(x, mid ? -11 : -14); r.sizeDelta = new Vector2(1, mid ? 12 : 6); cmpI[k].color = A(BONE, 0.15f + 0.45f * fade);
            }
        }
        for (; k < cmpT.Length; k++) { cmpT[k].enabled = false; cmpI[k].enabled = false; }
    }
    static void setBar(Image f, float v, Color c) { var r = f.rectTransform; r.anchorMax = new Vector2(Mathf.Clamp01(v / 100f), 1); if (f.color != c) f.color = c; }
    static void updateHUD(float dt)
    {
        var p = G.player; float night = G.night, indoorF = G.indoorF, san = p.sanity;
        drawCompass();
        setFx("vignette", 0.42f + night * 0.3f + indoorF * 0.25f + (1 - san / 100f) * 0.25f);
        setFx("danger", p.danger * 0.75f + Mathf.Max(0, (15 - san) / 15f) * 0.3f);
        setFx("grain", 0.06f + night * 0.07f + indoorF * 0.05f + (1 - san / 100f) * 0.08f);
        setFx("flashfx", G.lightning.flash * 0.25f * (1 - indoorF * 0.6f));
        setBar(fSta, p.stamina, p.exhausted ? DIM : BONE); setBar(fBat, p.battery, p.battery < 20 ? BLOOD : LANTERN); setBar(fSan, san, san < 30 ? BLOOD : MOSS);
        hudT -= dt; if (hudT > 0) return; hudT = 0.12f;
        float hour = G.hour; int hh = Mathf.FloorToInt(hour), mm = Mathf.FloorToInt((hour - hh) * 60);
        clockT.text = hh.ToString("00") + ":" + mm.ToString("00");
        string where; if (p.inside != null) where = string.IsNullOrEmpty(p.inside.name) ? "廢屋" : p.inside.name; else where = G.WX[G.weather.name].label;
        phaseT.text = PH_(hour) + " · " + where + (p.safe ? " · 燈火旁" : "");
        deathsT.text = (night > 0.5f ? "第 " + (p.nights + 1) + " 夜" : "第 " + (p.nights + 1) + " 天") + (p.deaths > 0 ? " · 被抓 " + p.deaths : "");
        refreshInv();
        float s = san / 100f; setFilter(0.84f * (0.6f + 0.4f * s), 1.03f + (1 - s) * 0.25f);
    }
    static void updateMsg(float dt)
    {
        bool on = G.msgOn && !string.IsNullOrEmpty(G.msgText);
        if (on && (G.msgText != msgShown || (G.msgCls ?? "") != msgShownCls))
        {
            msgShown = G.msgText; msgShownCls = G.msgCls ?? ""; msgT.text = msgShown; bool voice = msgShownCls == "voice";
            msgT.fontSize = voice ? 24 : 20; msgOutline.effectColor = voice ? new Color(120 / 255f, 14 / 255f, 8 / 255f, 0.9f) : new Color(0, 0, 0, 0.85f);
            msgOutline.effectDistance = voice ? new Vector2(1.8f, 1.8f) : new Vector2(1.2f, 1.2f);
        }
        float target = on ? 1 : 0; if (msgA == target && msgT.color.a == target) return;
        msgA = Mathf.MoveTowards(msgA, target, dt / (on ? 0.5f : 0.7f));
        Color c = msgShownCls == "warn" ? H(0xf0a49a) : msgShownCls == "voice" ? H(0xead9ad) : BONE; c.a = msgA; msgT.color = c;
        msgT.rectTransform.anchoredPosition = new Vector2(0, -10 * (1 - msgA));
    }
}
