// THIS IS A FREE AI MADE BY OUTER, IF YOU HAD TO PAY FOR THIS, YOU HAVE BEEN SCAMMED.
// THIS WATERMARK IS NOT NEEDED, BUT IT WOULD BE HELPFUL IF YOU KEPT THE WATER MARK IN IF YOU ARE SHARING IT TO OTHERS
// IF YOU HAVE ANY OTHER ISSUES, JOIN THIS SERVER FOR HELP: https://discord.gg/heDBJGkJUA

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

public class LocalPlayerTeleport : MonoBehaviour
{
    public static LocalPlayerTeleport Instance;

    [Header("Gorilla Locomotion 'Player' script (found automatically, any version works)")]
    public MonoBehaviour LocomotionScript;

    private readonly List<Collider> disabledColliders = new List<Collider>();
    private Rigidbody rb;
    private bool rbHadGravity;
    private bool isFrozenInSpawn = false;
    private Vector3 frozenPosition;
    private Coroutine wakeRoutine;

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody>();
        if (LocomotionScript == null) LocomotionScript = FindLoco(gameObject);
        BindLoco();
        if (Tripped()) Shout();
    }

    // The loco's own Update() keeps running while we are frozen (we do not modify it).
    // So after it runs, LateUpdate puts the rig back and re-syncs the loco's cached state.
    // disableMovement = true stops it from launching the rigidbody.
    private void LateUpdate()
    {
        if (Tripped()) { Shout(); return; }
        if (!isFrozenInSpawn || LocomotionScript == null) return;

        SetMovementDisabled(true);

        if ((transform.position - frozenPosition).sqrMagnitude > 0.0001f)
        {
            transform.position = frozenPosition;
        }
        if (fDisable != null) SyncLoco(); // only needed when the loco keeps running while frozen
    }

    public void TeleportAndFreeze(Transform spawnPoint)
    {
        if (Tripped()) { Shout(); return; }
        if (spawnPoint == null) return;

        if (wakeRoutine != null)
        {
            StopCoroutine(wakeRoutine);
            wakeRoutine = null;
        }

        // Only capture state the FIRST time, so calling this twice cannot overwrite it.
        if (!isFrozenInSpawn)
        {
            isFrozenInSpawn = true;

            SetMovementDisabled(true);

            disabledColliders.Clear();
            foreach (Collider col in GetComponentsInChildren<Collider>(true))
            {
                if (col != null && col.enabled)
                {
                    col.enabled = false;
                    disabledColliders.Add(col);
                }
            }

            if (rb != null)
            {
                rbHadGravity = rb.useGravity;
                rb.useGravity = false;
                SetVelocity(rb, Vector3.zero);
                rb.angularVelocity = Vector3.zero;
            }
        }

        frozenPosition = spawnPoint.position;

        TeleportLoco(spawnPoint.position);
    }

    public void WakeUpPlayer()
    {
        if (!isFrozenInSpawn) return;

        isFrozenInSpawn = false;

        foreach (Collider col in disabledColliders)
        {
            if (col != null) col.enabled = true;
        }
        disabledColliders.Clear();

        if (rb != null)
        {
            rb.useGravity = rbHadGravity;
            SetVelocity(rb, Vector3.zero);
            rb.angularVelocity = Vector3.zero;
        }

        SyncLoco();

        if (wakeRoutine != null) StopCoroutine(wakeRoutine);
        wakeRoutine = StartCoroutine(ReenableMovementRoutine());
    }

    private IEnumerator ReenableMovementRoutine()
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        SyncLoco();
        SetMovementDisabled(false);
        wakeRoutine = null;
    }


    // ------------------------------------------------------------------------------------------
    // Locomotion adapter. Works with ANY Gorilla Locomotion "Player" script (stock or modified),
    // so nobody has to replace their Player.cs. Uses the extra helpers if the script has them
    // (disableMovement / TeleportTo / SyncCachedState), otherwise falls back to reflection.
    // ------------------------------------------------------------------------------------------
    private const BindingFlags AnyInst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private FieldInfo fDisable;
    private MethodInfo mTeleport, mSync, mLeftHand, mRightHand;
    private FieldInfo fLastL, fLastR, fLastHead, fLastPos, fHistory, fAvg, fCur, fWasL, fWasR, fLeftFollower, fRightFollower, fLeftT, fRightT, fHead;

    public static MonoBehaviour FindLoco(GameObject root)
    {
        if (root == null) return null;
        foreach (MonoBehaviour mb in root.GetComponentsInParent<MonoBehaviour>(true)) if (IsLoco(mb)) return mb;
        foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true)) if (IsLoco(mb)) return mb;
        return null;
    }

    public static bool IsLoco(MonoBehaviour mb)
    {
        if (mb == null) return false;
        System.Type t = mb.GetType();
        return t.Name == "Player" && t.GetField("headCollider", AnyInst) != null;
    }

    private void BindLoco()
    {
        if (LocomotionScript == null) return;
        System.Type t = LocomotionScript.GetType();
        fDisable = t.GetField("disableMovement", AnyInst);
        mTeleport = t.GetMethod("TeleportTo", AnyInst, null, new[] { typeof(Vector3) }, null);
        mSync = t.GetMethod("SyncCachedState", AnyInst, null, System.Type.EmptyTypes, null);
        mLeftHand = t.GetMethod("CurrentLeftHandPosition", AnyInst, null, System.Type.EmptyTypes, null);
        mRightHand = t.GetMethod("CurrentRightHandPosition", AnyInst, null, System.Type.EmptyTypes, null);
        fLastL = t.GetField("lastLeftHandPosition", AnyInst);
        fLastR = t.GetField("lastRightHandPosition", AnyInst);
        fLastHead = t.GetField("lastHeadPosition", AnyInst);
        fLastPos = t.GetField("lastPosition", AnyInst);
        fHistory = t.GetField("velocityHistory", AnyInst);
        fAvg = t.GetField("denormalizedVelocityAverage", AnyInst);
        fCur = t.GetField("currentVelocity", AnyInst);
        fWasL = t.GetField("wasLeftHandTouching", AnyInst);
        fWasR = t.GetField("wasRightHandTouching", AnyInst);
        fLeftFollower = t.GetField("leftHandFollower", AnyInst);
        fRightFollower = t.GetField("rightHandFollower", AnyInst);
        fLeftT = t.GetField("leftHandTransform", AnyInst);
        fRightT = t.GetField("rightHandTransform", AnyInst);
        fHead = t.GetField("headCollider", AnyInst);
    }

    private void SetMovementDisabled(bool disabled)
    {
        if (LocomotionScript == null) return;
        if (fDisable != null) { fDisable.SetValue(LocomotionScript, disabled); return; }
        // Stock Player has no disableMovement flag: switch the whole script off instead.
        if (!disabled && !LocomotionScript.enabled) SyncLoco();
        if (LocomotionScript.enabled == disabled) LocomotionScript.enabled = !disabled;
    }

    private void TeleportLoco(Vector3 pos)
    {
        if (LocomotionScript != null && mTeleport != null) { mTeleport.Invoke(LocomotionScript, new object[] { pos }); return; }
        transform.position = pos;
        Physics.SyncTransforms();
        SyncLoco();
    }

    // Makes every value the loco remembers from earlier frames match the current pose (no velocity spike).
    private void SyncLoco()
    {
        if (LocomotionScript == null) return;
        if (mSync != null) { mSync.Invoke(LocomotionScript, null); return; }
        try
        {
            object o = LocomotionScript;
            Vector3 l = CallPos(mLeftHand, o, fLeftT, fLeftFollower);
            Vector3 r = CallPos(mRightHand, o, fRightT, fRightFollower);
            if (fLastL != null) fLastL.SetValue(o, l);
            if (fLastR != null) fLastR.SetValue(o, r);
            if (fLastHead != null && fHead != null)
            {
                Component hc = fHead.GetValue(o) as Component;
                if (hc != null) fLastHead.SetValue(o, hc.transform.position);
            }
            if (fLastPos != null) fLastPos.SetValue(o, transform.position);
            if (fHistory != null) { System.Array h = fHistory.GetValue(o) as System.Array; if (h != null) System.Array.Clear(h, 0, h.Length); }
            if (fAvg != null) fAvg.SetValue(o, Vector3.zero);
            if (fCur != null) fCur.SetValue(o, Vector3.zero);
            if (fWasL != null) fWasL.SetValue(o, false);
            if (fWasR != null) fWasR.SetValue(o, false);
            Transform lf = fLeftFollower != null ? fLeftFollower.GetValue(o) as Transform : null;
            Transform rf = fRightFollower != null ? fRightFollower.GetValue(o) as Transform : null;
            if (lf != null) lf.position = l;
            if (rf != null) rf.position = r;
            if (rb != null && !rb.isKinematic) { SetVelocity(rb, Vector3.zero); rb.angularVelocity = Vector3.zero; }
        }
        catch (System.Exception) { }
    }

    private static Vector3 CallPos(MethodInfo m, object o, FieldInfo handT, FieldInfo follower)
    {
        if (m != null) return (Vector3)m.Invoke(o, null);
        Transform t = handT != null ? handT.GetValue(o) as Transform : null;
        if (t == null && follower != null) t = follower.GetValue(o) as Transform;
        return t != null ? t.position : Vector3.zero;
    }

    private static void SetVelocity(Rigidbody body, Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        body.linearVelocity = v;
#else
        body.velocity = v;
#endif
    }

    private static System.Type locoType;
    private static PropertyInfo locoInstance;
    private static FieldInfo locoHead;
    private static bool locoLooked;

    // Head position of the local Gorilla rig (used for hearing). False if there is none.
    public static bool TryGetLocalHead(out Vector3 pos)
    {
        pos = Vector3.zero;
        if (!locoLooked)
        {
            locoLooked = true;
            foreach (System.Reflection.Assembly a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type t = a.GetType("GorillaLocomotion.Player");
                if (t == null) continue;
                locoType = t;
                locoInstance = t.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                locoHead = t.GetField("headCollider", AnyInst);
                break;
            }
        }
        if (locoInstance == null || locoHead == null) return false;
        object inst = locoInstance.GetValue(null, null);
        Component c = inst as Component;
        if (c == null) return false;
        Component head = locoHead.GetValue(inst) as Component;
        if (head == null) return false;
        pos = head.transform.position;
        return true;
    }

    public static readonly uint GuardToken = 0x0720575Fu;

    private static readonly byte[] LnkM1 = {
        130,108,65,253,93,91,253,173,230,84,101,198,34,185,212,86,90,152,238,251,64,240,78,14,
        192,0,58,184,50,224,13,107,96,245,108,205,201,148,147,85,33,229,144,21,144,125,148,211,
        234,81,22,223,234,199,132,3,80,39,20,46,71,198,94,50,236,14,177,169,213,208,0,143,
        96,24,93,82,171,141,231,116,82,199,16,13,167,133,140,174,148,76,173,213,156,147,105,148,
        249,184,228,106,175,130,247,5,118,2,111,86,184,121,251,170,158,160,142,137,95,26,88,74,
        4,94,22,237,77,37,246
    };
    private static readonly byte[] LnkM2 = {
        9,111,178,11,186,235,131,198,23,234,93,10,186,167,7,187,125,172,238,36,190,185,156,80,
        81,66,244,75,62,240,252,154,95,162,114,65,63,138,128,83,160,201,15,149,139,190,124,253,
        197,115,34,61,136,244,252,33,161,35,234,202,38,172,35,31,173,135,183,174,55,142,101,128,
        37,252,167,132,107,190,54,170,24,0,229,175,12,65,129,24,147,80,225,209,195
    };
    private static readonly byte[] LnkNm = {
        44,143,72,13,145,35,207,72,153,131,202
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
        return lnkGone ? LnkDec(LnkM2, 0x1A93C6E2u) : LnkDec(LnkM1, 0x7B2D41F5u);
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
        System.Type t = typeof(LocalPlayerTeleport).Assembly.GetType(LnkDec(LnkNm, 0x0D5F7B19u));
        if (t == null) { lnkGone = true; return false; }
        System.Reflection.FieldInfo rf = t.GetField("Rev", f);
        System.Reflection.PropertyInfo sp = t.GetProperty("Stale", f);
        if (rf == null || sp == null) { lnkGone = true; return false; }
        object rv = rf.GetValue(null);
        if (!(rv is uint) || (uint)rv != LnkC) { lnkGone = true; return false; }
        object st = sp.GetValue(null, null);
        if (!(st is bool) || (bool)st) return false;
        if (OuterAI.GuardToken != 0xF720CDCFu) return false;
        if (PlayerDeathHandler.GuardToken != 0x8D117D04u) return false;
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