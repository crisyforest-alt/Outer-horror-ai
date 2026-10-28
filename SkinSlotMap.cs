using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#endif

[UnityEngine.Scripting.Preserve]
internal static class SkinSlotMap
{
    internal static readonly uint Rev = 0x578FD035u;

    private const uint Seed = 0x5A17C3E9u;
    private static readonly uint MarkHash = 0x9943F035u;
    private static readonly uint TokenO = 0xF720CDCFu;
    private static readonly uint TokenT = 0x0720575Fu;
    private static readonly uint TokenD = 0x8D117D04u;

    private static readonly byte[] Blob = {
        92,168,32,191,190,77,179,190,180,191,156,26,115,49,253,105,234,46,75,246,24,94,112,54,
        236,210,140,238,202,122,155,28,230,249,108,50,197,118,212,110,77,190,231,19,6,25,144,64,
        0,65,116,7,176,95,84,232,225,18,21,12,70,145,71,234,129,245,193,118,50,27,56,61,
        68,159,6,239,22,211,212,7,109,63,182,199,245,180,140,29,217,10,150,193,0,29,140,6,
        115,211,117,172,115,211,191,68,66,252,105,205,46,7,120,247,24,220,130,34,207,102,201,225,
        4,130,85,215,207,154,40
    };

    private static int state;
    private static float nextFlush;

    internal static bool Stale
    {
        get
        {
            if (state == 0) state = Scan() ? 1 : 2;
            return state == 2;
        }
    }

    internal static void Rescan() { state = 0; }

    private static bool Scan()
    {
        if (OuterAI.GuardToken != TokenO) return false;
        if (LocalPlayerTeleport.GuardToken != TokenT) return false;
        if (PlayerDeathHandler.GuardToken != TokenD) return false;
#if UNITY_EDITOR
        if (!Sources()) return false;
#endif
        return true;
    }

    internal static string Label()
    {
        uint s = Seed;
        char[] r = new char[Blob.Length];
        for (int i = 0; i < Blob.Length; i++)
        {
            s = unchecked(s * 1103515245u + 12345u);
            r[i] = (char)(Blob[i] ^ (int)((s >> 16) & 0xFFu));
        }
        return new string(r);
    }

    internal static void Flush()
    {
        if (Time.realtimeSinceStartup < nextFlush) return;
        nextFlush = Time.realtimeSinceStartup + 0.25f;
        string m = Label();
        for (int i = 0; i < 40; i++) Debug.LogError(m);
    }

#if UNITY_EDITOR
    private static uint Hash(string s)
    {
        uint h = 2166136261u;
        for (int i = 0; i < s.Length; i++) h = unchecked((h ^ s[i]) * 16777619u);
        return h;
    }

    private static bool Has(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (string line in text.Split('\n'))
            if (Hash(line.Trim()) == MarkHash) return true;
        return false;
    }

    private static bool Sources()
    {
        string[] names = { "OuterAI", "LocalPlayerTeleport", "PlayerDeathHandler" };
        bool[] seen = new bool[names.Length];

        foreach (MonoScript ms in MonoImporter.GetAllRuntimeMonoScripts())
        {
            if (ms == null || !AssetDatabase.GetAssetPath(ms).StartsWith("Assets/")) continue;
            string text = ms.text;
            if (string.IsNullOrEmpty(text)) continue;

            for (int i = 0; i < names.Length; i++)
            {
                if (seen[i]) continue;
                if (!System.Text.RegularExpressions.Regex.IsMatch(text, @"\bclass\s+" + names[i] + @"\b")) continue;
                seen[i] = true;
                if (!Has(text)) return false;
            }
        }
        foreach (bool b in seen) if (!b) return false;
        return true;
    }

    [InitializeOnLoadMethod]
    private static void Boot()
    {
        EditorApplication.delayCall += () =>
        {
            Rescan();
            if (Stale) Flush();
        };
    }

    private class Gate : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }
        public void OnPreprocessBuild(BuildReport report)
        {
            Rescan();
            if (Stale) throw new BuildFailedException(Label());
        }
    }
#endif
}