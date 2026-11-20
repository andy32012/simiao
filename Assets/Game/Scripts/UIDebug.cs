// On-screen record panel (top right): frame rate, slowest frames, CPU/GPU time, memory, device, ray tracing support,
// draw calls, errors and suspected bugs. Tap the header (or press F3) to fold it. Also runs a small bug watchdog and
// writes a performance line to qf_log.txt every 10 seconds.
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Profiling;
using Unity.Profiling;

public static partial class UI
{
    static RectTransform dbgPanel; static Text dbgHead, dbgBody, dbgCopyLabel, dbgShareLabel; static GameObject dbgExp; static bool dbgOpen = true;
    static float dbgT, dbgLogT, fpsAcc, copyT; static int fpsN;
    static readonly float[] ftBuf = new float[600]; static readonly float[] ftSort = new float[600]; static int ftI, ftN;
    static ProfilerRecorder recSys, recTotal, recGfx, recDraw, recTris;
    static readonly FrameTiming[] fts = new FrameTiming[1]; static double cpuMs, gpuMs;
    static float stuckT, stuckX, stuckZ, hitchCD; static string lastStateDbg = "";
    static float lastFps, lastP95, lastWorst; static long lastMemMB = -1;

    static void buildDebug()
    {
        dbgOpen = PlayerPrefs.GetInt("qf_dbg", 1) == 1;
        var root = rt("debug", canvasRT); fill(root); var s = safeChild(root);
        dbgPanel = rt("dbgPanel", s); place(dbgPanel, 1, 1, 1, 1, -18, -84, 300, 24);
        var bg = img(dbgPanel, new Color(0, 0, 0, 0.58f)); fill(bg.rectTransform); bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        group<VerticalLayoutGroup>(dbgPanel, 4, TextAnchor.UpperLeft, 8, 6);
        var fit = dbgPanel.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var hb = button(dbgPanel, "記錄", 12, BONE, CLEAR, CLEAR, 0, 0, toggleDbg); le(hb, 284, 18);
        dbgHead = hb.GetComponentInChildren<Text>(); dbgHead.alignment = TextAnchor.MiddleLeft; hb.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft; blockRects.Add((RectTransform)hb.transform);

        var exp = rt("exp", dbgPanel); group<VerticalLayoutGroup>(exp, 4, TextAnchor.UpperLeft); dbgExp = exp.gameObject;
        dbgBody = txt(exp, "", 11, BONE, TextAnchor.UpperLeft, false, true); dbgBody.lineSpacing = 1.15f; le(dbgBody, 284);
        var row = rt("btns", exp); group<HorizontalLayoutGroup>(row, 8, TextAnchor.MiddleLeft);
        var cb = button(row, "複製記錄", 11, BONE, BTN_BG, A(BONE, 0.35f), 8, 3, copyLog); dbgCopyLabel = cb.GetComponentInChildren<Text>(); blockRects.Add((RectTransform)cb.transform);
        var sh = button(row, "分享檔案", 11, BONE, BTN_BG, A(LANTERN, 0.6f), 8, 3, shareLog); dbgShareLabel = sh.GetComponentInChildren<Text>(); blockRects.Add((RectTransform)sh.transform);
        var clr = button(row, "清除計數", 11, DIM, BTN_BG, A(BONE, 0.2f), 8, 3, () => QFLog.clearCounters()); blockRects.Add((RectTransform)clr.transform);
        dbgExp.SetActive(dbgOpen);

        recSys = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        recTotal = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        recGfx = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
        recDraw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        recTris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        root.gameObject.SetActive(false);
    }

    static void toggleDbg() { dbgOpen = !dbgOpen; if (dbgExp != null) dbgExp.SetActive(dbgOpen); PlayerPrefs.SetInt("qf_dbg", dbgOpen ? 1 : 0); dbgT = 0; }

