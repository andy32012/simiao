// Project configuration + build entry points (run from the command line with -executeMethod QFSetup.X).
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class QFSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("QF/Configure Project")]
    public static void Configure()
    {
        // ---- empty scene: everything is created by Boot at runtime ----
        if (!File.Exists(ScenePath)) { var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorSceneManager.SaveScene(sc, ScenePath); }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        // ---- URP assets: Forward+, HDR, additional light shadows, SSAO ----
        foreach (var name in new[] { "PC", "Mobile" })
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/" + name + "_RPAsset.asset");
            if (rp != null)
            {
                var so = new SerializedObject(rp);
                Set(so, "m_SupportsHDR", true); Set(so, "m_MSAA", 1); Set(so, "m_RenderScale", 1f);
                Set(so, "m_MainLightRenderingMode", 1); Set(so, "m_MainLightShadowsSupported", true);
                Set(so, "m_AdditionalLightsRenderingMode", 1); Set(so, "m_AdditionalLightsPerObjectLimit", 8);
                Set(so, "m_AdditionalLightShadowsSupported", true); Set(so, "m_AdditionalLightsShadowmapResolution", 4096);
                Set(so, "m_SoftShadowsSupported", true); Set(so, "m_SoftShadowQuality", 3);
                Set(so, "m_ShadowDistance", 70f); Set(so, "m_RequireDepthTexture", true); Set(so, "m_RequireOpaqueTexture", false);
                Set(so, "m_SupportsLightCookies", false); Set(so, "m_UseSRPBatcher", true);
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(rp);
            }
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/" + name + "_Renderer.asset");
            if (rd != null)
            {
                var so = new SerializedObject(rd);
                Set(so, "m_RenderingMode", 2);            // Forward+
                Set(so, "m_DepthPrimingMode", 0);
                so.ApplyModifiedPropertiesWithoutUndo();
                ScreenSpaceAmbientOcclusion ssao = null;
                foreach (var f in rd.rendererFeatures) if (f is ScreenSpaceAmbientOcclusion) ssao = (ScreenSpaceAmbientOcclusion)f;
                if (ssao == null)
                {
                    ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>(); ssao.name = "SSAO";
                    AssetDatabase.AddObjectToAsset(ssao, rd); rd.rendererFeatures.Add(ssao);
                    var so2 = new SerializedObject(rd); so2.Update();
                    var maps = so2.FindProperty("m_RendererFeatureMap");
                    if (maps != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string g, out long id)) { maps.arraySize++; maps.GetArrayElementAtIndex(maps.arraySize - 1).longValue = id; }
                    so2.ApplyModifiedPropertiesWithoutUndo();
                }
                var sso = new SerializedObject(ssao);
                Set(sso, "m_Settings.Intensity", 1.6f); Set(sso, "m_Settings.Radius", 0.45f); Set(sso, "m_Settings.DirectLightingStrength", 0.35f);
                Set(sso, "m_Settings.Falloff", 60f); Set(sso, "m_Settings.AfterOpaque", false);
                sso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rd);
            }
        }

        // ---- always include the runtime-created shaders ----
        var gs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        // materials are created at runtime, so Unity cannot see that instancing / fog variants are used: keep them all
        Set(gs, "m_InstancingStripping", 2); Set(gs, "m_FogStripping", 0); Set(gs, "m_LightmapStripping", 0);
        var arr = gs.FindProperty("m_AlwaysIncludedShaders");
        var have = new HashSet<Object>(); for (int i = 0; i < arr.arraySize; i++) have.Add(arr.GetArrayElementAtIndex(i).objectReferenceValue);
        foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Game/Shaders" }))
        {
            var sh = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
            if (sh != null && !have.Contains(sh)) { arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh; }
        }
        gs.ApplyModifiedPropertiesWithoutUndo();

        // ---- player ----
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.qianfoshan.fogtemple");
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.qianfoshan.fogtemple");
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPadOnly; PlayerSettings.iOS.requiresFullScreen = true; PlayerSettings.iOS.hideHomeButton = true;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = false;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[QF] Configure done");
    }

    static void Set(SerializedObject so, string prop, object v)
    {
        var p = so.FindProperty(prop); if (p == null) { Debug.LogWarning("[QF] no property " + prop + " on " + so.targetObject.name); return; }
        if (v is bool b) { if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = b; else p.intValue = b ? 1 : 0; }
        else if (v is int i) { if (p.propertyType == SerializedPropertyType.Float) p.floatValue = i; else p.intValue = i; }
        else if (v is float f) p.floatValue = f;
    }

    [MenuItem("QF/Build Windows")]
    public static void BuildWindows()
    {
        Configure();
        var opts = new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = "Builds/Windows/QianFoShan.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None };
        var rep = BuildPipeline.BuildPlayer(opts);
        Debug.Log("[QF] build result: " + rep.summary.result + " errors=" + rep.summary.totalErrors + " size=" + rep.summary.totalSize);
        if (Application.isBatchMode) EditorApplication.Exit(rep.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }

    [MenuItem("QF/Build iOS (Xcode project)")]
    public static void BuildIOS()
    {
        Configure();
        var opts = new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = "Builds/iOS", target = BuildTarget.iOS, options = BuildOptions.None };
        var rep = BuildPipeline.BuildPlayer(opts);
        Debug.Log("[QF] build result: " + rep.summary.result + " errors=" + rep.summary.totalErrors);
        if (Application.isBatchMode) EditorApplication.Exit(rep.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }

    /// <summary>Batch entry: just compile + configure, then quit (use to check for compile errors).</summary>
    public static void ConfigureAndQuit() { Configure(); if (Application.isBatchMode) EditorApplication.Exit(0); }

    /// <summary>Batch entry: open the main scene and enter play mode (AutoTest drives it when -qftest is passed).</summary>
    public static void PlayTest()
    {
        Configure();
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.EnterPlaymode();
    }
}
