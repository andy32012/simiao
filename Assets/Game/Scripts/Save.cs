// Persistent save (written at stone lanterns). The web version had no persistent save at all.
using System;
using System.IO;
using UnityEngine;

[Serializable]
public class SaveData
{
    public int version = 1, seed, nights, deaths, keys, charms, lantern;
    public float hour, elapsed, battery, sanity, stamina, wet;
    public string weather = "mist", name = "";
    public bool[] lanternLit, pickupTaken, steleRead;
    public string[] flags;
    public bool wasNight;
}

public static class Save
{
    public static SaveData data; public static bool hasSave;
    static string path { get { return Path.Combine(Application.persistentDataPath, AutoTest.active ? "save_test.json" : "save.json"); } }
    public static void load()
    {
        hasSave = false; data = null;
        try { if (File.Exists(path)) { data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path)); hasSave = data != null && data.version == 1 && data.seed != 0; } }
        catch (Exception e) { Debug.LogWarning("save load failed: " + e.Message); }
    }
    public static void write(SaveData d) { try { File.WriteAllText(path, JsonUtility.ToJson(d)); data = d; hasSave = true; } catch (Exception e) { Debug.LogWarning("save write failed: " + e.Message); } }
    public static void clear() { try { if (File.Exists(path)) File.Delete(path); } catch { } data = null; hasSave = false; }
}
