// Entry point: creates everything from code (the scene is empty), drives the main loop, post-processing and the automated smoke test.
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Boot : MonoBehaviour
{
    public static Boot inst; public static bool ready; public static string bootError;
    Volume vol; ColorAdjustments ca; Bloom bloom; ChromaticAberration chroma; LensDistortion lens; MotionBlur mblur;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Go() { if (inst != null) return; var go = new GameObject("Boot"); DontDestroyOnLoad(go); inst = go.AddComponent<Boot>(); }

    IEnumerator Start()
    {
        Application.targetFrameRate = 60; QualitySettings.vSyncCount = 1; Screen.sleepTimeout = SleepTimeout.NeverSleep;
        G.state = "loading";
        if (!AutoTest.active) Save.load();
        G.renderInit(); G.thingsInit(); setupPost(); UI.init();
        UI.setLoad(0.02f, "正在把佛像搬上山……", false);
        yield return null; yield return null;
        float lastYield = Time.realtimeSinceStartup;
        Exception err = null;
        var steps = G.loadAllSteps((f, t) => { UI.setLoad(0.02f + f * 0.7f, t, false); });
        while (true)
        {
            bool more;
            try { more = steps.MoveNext(); } catch (Exception e) { err = e; break; }
            if (!more) break;
            if (Time.realtimeSinceStartup - lastYield > 0.05f) { yield return null; lastYield = Time.realtimeSinceStartup; }
        }
        if (err != null) { fail("素材沒有載入成功", err); yield break; }
        UI.setLoad(0.75f, "正在種樹、起霧……", false);
        yield return null; yield return null;
        try
        {
            int seed = AutoTest.seedOverride != 0 ? AutoTest.seedOverride : (Save.hasSave ? Save.data.seed : UnityEngine.Random.Range(1, int.MaxValue));
            G.seedWorld(seed);
            G.boot();
        }
        catch (Exception e) { err = e; }
        if (err != null) { fail("建立場景失敗", err); yield break; }
        UI.setLoad(1f, "準備好了。", true);
        ready = true;
        Debug.Log("[QF] boot ok, seed " + G.SEED + ", statues " + G.STATUES.Count + ", chasers " + G.CHASERS.Count + ", batches " + G.BATCHES.Count);
    }

    void fail(string what, Exception e) { bootError = what + "：" + e.Message; Debug.LogException(e); UI.setLoad(0f, bootError, false); }

    void Update()
    {
        if (!ready) { if (G.camera.cam != null) G.renderApply(); return; }
        float dt = Time.unscaledDeltaTime;
        try { G.frame(dt); } catch (Exception e) { Debug.LogException(e); }
        try { UI.update(dt); } catch (Exception e) { Debug.LogException(e); }
        updatePost(dt);
        G.renderApply();
    }

    void setupPost()
    {
        var go = new GameObject("PostFX"); DontDestroyOnLoad(go); vol = go.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 10;
        var p = ScriptableObject.CreateInstance<VolumeProfile>(); vol.sharedProfile = p;
        var tm = p.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
        ca = p.Add<ColorAdjustments>(true); ca.postExposure.Override(0.26f); ca.saturation.Override(-16f); ca.contrast.Override(3f);
        bloom = p.Add<Bloom>(true); bloom.threshold.Override(0.95f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.72f); bloom.highQualityFiltering.Override(true);
        chroma = p.Add<ChromaticAberration>(true); chroma.intensity.Override(0f);
        lens = p.Add<LensDistortion>(true); lens.intensity.Override(0f);
        mblur = p.Add<MotionBlur>(true); mblur.intensity.Override(0.12f); mblur.quality.Override(MotionBlurQuality.Medium);
        var wb = p.Add<WhiteBalance>(true); wb.temperature.Override(-6f);
    }

    void updatePost(float dt)
    {
        if (ca == null) return;
        // web version: canvas.style.filter = saturate(0.84*(0.6+0.4*s)) contrast(1.03+(1-s)*0.25)
        ca.saturation.Override((UI.saturation - 1f) * 100f);
        ca.contrast.Override((UI.contrast - 1f) * 100f);
        float s = Mathf.Clamp01(G.player.sanity / 100f), danger = G.player.danger;
        bool live = G.state == "playing" || G.state == "dying";
        chroma.intensity.Override(live ? Mathf.Clamp01((1 - s) * 0.45f + danger * 0.35f + G.shake * 0.5f) : 0f);
        lens.intensity.Override(live ? -0.12f * Mathf.Clamp01(danger * 0.8f + (1 - s) * 0.5f) : 0f);
    }
}
