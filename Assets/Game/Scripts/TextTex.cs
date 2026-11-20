// Text textures: plaques, stele inscriptions, paper talismans. Port of index.html lines 584-603 (canvas 2D drawings).
// A tiny canvas emulation: GL immediate mode into a linear (raw byte) RenderTexture, glyphs from the dynamic OS font G.FONT,
// then ReadPixels into an sRGB Texture2D. Canvas y (down) is mapped so that the top of the canvas ends up at v = 1.
using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class G
{
    /// <summary>Flip this if text textures come out upside down on some graphics API.</summary>
    public static bool TEXT_FLIP_Y = false;

    /// <summary>Minimal HTML-canvas-like painter (origin top-left, y down, colours are raw sRGB values like CSS).</summary>
    class Cv
    {
        public readonly int w, h; RenderTexture rt, prev; static Material colMat; bool open;
        public Cv(int w, int h, Texture2D bg = null)
        {
            this.w = w; this.h = h;
            if (colMat == null)
            {
                var s = Shader.Find("Hidden/Internal-Colored");
                colMat = new Material(s); colMat.hideFlags = HideFlags.HideAndDontSave;
                colMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); colMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                colMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); colMat.SetInt("_ZWrite", 0); colMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
            rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            prev = RenderTexture.active;
            if (bg != null) Graphics.Blit(bg, rt);
            RenderTexture.active = rt;
            if (bg == null) GL.Clear(true, true, new Color(0, 0, 0, 1));
            GL.PushMatrix();
            if (TEXT_FLIP_Y) GL.LoadPixelMatrix(0, w, 0, h); else GL.LoadPixelMatrix(0, w, h, 0);
            open = true;
        }
        public void fillRect(float x, float y, float rw, float rh, Color c)
        {
            colMat.SetPass(0); GL.Begin(GL.QUADS); GL.Color(c);
            GL.Vertex3(x, y, 0); GL.Vertex3(x + rw, y, 0); GL.Vertex3(x + rw, y + rh, 0); GL.Vertex3(x, y + rh, 0); GL.End();
        }
        /// <summary>ctx.strokeRect with lineWidth lw (the stroke is centred on the rectangle edge).</summary>
        public void strokeRect(float x, float y, float rw, float rh, float lw, Color c)
        {
            float hl = lw / 2;
            fillRect(x - hl, y - hl, rw + lw, lw, c); fillRect(x - hl, y + rh - hl, rw + lw, lw, c);
            fillRect(x - hl, y + hl, lw, rh - lw, c); fillRect(x + rw - hl, y + hl, lw, rh - lw, c);
        }
        /// <summary>ctx.fillText(ch, cx, cy) with textAlign=center, textBaseline=middle, bold. Missing glyphs are skipped.</summary>
        public void text(char ch, float cx, float cy, float size, Color c)
        {
            var font = FONT; if (font == null) return; int px = Mathf.Max(4, Mathf.RoundToInt(size));
            string s = ch.ToString(); font.RequestCharactersInTexture(s, px, FontStyle.Bold);
            CharacterInfo ci; if (!font.GetCharacterInfo(ch, out ci, px, FontStyle.Bold)) return;
            var fm = font.material; if (fm == null) return;
            float left = cx - ci.advance * 0.5f + ci.minX, right = cx - ci.advance * 0.5f + ci.maxX; float baseY = cy + px * 0.36f; float top = baseY - ci.maxY, bottom = baseY - ci.minY;
            fm.SetPass(0); GL.Begin(GL.QUADS); GL.Color(c);
            GL.TexCoord(ci.uvTopLeft); GL.Vertex3(left, top, 0);
            GL.TexCoord(ci.uvTopRight); GL.Vertex3(right, top, 0);
            GL.TexCoord(ci.uvBottomRight); GL.Vertex3(right, bottom, 0);
            GL.TexCoord(ci.uvBottomLeft); GL.Vertex3(left, bottom, 0);
            GL.End();
        }
        public Texture2D finish(TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            if (open) { GL.PopMatrix(); open = false; }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); rt = null;
            var px = t.GetPixels32(); for (int i = 0; i < px.Length; i++) px[i].a = 255; t.SetPixels32(px);
            t.wrapMode = wrap; t.anisoLevel = 4; t.filterMode = FilterMode.Trilinear; t.Apply(true);
            return t;
        }
    }
    /// <summary>CSS colour with alpha as a raw (sRGB valued) Color.</summary>
    static Color css(int r, int g, int b, float a = 1f) { return new Color(r / 255f, g / 255f, b / 255f, a); }
    static Color cssHex(int hex, float a = 1f) { return new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, a); }

    // ================= text textures =================
    public static Texture2D plaqueTex(string text, bool vertical = false)
    {
        if (text == null) text = "";
        var c = vertical ? new Cv(128, 384) : new Cv(384, 128);
        c.fillRect(0, 0, c.w, c.h, cssHex(0x17100c));
        c.strokeRect(7, 7, c.w - 14, c.h - 14, 6, cssHex(0x7c6338));
        Color fg = cssHex(0xb9975a); int n = text.Length;
        if (n > 0)
        {
            if (vertical) { float fs = Mathf.Min(96, 330f / n); for (int i = 0; i < n; i++) c.text(text[i], 64, 192 + (i - (n - 1) / 2f) * fs * 1.05f, fs, fg); }
            else { float fs = Mathf.Min(88, 330f / n); for (int i = 0; i < n; i++) c.text(text[i], 192 + (i - (n - 1) / 2f) * fs * 1.08f, 68, fs, fg); }
        }
        return c.finish();
    }
    public static Texture2D steleTex(string text)
    {
        if (text == null) text = "";
        const int CW = 176, CH = 384;
        // stone background: per-pixel noise (canvas row y -> texture row CH-1-y, raw bytes)
        var bg = new Texture2D(CW, CH, TextureFormat.RGBA32, false, true); var px = new Color32[CW * CH];
        for (int i = 0; i < CW * CH; i++)
        {
            int cxp = i % CW, cyp = i / CW; float v = 118 + rnd() * 30 + vnoise(cxp / 10f, cyp / 10f) * 14;
            px[(CH - 1 - cyp) * CW + cxp] = new Color32((byte)clamp(v, 0, 255), (byte)clamp(v * 0.99f, 0, 255), (byte)clamp(v * 0.94f, 0, 255), 255);
        }
        bg.SetPixels32(px); bg.filterMode = FilterMode.Point; bg.Apply(false);
        var c = new Cv(CW, CH, bg);
        c.strokeRect(10, 10, 156, 364, 3, css(30, 28, 24, 0.6f));
        string[] cols = text.Split(' '); int maxLen = 1; foreach (var s in cols) if (s.Length > maxLen) maxLen = s.Length;
        float fs = Mathf.Min(48, Mathf.Floor(320f / maxLen)); float colW = Mathf.Min(60, 140f / cols.Length);
        Color hi = css(230, 225, 210, 0.35f), ink = cssHex(0x4a1410);
        for (int ci = 0; ci < cols.Length; ci++)
        {
            string s = cols[ci]; float cx = 88 + ((cols.Length - 1) / 2f - ci) * colW; float top = 192 - (s.Length * fs * 1.05f) / 2 + fs * 0.5f;
            for (int k = 0; k < s.Length; k++) { float y = top + k * fs * 1.05f; c.text(s[k], cx + 1, y + 1, fs, hi); c.text(s[k], cx, y, fs, ink); }
        }
        var t = c.finish();
        UnityEngine.Object.Destroy(bg);
        return t;
    }
    public static Texture2D ofudaTex()
    {
        var c = new Cv(256, 256); const int W_ = 64;
        int[] paper = { 0xd8c48c, 0xcdb878, 0xe0d0a0, 0xc9b070 };
        string[] lines = { "急急如律令", "南無阿彌陀佛", "怨靈退散", "鎮此山千佛" };
        for (int i = 0; i < 4; i++)
        {
            int x = i * W_; c.fillRect(x, 0, W_, 256, cssHex(paper[i]));
            Color speck = css(60, 40, 10, 0.18f); for (int k = 0; k < 40; k++) c.fillRect(x + rnd() * W_, rnd() * 256, 1 + rnd() * 3, 1 + rnd() * 10, speck);
            c.strokeRect(x + 5, 5, W_ - 10, 246, 3, cssHex(0x7a1a12));
            Color ink = (i % 2 != 0) ? cssHex(0x7a1a12) : cssHex(0x1a1410);
            string t = lines[i]; float fs = 38; int den = t.Length - 1; if (den == 0) den = 1;
            for (int k = 0; k < t.Length; k++) c.text(t[k], x + W_ / 2f, 34 + k * (210f / den), fs, ink);
        }
        return c.finish();
    }
}
