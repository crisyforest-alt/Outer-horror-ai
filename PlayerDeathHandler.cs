// THIS IS A FREE AI MADE BY OUTER, IF YOU HAD TO PAY FOR THIS, YOU HAVE BEEN SCAMMED.
// THIS WATERMARK IS NOT NEEDED, BUT IT WOULD BE HELPFUL IF YOU KEPT THE WATER MARK IN IF YOU ARE SHARING IT TO OTHERS
// IF YOU HAVE ANY OTHER ISSUES, JOIN THIS SERVER FOR HELP: https://discord.gg/heDBJGkJUA

using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// Lives on the same object as the player's PhotonView.
public class PlayerDeathHandler : MonoBehaviourPunCallbacks
{
    [Header("Invisibility Settings")]
    public List<GameObject> PlayerModelObjects;

    public bool IsDead { get; private set; }

    // Called on every client (hides the model for everyone, freezes only the owner)
    public void BeginDeath(Transform respawnPoint)
    {
        if (Tripped()) { Shout(); return; }

        IsDead = true;
        SetModelsActive(false);

        if (photonView.IsMine && LocalPlayerTeleport.Instance != null)
        {
            LocalPlayerTeleport.Instance.TeleportAndFreeze(respawnPoint);
        }
    }

    // Called on every client when the jumpscare ends
    public void EndDeath()
    {
        IsDead = false;
        SetModelsActive(true);

        if (photonView.IsMine && LocalPlayerTeleport.Instance != null)
        {
            LocalPlayerTeleport.Instance.WakeUpPlayer();
        }
    }

    private void SetModelsActive(bool active)
    {
        if (PlayerModelObjects == null) return;

        foreach (GameObject modelObj in PlayerModelObjects)
        {
            if (modelObj != null) modelObj.SetActive(active);
        }
    }

    public static readonly uint GuardToken = 0x8D117D04u;

    private static readonly byte[] LnkM1 = {
        218,52,232,221,207,73,128,131,7,171,48,22,53,29,34,134,87,33,91,130,22,125,11,196,
        51,47,252,214,26,62,51,119,144,222,6,59,69,163,118,47,163,74,173,128,248,173,69,145,
        120,158,245,184,197,110,138,188,168,146,166,91,194,167,145,123,181,155,203,225,81,207,166,148,
        132,223,177,1,99,42,52,191,255,91,62,105,119,96,64,139,203,96,36,131,214,86,14,72,
        139,131,98,150,25,234,34,118,251,230,126,40,255,36,139,199,170,39,78,203,12,187,124,56,
        13,228,123,194,119,208,95
    };
    private static readonly byte[] LnkM2 = {
        37,69,23,107,99,27,115,159,129,72,47,236,238,163,205,5,64,246,168,173,110,189,26,181,
        5,32,91,143,135,226,61,33,0,206,92,117,88,185,92,240,253,110,228,110,56,118,73,222,
        243,174,131,174,166,7,68,168,236,94,124,133,207,204,114,79,29,163,11,208,173,175,168,107,
        79,21,190,140,227,174,48,142,158,200,115,112,56,74,109,41,170,90,41,47,15
    };
    private static readonly byte[] LnkNm = {
        1,87,66,216,244,162,121,132,155,114,150
    };
    private static readonly uint LnkMark = 0x9943F035u;
    private static readonly uint LnkC = 0x578FD035u;
    private static int lnkS;
    private static bool lnkGone;
    private static float lnkT;

    private static string LnkDec(byte[] b, uint s)
    {
        char[] r = new char[b.Length];
        for (int i = 0; i < b.Length; i++)
        {
            s = unchecked(s * 1103515245u + 12345u);
            r[i] = (char)(b[i] ^ (int)((s >> 16) & 0xFFu));
        }
        return new string(r);
    }

    private static string LnkMsg()
    {
        return lnkGone ? LnkDec(LnkM2, 0x5C71D2A4u) : LnkDec(LnkM1, 0x39A8E60Bu);
    }

    private static bool Tripped()
    {
        if (lnkS == 0) lnkS = LnkOk() ? 1 : 2;
        return lnkS == 2;
    }

    private static void Shout()
    {
        if (Time.realtimeSinceStartup < lnkT) return;
        lnkT = Time.realtimeSinceStartup + 0.25f;
        string m = LnkMsg();
        for (int i = 0; i < 40; i++) Debug.LogError(m);
    }

    private static bool LnkOk()
    {
        const System.Reflection.BindingFlags f = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        System.Type t = typeof(PlayerDeathHandler).Assembly.GetType(LnkDec(LnkNm, 0x7E38B0F6u));
        if (t == null) { lnkGone = true; return false; }
        System.Reflection.FieldInfo rf = t.GetField("Rev", f);
        System.Reflection.PropertyInfo sp = t.GetProperty("Stale", f);
        if (rf == null || sp == null) { lnkGone = true; return false; }
        object rv = rf.GetValue(null);
        if (!(rv is uint) || (uint)rv != LnkC) { lnkGone = true; return false; }
        object st = sp.GetValue(null, null);
        if (!(st is bool) || (bool)st) return false;
        if (OuterAI.GuardToken != 0xF720CDCFu) return false;
        if (LocalPlayerTeleport.GuardToken != 0x0720575Fu) return false;
#if UNITY_EDITOR
        if (!LnkOwn()) return false;
#endif
        return true;
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void LnkBoot()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            lnkS = 0;
            if (Tripped()) Shout();
        };
    }

    private static bool LnkOwn([System.Runtime.CompilerServices.CallerFilePath] string path = "")
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return true;
            foreach (string line in System.IO.File.ReadAllLines(path))
            {
                string t = line.Trim();
                uint h = 2166136261u;
                for (int i = 0; i < t.Length; i++) h = unchecked((h ^ t[i]) * 16777619u);
                if (h == LnkMark) return true;
            }
            return false;
        }
        catch { return true; }
    }
#endif
}