    static void copyLog()
    {
        string txt_ = "【霧鎖千佛山 記錄】\n" + (dbgBody != null ? StripTags(dbgBody.text) : "") + "\n---- 最近記錄 ----\n" + QFLog.recentText(60);
        GUIUtility.systemCopyBuffer = txt_; copyT = 2f; if (dbgCopyLabel != null) dbgCopyLabel.text = "已複製";
    }
    static float shareT;
    static void shareLog()
    {
        dbgT = 0; string res = QFShare.share(dbgBody != null ? StripTags(dbgBody.text) : "");
        shareT = 3f; if (dbgShareLabel != null) dbgShareLabel.text = res;
    }
    static string StripTags(string s) { return System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", ""); }

    static string mb(long bytes) { return bytes <= 0 ? "—" : (bytes >= 1024L * 1024 * 1024 ? (bytes / (1024f * 1024 * 1024)).ToString("0.00") + " GB" : (bytes / (1024 * 1024)) + " MB"); }
    static long recVal(ProfilerRecorder r) { return r.Valid ? r.LastValue : 0; }

    static void debugUpdate(float dt)
    {
        if (dbgPanel == null) return;
        string st = G.state ?? "";
        bool show = st != "title" && st != "loading";
        var rootGo = dbgPanel.parent.parent.gameObject; if (rootGo.activeSelf != show) rootGo.SetActive(show);
        float rdt = Time.unscaledDeltaTime; if (rdt <= 0) return;
        ftBuf[ftI] = rdt * 1000f; ftI = (ftI + 1) % ftBuf.Length; if (ftN < ftBuf.Length) ftN++;
        fpsAcc += rdt; fpsN++;
        if (Input.GetKeyDown(KeyCode.F3)) toggleDbg();
        if (copyT > 0) { copyT -= rdt; if (copyT <= 0 && dbgCopyLabel != null) dbgCopyLabel.text = "複製記錄"; }
        if (shareT > 0) { shareT -= rdt; if (shareT <= 0 && dbgShareLabel != null) dbgShareLabel.text = "分享檔案"; }
        FrameTimingManager.CaptureFrameTimings();
        if (FrameTimingManager.GetLatestTimings(1, fts) > 0) { cpuMs = fts[0].cpuFrameTime; gpuMs = fts[0].gpuFrameTime; }
        watchdog(rdt, st);

        dbgT -= rdt; if (dbgT > 0) return; dbgT = 0.5f;
        lastFps = fpsN / Mathf.Max(1e-4f, fpsAcc); fpsAcc = 0; fpsN = 0;
        Array.Copy(ftBuf, ftSort, ftN); Array.Sort(ftSort, 0, ftN);
        lastP95 = ftN > 0 ? ftSort[Mathf.Min(ftN - 1, (int)(ftN * 0.95f))] : 0; lastWorst = ftN > 0 ? ftSort[ftN - 1] : 0;
        long sys = recVal(recSys), unity = recVal(recTotal), gfx = recVal(recGfx);
        if (unity <= 0) unity = Profiler.GetTotalReservedMemoryLong();
        if (gfx <= 0) gfx = Profiler.GetAllocatedMemoryForGraphicsDriver();
        long memMain = sys > 0 ? sys : unity; lastMemMB = memMain / (1024 * 1024);
        string fpsCol = lastFps >= 55 ? "#a6d07a" : lastFps >= 30 ? "#f0c070" : "#ff7a6a";

        dbgHead.text = (dbgOpen ? "記錄 ▾  " : "記錄 ▸  ") + "<color=" + fpsCol + ">" + lastFps.ToString("0") + " fps</color> · " + mb(memMain)
                       + (QFLog.errors > 0 ? " · <color=#ff7a6a>錯誤 " + QFLog.errors + "</color>" : "") + (QFLog.bugs > 0 ? " · <color=#f0c070>疑似 bug " + QFLog.bugs + "</color>" : "");
        if (!dbgOpen) { periodicLog(st, memMain); return; }

        var p = G.player; var sb = new System.Text.StringBuilder();
        sb.Append("幀率 <color=").Append(fpsCol).Append('>').Append(lastFps.ToString("0")).Append("</color> · 最慢5% ").Append(lastP95.ToString("0.0")).Append(" ms · 最慢 ").Append(lastWorst.ToString("0")).Append(" ms\n");
        sb.Append("CPU ").Append(cpuMs > 0 ? cpuMs.ToString("0.0") + " ms" : "—").Append(" · GPU ").Append(gpuMs > 0 ? gpuMs.ToString("0.0") + " ms" : "—").Append('\n');
        sb.Append("記憶體 ").Append(sys > 0 ? "系統 " + mb(sys) + " · " : "").Append("Unity ").Append(mb(unity)).Append(" · 顯示 ").Append(mb(gfx)).Append('\n');
        sb.Append("裝置 ").Append(SystemInfo.deviceModel).Append(" · ").Append(SystemInfo.graphicsDeviceType).Append(" · ").Append(Screen.width).Append('×').Append(Screen.height).Append('\n');
        sb.Append("光線追蹤 硬體").Append(SystemInfo.supportsRayTracing ? "<color=#a6d07a>支援</color>" : "不支援").Append(" · 遊戲未使用（URP 不支援）\n");
        if (recDraw.Valid && recDraw.LastValue > 0) sb.Append("繪製 ").Append(recDraw.LastValue).Append(" 次 · 三角形 ").Append((recVal(recTris) / 1000f).ToString("0")).Append(" 千\n");
        sb.Append("位置 ").Append(p.inside != null ? p.inside.id : p.zone).Append(" (").Append(p.x.ToString("0")).Append(", ").Append(p.z.ToString("0")).Append(") · ").Append(st).Append('\n');
        sb.Append("錯誤 ").Append(QFLog.errors).Append(" · 警告 ").Append(QFLog.warnings).Append(" · 疑似 bug ").Append(QFLog.bugs);
        if (QFLog.lastError.Length > 0) sb.Append("\n<color=#ff8a7a>最後錯誤：").Append(trunc(QFLog.lastError, 70)).Append("</color>");
        if (QFLog.lastBug.Length > 0) sb.Append("\n<color=#f0c070>最後疑似 bug：").Append(trunc(QFLog.lastBug, 70)).Append("</color>");
        dbgBody.text = sb.ToString();
        periodicLog(st, memMain);
    }
    static string trunc(string s, int n) { return s.Length > n ? s.Substring(0, n) + "…" : s; }

    static void periodicLog(string st, long memMain)
    {
        dbgLogT -= 0.5f; if (dbgLogT > 0 || st != "playing") return; dbgLogT = 10f;
        var p = G.player;
        QFLog.info("效能 " + lastFps.ToString("0") + " fps · 最慢5% " + lastP95.ToString("0.0") + " ms · 最慢 " + lastWorst.ToString("0") + " ms · CPU " + cpuMs.ToString("0.0") + " · GPU " + gpuMs.ToString("0.0")
                   + " · 記憶體 " + mb(memMain) + " · " + (p.inside != null ? p.inside.id : p.zone) + " (" + p.x.ToString("0") + "," + p.z.ToString("0") + ") · 夜 " + G.night.ToString("0.0") + " · 手電筒 " + (G.flashOn() ? "開" : "關"));
    }

    // ---------- watchdog: things that should never happen ----------
    static float slowCheckT;
    static void watchdog(float rdt, string st)
    {
        if (st != lastStateDbg) { QFLog.info("狀態 " + (lastStateDbg.Length > 0 ? lastStateDbg : "—") + " → " + st); lastStateDbg = st; }
        if (st != "playing") { stuckT = 0; return; }
        var p = G.player; string where = (p.inside != null ? "室內 " + p.inside.id : p.zone) + " (" + p.x.ToString("0.0") + ", " + p.z.ToString("0.0") + ")";
        if (float.IsNaN(p.x) || float.IsNaN(p.z) || Mathf.Abs(p.x) > G.XB + 1 || p.z > G.ZS + 1 || p.z < G.ZN - 1) QFLog.bug("oob", "玩家位置異常：" + where);
        // stuck: holding a direction but not moving (door sills, narrow entrances, stairs)
        bool wants = key("KeyW") || key("KeyA") || key("KeyS") || key("KeyD") || key("ArrowUp") || key("ArrowDown") || key("ArrowLeft") || key("ArrowRight") || (joy.x * joy.x + joy.y * joy.y) > 0.25f;
        if (wants)
        {
            if (stuckT == 0) { stuckX = p.x; stuckZ = p.z; }
            stuckT += rdt;
            if (stuckT > 2.5f)
            {
                if (G.hypot(p.x - stuckX, p.z - stuckZ) < 0.35f) QFLog.bug("stuck" + Mathf.RoundToInt(p.x / 2) + "_" + Mathf.RoundToInt(p.z / 2), "可能卡住：按著移動 2.5 秒幾乎沒動，" + where);
                stuckT = 0;
            }
        }
        else stuckT = 0;
        // hitches
        hitchCD -= rdt;
        if (rdt > 0.1f && hitchCD <= 0 && Time.realtimeSinceStartup - AutoTest.lastShotTime > 1f) { hitchCD = 5; QFLog.info("[卡頓] 一幀 " + (rdt * 1000).ToString("0") + " ms，" + where + "，夜 " + G.night.ToString("0.0") + "，手電筒 " + (G.flashOn() ? "開" : "關")); }
        if (p.y < G.groundH(p.x, p.z) - 1.5f) QFLog.bug("fall" + Mathf.RoundToInt(p.x / 4) + "_" + Mathf.RoundToInt(p.z / 4), "玩家掉到地面以下：" + where);

        slowCheckT -= rdt; if (slowCheckT > 0) return; slowCheckT = 0.5f;
        int taken = 0; foreach (var pk in G.PICKUPS) if (pk.type == "key" && pk.taken) taken++;
        if (taken != p.keys) QFLog.bug("keys" + taken + "_" + p.keys, "鑰匙數不一致：畫面 " + p.keys + " 把，實際撿到 " + taken + " 把");
        if (G.GATEOBJ != null && G.GATEOBJ.opening && p.keys < 3) QFLog.bug("gate", "山門在鑰匙不足時打開了（" + p.keys + " 把）");
        if (p.charms < 0) QFLog.bug("charm", "護身符數量變成負數：" + p.charms);
        for (int i = 0; i < G.LANTERNS.Count; i++)
        {
            var L = G.LANTERNS[i]; if (!L.lit) continue;
            foreach (var c in G.CHASERS)
            {
                if (c.dormant || c.hall != null) continue;
                if (G.hypot(c.x - L.x, c.z - L.z) < G.SAFE_R - 0.6f) { QFLog.bug("lant" + i, "石僧進入了亮著的燈籠光圈（燈籠 " + i + "）"); break; }
            }
        }
    }
}
