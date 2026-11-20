// Automated smoke test, enabled with the command line switch -qftest (editor play mode or player).
// Starts a run, teleports through a list of viewpoints, saves screenshots to <project>/Shots (or persistentDataPath/Shots) and quits.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AutoTest : MonoBehaviour
{
    public static int seedOverride; public static bool active; static string outDir;
    public static int errorCount; static string firstError;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        var args = Environment.GetCommandLineArgs(); bool on = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-qftest") on = true;
            if (args[i] == "-qfseed" && i + 1 < args.Length) int.TryParse(args[i + 1], out seedOverride);
            if (args[i] == "-qfout" && i + 1 < args.Length) outDir = args[i + 1];
        }
        if (!on) return;
        active = true; if (seedOverride == 0) seedOverride = 12345;
        if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Application.isEditor ? Directory.GetCurrentDirectory() : Application.persistentDataPath, "Shots");
        Directory.CreateDirectory(outDir);
        Application.logMessageReceived += (c, s, t) => { if (t == LogType.Exception || t == LogType.Error) { errorCount++; if (firstError == null) firstError = c + "\n" + s; } };
        var go = new GameObject("AutoTest"); DontDestroyOnLoad(go); go.AddComponent<AutoTest>();
    }

    IEnumerator Start()
    {
        float t0 = Time.realtimeSinceStartup;
        while (!Boot.ready && Boot.bootError == null && Time.realtimeSinceStartup - t0 < 300) yield return null;
        log("boot ready=" + Boot.ready + " err=" + Boot.bootError + " in " + (Time.realtimeSinceStartup - t0).ToString("0.0") + "s");
        if (!Boot.ready) { quit(2); yield break; }
        yield return wait(1.0f); yield return shot("00_title");
        G.startGame(); yield return wait(2.5f); yield return shot("01_start");
        G.player.yaw = 1.2f; yield return wait(0.5f); yield return shot("02_start_left");
        G.player.yaw = Mathf.PI; yield return wait(0.5f); yield return shot("03_start_back");
        for (int k = 0; k < 4; k++) { yield return tp(G.paths[0].pts, 0.12f + k * 0.2f); yield return shot("04_path_" + k); }
        foreach (var t in G.TEMPLES)
        {
            var h = t.H as Hall; if (h == null) continue; var d = h.W(0, h.d / 2 + 9); var c = h.W(0, 0);
            place(d[0], d[1], Mathf.Atan2(-(c[0] - d[0]), -(c[1] - d[1]))); yield return wait(0.6f); yield return shot("05_out_" + h.id);
            var q = h.W(0, h.d / 2 - 1.5f); place(q[0], q[1], Mathf.Atan2(-(c[0] - q[0]), -(c[1] - q[1]))); G.player.flash = true; yield return wait(0.8f); yield return shot("06_in_" + h.id);
        }
        G.hour = 22.5f; G.player.flash = true; yield return tp(G.paths[0].pts, 0.4f); yield return wait(1.0f); yield return shot("07_night_torch");
        log("torch enabled=" + G.torch.l.enabled + " intensity=" + G.torch.l.intensity + " range=" + G.torch.l.range + " color=" + G.torch.l.color + " shadows=" + G.torch.l.shadows);
        G.torch.l.shadows = LightShadows.None; yield return wait(0.4f); yield return shot("07b_night_torch_noshadow"); G.torch.l.shadows = LightShadows.Soft;
        G.player.flash = false; yield return wait(0.5f); yield return shot("08_night_dark");
        { var L0 = G.LANTERNS[0]; place(L0.x + 4.0f, L0.z + 4.0f, Mathf.Atan2(4f, 4f)); yield return wait(1.0f); yield return shot("08b_lit_lantern_night");
          log("lampA enabled=" + G.lampA.l.enabled + " int=" + G.lampA.l.intensity + " range=" + G.lampA.l.range + " pos=" + G.lampA.l.transform.position); }
        var L = G.LANTERNS[1]; place(L.x + 3.0f, L.z + 3.0f, Mathf.Atan2(3f, 3f)); yield return wait(1.2f); yield return shot("09_lantern_night");
        G.hour = 10f; yield return wait(0.3f);
        var m = G.m1; var cc = m.center(m.entC, 0); place(cc[0], cc[1] + 14, 0); yield return wait(0.8f); yield return shot("10_maze1_entry");
        place(0, 60, 0.6f); yield return wait(0.8f); yield return shot("11_field");
        place(0, -296, 0); yield return wait(0.8f); yield return shot("12_gate");
        UI.openMap(); yield return wait(0.4f); yield return shot("13_map"); UI.closeMap();
        // ---------- gameplay checks from the bug list ----------
        G.player.grace = 999; G.hour = 11f;
        place(0, -315, 0); yield return wait(1.5f); check("gate stays locked with 0 keys", !G.GATEOBJ.opening);
        Pickup k2 = null, k3 = null, k6 = null;
        foreach (var p in G.PICKUPS) if (p.type == "key" && p.hall != null) { if (p.hall.id == "t2") k2 = p; if (p.hall.id == "t3") k3 = p; if (p.hall.id == "t6") k6 = p; }
        check("three key pickups exist", k2 != null && k3 != null && k6 != null);
        place(k2.x, k2.z, 0); yield return wait(0.5f); place(k2.x, k2.z, 0); yield return wait(0.5f);
        place(k3.x, k3.z, 0); yield return wait(0.5f); place(k3.x, k3.z, 0); yield return wait(0.5f);
        check("picked two keys", G.player.keys == 2 && k2.taken && k3.taken);
        place(k2.x, k2.z, 0); yield return wait(0.4f); check("same key cannot be taken twice", G.player.keys == 2);
        place(0, -315, 0); yield return wait(1.0f); check("gate stays locked with 2 keys", !G.GATEOBJ.opening);
        var L2 = G.LANTERNS[2]; place(L2.x + 1.5f, L2.z, 0); yield return wait(0.8f); G.player.lastLantern = L2; G.saveNow();
        check("save written", Save.hasSave && Save.data.pickupTaken[k2.id] && Save.data.pickupTaken[k3.id] && !Save.data.pickupTaken[k6.id]);
        G.player.grace = 0; G.player.safe = false; G.die(G.CHASERS[0]); yield return wait(3.2f); yield return shot("14_dead"); check("death screen", G.state == "dead");
        G.respawn(); yield return wait(0.8f); check("respawn keeps keys", G.player.keys == 2 && G.state == "playing");
        // simulate quitting and launching again
        Save.load(); G.continueRun = true; G.startGame(); yield return wait(1.0f); yield return shot("15_continue");
        check("continue restores 2/3", G.player.keys == 2 && k2.taken && k3.taken && !k6.taken && !k2.g.visible && k6.g.visible);
        check("continue spawns at saved lantern", Mathf.Abs(G.player.x - L2.x) < 4 && Mathf.Abs(G.player.z - L2.z) < 5);
        G.player.grace = 999;
        place(k6.x, k6.z, 0); yield return wait(0.5f); place(k6.x, k6.z, 0); yield return wait(0.5f); check("third key", G.player.keys == 3);
        place(0, -315, 0); yield return wait(4.0f); check("gate opens with 3 keys", G.GATEOBJ.opening && G.GATEOBJ.open > 0.6f); yield return shot("16_gate_open");
        place(0, -326, 0); yield return wait(1.0f); yield return shot("17_win"); check("win", G.state == "won" && !Save.hasSave);
        log("fps~" + (1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("0") + " errors=" + errorCount + " fails=" + fails + (firstError != null ? " first: " + firstError : ""));
        Save.clear();
        quit(errorCount > 0 || fails > 0 ? 1 : 0);
    }

    static int fails;
    static void check(string what, bool ok) { if (!ok) fails++; log((ok ? "PASS " : "FAIL ") + what + "  keys=" + G.player.keys + " state=" + G.state); }
    static void place(float x, float z, float yaw) { var p = G.player; p.x = x; p.z = z; p.y = G.groundH(x, z); p.yaw = yaw; p.pitch = 0; p.vx = p.vz = 0; }
    IEnumerator tp(List<float[]> pts, float f) { var q = G.pointAlong(pts, f); place(q[0], q[1], Mathf.Atan2(-q[2], -q[3])); yield return wait(0.7f); }
    static IEnumerator wait(float s) { float t = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t < s) yield return null; }
    IEnumerator shot(string name)
    {
        yield return new WaitForEndOfFrame();
        try
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG()); Destroy(tex);
            log("shot " + name + " p=(" + G.player.x.ToString("0.0") + "," + G.player.z.ToString("0.0") + ") state=" + G.state + " dt=" + Time.deltaTime.ToString("0.000"));
        }
        catch (Exception e) { log("shot failed " + name + ": " + e.Message); }
    }
    static void log(string s) { Debug.Log("[QFTEST] " + s); try { File.AppendAllText(Path.Combine(outDir, "log.txt"), s + "\n"); } catch { } }
    static void quit(int code)
    {
        log("quit " + code);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
}
