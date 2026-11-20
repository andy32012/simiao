// Lets the Files app on iPad see the game's Documents folder (qf_log.txt, save.json), so the record can be shared.
#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class QFiOSPostBuild
{
    [PostProcessBuild(100)]
    public static void OnPostBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument(); plist.ReadFromFile(plistPath);
        plist.root.SetBoolean("UIFileSharingEnabled", true);
        plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);
        plist.WriteToFile(plistPath);
    }
}
#endif
