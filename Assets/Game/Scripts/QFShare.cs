// "分享檔案": bundles device info, the panel's numbers, the whole qf_log.txt and the save into one text file,
// then opens the iPad share sheet (Gmail, Mail, AirDrop, LINE, Save to Files…) or, on Windows, Explorer with the file selected.
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public static class QFShare
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void QFShareFile(string path, string subject);
#endif

    static string readShared(string path)
    {
        try
        {
            if (!File.Exists(path)) return "";
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sr = new StreamReader(fs, System.Text.Encoding.UTF8)) return sr.ReadToEnd();
        }
        catch (Exception e) { return "（讀取失敗：" + e.Message + "）"; }
    }

    /// <summary>Writes the bundle and returns its path (null on failure).</summary>
    public static string makeBundle(string panelText)
    {
        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "share"); Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "QianFoShan_log_*.txt")) { try { File.Delete(old); } catch (Exception) { } }
            string path = Path.Combine(dir, "QianFoShan_log_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".txt");
            var sb = new System.Text.StringBuilder();
            sb.Append("【霧鎖千佛山 記錄】 ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(" · 版本 ").Append(Application.version).Append('\n');
            sb.Append("裝置 ").Append(SystemInfo.deviceModel).Append(" · ").Append(SystemInfo.operatingSystem).Append('\n');
            sb.Append("顯示卡 ").Append(SystemInfo.graphicsDeviceName).Append(" · ").Append(SystemInfo.graphicsDeviceType).Append(" · 系統記憶體 ").Append(SystemInfo.systemMemorySize).Append(" MB\n");
            sb.Append("螢幕 ").Append(Screen.width).Append('×').Append(Screen.height).Append(" · 錯誤 ").Append(QFLog.errors).Append(" · 警告 ").Append(QFLog.warnings).Append(" · 疑似 bug ").Append(QFLog.bugs).Append("\n\n");
            sb.Append("---- 面板 ----\n").Append(panelText).Append("\n\n");
            string oldLog = Path.Combine(Application.persistentDataPath, "qf_log_old.txt");
            if (File.Exists(oldLog)) sb.Append("---- 較早的記錄 ----\n").Append(readShared(oldLog)).Append('\n');
            sb.Append("---- 記錄 ----\n").Append(readShared(QFLog.filePath)).Append('\n');
            string save = Path.Combine(Application.persistentDataPath, "save.json");
            if (File.Exists(save)) sb.Append("---- 存檔 ----\n").Append(readShared(save)).Append('\n');
            File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(false));
            return path;
        }
        catch (Exception e) { Debug.LogWarning("share bundle failed: " + e.Message); return null; }
    }

    /// <summary>Returns a short status text for the button.</summary>
    public static string share(string panelText)
    {
        string path = makeBundle(panelText);
        if (path == null) return "失敗";
        QFLog.info("已產生分享檔：" + Path.GetFileName(path));
#if UNITY_IOS && !UNITY_EDITOR
        try { QFShareFile(path, "霧鎖千佛山 記錄"); return "已開啟分享"; } catch (Exception e) { Debug.LogWarning("share sheet failed: " + e.Message); return "失敗"; }
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path.Replace('/', '\') + "\""); return "已開啟資料夾"; }
        catch (Exception) { GUIUtility.systemCopyBuffer = path; return "路徑已複製"; }
#else
        GUIUtility.systemCopyBuffer = path; return "路徑已複製";
#endif
    }
}
