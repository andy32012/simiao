// Game-wide log: counts errors / warnings / suspected bugs, keeps the recent lines for the on-screen panel and writes
// everything to <persistentDataPath>/qf_log.txt (on iPad: Files app → 我的 iPad → 霧鎖千佛山).
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class QFLog
{
    public static int errors, warnings, bugs;
    public static string lastError = "", lastBug = "";
    public static string filePath = "";
    static readonly object lk = new object();
    static readonly List<string> recent = new List<string>();
    static readonly HashSet<string> bugKeys = new HashSet<string>();
    static StreamWriter w;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        try
        {
            filePath = Path.Combine(Application.persistentDataPath, "qf_log.txt");
            if (File.Exists(filePath) && new FileInfo(filePath).Length > 2 * 1024 * 1024)
            {
                string old = Path.Combine(Application.persistentDataPath, "qf_log_old.txt");
                if (File.Exists(old)) File.Delete(old);
                File.Move(filePath, old);
            }
            w = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete), new System.Text.UTF8Encoding(false));
        }
        catch (Exception) { w = null; }
        write("========== 啟動 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " · 版本 " + Application.version + " ==========");
        write("裝置 " + SystemInfo.deviceModel + " · " + SystemInfo.operatingSystem);
        write("顯示卡 " + SystemInfo.graphicsDeviceName + " · " + SystemInfo.graphicsDeviceType + " · 顯示記憶體 " + SystemInfo.graphicsMemorySize + " MB · 系統記憶體 " + SystemInfo.systemMemorySize + " MB");
        write("光線追蹤硬體支援：" + (SystemInfo.supportsRayTracing ? "是" : "否") + "（本遊戲使用 URP，不支援光線追蹤）");
        Application.logMessageReceivedThreaded += onLog;
    }

    static void onLog(string cond, string stack, LogType t)
    {
        if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert)
        {
            errors++; lastError = firstLine(cond);
            string st = "";
            if (!string.IsNullOrEmpty(stack)) { var ls = stack.Split('\n'); for (int i = 0; i < ls.Length && i < 3; i++) st += "\n      " + ls[i].Trim(); }
            write("[錯誤] " + cond + st);
        }
        else if (t == LogType.Warning) { warnings++; if (warnings <= 200) write("[警告] " + firstLine(cond)); }
    }

    /// <summary>Record a suspected gameplay bug once per key (key groups repeats, e.g. one entry per stuck location).</summary>
    public static void bug(string key, string msg)
    {
        lock (lk) { if (bugKeys.Contains(key)) return; bugKeys.Add(key); }
        bugs++; lastBug = msg; write("[疑似 bug] " + msg);
    }
    public static void info(string msg) { write(msg); }

    static string firstLine(string s) { if (string.IsNullOrEmpty(s)) return ""; int i = s.IndexOf('\n'); return i >= 0 ? s.Substring(0, i) : s; }

    static void write(string line)
    {
        string s = DateTime.Now.ToString("HH:mm:ss ") + line;
        lock (lk)
        {
            recent.Add(s); if (recent.Count > 80) recent.RemoveAt(0);
            if (w != null) { try { w.WriteLine(s); w.Flush(); } catch (Exception) { } }
        }
    }

    /// <summary>Last n log lines (newest last).</summary>
    public static string recentText(int n)
    {
        lock (lk)
        {
            var sb = new System.Text.StringBuilder(); int s = Math.Max(0, recent.Count - n);
            for (int i = s; i < recent.Count; i++) sb.Append(recent[i]).Append('\n');
            return sb.ToString();
        }
    }
    public static void clearCounters() { lock (lk) { errors = warnings = bugs = 0; lastError = lastBug = ""; bugKeys.Clear(); } write("（計數已清除）"); }
}
