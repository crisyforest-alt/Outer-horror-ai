// THIS IS A FREE AI MADE BY OUTER, IF YOU HAD TO PAY FOR THIS, YOU HAVE BEEN SCAMMED.
// THIS WATERMARK IS NOT NEEDED, BUT IT WOULD BE HELPFUL IF YOU KEPT THE WATER MARK IN IF YOU ARE SHARING IT TO OTHERS
// IF YOU HAVE ANY OTHER ISSUES, JOIN THIS SERVER FOR HELP: https://discord.gg/heDBJGkJUA
// OUTER AI MANAGER (ONE FILE): the GameObject script (OuterAIManager), the Inspector, the top "Outer AI" menu, themes, gizmos and auto setup.
// Put the OuterAIManager script on any GameObject, OR open the top menu Outer AI > Manager. Both show the same manager.

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using System.Collections;
using UnityEngine.AI;
using UnityEngine.Serialization;
using Photon.Pun;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using System.Linq;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorState = UnityEditor.Animations.AnimatorState;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;
using AnimatorControllerParameterType = UnityEngine.AnimatorControllerParameterType;
using AnimatorOverrideController = UnityEngine.AnimatorOverrideController;
using AnimationClip = UnityEngine.AnimationClip;
using Animator = UnityEngine.Animator;
using AnimationUtility = UnityEditor.AnimationUtility;
using AnimationClipSettings = UnityEditor.AnimationClipSettings;

#endif

[DisallowMultipleComponent]
[AddComponentMenu("Outer AI/Outer AI Manager")]
public class OuterAIManager : MonoBehaviour
{
    // Everything below is just saved data for the Inspector. The manager does nothing at runtime.
    [HideInInspector] public int ActiveInspectorTab = 0;
    [HideInInspector] public List<OuterAI> Monsters = new List<OuterAI>();
    [HideInInspector] public List<bool> Ticks = new List<bool>();
    [HideInInspector] public int SrcIndex = 0;
    [HideInInspector] public bool OptDurations = true;
    [HideInInspector] public bool OptNavMesh = true;
    [HideInInspector] public bool OptPerf = true;
    [HideInInspector] public bool OptPlayers = true;

    // Adds a monster to the list (ticked). Returns false if it was null or already there.
    public bool AddMonster(OuterAI a)
    {
        if (a == null) return false;
        if (Monsters == null) Monsters = new List<OuterAI>();
        if (Ticks == null) Ticks = new List<bool>();
        Clean();
        if (Monsters.Contains(a)) return false;
        Monsters.Add(a);
        Ticks.Add(true);
        return true;
    }

    // Adds every OuterAI in the open scene(s). Returns how many were new.
    public int FindAllInScene()
    {
        int added = 0;
        foreach (OuterAI a in FindObjectsByType<OuterAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (AddMonster(a)) added++;
        return added;
    }

    // Removes deleted monsters and keeps the tick list the same length. Returns true if anything changed.
    public bool Clean()
    {
        bool changed = false;
        if (Monsters == null) { Monsters = new List<OuterAI>(); changed = true; }
        if (Ticks == null) { Ticks = new List<bool>(); changed = true; }
        while (Ticks.Count < Monsters.Count) { Ticks.Add(true); changed = true; }
        for (int i = Monsters.Count - 1; i >= 0; i--)
            if (Monsters[i] == null) { Monsters.RemoveAt(i); Ticks.RemoveAt(i); changed = true; }
        while (Ticks.Count > Monsters.Count) { Ticks.RemoveAt(Ticks.Count - 1); changed = true; }
        if (SrcIndex >= Monsters.Count || SrcIndex < 0) { SrcIndex = 0; changed = true; }
        return changed;
    }

    public List<OuterAI> TickedMonsters()
    {
        List<OuterAI> r = new List<OuterAI>();
        if (Monsters == null || Ticks == null) return r;
        for (int i = 0; i < Monsters.Count; i++)
            if (Monsters[i] != null && i < Ticks.Count && Ticks[i]) r.Add(Monsters[i]);
        return r;
    }

    public void SetAllTicked(bool value)
    {
        Clean();
        for (int i = 0; i < Ticks.Count; i++) Ticks[i] = value;
    }

#if UNITY_EDITOR
    // Runs when you add the script to a GameObject: it grabs every monster in the scene for you.
    private void Reset()
    {
        FindAllInScene();
    }
#endif
}

#if UNITY_EDITOR
// ---------------------------------------------------------------------------------------------
// Gizmo settings. Shared by EVERY monster (stored in EditorPrefs). Edit them in the Manager.
// ---------------------------------------------------------------------------------------------
internal static class OuterAIGizmoPrefs
{
    private const string K = "OuterAI.Gizmos.";
    private static bool loaded;
    private static bool showGizmos, showDetection, showMovement, showPath, showKill, showLabels;
    private static float fill;
    private static int cone;
    private static Color chase, clear, blocked, path, target, kill;

    private static readonly Color DChase = new Color(0f, 0.5f, 1f, 0.15f);
    private static readonly Color DKill = new Color(1f, 0.25f, 0.25f, 0.7f);
    private static readonly string[] Keys = { "Show", "Detect", "Move", "Path", "Kill", "Labels", "Fill", "Cone", "Chase", "Clear", "Blocked", "PathCol", "Target", "KillCol" };

    private static Color GetCol(string key, Color def)
    {
        string s = EditorPrefs.GetString(K + key, "");
        Color c;
        if (!string.IsNullOrEmpty(s) && ColorUtility.TryParseHtmlString("#" + s, out c)) return c;
        return def;
    }

    private static void SetCol(string key, Color c) { EditorPrefs.SetString(K + key, ColorUtility.ToHtmlStringRGBA(c)); }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        showGizmos = EditorPrefs.GetBool(K + "Show", true);
        showDetection = EditorPrefs.GetBool(K + "Detect", true);
        showMovement = EditorPrefs.GetBool(K + "Move", true);
        showPath = EditorPrefs.GetBool(K + "Path", true);
        showKill = EditorPrefs.GetBool(K + "Kill", false);
        showLabels = EditorPrefs.GetBool(K + "Labels", false);
        fill = EditorPrefs.GetFloat(K + "Fill", 0.28f);
        cone = EditorPrefs.GetInt(K + "Cone", 40);
        chase = GetCol("Chase", DChase);
        clear = GetCol("Clear", Color.green);
        blocked = GetCol("Blocked", Color.red);
        path = GetCol("PathCol", Color.white);
        target = GetCol("Target", Color.yellow);
        kill = GetCol("KillCol", DKill);
    }

    private static void Save()
    {
        EditorPrefs.SetBool(K + "Show", showGizmos);
        EditorPrefs.SetBool(K + "Detect", showDetection);
        EditorPrefs.SetBool(K + "Move", showMovement);
        EditorPrefs.SetBool(K + "Path", showPath);
        EditorPrefs.SetBool(K + "Kill", showKill);
        EditorPrefs.SetBool(K + "Labels", showLabels);
        EditorPrefs.SetFloat(K + "Fill", fill);
        EditorPrefs.SetInt(K + "Cone", cone);
        SetCol("Chase", chase);
        SetCol("Clear", clear);
        SetCol("Blocked", blocked);
        SetCol("PathCol", path);
        SetCol("Target", target);
        SetCol("KillCol", kill);
        SceneView.RepaintAll();
    }

    internal static void ResetAll()
    {
        foreach (string k in Keys) EditorPrefs.DeleteKey(K + k);
        loaded = false;
        SceneView.RepaintAll();
    }

    internal static bool ShowGizmos { get { Load(); return showGizmos; } set { Load(); showGizmos = value; Save(); } }
    internal static bool ShowDetectionGizmos { get { Load(); return showDetection; } set { Load(); showDetection = value; Save(); } }
    internal static bool ShowMovementGizmos { get { Load(); return showMovement; } set { Load(); showMovement = value; Save(); } }
    internal static bool ShowPathGizmos { get { Load(); return showPath; } set { Load(); showPath = value; Save(); } }
    internal static bool ShowKillRange { get { Load(); return showKill; } set { Load(); showKill = value; Save(); } }
    internal static bool ShowGizmoLabels { get { Load(); return showLabels; } set { Load(); showLabels = value; Save(); } }
    internal static float GizmoFillOpacity { get { Load(); return fill; } set { Load(); fill = value; Save(); } }
    internal static int GizmoConeDetail { get { Load(); return cone; } set { Load(); cone = value; Save(); } }
    internal static Color ChaseSphereColor { get { Load(); return chase; } set { Load(); chase = value; Save(); } }
    internal static Color VisionConeClearColor { get { Load(); return clear; } set { Load(); clear = value; Save(); } }
    internal static Color VisionConeBlockedColor { get { Load(); return blocked; } set { Load(); blocked = value; Save(); } }
    internal static Color PathColor { get { Load(); return path; } set { Load(); path = value; Save(); } }
    internal static Color TargetLineColor { get { Load(); return target; } set { Load(); target = value; Save(); } }
    internal static Color KillRangeColor { get { Load(); return kill; } set { Load(); kill = value; Save(); } }
}


// ---------------------------------------------------------------------------------------------
// Shared helpers for the Outer AI Manager component (setup, check, navmesh, copy settings, anti lag).
// ---------------------------------------------------------------------------------------------
internal static class OuterAIManagerTools
{
    private static readonly Dictionary<int, OuterAISetupEngine> cache = new Dictionary<int, OuterAISetupEngine>();

    // ---- top menu bar: "Outer AI" ----
    [MenuItem("Outer AI/Create or Select Manager Object", false, 1)]
    private static void MenuSelect() { OpenOrCreate(null); }

    [MenuItem("Outer AI/Auto Setup All Monsters", false, 20)]
    private static void MenuSetupAll() { RunSetup(OuterAISetupEngine.FindAll<OuterAI>().ToList()); }

    [MenuItem("Outer AI/Check All Monsters", false, 21)]
    private static void MenuCheckAll() { RunCheck(OuterAISetupEngine.FindAll<OuterAI>().ToList()); }

    [MenuItem("Outer AI/Create or Rebake NavMesh", false, 22)]
    private static void MenuNavMesh() { RunNavMesh(OuterAISetupEngine.FindAll<OuterAI>().ToList()); }

    [MenuItem("Outer AI/Hierarchy Colors + Badges", false, 40)]
    private static void MenuHierarchy() { OuterAIHierarchy.Enabled = !OuterAIHierarchy.Enabled; }

    [MenuItem("Outer AI/Hierarchy Colors + Badges", true)]
    private static bool MenuHierarchyCheck() { Menu.SetChecked("Outer AI/Hierarchy Colors + Badges", OuterAIHierarchy.Enabled); return true; }

    [MenuItem("GameObject/Outer AI/Outer AI Manager", false, 10)]
    private static void MenuCreate() { OpenOrCreate(null); }

    [MenuItem("CONTEXT/OuterAI/Add To Outer AI Manager")]
    private static void FromContext(MenuCommand cmd) { OpenOrCreate(cmd.context as OuterAI); }

    // Selects the manager in the scene (makes one if there is none). Optionally adds a monster to it.
    internal static void OpenOrCreate(OuterAI add)
    {
        if (OuterAITos.Guard()) return;
        OuterAIManager m = null;
        OuterAIManager[] all = Object.FindObjectsByType<OuterAIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all.Length > 0) m = all[0];
        if (m == null)
        {
            GameObject go = new GameObject("Outer AI Manager");
            Undo.RegisterCreatedObjectUndo(go, "Create Outer AI Manager");
            m = go.AddComponent<OuterAIManager>();
            m.FindAllInScene();
        }
        if (add != null)
        {
            Undo.RecordObject(m, "Add to Outer AI Manager");
            m.AddMonster(add);
        }
        EditorUtility.SetDirty(m);
        Selection.activeGameObject = m.gameObject;
        EditorGUIUtility.PingObject(m);
    }

    // ------------------------------------------------------------ shared engine access
    internal static OuterAISetupEngine EngineFor(OuterAI a)
    {
        OuterAISetupEngine e;
        int id = a.GetInstanceID();
        if (!cache.TryGetValue(id, out e) || e == null) { e = new OuterAISetupEngine(); cache[id] = e; }
        e.ai = a;
        return e;
    }

    internal static OuterAISetupEngine Peek(OuterAI a)
    {
        OuterAISetupEngine e;
        return a != null && cache.TryGetValue(a.GetInstanceID(), out e) ? e : null;
    }

    internal static void RunSetup(List<OuterAI> targets, bool durations = true, bool navMesh = true, bool perf = true, bool players = true)
    {
        if (OuterAITos.Guard()) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Outer AI Auto Setup");
        try
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null) continue;
                EditorUtility.DisplayProgressBar("Outer AI Auto Setup", targets[i].name, (float)i / targets.Count);
                OuterAISetupEngine e = EngineFor(targets[i]);
                e.matchDurations = durations;
                e.createNavMesh = navMesh;
                e.maxPerformance = perf;
                e.setupPlayers = players;
                e.AutoSetup();
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Undo.CollapseUndoOperations(group);
        }
        InternalEditorUtility.RepaintAllViews();
    }

    internal static void RunCheck(List<OuterAI> targets)
    {
        if (OuterAITos.Guard()) return;
        foreach (OuterAI a in targets)
        {
            if (a == null) continue;
            OuterAISetupEngine e = EngineFor(a);
            e.log.Clear();
            e.Check();
        }
        InternalEditorUtility.RepaintAllViews();
    }

    internal static void RunNavMesh(List<OuterAI> targets)
    {
        if (OuterAITos.Guard()) return;
        OuterAI first = targets.FirstOrDefault(a => a != null);
        if (first == null) return;
        OuterAISetupEngine fe = EngineFor(first);
        fe.log.Clear();
        fe.CreateNavMesh();
        foreach (OuterAI a in targets)
        {
            if (a == null) continue;
            OuterAISetupEngine e = EngineFor(a);
            if (a != first) e.log.Clear();
            e.Check();
        }
        InternalEditorUtility.RepaintAllViews();
    }

    internal static void DrawInline(OuterAI a)
    {
        OuterAISetupEngine e = Peek(a);
        if (e != null && e.items.Count > 0) e.DrawItems(false);
    }

    // The three anti-lag presets (0 = Max Performance, 1 = Balanced, 2 = Best Quality).
    internal static void AntiLagPreset(OuterAI s, int which)
    {
        if (s == null) return;
        Undo.RecordObject(s, "Anti Lag preset");
        s.UseAntiLag = true;
        if (which == 0)
        {
            s.NearChecksPerSecond = 6f; s.FarChecksPerSecond = 1.5f; s.ChaseChecksPerSecond = 6f;
            s.MaxPlayersPerCheck = 2; s.MaxHeavyChecks = 3; s.GlobalThinkBudgetPerFrame = 2; s.PlayerScanInterval = 1.5f;
            s.FootstepChecksPerSecond = 20; s.SyncMoveThreshold = 0.05f; s.CullAnimatorsWhenHidden = true; s.SnapDistance = 5f;
        }
        else if (which == 1)
        {
            s.NearChecksPerSecond = 10f; s.FarChecksPerSecond = 2.5f; s.ChaseChecksPerSecond = 10f;
            s.MaxPlayersPerCheck = 0; s.MaxHeavyChecks = 0; s.GlobalThinkBudgetPerFrame = 0; s.PlayerScanInterval = 1f;
            s.FootstepChecksPerSecond = 30; s.SyncMoveThreshold = 0.02f; s.CullAnimatorsWhenHidden = true; s.SnapDistance = 6f;
        }
        else
        {
            s.NearChecksPerSecond = 20f; s.FarChecksPerSecond = 8f; s.ChaseChecksPerSecond = 20f;
            s.MaxPlayersPerCheck = 0; s.MaxHeavyChecks = 0; s.GlobalThinkBudgetPerFrame = 0; s.PlayerScanInterval = 0.5f;
            s.FootstepChecksPerSecond = 0; s.SyncMoveThreshold = 0f; s.CullAnimatorsWhenHidden = false; s.SnapDistance = 8f;
        }
        EditorUtility.SetDirty(s);
    }

    private static readonly HashSet<string> SkipProps = new HashSet<string>
    {
        "m_Script", "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_EditorClassIdentifier", "ActiveInspectorTab", "agent"
    };

    // Copies speeds, ranges, timings and switches from one monster to the others. Returns how many were changed.
    internal static int CopyTuning(OuterAI src, List<OuterAI> dst)
    {
        int count = 0;
        foreach (OuterAI d in dst)
        {
            if (d == null || d == src) continue;
            Undo.RecordObject(d, "Copy Outer AI settings");
            SerializedObject so = new SerializedObject(src);
            SerializedObject sd = new SerializedObject(d);
            SerializedProperty it = so.GetIterator();
            bool enter = true;
            while (it.Next(enter))
            {
                enter = false;
                if (SkipProps.Contains(it.name)) continue;
                SerializedProperty dp = sd.FindProperty(it.propertyPath);
                if (dp == null || dp.propertyType != it.propertyType) continue;
                Assign(it, dp);
            }
            sd.ApplyModifiedProperties();
            EditorUtility.SetDirty(d);
            count++;
        }
        return count;
    }

    private static void Assign(SerializedProperty s, SerializedProperty d)
    {
        switch (s.propertyType)
        {
            case SerializedPropertyType.Integer: d.intValue = s.intValue; break;
            case SerializedPropertyType.Boolean: d.boolValue = s.boolValue; break;
            case SerializedPropertyType.Float: d.floatValue = s.floatValue; break;
            case SerializedPropertyType.String: d.stringValue = s.stringValue; break;
            case SerializedPropertyType.Color: d.colorValue = s.colorValue; break;
            case SerializedPropertyType.LayerMask: d.intValue = s.intValue; break;
            case SerializedPropertyType.Enum: d.enumValueIndex = s.enumValueIndex; break;
            case SerializedPropertyType.Vector2: d.vector2Value = s.vector2Value; break;
            case SerializedPropertyType.Vector3: d.vector3Value = s.vector3Value; break;
            case SerializedPropertyType.Generic:
                // only plain text lists (like the vision layer names)
                if (s.isArray && s.arrayElementType == "string")
                {
                    d.arraySize = s.arraySize;
                    for (int i = 0; i < s.arraySize; i++)
                        d.GetArrayElementAtIndex(i).stringValue = s.GetArrayElementAtIndex(i).stringValue;
                }
                break;
        }
    }

    internal static void SetActiveAll(List<OuterAI> t, bool on)
    {
        foreach (OuterAI a in t)
        {
            if (a == null) continue;
            Undo.RecordObject(a.gameObject, on ? "Turn monsters on" : "Turn monsters off");
            a.gameObject.SetActive(on);
        }
        InternalEditorUtility.RepaintAllViews();
    }

    internal const string ManagerHelp =
        "USING THE OUTER AI MANAGER (made for lots of monsters)\n" +
        "  1. Put the OuterAIManager script on any empty GameObject (Add Component > Outer AI > Outer AI Manager).\n" +
        "     Or right-click the Hierarchy > Outer AI > Outer AI Manager. Or use the top menu: Outer AI > Manager (same manager, in a window). Outer AI > Update Log shows what is new.\n" +
        "  2. MONSTERS tab: press 'Add All In Scene', or drag monsters from the Hierarchy into the drop box.\n" +
        "     (Hold Ctrl or Shift in the Hierarchy to drag many at once.)\n" +
        "  3. Tick the monsters you want to change. Setup, Bulk Edit and Quick Actions only touch the ticked ones.\n" +
        "  4. SETUP tab: press 'Auto Setup + Check' ONCE and every ticked monster gets set up.\n" +
        "     Click a message in the results to highlight the thing it talks about in the Hierarchy.\n" +
        "  5. BULK EDIT tab: change a number once (speed, range, jumpscare time...) and it changes on all ticked monsters.\n" +
        "     A dash (-) means they have different numbers right now.\n" +
        "  6. COPY SETTINGS (Monsters tab): tune one monster, then copy its numbers to the others. It never copies models, sounds or cameras.\n" +
        "  7. GIZMOS and THEMES are shared by every monster, and by this manager. Change them once.\n" +
        "TIP: you can also click many monsters and use the normal OuterAI Inspector. It edits all of them together.\n\n";

    internal const string Help =
    "QUICK START (about 30 seconds)\n" +
    "1. Click your monster in the Hierarchy (the list on the left).\n" +
    "2. In the Inspector, press 'Auto Setup + Check' (or tick it in the Outer AI Manager and press it there once for many monsters).\n" +
    "3. Read the list it shows:\n" +
    "     RED = something is broken, fix it.\n" +
    "     YELLOW = might be a problem.\n" +
    "     BLUE = just a tip.\n" +
    "   Click any message to jump to the thing it talks about.\n" +
    "\n" +
    "STEP 1: BUILD YOUR MONSTER\n" +
    "Make an empty object for the monster and put the OuterAI script on it. Everything below goes INSIDE it (drag the parts onto it in the Hierarchy so they become its children). Example:\n" +
    "  Monster  (has OuterAI)\n" +
    "     MonsterWalk          <- walking model\n" +
    "     MonsterJumpscare     <- jumpscare model, with a Camera inside it\n" +
    "     MonsterDetect        <- 'I see you' model (optional)\n" +
    "     IdleSound, ChaseSound, FirstPersonSound, NetworkSound\n" +
    "\n" +
    "HOW TO ADD THE WALK ANIMATION\n" +
    "  1. Drag your monster model into the monster object and put 'Walk' in its name.\n" +
    "  2. Click it, press Add Component, pick Animator (skip if it already has one).\n" +
    "  3. In the Project window: right-click > Create > Animator Controller. Drag it into the Animator's 'Controller' slot.\n" +
    "  4. Double-click the controller. Drag your walk animation into the window. It turns orange. That is the one that plays.\n" +
    "  5. Click the walk animation in the Project window and tick 'Loop Time' so it keeps repeating.\n" +
    "\n" +
    "HOW TO ADD THE JUMPSCARE\n" +
    "  1. Make another model inside the monster and put 'Jumpscare' (or 'Kill') in its name.\n" +
    "  2. Give it an Animator + Controller like above, with your jumpscare animation as the orange one.\n" +
    "  3. Right-click it > Camera. Point the camera at the monster's face. This is what the caught player sees.\n" +
    "  4. Leave it turned on or off, Auto Setup hides it. It only shows when someone is caught.\n" +
    "\n" +
    "HOW TO ADD THE DETECTION ANIMATION (optional)\n" +
    "  1. Same as the jumpscare, but put 'Detect' (or 'Spot') in the name and use your 'noticed you' animation. No camera needed.\n" +
    "  2. Press Auto Setup. It turns on 'Use Detection Animation' for you.\n" +
    "\n" +
    "IMPORTED AN FBX (or .blend) FILE? DO THIS FIRST\n" +
    "An FBX is a model and/or animations from Blender, Maya, Mixamo, etc. Unity needs a few settings before the animations work. A .blend file works the same way.\n" +
    "  1. Click the FBX in the Project window. Look at the Inspector.\n" +
    "  2. Click the 'Rig' tab. Set Animation Type to 'Humanoid' if it is a person-shaped model, or 'Generic' for anything else (creatures, weird shapes). Press Apply.\n" +
    "     If you use animations from a DIFFERENT file, both files must use the same Animation Type.\n" +
    "  3. Click the 'Animation' tab. You will see the animations inside the file. Click the one you want (like Walk).\n" +
    "  4. Tick 'Loop Time' for walking/idle animations. Leave it OFF for jumpscare and detect animations. Press Apply (bottom right).\n" +
    "  5. Click the little arrow on the FBX in the Project window to open it. The animations are inside, they have a small play-button icon.\n" +
    "  6. Drag the animation (NOT the whole FBX) into your Animator Controller window. It turns orange.\n" +
    "  7. Drag the FBX model into your monster object in the Hierarchy. Unity adds an Animator to it for you. Put your Controller in its 'Controller' slot.\n" +
    "\n" +
    "IF THE FBX HAS ONE LONG ANIMATION\n" +
    "Some files have everything in one long clip (often called 'Take 001'). To split it:\n" +
    "  1. Click the FBX > 'Animation' tab.\n" +
    "  2. Press the '+' under the clip list to add a clip. Rename it (Walk, Jumpscare...).\n" +
    "  3. Set the Start and End numbers (frames) so it only covers that animation. Press Apply.\n" +
    "\n" +
    "IF THE ANIMATION IS GREYED OUT AND YOU CAN'T EDIT IT\n" +
    "That is normal for animations inside an FBX. Click the animation inside the FBX and press Ctrl+D (Cmd+D on Mac). That makes a normal copy you can edit.\n" +
    "\n" +
    "USING MIXAMO?\n" +
    "  - Download the character once with 'With Skin'. Download every other animation 'Without Skin'.\n" +
    "  - Tick 'In Place' when downloading walk animations, or the monster will walk away from its own body.\n" +
    "  - Set the Rig to Humanoid on every file and tick 'Loop Time' on walk/idle.\n" +
    "\n" +
    "ANIMATION PROBLEMS? TRY THIS\n" +
    "  - Monster slides around but does not move its legs: the Animator has no Controller, or your animation is not the orange one.\n" +
    "  - Monster stands in a T-pose: the Controller is empty. Drag an animation into it.\n" +
    "  - Animation plays once then freezes: tick 'Loop Time' on the animation (see the FBX steps above).\n" +
    "  - Model drifts away from the monster: the animation moves the model itself. Auto Setup turns off 'Apply Root Motion' for you. Use 'In Place' animations.\n" +
    "  - Animation too fast or slow: click the orange box in the Animator window and change 'Speed' in the Inspector.\n" +
    "  - Model faces the wrong way or floats: click the model and rotate it, or lower it, inside the monster object. Don't move the monster object itself.\n" +
    "  - Jumpscare cuts off early or hangs on the last frame: Auto Setup matches the time to your animation. Press it again after changing animations.\n" +
    "\n" +
    "SOUNDS\n" +
    "Make empty objects with an Audio Source on them and name them:\n" +
    "  Idle  /  Chase  /  Detect  /  FirstPerson (what the caught player hears)  /  Network (what everyone else hears)\n" +
    "Auto Setup turns off 'Play On Awake' on the jumpscare and detect sounds.\n" +
    "\n" +
    "WHERE CAUGHT PLAYERS GO\n" +
    "Make an empty object named 'Respawn' somewhere in your map. Auto Setup uses it.\n" +
    "\n" +
    "STEP 2: PLAYER SETUP (Auto Setup does it for you)\n" +
    "You do NOT need to replace your Player script. Works with the normal Gorilla Locomotion Player.\n" +
    "Auto Setup adds LocalPlayerTeleport to your Gorilla rig, and PlayerDeathHandler to your Photon player (in the scene and in Resources prefabs). It also fills in the model list.\n" +
    "Still check that your Photon player has:\n" +
    "  - the tag 'Player'\n" +
    "  - a PhotonView\n" +
    "\n" +
    "WALK AND RUN ANIMATIONS (optional)\n" +
    "Add a 'Speed' (Float) and a 'Run' (Bool) to your Walk Animator, then press Auto Setup again.\n" +
    "\n" +
    "STUCK?\n" +
    "Press 'Check Only'. It never changes anything, it just tells you what is wrong.";
}

public abstract class OuterAIThemedEditor : Editor
{
protected virtual string LogoText { get { return "OUTER AI"; } }
protected virtual float LogoScale { get { return 1f; } }
protected int LogoPx { get { return Mathf.Max(10, Mathf.RoundToInt(Cur.logoSize * LogoScale)); } }
protected abstract void DrawBody();
internal static ThemeData CurrentTheme { get { return Cur; } }
// Themed logo text (same colors, fade, lighter top half and outline as the title). Used for the "OA" Hierarchy icon and the Update Log.
private class LogoCache { public GUIStyle fill, outline; public string baseTxt, highTxt; }
private static readonly Dictionary<string, LogoCache> logoCache = new Dictionary<string, LogoCache>();
private static int logoCacheVer = -1;
internal static void DrawMiniLogo(Rect rect, string text, int size) { DrawLogoText(rect, text, size); }
internal static void DrawLogoText(Rect rect, string text, int size)
{
if (logoCacheVer != themeVersion) { logoCache.Clear(); logoCacheVer = themeVersion; RefreshFont(); }
string key = size + "|" + text;
LogoCache lc;
if (!logoCache.TryGetValue(key, out lc))
{
ThemeData t = Cur;
Color l = t.logo;
Color b1 = l, b2 = Color.Lerp(l, Color.white, 0.20f), h1 = Color.Lerp(l, Color.white, 0.40f), h2 = Color.Lerp(l, Color.white, 0.70f);
if (!t.logoGradient) { b2 = b1; h2 = h1; }
Color ol = t.autoOutline ? Color.Lerp(l, Color.black, 0.60f) : t.outlineColor;
lc = new LogoCache();
lc.fill = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = size, fontStyle = t.logoBold ? FontStyle.Bold : FontStyle.Normal, richText = true };
if (curFont != null) lc.fill.font = curFont;
lc.fill.normal.textColor = Color.white;
lc.outline = new GUIStyle(lc.fill) { richText = false };
lc.outline.normal.textColor = ol;
lc.baseTxt = GradientText(text, b1, b2);
lc.highTxt = GradientText(text, h1, h2);
logoCache[key] = lc;
}
if (Cur.logoOutline)
for (int ox = -1; ox <= 1; ox++)
for (int oy = -1; oy <= 1; oy++)
if (ox != 0 || oy != 0) GUI.Label(new Rect(rect.x + ox, rect.y + oy, rect.width, rect.height), text, lc.outline);
GUI.Label(rect, lc.baseTxt, lc.fill);
if (Cur.logoHighlight)
{
GUI.BeginGroup(new Rect(rect.x, rect.y, rect.width, rect.height * 0.5f));
GUI.Label(new Rect(0f, 0f, rect.width, rect.height), lc.highTxt, lc.fill);
GUI.EndGroup();
}
}
protected Texture2D gradientTexture;
protected GUIStyle backgroundStyle;
protected GUIStyle sectionTitleStyle;
protected GUIStyle noteStyle;
protected GUIStyle subStyle;
protected GUIStyle logoStyle;
protected GUIStyle logoOutlineStyle;
protected string logoBaseText;
protected string logoHighText;
protected GUIStyle tabOffStyle;
protected GUIStyle tabOnStyle;
protected Texture2D tabOffTex, tabHoverTex, tabOnTex;
protected readonly Dictionary<string, ReorderableList> lists = new Dictionary<string, ReorderableList>();
protected const string ThemePref = "OuterAI.Theme.";
[System.Serializable]
public class ThemeData
{
public int ver;
public string name;
public int bgMode;
public bool solidBg;
public Color solid;
public Color top, bottom;
public string bgImage;
public int imageFit;
public Color imageTint;
public float imageDim;
public float radius;
public Color tabOff, tabHover, tabOn, tabText, tabTextOn;
public int tabHeight, tabFontSize;
public string uiFont;
public Color titleColor, sub;
public int titleSize, subSize;
public Color logo;
public bool logoGradient, logoHighlight, logoOutline, logoBold;
public string logoFont;
public int logoSize;
public bool showLogo, autoOutline;
public Color outlineColor;
public Color boxColor, borderColor;
public int borderWidth, bgPadding, sectionGap, tabsPerRow;
public bool titleCenter, subItalic;
public ThemeData Copy() { return (ThemeData)MemberwiseClone(); }
public bool Same(ThemeData o) { return JsonUtility.ToJson(this) == JsonUtility.ToJson(o); }
}
protected static Color Hex(string hex)
{
Color c;
return ColorUtility.TryParseHtmlString("#" + hex, out c) ? c : Color.magenta;
}
protected static Color WithAlpha(Color c, float a)
{
c.a = a;
return c;
}
protected static float Lum(Color c) { return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b; }
protected static bool LightBg(ThemeData t)
{
if (t == null || t.bgMode == 2) return false;
float l = t.bgMode == 1 ? Lum(t.solid) : (Lum(t.top) + Lum(t.bottom)) * 0.5f;
return l > 0.55f;
}
protected static ThemeData EnsureContrast(ThemeData t)
{
if (t == null) return t;
Color dark = new Color(0.07f, 0.07f, 0.10f);
Color lite = new Color(0.96f, 0.94f, 1f);
if (t.bgMode != 2)
{
float bgL = t.bgMode == 1 ? Lum(t.solid) : (Lum(t.top) + Lum(t.bottom)) * 0.5f;
if (bgL > 0.55f)
{
if (Lum(t.titleColor) > 0.45f) t.titleColor = dark;
if (Lum(t.sub) > 0.45f) t.sub = dark;
}
else if (bgL < 0.35f)
{
if (Lum(t.titleColor) < 0.5f) t.titleColor = lite;
if (Lum(t.sub) < 0.5f) t.sub = Color.Lerp(lite, t.logo, 0.25f);
}
}
if (Lum(t.tabOff) > 0.55f) { if (Lum(t.tabText) > 0.45f) t.tabText = dark; }
else if (Lum(t.tabOff) < 0.45f) { if (Lum(t.tabText) < 0.5f) t.tabText = lite; }
if (Lum(t.tabOn) > 0.55f) { if (Lum(t.tabTextOn) > 0.45f) t.tabTextOn = dark; }
else if (Lum(t.tabOn) < 0.45f) { if (Lum(t.tabTextOn) < 0.5f) t.tabTextOn = Color.white; }
return t;
}
protected static ThemeData Blank(string name)
{
return new ThemeData
{
ver = 6, name = name, bgMode = 0, radius = 6f,
bgImage = "", imageFit = 0, imageTint = Color.white, imageDim = 0f,
tabText = new Color(0.85f, 0.82f, 1f), tabTextOn = Color.white, tabHeight = 30, tabFontSize = 10,
uiFont = "", titleColor = new Color(0.96f, 0.92f, 1f), sub = new Color(0.8f, 0.8f, 0.9f), titleSize = 15, subSize = 11,
logoGradient = true, logoHighlight = true, logoOutline = true, logoSize = 28, logoBold = true, logoFont = "",
showLogo = true, autoOutline = true, outlineColor = new Color(0.28f, 0.06f, 0.55f),
boxColor = new Color(0f, 0f, 0f, 0f), borderColor = new Color(1f, 1f, 1f, 0.25f),
borderWidth = 0, bgPadding = 14, sectionGap = 6, tabsPerRow = 3, titleCenter = true, subItalic = true
};
}
protected static ThemeData Dark(string name, Color accent)
{
Color d = Hex("0D0D0D");
ThemeData t = Blank(name);
t.top = d; t.bottom = accent; t.solid = d;
t.tabOff = WithAlpha(Color.Lerp(d, accent, 0.30f), 0.85f);
t.tabHover = WithAlpha(Color.Lerp(d, accent, 0.70f), 0.95f);
t.tabOn = accent;
t.logo = accent;
return t;
}
protected static ThemeData MakeDefault()
{
ThemeData t = Blank("Default");
t.top = new Color(0.10f, 0.20f, 0.58f);
t.bottom = new Color(0.36f, 0.12f, 0.60f);
t.solid = t.top;
t.tabOff = new Color(0.08f, 0.07f, 0.22f, 0.80f);
t.tabHover = new Color(0.30f, 0.18f, 0.55f, 0.95f);
t.tabOn = new Color(0.62f, 0.28f, 1f, 1f);
t.logo = new Color(0.50f, 0.15f, 0.90f);
return t;
}
protected static ThemeData MakeGold() { return Dark("Dark Gold", Hex("5E4B00")); }
protected static ThemeData MakeGreen() { return Dark("Dark Green", Hex("103800")); }
protected static ThemeData MakeRed()
{
ThemeData t = Dark("Calm Red", Hex("450000"));
t.logo = Hex("B00000");
return t;
}
protected static ThemeData MakeBubble()
{
Color pink = Hex("FF9CEA");
ThemeData t = Blank("Bubblegum");
t.top = Color.white; t.bottom = pink; t.solid = pink;
t.tabOff = WithAlpha(Color.Lerp(pink, Color.black, 0.50f), 0.90f);
t.tabHover = WithAlpha(Color.Lerp(pink, Color.black, 0.38f), 0.95f);
t.tabOn = Color.Lerp(pink, Color.black, 0.25f);
t.logo = Color.Lerp(pink, Color.black, 0.30f);
t.sub = Hex("6B1F5C");
return t;
}
protected static ThemeData MakeBlue()
{
Color bg = Hex("001045");
Color blue = Hex("0057B5");
ThemeData t = Blank("Midnight Blue");
t.bgMode = 1; t.solid = bg; t.top = bg; t.bottom = bg;
t.tabOff = WithAlpha(Color.Lerp(bg, blue, 0.30f), 0.85f);
t.tabHover = WithAlpha(Color.Lerp(bg, blue, 0.70f), 0.95f);
t.tabOn = blue;
t.logo = blue;
t.sub = blue;
return t;
}
protected static ThemeData Grad(string name, string top, string bottom, string on, string logo)
{
Color t0 = Hex(top), b0 = Hex(bottom), a = Hex(on);
ThemeData t = Blank(name);
t.top = t0; t.bottom = b0; t.solid = t0;
t.tabOff = WithAlpha(Color.Lerp(t0, a, 0.25f), 0.85f);
t.tabHover = WithAlpha(Color.Lerp(t0, a, 0.65f), 0.95f);
t.tabOn = a;
t.logo = Hex(logo);
return t;
}
protected static ThemeData Light(string name, string top, string bottom, string title, string subColor)
{
Color b0 = Hex(bottom);
ThemeData t = Blank(name);
t.top = Hex(top); t.bottom = b0; t.solid = b0;
t.tabOff = WithAlpha(Color.Lerp(b0, Color.black, 0.55f), 0.90f);
t.tabHover = WithAlpha(Color.Lerp(b0, Color.black, 0.40f), 0.95f);
t.tabOn = Color.Lerp(b0, Color.black, 0.28f);
t.logo = Color.Lerp(b0, Color.black, 0.35f);
t.titleColor = Hex(title);
t.sub = Hex(subColor);
return t;
}
protected static ThemeData MakeNeonPurple() { return Grad("Neon Purple", "0A0014", "3B0A6B", "8A2BE2", "C77DFF"); }
protected static ThemeData MakeCyber() { return Grad("Cyber Cyan", "001419", "006E7A", "00C2D4", "00F0FF"); }
protected static ThemeData MakeBlood() { return Grad("Blood Moon", "140000", "6E0010", "B3001B", "FF3045"); }
protected static ThemeData MakeToxic() { return Grad("Toxic Lime", "0A1400", "2F5C00", "6BBF00", "A8FF3A"); }
protected static ThemeData MakeOcean() { return Grad("Ocean", "00203F", "00A6A6", "008C8C", "5EF2E8"); }
protected static ThemeData MakeSunset()
{
ThemeData t = Grad("Sunset", "2B1055", "FF6A3D", "E8542A", "FFC15E");
t.sub = Hex("FFE2C2");
return t;
}
protected static ThemeData MakeMono()
{
ThemeData t = Grad("Monochrome", "0D0D0D", "3A3A3A", "6E6E6E", "E6E6E6");
t.sub = Hex("BDBDBD");
t.autoOutline = false;
t.outlineColor = Hex("333333");
return t;
}
protected static ThemeData MakeMatrix()
{
ThemeData t = Grad("Matrix", "000A00", "012A0A", "00A838", "00FF55");
t.bgMode = 1;
t.solid = Hex("020B02");
t.titleColor = Hex("7CFF9A");
t.sub = Hex("00B33C");
t.tabText = Hex("9CFFB5");
return t;
}
protected static ThemeData MakeBlossom() { return Light("Cherry Blossom", "FFF1F5", "F7A8C4", "4A1230", "7A2F4D"); }
protected static ThemeData MakeGhost() { return Light("Ghost White", "F4F7FF", "B8C4E0", "141B3A", "2A3355"); }
protected static Font curFont;
protected static Font curUiFont;
protected static Font LoadFont(string guid)
{
if (string.IsNullOrEmpty(guid)) return null;
string p = AssetDatabase.GUIDToAssetPath(guid);
return string.IsNullOrEmpty(p) ? null : AssetDatabase.LoadAssetAtPath<Font>(p);
}
protected static string FontGuid(Font f)
{
return f == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(f));
}
protected static Texture2D curBg;
protected static int themeVersion;
protected int builtVersion;
protected static Texture2D LoadTex(string guid)
{
if (string.IsNullOrEmpty(guid)) return null;
string p = AssetDatabase.GUIDToAssetPath(guid);
return string.IsNullOrEmpty(p) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(p);
}
protected static string TexGuid(Texture2D t)
{
return t == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(t));
}
protected static void RefreshFont()
{
curFont = LoadFont(Cur.logoFont);
curUiFont = LoadFont(Cur.uiFont);
curBg = LoadTex(Cur.bgImage);
}
protected static void DrawImage(Rect r, Texture2D img, ThemeData t)
{
GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, t.solid, 0f, t.radius);
ScaleMode m = t.imageFit == 1 ? ScaleMode.StretchToFill : t.imageFit == 2 ? ScaleMode.ScaleToFit : ScaleMode.ScaleAndCrop;
GUI.DrawTexture(r, img, m, true, 0f, t.imageTint, 0f, t.radius);
if (t.imageDim > 0.001f)
GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, t.imageDim), 0f, t.radius);
}
protected static ThemeData Cur = LoadCurrent();
protected ThemeData pend;
protected static ThemeData LoadCurrent()
{
string json = EditorPrefs.GetString(ThemePref + "json", "");
if (!string.IsNullOrEmpty(json))
{
try
{
ThemeData t = JsonUtility.FromJson<ThemeData>(json);
if (t != null && !string.IsNullOrEmpty(t.name))
{
if (t.ver < 2)
{
t.ver = 2;
t.solid = t.top;
t.logoGradient = true;
t.logoHighlight = true;
t.logoSize = 28;
t.logoBold = true;
t.logoFont = "";
}
if (t.ver < 3)
{
t.ver = 3;
t.uiFont = "";
}
if (t.ver < 4)
{
t.ver = 4;
t.sub = new Color(0.8f, 0.8f, 0.9f);
if (t.logo.r > 0.99f && t.logo.g > 0.99f && t.logo.b > 0.99f) t.logo = new Color(0.50f, 0.15f, 0.90f);
}
if (t.ver < 5)
{
t.ver = 5;
t.bgMode = t.solidBg ? 1 : 0;
t.bgImage = "";
t.imageFit = 0;
t.imageTint = Color.white;
t.imageDim = 0f;
t.tabText = new Color(0.85f, 0.82f, 1f);
t.tabTextOn = Color.white;
t.tabHeight = 30;
t.tabFontSize = 10;
t.titleColor = new Color(0.96f, 0.92f, 1f);
t.titleSize = 15;
t.subSize = 11;
t.logoOutline = true;
}
if (t.ver < 6)
{
t.ver = 6;
t.showLogo = true;
t.autoOutline = true;
t.outlineColor = new Color(0.28f, 0.06f, 0.55f);
t.boxColor = new Color(0f, 0f, 0f, 0f);
t.borderColor = new Color(1f, 1f, 1f, 0.25f);
t.borderWidth = 0;
t.bgPadding = 14;
t.sectionGap = 6;
t.tabsPerRow = 3;
t.titleCenter = true;
t.subItalic = true;
}
return EnsureContrast(t);
}
}
catch { }
}
return MakeDefault();
}
protected void LoadPending()
{
pend = Cur.Copy();
}
protected void ApplyTheme()
{
Cur = pend.Copy();
EditorPrefs.SetString(ThemePref + "json", JsonUtility.ToJson(Cur));
RefreshFont();
themeVersion++;
RebuildVisuals();
InternalEditorUtility.RepaintAllViews();
}
protected void RebuildVisuals()
{
builtVersion = themeVersion;
if (gradientTexture != null) DestroyImmediate(gradientTexture);
BuildGradient();
if (tabOffTex != null) DestroyImmediate(tabOffTex);
if (tabHoverTex != null) DestroyImmediate(tabHoverTex);
if (tabOnTex != null) DestroyImmediate(tabOnTex);
tabOffTex = tabHoverTex = tabOnTex = null;
tabOffStyle = tabOnStyle = null;
sectionTitleStyle = null;
noteStyle = null;
subStyle = null;
logoStyle = null;
logoOutlineStyle = null;
backgroundStyle = null;
Repaint();
}
protected void ResetTheme()
{
pend = MakeDefault();
ApplyTheme();
}
protected void DrawPreview()
{
Rect r = GUILayoutUtility.GetRect(10f, 44f, GUILayout.ExpandWidth(true));
if (Event.current.type != EventType.Repaint) return;
if (pend.bgMode == 2)
{
Texture2D img = LoadTex(pend.bgImage);
if (img != null) { DrawImage(r, img, pend); return; }
}
if (pend.bgMode == 1) { EditorGUI.DrawRect(r, pend.solid); return; }
const int n = 16;
float h = r.height / n;
for (int i = 0; i < n; i++)
EditorGUI.DrawRect(new Rect(r.x, r.y + i * h, r.width, h + 1f), Color.Lerp(pend.top, pend.bottom, i / (n - 1f)));
}
protected void DrawTheme()
{
DrawSection("Themes", () =>
{
GUILayout.Label("Using now: " + Cur.name, EditorStyles.miniBoldLabel);
string[] names = { "Default", "Dark Gold", "Bubblegum", "Dark Green", "Midnight Blue", "Calm Red",
"Neon Purple", "Cyber Cyan", "Blood Moon", "Toxic Lime", "Ocean", "Sunset",
"Monochrome", "Matrix", "Cherry Blossom", "Ghost White" };
System.Func<ThemeData>[] makers = { MakeDefault, MakeGold, MakeBubble, MakeGreen, MakeBlue, MakeRed,
MakeNeonPurple, MakeCyber, MakeBlood, MakeToxic, MakeOcean, MakeSunset,
MakeMono, MakeMatrix, MakeBlossom, MakeGhost };
for (int i = 0; i < names.Length; i += 4)
{
EditorGUILayout.BeginHorizontal();
for (int j = i; j < i + 4 && j < names.Length; j++)
if (GUILayout.Button(names[j], GUILayout.Height(26f))) pend = EnsureContrast(makers[j]());
EditorGUILayout.EndHorizontal();
}
if (GUILayout.Button("Random Theme", GUILayout.Height(24f))) pend = EnsureContrast(makers[Random.Range(0, makers.Length)]());
GUILayout.Label("Picked: " + pend.name, EditorStyles.miniLabel);
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Background", () =>
{
EditorGUI.BeginChangeCheck();
pend.bgMode = GUILayout.Toolbar(pend.bgMode, new[] { "Gradient", "One Color", "Image" }, GUILayout.Height(24f));
if (pend.bgMode == 0)
{
pend.top = EditorGUILayout.ColorField("Top Color", pend.top);
pend.bottom = EditorGUILayout.ColorField("Bottom Color", pend.bottom);
}
else if (pend.bgMode == 1)
{
pend.solid = EditorGUILayout.ColorField("Color", pend.solid);
}
else
{
pend.bgImage = TexGuid((Texture2D)EditorGUILayout.ObjectField("Image", LoadTex(pend.bgImage), typeof(Texture2D), false));
pend.imageFit = EditorGUILayout.Popup("Size", pend.imageFit, new[] { "Fill (cuts the edges)", "Stretch", "Fit Inside" });
if (pend.imageFit == 2) pend.solid = EditorGUILayout.ColorField("Color Behind Image", pend.solid);
pend.imageTint = EditorGUILayout.ColorField("Tint", pend.imageTint);
pend.imageDim = EditorGUILayout.Slider("Darkness", pend.imageDim, 0f, 0.9f);
}
pend.radius = EditorGUILayout.Slider("Rounded Corners", pend.radius, 0f, 24f);
if (EditorGUI.EndChangeCheck()) pend.name = "Custom";
GUILayout.Space(2);
DrawPreview();
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Boxes And Border", () =>
{
EditorGUI.BeginChangeCheck();
pend.boxColor = EditorGUILayout.ColorField("Box Color", pend.boxColor);
pend.borderWidth = EditorGUILayout.IntSlider("Border Size", pend.borderWidth, 0, 6);
if (pend.borderWidth > 0) pend.borderColor = EditorGUILayout.ColorField("Border Color", pend.borderColor);
pend.bgPadding = EditorGUILayout.IntSlider("Side Space", pend.bgPadding, 4, 30);
pend.sectionGap = EditorGUILayout.IntSlider("Space Between Boxes", pend.sectionGap, 0, 20);
if (EditorGUI.EndChangeCheck()) pend.name = "Custom";
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Tabs", () =>
{
EditorGUI.BeginChangeCheck();
pend.tabOff = EditorGUILayout.ColorField("Normal", pend.tabOff);
pend.tabHover = EditorGUILayout.ColorField("Mouse Over", pend.tabHover);
pend.tabOn = EditorGUILayout.ColorField("Picked", pend.tabOn);
pend.tabText = EditorGUILayout.ColorField("Text", pend.tabText);
pend.tabTextOn = EditorGUILayout.ColorField("Text (Picked)", pend.tabTextOn);
pend.tabHeight = EditorGUILayout.IntSlider("Height", pend.tabHeight, 20, 50);
pend.tabFontSize = EditorGUILayout.IntSlider("Text Size", pend.tabFontSize, 8, 16);
pend.tabsPerRow = EditorGUILayout.IntSlider("Tabs Per Row", pend.tabsPerRow, 2, 6);
if (EditorGUI.EndChangeCheck()) pend.name = "Custom";
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Text (not the logo)", () =>
{
EditorGUI.BeginChangeCheck();
Font tf = (Font)EditorGUILayout.ObjectField("Font File", LoadFont(pend.uiFont), typeof(Font), false);
pend.uiFont = FontGuid(tf);
pend.titleColor = EditorGUILayout.ColorField("Title Color", pend.titleColor);
pend.titleSize = EditorGUILayout.IntSlider("Title Size", pend.titleSize, 10, 24);
pend.titleCenter = EditorGUILayout.Toggle("Center Titles", pend.titleCenter);
pend.sub = EditorGUILayout.ColorField("Subtitle Color", pend.sub);
pend.subSize = EditorGUILayout.IntSlider("Subtitle Size", pend.subSize, 8, 18);
pend.subItalic = EditorGUILayout.Toggle("Slanted Subtitle", pend.subItalic);
if (EditorGUI.EndChangeCheck()) pend.name = "Custom";
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Logo", () =>
{
GUILayout.Label("This only applies to the logo.", EditorStyles.miniLabel);
EditorGUI.BeginChangeCheck();
pend.showLogo = EditorGUILayout.Toggle("Show Logo", pend.showLogo);
pend.logo = EditorGUILayout.ColorField("Logo Color", pend.logo);
pend.logoGradient = EditorGUILayout.Toggle("Fade Between Letters", pend.logoGradient);
pend.logoHighlight = EditorGUILayout.Toggle("Lighter Top Half", pend.logoHighlight);
pend.logoOutline = EditorGUILayout.Toggle("Outline", pend.logoOutline);
if (pend.logoOutline)
{
pend.autoOutline = EditorGUILayout.Toggle("Pick Outline Color For Me", pend.autoOutline);
if (!pend.autoOutline) pend.outlineColor = EditorGUILayout.ColorField("Outline Color", pend.outlineColor);
}
Font f = (Font)EditorGUILayout.ObjectField("Logo Font File", LoadFont(pend.logoFont), typeof(Font), false);
pend.logoFont = FontGuid(f);
pend.logoSize = EditorGUILayout.IntSlider("Font Size", pend.logoSize, 12, 60);
pend.logoBold = EditorGUILayout.Toggle("Bold", pend.logoBold);
if (EditorGUI.EndChangeCheck()) pend.name = "Custom";
});
GUILayout.Space(Cur.sectionGap);
bool dirty = !pend.Same(Cur);
if (dirty) EditorGUILayout.HelpBox("You changed some things. Press Apply to use them.", MessageType.Info);
EditorGUILayout.BeginHorizontal();
GUI.enabled = dirty;
if (GUILayout.Button("Apply", GUILayout.Height(26f))) ApplyTheme();
if (GUILayout.Button("Undo Changes", GUILayout.Height(26f))) LoadPending();
GUI.enabled = true;
if (GUILayout.Button("Reset", GUILayout.Height(26f))) ResetTheme();
EditorGUILayout.EndHorizontal();
EditorGUILayout.BeginHorizontal();
if (GUILayout.Button("Copy Theme", GUILayout.Height(22f)))
EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(pend);
if (GUILayout.Button("Paste Theme", GUILayout.Height(22f)))
{
try
{
ThemeData t = JsonUtility.FromJson<ThemeData>(EditorGUIUtility.systemCopyBuffer);
if (t != null && t.ver >= 6) { t.name = "Pasted"; pend = EnsureContrast(t); }
}
catch { }
}
EditorGUILayout.EndHorizontal();
}
protected void OnEnable()
{
LoadPending();
RefreshFont();
builtVersion = themeVersion;
BuildGradient();
OuterAI.CheckAssetLinks();
}
protected void OnDisable()
{
if (gradientTexture != null) DestroyImmediate(gradientTexture);
if (tabOffTex != null) DestroyImmediate(tabOffTex);
if (tabHoverTex != null) DestroyImmediate(tabHoverTex);
if (tabOnTex != null) DestroyImmediate(tabOnTex);
}
protected static Texture2D Solid(Color c)
{
Texture2D t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
t.SetPixel(0, 0, c);
t.Apply(false, true);
return t;
}
protected void BuildTabStyles()
{
if (tabOffTex == null) tabOffTex = Solid(Cur.tabOff);
if (tabHoverTex == null) tabHoverTex = Solid(Cur.tabHover);
if (tabOnTex == null) tabOnTex = Solid(Cur.tabOn);
tabOffStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontSize = Cur.tabFontSize, wordWrap = true, fixedHeight = Cur.tabHeight };
if (curUiFont != null) tabOffStyle.font = curUiFont;
tabOffStyle.normal.background = tabOffTex;
tabOffStyle.hover.background = tabHoverTex;
tabOffStyle.active.background = tabOnTex;
tabOffStyle.normal.textColor = tabOffStyle.hover.textColor = tabOffStyle.active.textColor = Cur.tabText;
tabOnStyle = new GUIStyle(tabOffStyle) { fontStyle = FontStyle.Bold };
tabOnStyle.normal.background = tabOnStyle.hover.background = tabOnStyle.active.background = tabOnTex;
tabOnStyle.normal.textColor = tabOnStyle.hover.textColor = tabOnStyle.active.textColor = Cur.tabTextOn;
}
protected void BuildGradient()
{
gradientTexture = new Texture2D(1, 64, TextureFormat.RGBA32, false)
{
hideFlags = HideFlags.HideAndDontSave,
wrapMode = TextureWrapMode.Clamp,
filterMode = FilterMode.Bilinear
};
for (int y = 0; y < 64; y++) gradientTexture.SetPixel(0, y, Color.Lerp(Cur.bgMode == 1 ? Cur.solid : Cur.bottom, Cur.bgMode == 1 ? Cur.solid : Cur.top, y / 63f));
gradientTexture.Apply(false, true);
}
protected void OnSceneGUI()
{
OuterAI m = target as OuterAI;
if (m == null || OuterAITos.Blocked) return;
Vector3 origin = m.transform.position + Vector3.up;
if (Event.current.type == EventType.Repaint && OuterAIGizmoPrefs.ShowGizmos)
{
if (OuterAIGizmoPrefs.ShowMovementGizmos)
{
DrawRange(origin, m.ChaseSphereRange, m.transform, OuterAIGizmoPrefs.ChaseSphereColor, OuterAIGizmoPrefs.GizmoFillOpacity);
if (OuterAIGizmoPrefs.ShowGizmoLabels) Handles.Label(origin + Vector3.right * m.ChaseSphereRange, "Chase " + m.ChaseSphereRange.ToString("F1"));
if (m.UseBlindAudioAI)
{
DrawRange(origin, m.AudioDetectionRadius, m.transform, OuterAIGizmoPrefs.TargetLineColor, OuterAIGizmoPrefs.GizmoFillOpacity);
if (OuterAIGizmoPrefs.ShowGizmoLabels) Handles.Label(origin + Vector3.left * m.AudioDetectionRadius, "Audio " + m.AudioDetectionRadius.ToString("F1"));
}
if (OuterAIGizmoPrefs.ShowKillRange)
{
DrawRange(origin, m.KillDistanceRadius, m.transform, OuterAIGizmoPrefs.KillRangeColor, OuterAIGizmoPrefs.GizmoFillOpacity);
if (OuterAIGizmoPrefs.ShowGizmoLabels) Handles.Label(origin + Vector3.forward * m.KillDistanceRadius, "Kill " + m.KillDistanceRadius.ToString("F1"));
}
}
if (OuterAIGizmoPrefs.ShowDetectionGizmos && !m.UseBlindAudioAI) DrawVisionCone(m, origin);
if (OuterAIGizmoPrefs.ShowPathGizmos) DrawPath(m);
}
EditorGUI.BeginChangeCheck();
Handles.color = Color.yellow;
float cone = Handles.RadiusHandle(Quaternion.identity, origin + m.transform.forward * 0.1f, m.ConeVisionRange, true);
Handles.color = Color.cyan;
float chase = Handles.RadiusHandle(Quaternion.identity, origin, m.ChaseSphereRange);
float audio = m.AudioDetectionRadius;
if (m.UseBlindAudioAI)
{
Handles.color = Color.magenta;
audio = Handles.RadiusHandle(Quaternion.identity, origin, m.AudioDetectionRadius);
}
if (EditorGUI.EndChangeCheck())
{
Undo.RecordObject(m, "Adjust Monster Ranges");
m.ConeVisionRange = Mathf.Max(0.1f, cone);
m.ChaseSphereRange = Mathf.Max(0.1f, chase);
m.AudioDetectionRadius = Mathf.Max(0.1f, audio);
EditorUtility.SetDirty(m);
}
}
protected static float Fov(OuterAI m) { return m.Use360Vision ? 360f : m.ViewAngle; }
protected static int BlockMask(OuterAI m)
{
int mask = 0;
foreach (string layerName in m.VisionBlockLayerNames)
{
int layer = LayerMask.NameToLayer(layerName);
if (layer != -1) mask |= 1 << layer;
}
return mask;
}
protected static float CastDistance(Vector3 o, Vector3 dir, float range, int mask)
{
RaycastHit hit;
return Physics.Raycast(o, dir, out hit, range, mask) ? hit.distance : range;
}
protected static void DrawVisionCone(OuterAI m, Vector3 origin)
{
DrawConeSlices(m, origin);
}
protected static void DrawConeSlices(OuterAI m, Vector3 origin)
{
int mask = BlockMask(m);
int slices = Mathf.Max(4, OuterAIGizmoPrefs.GizmoConeDetail);
float range = m.ConeVisionRange;
float fov = Fov(m);
float step = fov / slices;
float start = -fov / 2f;
Color clear = OuterAIGizmoPrefs.VisionConeClearColor;
clear.a = OuterAIGizmoPrefs.GizmoFillOpacity;
Color blocked = OuterAIGizmoPrefs.VisionConeBlockedColor;
blocked.a = OuterAIGizmoPrefs.GizmoFillOpacity;
Vector3 prevDir = m.DirFromAngle(start, false);
float prevD = CastDistance(origin, prevDir, range, mask);
for (int i = 1; i <= slices; i++)
{
Vector3 dir = m.DirFromAngle(start + step * i, false);
float d = CastDistance(origin, dir, range, mask);
Handles.color = clear;
Handles.DrawAAConvexPolygon(origin, origin + prevDir * prevD, origin + dir * d);
if (prevD < range || d < range)
{
Handles.color = blocked;
Handles.DrawAAConvexPolygon(origin + prevDir * prevD, origin + prevDir * range, origin + dir * range, origin + dir * d);
}
prevDir = dir;
prevD = d;
}
}
protected static void DrawPath(OuterAI m)
{
if (!Application.isPlaying || m.agent == null || !m.agent.enabled || !m.agent.isOnNavMesh || !m.agent.hasPath) return;
Handles.color = OuterAIGizmoPrefs.PathColor;
Handles.DrawAAPolyLine(3f, m.agent.path.corners);
}
protected static void DrawRange(Vector3 o, float r, Transform t, Color col, float fillA)
{
Handles.color = col;
Handles.DrawWireDisc(o, Vector3.up, r);
}
protected static string GradientText(string text, Color from, Color to)
{
System.Text.StringBuilder sb = new System.Text.StringBuilder();
int last = Mathf.Max(1, text.Length - 1);
for (int i = 0; i < text.Length; i++)
{
if (text[i] == ' ') { sb.Append(' '); continue; }
sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(Color.Lerp(from, to, (float)i / last))).Append(">").Append(text[i]).Append("</color>");
}
return sb.ToString();
}
[InitializeOnLoadMethod]
protected static void RefreshGizmoCache()
{
OuterAI.CheckAssetLinks();
}
protected void BuildLogo()
{
Color l = Cur.logo;
Color b1 = l;
Color b2 = Color.Lerp(l, Color.white, 0.20f);
Color h1 = Color.Lerp(l, Color.white, 0.40f);
Color h2 = Color.Lerp(l, Color.white, 0.70f);
Color ol = Cur.autoOutline ? Color.Lerp(l, Color.black, 0.60f) : Cur.outlineColor;
if (!Cur.logoGradient) { b2 = b1; h2 = h1; }
logoStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = LogoPx, fontStyle = Cur.logoBold ? FontStyle.Bold : FontStyle.Normal, richText = true };
if (curFont != null) logoStyle.font = curFont;
logoStyle.normal.textColor = Color.white;
logoOutlineStyle = new GUIStyle(logoStyle) { richText = false };
logoOutlineStyle.normal.textColor = ol;
logoBaseText = GradientText(LogoText, b1, b2);
logoHighText = GradientText(LogoText, h1, h2);
}
protected void DrawLogo()
{
if (!Cur.showLogo) return;
if (logoStyle == null) BuildLogo();
Rect rect = GUILayoutUtility.GetRect(GUIContent.none, logoStyle, GUILayout.Height(LogoPx + 12f));
if (Event.current.type != EventType.Repaint) return;
if (Cur.logoOutline)
{
for (int ox = -1; ox <= 1; ox++)
for (int oy = -1; oy <= 1; oy++)
if (ox != 0 || oy != 0)
GUI.Label(new Rect(rect.x + ox, rect.y + oy, rect.width, rect.height), LogoText, logoOutlineStyle);
}
GUI.Label(rect, logoBaseText, logoStyle);
if (Cur.logoHighlight)
{
GUI.BeginGroup(new Rect(rect.x, rect.y, rect.width, rect.height * 0.5f));
GUI.Label(new Rect(0f, 0f, rect.width, rect.height), logoHighText, logoStyle);
GUI.EndGroup();
}
}
protected void DrawSection(string title, System.Action contents)
{
Rect box = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
if (Event.current.type == EventType.Repaint && Cur.boxColor.a > 0.001f)
GUI.DrawTexture(box, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, Cur.boxColor, 0f, 4f);
GUILayout.Space(2);
if (sectionTitleStyle == null)
{
sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = Cur.titleCenter ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, fontSize = Cur.titleSize };
if (curUiFont != null) sectionTitleStyle.font = curUiFont;
sectionTitleStyle.normal.textColor = Cur.titleColor;
}
GUILayout.Label(title, sectionTitleStyle, GUILayout.Height(24f));
GUILayout.Space(3);
contents();
GUILayout.Space(2);
EditorGUILayout.EndVertical();
}
protected void DrawList(string propName, string label)
{
if (targets.Length > 1) { EditorGUILayout.HelpBox(label + ": select ONE monster to edit this list.", MessageType.Info); return; }
ReorderableList list;
if (!lists.TryGetValue(propName, out list))
{
list = new ReorderableList(serializedObject, serializedObject.FindProperty(propName), true, true, true, true);
ReorderableList l = list;
list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, label);
list.drawElementCallback = (rect, index, isActive, isFocused) =>
{
rect.y += 2f;
rect.height = EditorGUIUtility.singleLineHeight;
EditorGUI.PropertyField(rect, l.serializedProperty.GetArrayElementAtIndex(index), GUIContent.none);
};
lists[propName] = list;
}
list.DoLayoutList();
}
protected void Field(string name, string label, string tip)
{
EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label, tip), true);
}
protected void Fields(params string[] names)
{
foreach (string n in names) EditorGUILayout.PropertyField(serializedObject.FindProperty(n));
}
protected GUIStyle[] darkStyles;
protected Color[] darkBackup;
protected void PushDarkText()
{
GUIStyle[] st = { EditorStyles.label, EditorStyles.miniLabel, EditorStyles.miniBoldLabel, EditorStyles.boldLabel,
EditorStyles.wordWrappedLabel, EditorStyles.wordWrappedMiniLabel, EditorStyles.toggle, EditorStyles.foldout,
GUI.skin.label, GUI.skin.toggle };
darkStyles = st;
darkBackup = new Color[st.Length * 8];
Color dark = new Color(0.07f, 0.07f, 0.10f);
for (int i = 0; i < st.Length; i++)
{
GUIStyleState[] states = { st[i].normal, st[i].hover, st[i].active, st[i].focused, st[i].onNormal, st[i].onHover, st[i].onActive, st[i].onFocused };
for (int k = 0; k < 8; k++)
{
darkBackup[i * 8 + k] = states[k].textColor;
states[k].textColor = dark;
}
}
}
protected void PopDarkText()
{
if (darkStyles == null) return;
for (int i = darkStyles.Length - 1; i >= 0; i--)
{
GUIStyle g = darkStyles[i];
GUIStyleState[] states = { g.normal, g.hover, g.active, g.focused, g.onNormal, g.onHover, g.onActive, g.onFocused };
for (int k = 7; k >= 0; k--) states[k].textColor = darkBackup[i * 8 + k];
}
darkStyles = null;
darkBackup = null;
}
protected List<OuterAI> AllTargets()
{
List<OuterAI> r = new List<OuterAI>();
foreach (Object o in targets) { OuterAI a = o as OuterAI; if (a != null) r.Add(a); }
return r;
}
internal void DrawThemesPanel()
{
Font oldFont = GUI.skin.font;
if (curUiFont != null) GUI.skin.font = curUiFont;
bool darkText = LightBg(Cur);
if (darkText) PushDarkText();
try
{
if (builtVersion != themeVersion)
{
LoadPending();
RebuildVisuals();
}
if (gradientTexture == null) BuildGradient();
if (tabOffStyle == null || tabOffTex == null || tabOnTex == null) BuildTabStyles();
if (backgroundStyle == null) backgroundStyle = new GUIStyle { padding = new RectOffset(Cur.bgPadding, Cur.bgPadding, 6, 8) };
Rect bg = EditorGUILayout.BeginVertical(backgroundStyle);
if (Event.current.type == EventType.Repaint)
{
if (Cur.bgMode == 2 && curBg != null) DrawImage(bg, curBg, Cur);
else GUI.DrawTexture(bg, gradientTexture, ScaleMode.StretchToFill, true, 0f, Color.white, 0f, Cur.radius);
if (Cur.borderWidth > 0)
GUI.DrawTexture(bg, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, Cur.borderColor, Cur.borderWidth, Cur.radius);
}
GUILayout.Space(6);
DrawTheme();
EditorGUILayout.EndVertical();
}
finally
{
if (darkText) PopDarkText();
GUI.skin.font = oldFont;
}
}
public override void OnInspectorGUI()
{
if (OuterAITos.Blocked) { OuterAITos.DrawBlocked(); return; }
Font oldFont = GUI.skin.font;
if (curUiFont != null) GUI.skin.font = curUiFont;
bool darkText = LightBg(Cur);
if (darkText) PushDarkText();
try { DrawMain(); }
finally
{
if (darkText) PopDarkText();
GUI.skin.font = oldFont;
}
}
protected void DrawMain()
{
serializedObject.Update();
if (builtVersion != themeVersion)
{
LoadPending();
RebuildVisuals();
}
if (gradientTexture == null) BuildGradient();
if (tabOffStyle == null || tabOffTex == null || tabOnTex == null) BuildTabStyles();
if (backgroundStyle == null) backgroundStyle = new GUIStyle { padding = new RectOffset(Cur.bgPadding, Cur.bgPadding, 6, 8) };
Rect bg = EditorGUILayout.BeginVertical(backgroundStyle);
if (Event.current.type == EventType.Repaint)
{
if (Cur.bgMode == 2 && curBg != null) DrawImage(bg, curBg, Cur);
else GUI.DrawTexture(bg, gradientTexture, ScaleMode.StretchToFill, true, 0f, Color.white, 0f, Cur.radius);
if (Cur.borderWidth > 0)
GUI.DrawTexture(bg, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, Cur.borderColor, Cur.borderWidth, Cur.radius);
}
GUILayout.Space(9);
DrawLogo();
if (subStyle == null)
{
subStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = Cur.subSize, fontStyle = Cur.subItalic ? FontStyle.Italic : FontStyle.Normal };
if (curUiFont != null) subStyle.font = curUiFont;
subStyle.normal.textColor = Cur.sub;
}
GUILayout.Label("Public free horror AI made by outer, no credits needed :)", subStyle);
GUILayout.Space(12);
DrawBody();
EditorGUILayout.EndVertical();
serializedObject.ApplyModifiedProperties();
}
}

[CustomEditor(typeof(OuterAI))]
[CanEditMultipleObjects]
public class OuterAIEditor : OuterAIThemedEditor
{
protected override void DrawBody()
{
OuterAI script = (OuterAI)target;
string[] tabs = { "Main", "Movement", "Sight & Hearing", "Jumpscare", "Extras", "Anti Lag" };
if (script.ActiveInspectorTab >= tabs.Length || script.ActiveInspectorTab < 0) script.ActiveInspectorTab = 0;
int perRow = Mathf.Clamp(Cur.tabsPerRow, 2, 6);
for (int i = 0; i < tabs.Length; i++)
{
if (i % perRow == 0) EditorGUILayout.BeginHorizontal();
if (GUILayout.Button(tabs[i], i == script.ActiveInspectorTab ? tabOnStyle : tabOffStyle))
script.ActiveInspectorTab = i;
if (i % perRow == perRow - 1 || i == tabs.Length - 1) EditorGUILayout.EndHorizontal();
}
GUILayout.Space(10);
switch (script.ActiveInspectorTab)
{
case 0:
DrawSection("Main Settings", () => Fields("tagString", "agent"));
GUILayout.Space(Cur.sectionGap);
DrawSection("Auto Setup & Checker", () =>
{
GUILayout.Label(targets.Length > 1 ? "Runs on all " + targets.Length + " selected monsters at once." : "Finds the walk / kill / detect objects, cameras, sounds and respawn point, then checks the whole setup.", EditorStyles.wordWrappedMiniLabel);
EditorGUILayout.BeginHorizontal();
if (GUILayout.Button("Auto Setup + Check", GUILayout.Height(26f)))
{ List<OuterAI> all = AllTargets(); EditorApplication.delayCall += () => OuterAIManagerTools.RunSetup(all); }
if (GUILayout.Button("Check Only", GUILayout.Height(26f)))
{ List<OuterAI> all = AllTargets(); EditorApplication.delayCall += () => OuterAIManagerTools.RunCheck(all); }
if (GUILayout.Button(new GUIContent("Create NavMesh", "Creates a NavMesh Surface and bakes it (also rebakes)."), GUILayout.Height(26f)))
{ List<OuterAI> all = AllTargets(); EditorApplication.delayCall += () => OuterAIManagerTools.RunNavMesh(all); }
EditorGUILayout.EndHorizontal();
OuterAIManagerTools.DrawInline(script);
if (GUILayout.Button(new GUIContent("Open the Outer AI Manager (many monsters, gizmos, themes)", "Selects the Outer AI Manager object in your scene (makes one if needed) and adds this monster to it.")))
{ OuterAI addMe = script; EditorApplication.delayCall += () => OuterAIManagerTools.OpenOrCreate(addMe); }
});
break;
case 1:
DrawSection("Speed", () => Fields("MonsterSpeedWander", "MonsterSpeedChase"));
GUILayout.Space(Cur.sectionGap);
DrawSection("Turning", () => Fields("RotationSpeed"));
GUILayout.Space(Cur.sectionGap);
DrawSection("Walk / Run Animation", () => Fields("UseAnimParams", "AnimSpeedParam", "AnimRunParam"));
GUILayout.Space(Cur.sectionGap);
DrawSection("How It Moves", () =>
{
Fields("UseRandomRoaming");
if (script.UseRandomRoaming) Fields("RandomRoamingRadius");
else DrawList("points", "Patrol Points");
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Footsteps", () =>
{
Field("UseFootsteps", "Use Footsteps", "Plays a sound each time a hand or foot hits the ground.");
if (!script.UseFootsteps) return;
Field("LeftHand", "Left Hand/Foot", "The left part that hits the ground. The sound plays when it touches the floor.");
Field("RightHand", "Right Hand/Foot", "The right part that hits the ground. The sound plays when it touches the floor.");
Field("LeftStepSource", "Left Sound Source", "The audio source that plays the left sound.");
Field("RightStepSource", "Right Sound Source", "The audio source that plays the right sound.");
Field("StepGroundLayers", "Ground Layers", "Only these layers count as ground.");
Field("StepSounds", "Sounds For Each Material", "Pick a material, then add as many sounds as you want for it. One is picked at random each step.");
Field("DefaultStepSounds", "Default Sounds", "Used when the material isn't in the list above. One is picked at random each step.");
Field("StepCheckDistance", "Check Distance", "How far below the hand or foot to look for the ground.");
Field("StepLoudness", "Loudness", "1 is normal, 2 is twice as loud, 5 is five times as loud. Very high numbers can sound crackly.");
Field("StepCooldown", "Time Between Steps", "The shortest time between two steps from the same hand or foot.");
Field("StepRayHeight", "Check Height", "Starts the ground check this far above the hand or foot. Raise it if steps get missed.");
});
break;
case 2:
DrawSection("Seeing", () =>
{
Fields("ConeVisionRange", "ChaseSphereRange");
SerializedProperty p360 = serializedObject.FindProperty("Use360Vision");
p360.boolValue = GUILayout.Toolbar(p360.boolValue ? 1 : 0, new[] { "Cone", "All Around" }, GUILayout.Height(24f)) == 1;
if (!p360.boolValue) Fields("ViewAngle");
DrawList("VisionBlockLayerNames", "Layers That Block Sight");
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Chasing", () => Fields("ChaseMemoryDuration"));
GUILayout.Space(Cur.sectionGap);
DrawSection("Detection Animation", () =>
{
Field("UseDetectionAnim", "Use Detection Animation", "Plays an animation and a sound when the monster first spots a player.");
if (!script.UseDetectionAnim) return;
Field("DetectObject", "Detection Animation Object", "The object that plays when a player is spotted. It shows in place of the walking object, then the walking object comes back.");
Field("DetectSound", "Detection Sound", "The audio source that plays when a player is spotted.");
Field("DetectDuration", "How Long It Plays (seconds)", "How long the detection animation stays on before going back to walking.");
Field("DetectCooldown", "Wait Before It Can Play Again (seconds)", "Stops the animation from replaying over and over when the monster keeps losing and spotting the player.");
Field("FreezeDuringDetect", "Stand Still While It Plays", "Off: the monster starts chasing right away. On: it waits until the animation is done.");
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Hearing Mode (Blind AI)", () =>
{
Fields("UseBlindAudioAI");
if (script.UseBlindAudioAI) Fields("MinVoiceVolumeRadius", "AudioDetectionRadius", "DebugBlindLog");
});
break;
case 3:
DrawSection("Sounds", () =>
{
Fields("IdleSound", "ChaseSound", "FirstPersonKillAudio", "NetworkedKillAudio");
DrawList("AudioSourcesToSkipMute", "Sounds To Keep On");
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Jumpscare Setup", () => Fields("WalkObject", "KillObject", "JumpscareCamera", "PlayerMainCamera", "JumpscareDuration", "RespawnPoint"));
GUILayout.Space(Cur.sectionGap);
DrawSection("Kill Settings", () =>
{
if (noteStyle == null)
{
noteStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Italic };
if (curUiFont != null) noteStyle.font = curUiFont;
noteStyle.normal.textColor = LightBg(Cur) ? new Color(0.07f, 0.07f, 0.10f) : new Color(0.85f, 0.8f, 1f);
}
GUILayout.Label("(NO TOUCHY TOUCHY >:( )", noteStyle);
Fields("KillDistanceRadius", "PostRespawnGraceSeconds");
});
break;
case 4:
DrawSection("Search After Losing You", () =>
{
Field("UseSearchBehavior", "Search The Area", "After it loses you it walks around the last place it saw you before going back to patrol.");
if (script.UseSearchBehavior) Fields("SearchDuration", "SearchRadius", "SearchPoints");
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Chase Speed Ramp", () =>
{
Field("UseChaseRamp", "Gets Faster The Longer It Chases", "Speed climbs the longer it keeps chasing you.");
if (script.UseChaseRamp) Fields("ChaseRampBonus", "ChaseRampSeconds");
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Chase Fatigue", () =>
{
Field("UseChaseFatigue", "Gives Up After A Long Chase", "After a long chase it stops and rests so players can breathe.");
if (script.UseChaseFatigue) Fields("MaxChaseSeconds", "RestSeconds");
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Kill Rage", () =>
{
Field("UseKillRage", "Faster After Every Kill", "Every kill makes it a bit faster.");
if (script.UseKillRage) Fields("RagePerKill", "MaxRage");
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Proximity Heartbeat", () => Fields("ProximityHeartbeat", "HeartbeatRange", "HeartbeatMaxVolume"));
GUILayout.Space(Cur.sectionGap);
DrawSection("Debug", () => Fields("ShowDebugInfo"));
break;
case 5:
DrawSection("Anti Lag", () =>
{
Field("UseAntiLag", "Use Anti Lag", "Master switch for everything on this tab.");
GUILayout.Label("Presets", EditorStyles.miniBoldLabel);
EditorGUILayout.BeginHorizontal();
if (GUILayout.Button("Max Performance", GUILayout.Height(24f)))
{ foreach (OuterAI s in AllTargets()) OuterAIManagerTools.AntiLagPreset(s, 0); }
if (GUILayout.Button("Balanced", GUILayout.Height(24f)))
{ foreach (OuterAI s in AllTargets()) OuterAIManagerTools.AntiLagPreset(s, 1); }
if (GUILayout.Button("Best Quality", GUILayout.Height(24f)))
{ foreach (OuterAI s in AllTargets()) OuterAIManagerTools.AntiLagPreset(s, 2); }
EditorGUILayout.EndHorizontal();
serializedObject.Update();
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Player Checks (the main lag knob)", () =>
{
Fields("FarChecksPerSecond", "NearChecksPerSecond", "ChaseChecksPerSecond", "FarDistance", "PlayerScanInterval");
GUILayout.Label("Each check looks for players in range. Lower numbers = less lag, but a slower reaction. It automatically uses the near/chase numbers the moment a player gets close or it starts chasing.", EditorStyles.wordWrappedMiniLabel);
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Budgets", () =>
{
Fields("MaxPlayersPerCheck", "MaxHeavyChecks", "GlobalThinkBudgetPerFrame");
GUILayout.Label("Budgets cap the work done per check. 0 means no limit. Great when you have lots of players or lots of monsters.", EditorStyles.wordWrappedMiniLabel);
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Live Stats (Play Mode)", () =>
{
if (Application.isPlaying)
{
EditorGUILayout.LabelField("Checks per second", script.StatChecksPerSec.ToString());
EditorGUILayout.LabelField("Raycasts / voice reads per second", script.StatRaycastsPerSec.ToString());
Repaint();
}
else GUILayout.Label("Press Play to see how many checks it really does each second.", EditorStyles.wordWrappedMiniLabel);
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Network", () =>
{
Fields("SyncMoveThreshold", "SnapDistance");
GUILayout.Label("Only sends the position when the monster really moved, and sends one turn number instead of a full rotation.", EditorStyles.wordWrappedMiniLabel);
});
GUILayout.Space(Cur.sectionGap);
DrawSection("Footsteps And Animation", () => Fields("FootstepChecksPerSecond", "CullAnimatorsWhenHidden"));
break;
}
}
}

// ---------------------------------------------------------------------------------------------
// OUTER AI MANAGER inspector. Same look as the OuterAI inspector (theme, logo, tabs).
// ---------------------------------------------------------------------------------------------
[CustomEditor(typeof(OuterAIManager))]
public class OuterAIManagerEditor : OuterAIThemedEditor
{
    private static readonly string[] Tabs = { "Monsters", "Setup", "Bulk Edit", "Gizmos", "Themes", "Help" };

    private SerializedObject bulk;
    private int bulkHash;
    private string notice = "";
    private GUIStyle dropStyle;
    private readonly Dictionary<OuterAI, bool> foldouts = new Dictionary<OuterAI, bool>();
    private readonly Dictionary<string, bool> secOpen = new Dictionary<string, bool>();

    protected override string LogoText { get { return "OUTER AI MANAGER"; } }
    protected override float LogoScale { get { return 0.7f; } }

    private static void Defer(System.Action a) { EditorApplication.delayCall += () => a(); }

    private static void Change(OuterAIManager m, string what)
    {
        Undo.RecordObject(m, what);
        EditorUtility.SetDirty(m);
    }

    protected override void DrawBody()
    {
        OuterAIManager m = (OuterAIManager)target;
        if (m.Clean()) EditorUtility.SetDirty(m);
        List<OuterAI> t = m.TickedMonsters();

        if (m.ActiveInspectorTab < 0 || m.ActiveInspectorTab >= Tabs.Length) m.ActiveInspectorTab = 0;
        int perRow = Mathf.Clamp(Cur.tabsPerRow, 2, 6);
        for (int i = 0; i < Tabs.Length; i++)
        {
            if (i % perRow == 0) EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Tabs[i], i == m.ActiveInspectorTab ? tabOnStyle : tabOffStyle))
                m.ActiveInspectorTab = i;
            if (i % perRow == perRow - 1 || i == Tabs.Length - 1) EditorGUILayout.EndHorizontal();
        }
        GUILayout.Space(10);

        OuterAISetupEngine.pingOnly = true;
        try
        {
            switch (m.ActiveInspectorTab)
            {
                case 0: DrawMonstersTab(m, t); break;
                case 1: DrawSetupTab(m, t); break;
                case 2: DrawBulkTab(t); break;
                case 3: DrawGizmosTab(); break;
                case 4: DrawTheme(); break;
                default: DrawHelpTab(); break;
            }
        }
        finally { OuterAISetupEngine.pingOnly = false; }
    }

    // ------------------------------------------------------------ tab: monsters
    private static string Status(OuterAI a)
    {
        OuterAISetupEngine e = OuterAIManagerTools.Peek(a);
        if (e == null || e.items.Count == 0) return "not checked yet";
        return e.Errors + " errors, " + e.Warnings + " warnings";
    }

    private void DrawDropBox(OuterAIManager m)
    {
        if (dropStyle == null) dropStyle = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
        Rect r = GUILayoutUtility.GetRect(0f, 40f, GUILayout.ExpandWidth(true));
        GUI.Box(r, "Drag monsters here from the Hierarchy", dropStyle);
        Event e = Event.current;
        if ((e.type == EventType.DragUpdated || e.type == EventType.DragPerform) && r.Contains(e.mousePosition))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                Change(m, "Add monsters to Outer AI Manager");
                int added = 0;
                foreach (UnityEngine.Object o in DragAndDrop.objectReferences)
                {
                    OuterAI direct = o as OuterAI;
                    if (direct != null) { if (m.AddMonster(direct)) added++; continue; }
                    GameObject go = o as GameObject;
                    if (go == null || !go.scene.IsValid()) continue;
                    OuterAI parent = go.GetComponentInParent<OuterAI>();
                    if (parent != null) { if (m.AddMonster(parent)) added++; continue; }
                    foreach (OuterAI c in go.GetComponentsInChildren<OuterAI>(true))
                        if (m.AddMonster(c)) added++;
                }
                notice = added > 0 ? "Added " + added + " monster(s)." : "No new monsters found in what you dropped.";
                bulk = null;
            }
            e.Use();
        }
    }

    private void DrawMonstersTab(OuterAIManager m, List<OuterAI> t)
    {
        DrawSection("Monsters", () =>
        {
            GUILayout.Label(m.Monsters.Count + " monster(s) in the list, " + t.Count + " ticked. Setup, Bulk Edit and Quick Actions only change the ticked ones.", EditorStyles.wordWrappedMiniLabel);
            DrawDropBox(m);
            if (!string.IsNullOrEmpty(notice)) EditorGUILayout.HelpBox(notice, MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add All In Scene"))
            {
                Change(m, "Add all monsters");
                int n = m.FindAllInScene();
                notice = "Added " + n + " monster(s).";
                bulk = null;
            }
            if (GUILayout.Button("Add Children"))
            {
                Change(m, "Add child monsters");
                int n = 0;
                foreach (OuterAI a in m.GetComponentsInChildren<OuterAI>(true)) if (m.AddMonster(a)) n++;
                notice = n > 0 ? "Added " + n + " monster(s) from the children of this object." : "No monsters found under this object.";
                bulk = null;
            }
            if (GUILayout.Button("Clear List"))
            {
                Change(m, "Clear monster list");
                m.Monsters.Clear();
                m.Ticks.Clear();
                notice = "";
                bulk = null;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Tick All")) { Change(m, "Tick all"); m.SetAllTicked(true); bulk = null; }
            if (GUILayout.Button("Tick None")) { Change(m, "Tick none"); m.SetAllTicked(false); bulk = null; }
            if (GUILayout.Button("Select Ticked"))
            {
                OuterAIManager mm = m;
                Defer(() => Selection.objects = mm.TickedMonsters().Select(a => (UnityEngine.Object)a.gameObject).ToArray());
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4f);
            if (m.Monsters.Count == 0)
                GUILayout.Label("The list is empty. Press 'Add All In Scene' or drag your monsters into the box above.", EditorStyles.wordWrappedMiniLabel);

            int remove = -1;
            for (int i = 0; i < m.Monsters.Count; i++)
            {
                OuterAI a = m.Monsters[i];
                if (a == null) continue;
                EditorGUILayout.BeginHorizontal();
                bool on = EditorGUILayout.Toggle(m.Ticks[i], GUILayout.Width(18f));
                if (on != m.Ticks[i]) { Change(m, "Tick monster"); m.Ticks[i] = on; bulk = null; }
                OuterAI na = (OuterAI)EditorGUILayout.ObjectField(a, typeof(OuterAI), true);
                if (na != null && na != a && !m.Monsters.Contains(na)) { Change(m, "Replace monster"); m.Monsters[i] = na; bulk = null; }
                GUILayout.Label(Status(a), EditorStyles.miniLabel, GUILayout.Width(100f));
                if (GUILayout.Button("X", GUILayout.Width(24f))) remove = i;
                EditorGUILayout.EndHorizontal();
            }
            if (remove >= 0)
            {
                Change(m, "Remove monster");
                m.Monsters.RemoveAt(remove);
                m.Ticks.RemoveAt(remove);
                bulk = null;
            }
        });
        GUILayout.Space(Cur.sectionGap);

        DrawSection("Quick Actions (ticked monsters)", () =>
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Check All", GUILayout.Height(24f))) { OuterAIManager mm = m; Defer(() => OuterAIManagerTools.RunCheck(mm.TickedMonsters())); }
            if (GUILayout.Button("Turn On", GUILayout.Height(24f))) { OuterAIManager mm = m; Defer(() => OuterAIManagerTools.SetActiveAll(mm.TickedMonsters(), true)); }
            if (GUILayout.Button("Turn Off", GUILayout.Height(24f))) { OuterAIManager mm = m; Defer(() => OuterAIManagerTools.SetActiveAll(mm.TickedMonsters(), false)); }
            EditorGUILayout.EndHorizontal();
        });
        GUILayout.Space(Cur.sectionGap);

        DrawSection("Copy Settings Between Monsters", () =>
        {
            GUILayout.Label("Tune one monster, then copy its numbers to the others. This copies speeds, ranges, timings and switches. It does NOT copy models, sounds, cameras or patrol points.", EditorStyles.wordWrappedMiniLabel);
            if (m.Monsters.Count > 1)
            {
                string[] names = m.Monsters.Select(a => a == null ? "(missing)" : a.name).ToArray();
                int si = EditorGUILayout.Popup("Copy FROM", Mathf.Clamp(m.SrcIndex, 0, names.Length - 1), names);
                if (si != m.SrcIndex) { Change(m, "Change copy source"); m.SrcIndex = si; }
                if (GUILayout.Button("Copy to the other ticked monsters", GUILayout.Height(24f)))
                {
                    int fromIdx = m.SrcIndex;
                    OuterAIManager mm = m;
                    Defer(() =>
                    {
                        if (mm == null || fromIdx < 0 || fromIdx >= mm.Monsters.Count || mm.Monsters[fromIdx] == null) return;
                        OuterAI src = mm.Monsters[fromIdx];
                        if (EditorUtility.DisplayDialog("Copy settings", "Copy the numbers from '" + src.name + "' to the other ticked monsters? You can undo this with Ctrl+Z.", "Copy", "Cancel"))
                        {
                            int c = OuterAIManagerTools.CopyTuning(src, mm.TickedMonsters());
                            notice = "Copied settings to " + c + " monster(s).";
                            bulk = null;
                            InternalEditorUtility.RepaintAllViews();
                        }
                    });
                }
            }
            else GUILayout.Label("Add at least 2 monsters to use this.", EditorStyles.wordWrappedMiniLabel);
        });
    }

    // ------------------------------------------------------------ tab: setup
    private static void Flag(OuterAIManager m, string label, bool cur, System.Action<bool> set)
    {
        bool v = EditorGUILayout.ToggleLeft(label, cur);
        if (v != cur) { Change(m, "Outer AI Manager option"); set(v); }
    }

    private void DrawSetupTab(OuterAIManager m, List<OuterAI> t)
    {
        DrawSection("Auto Setup & Checker", () =>
        {
            GUILayout.Label("Runs on the " + t.Count + " ticked monster(s). Press the button once and every one of them gets set up.", EditorStyles.wordWrappedMiniLabel);
            Flag(m, "Match Jumpscare/Detect duration to animation length", m.OptDurations, v => m.OptDurations = v);
            Flag(m, "Create + bake a NavMesh if the scene has none", m.OptNavMesh, v => m.OptNavMesh = v);
            Flag(m, "Apply the Max Performance anti-lag preset", m.OptPerf, v => m.OptPerf = v);
            Flag(m, "Add PlayerDeathHandler + LocalPlayerTeleport to the player(s)", m.OptPlayers, v => m.OptPlayers = v);

            using (new EditorGUI.DisabledScope(t.Count == 0))
            {
                OuterAIManager mm = m;
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Auto Setup + Check", GUILayout.Height(30f)))
                    Defer(() => OuterAIManagerTools.RunSetup(mm.TickedMonsters(), mm.OptDurations, mm.OptNavMesh, mm.OptPerf, mm.OptPlayers));
                if (GUILayout.Button("Check Only", GUILayout.Height(30f)))
                    Defer(() => OuterAIManagerTools.RunCheck(mm.TickedMonsters()));
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("Create / Rebake NavMesh", GUILayout.Height(24f)))
                    Defer(() => OuterAIManagerTools.RunNavMesh(mm.TickedMonsters()));
            }

            int er = 0, wa = 0;
            foreach (OuterAI a in t)
            {
                OuterAISetupEngine e = OuterAIManagerTools.Peek(a);
                if (e != null) { er += e.Errors; wa += e.Warnings; }
            }
            GUILayout.Space(4f);
            EditorGUILayout.LabelField("All ticked monsters:  " + er + " errors,  " + wa + " warnings", EditorStyles.boldLabel);
        });

        foreach (OuterAI a in t)
        {
            OuterAISetupEngine e = OuterAIManagerTools.Peek(a);
            if (e == null || e.items.Count == 0) continue;
            GUILayout.Space(Cur.sectionGap);
            OuterAI ai = a;
            OuterAISetupEngine eng = e;
            DrawSection(a.name, () =>
            {
                bool open;
                if (!foldouts.TryGetValue(ai, out open)) open = t.Count == 1;
                open = EditorGUILayout.Foldout(open, eng.Errors + " errors, " + eng.Warnings + " warnings", true);
                foldouts[ai] = open;
                if (open) { eng.DrawLog(); eng.DrawItems(true); }
            });
        }
    }

    // ------------------------------------------------------------ tab: bulk edit
    private void BulkGroup(string title, bool defaultOpen, params string[] props)
    {
        DrawSection(title, () =>
        {
            bool open;
            if (!secOpen.TryGetValue(title, out open)) open = defaultOpen;
            open = EditorGUILayout.Foldout(open, open ? "Hide settings" : "Show settings", true);
            secOpen[title] = open;
            if (!open) return;
            foreach (string p in props)
            {
                SerializedProperty sp = bulk.FindProperty(p);
                if (sp != null) EditorGUILayout.PropertyField(sp, true);
            }
        });
        GUILayout.Space(Cur.sectionGap);
    }

    private void DrawBulkTab(List<OuterAI> t)
    {
        if (t.Count == 0)
        {
            DrawSection("Bulk Edit", () => GUILayout.Label("Tick at least one monster on the Monsters tab first.", EditorStyles.wordWrappedMiniLabel));
            return;
        }

        int h = 17;
        foreach (OuterAI a in t) h = h * 31 + a.GetInstanceID();
        if (bulk == null || bulkHash != h)
        {
            bulk = new SerializedObject(t.Cast<UnityEngine.Object>().ToArray());
            bulkHash = h;
        }
        bulk.Update();

        DrawSection("Bulk Edit", () => GUILayout.Label("Change a number here and it changes on all " + t.Count + " ticked monsters at once. A dash (-) means they currently have different values.", EditorStyles.wordWrappedMiniLabel));
        GUILayout.Space(Cur.sectionGap);

        BulkGroup("Speed And Turning", false, "MonsterSpeedWander", "MonsterSpeedChase", "RotationSpeed", "UseRandomRoaming", "RandomRoamingRadius");
        BulkGroup("Sight And Hearing", false, "ConeVisionRange", "ChaseSphereRange", "ViewAngle", "Use360Vision", "ChaseMemoryDuration", "UseBlindAudioAI", "AudioDetectionRadius", "MinVoiceVolumeRadius", "VisionBlockLayerNames");
        BulkGroup("Jumpscare And Detection", false, "KillDistanceRadius", "JumpscareDuration", "PostRespawnGraceSeconds", "UseDetectionAnim", "DetectDuration", "DetectCooldown", "FreezeDuringDetect");
        BulkGroup("Footsteps", false, "UseFootsteps", "StepCheckDistance", "StepCooldown", "StepLoudness", "StepRayHeight");
        BulkGroup("Extras", false, "UseSearchBehavior", "SearchDuration", "SearchRadius", "SearchPoints", "UseChaseRamp", "ChaseRampBonus", "ChaseRampSeconds", "UseChaseFatigue", "MaxChaseSeconds", "RestSeconds", "UseKillRage", "RagePerKill", "MaxRage", "HeartbeatRange", "HeartbeatMaxVolume", "ShowDebugInfo");

        DrawSection("Anti Lag", () =>
        {
            bool open;
            if (!secOpen.TryGetValue("Anti Lag", out open)) open = true;
            open = EditorGUILayout.Foldout(open, open ? "Hide settings" : "Show settings", true);
            secOpen["Anti Lag"] = open;
            if (!open) return;
            SerializedProperty master = bulk.FindProperty("UseAntiLag");
            if (master != null) EditorGUILayout.PropertyField(master);
            GUILayout.Label("Presets", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            string[] presetNames = { "Max Performance", "Balanced", "Best Quality" };
            for (int i = 0; i < presetNames.Length; i++)
            {
                if (GUILayout.Button(presetNames[i], GUILayout.Height(24f)))
                {
                    foreach (OuterAI a in t) OuterAIManagerTools.AntiLagPreset(a, i);
                    bulk.Update();
                }
            }
            EditorGUILayout.EndHorizontal();
            foreach (string p in new[] { "FarChecksPerSecond", "NearChecksPerSecond", "ChaseChecksPerSecond", "FarDistance", "PlayerScanInterval", "MaxPlayersPerCheck", "MaxHeavyChecks", "GlobalThinkBudgetPerFrame", "FootstepChecksPerSecond", "SyncMoveThreshold", "CullAnimatorsWhenHidden", "SnapDistance" })
            {
                SerializedProperty sp = bulk.FindProperty(p);
                if (sp != null) EditorGUILayout.PropertyField(sp, true);
            }
        });

        bulk.ApplyModifiedProperties();
    }

    // ------------------------------------------------------------ tab: gizmos
    private static void Tog(string label, bool cur, System.Action<bool> set)
    {
        bool v = EditorGUILayout.ToggleLeft(label, cur);
        if (v != cur) set(v);
    }

    private static void Col(string label, Color cur, System.Action<Color> set)
    {
        Color v = EditorGUILayout.ColorField(label, cur);
        if (v != cur) set(v);
    }

    private void DrawGizmosTab()
    {
        DrawSection("Gizmos (shared by every monster)", () =>
        {
            GUILayout.Label("These settings are shared by EVERY monster, in every scene. Change them once here. They only change what you see in the Scene view.", EditorStyles.wordWrappedMiniLabel);
        });
        GUILayout.Space(Cur.sectionGap);

        DrawSection("Show Or Hide", () =>
        {
            Tog("Show gizmos (master switch)", OuterAIGizmoPrefs.ShowGizmos, v => OuterAIGizmoPrefs.ShowGizmos = v);
            Tog("Vision cone (what the monster can see)", OuterAIGizmoPrefs.ShowDetectionGizmos, v => OuterAIGizmoPrefs.ShowDetectionGizmos = v);
            Tog("Range circles (chase range, hearing range)", OuterAIGizmoPrefs.ShowMovementGizmos, v => OuterAIGizmoPrefs.ShowMovementGizmos = v);
            Tog("Path line (play mode)", OuterAIGizmoPrefs.ShowPathGizmos, v => OuterAIGizmoPrefs.ShowPathGizmos = v);
            Tog("Kill range circle", OuterAIGizmoPrefs.ShowKillRange, v => OuterAIGizmoPrefs.ShowKillRange = v);
            Tog("Text labels on the circles", OuterAIGizmoPrefs.ShowGizmoLabels, v => OuterAIGizmoPrefs.ShowGizmoLabels = v);
            Tog("Hierarchy colors + badges (manager, monsters, walk/kill/detect)", OuterAIHierarchy.Enabled, v => OuterAIHierarchy.Enabled = v);
        });
        GUILayout.Space(Cur.sectionGap);

        DrawSection("Look", () =>
        {
            float fill = EditorGUILayout.Slider("Fill opacity", OuterAIGizmoPrefs.GizmoFillOpacity, 0.05f, 0.8f);
            if (!Mathf.Approximately(fill, OuterAIGizmoPrefs.GizmoFillOpacity)) OuterAIGizmoPrefs.GizmoFillOpacity = fill;
            int cone = EditorGUILayout.IntSlider("Cone detail (higher = smoother)", OuterAIGizmoPrefs.GizmoConeDetail, 8, 120);
            if (cone != OuterAIGizmoPrefs.GizmoConeDetail) OuterAIGizmoPrefs.GizmoConeDetail = cone;
        });
        GUILayout.Space(Cur.sectionGap);

        DrawSection("Colors", () =>
        {
            Col("Chase range", OuterAIGizmoPrefs.ChaseSphereColor, v => OuterAIGizmoPrefs.ChaseSphereColor = v);
            Col("Vision cone (clear)", OuterAIGizmoPrefs.VisionConeClearColor, v => OuterAIGizmoPrefs.VisionConeClearColor = v);
            Col("Vision cone (blocked by a wall)", OuterAIGizmoPrefs.VisionConeBlockedColor, v => OuterAIGizmoPrefs.VisionConeBlockedColor = v);
            Col("Path line", OuterAIGizmoPrefs.PathColor, v => OuterAIGizmoPrefs.PathColor = v);
            Col("Hearing range", OuterAIGizmoPrefs.TargetLineColor, v => OuterAIGizmoPrefs.TargetLineColor = v);
            Col("Kill range", OuterAIGizmoPrefs.KillRangeColor, v => OuterAIGizmoPrefs.KillRangeColor = v);
            GUILayout.Space(4f);
            if (GUILayout.Button("Reset gizmo settings", GUILayout.Height(24f))) OuterAIGizmoPrefs.ResetAll();
        });
    }

    // ------------------------------------------------------------ tab: help
    private void DrawHelpTab()
    {
        DrawSection("Using The Manager", () => GUILayout.Label(OuterAIManagerTools.ManagerHelp, EditorStyles.wordWrappedLabel));
        GUILayout.Space(Cur.sectionGap);
        DrawSection("Setting Up A Monster", () => GUILayout.Label(OuterAIManagerTools.Help, EditorStyles.wordWrappedLabel));
    }
}

// ---------------------------------------------------------------------------------------------
// The TAB: top menu Outer AI > Manager. It shows the exact same manager as the GameObject script
// (it finds the Outer AI Manager object in your scene), so use whichever you like.
// ---------------------------------------------------------------------------------------------
public class OuterAIManagerWindow : EditorWindow
{
    private OuterAIManager target;
    private Editor inner;
    private Vector2 scroll;

    [MenuItem("Outer AI/Manager", false, 0)]
    public static void Open()
    {
        OuterAIManagerWindow w = GetWindow<OuterAIManagerWindow>("Outer AI Manager");
        w.minSize = new Vector2(400f, 540f);
        w.Show();
    }

    private void OnEnable() { Find(); }
    private void OnDisable() { if (inner != null) DestroyImmediate(inner); inner = null; }
    private void OnHierarchyChange() { if (target == null) Find(); Repaint(); }
    private void OnSelectionChange() { Repaint(); }

    private void Find()
    {
        if (target == null)
        {
            OuterAIManager[] all = Object.FindObjectsByType<OuterAIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            target = all.Length > 0 ? all[0] : null;
        }
        if (inner != null && (target == null || inner.target != target)) { DestroyImmediate(inner); inner = null; }
    }

    private void AddSelected()
    {
        if (target == null) return;
        Undo.RecordObject(target, "Add selected monsters");
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null || !go.scene.IsValid()) continue;
            OuterAI parent = go.GetComponentInParent<OuterAI>();
            if (parent != null) { target.AddMonster(parent); continue; }
            foreach (OuterAI c in go.GetComponentsInChildren<OuterAI>(true)) target.AddMonster(c);
        }
        EditorUtility.SetDirty(target);
    }

    private void OnGUI()
    {
        if (OuterAITos.Blocked) { GUILayout.Space(10f); OuterAITos.DrawBlocked(); return; }
        if (target == null) Find();
        if (target == null)
        {
            GUILayout.Space(10f);
            GUILayout.Label("There is no Outer AI Manager object in the open scene yet.", EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("Create Outer AI Manager object", GUILayout.Height(30f)))
                EditorApplication.delayCall += () => { OuterAIManagerTools.OpenOrCreate(null); Find(); Repaint(); };
            return;
        }

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Add Selected Monsters", EditorStyles.toolbarButton))
            EditorApplication.delayCall += () => { AddSelected(); Repaint(); };
        if (GUILayout.Button("Select Manager Object", EditorStyles.toolbarButton))
        {
            OuterAIManager t = target;
            EditorApplication.delayCall += () => { if (t != null) { Selection.activeGameObject = t.gameObject; EditorGUIUtility.PingObject(t); } };
        }
        EditorGUILayout.EndHorizontal();

        if (inner == null || inner.target != target)
        {
            if (inner != null) DestroyImmediate(inner);
            inner = Editor.CreateEditor(target);
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        inner.OnInspectorGUI();
        EditorGUILayout.EndScrollView();
    }
}

// ---------------------------------------------------------------------------------------------
// Hierarchy colors + badges. Uses the current Outer AI theme, so changing the theme recolors them.
// ---------------------------------------------------------------------------------------------
[InitializeOnLoad]
internal static class OuterAIHierarchy
{
    private const string Pref = "OuterAI.Hierarchy";
    private static GUIStyle style;

    internal static bool Enabled
    {
        get { return EditorPrefs.GetBool(Pref, true); }
        set { EditorPrefs.SetBool(Pref, value); EditorApplication.RepaintHierarchyWindow(); }
    }

    static OuterAIHierarchy() { EditorApplication.hierarchyWindowItemOnGUI += OnItem; }

    private static void OnItem(int id, Rect row)
    {
        if (Event.current.type != EventType.Repaint || !Enabled || OuterAITos.Blocked) return;
        GameObject go = EditorUtility.InstanceIDToObject(id) as GameObject;
        if (go == null) return;

        string label = null;
        int kind = 0; // 0 manager, 1 monster, 2 walk, 3 kill, 4 detect
        if (go.GetComponent<OuterAIManager>() != null) { label = "OUTER AI MANAGER"; kind = 0; }
        else if (go.GetComponent<OuterAI>() != null) { label = "OUTER AI"; kind = 1; }
        else if (go.transform.parent != null)
        {
            OuterAI ai = go.transform.parent.GetComponentInParent<OuterAI>();
            if (ai != null)
            {
                if (ai.WalkObject == go) { label = "WALK"; kind = 2; }
                else if (ai.KillObject == go) { label = "KILL"; kind = 3; }
                else if (ai.DetectObject == go) { label = "DETECT"; kind = 4; }
            }
        }
        if (label == null) return;

        OuterAIThemedEditor.ThemeData t = OuterAIThemedEditor.CurrentTheme;
        Color main = kind == 0 ? t.logo : t.tabOn;
        main.a = 1f;
        if (kind == 2) main = Color.Lerp(main, new Color(0.2f, 0.8f, 0.3f), 0.5f);
        else if (kind == 3) main = Color.Lerp(main, new Color(0.9f, 0.2f, 0.2f), 0.55f);
        else if (kind == 4) main = Color.Lerp(main, new Color(0.95f, 0.8f, 0.2f), 0.5f);

        Color tint = main; tint.a = 0.16f;
        EditorGUI.DrawRect(row, tint);
        EditorGUI.DrawRect(new Rect(row.x - 5f, row.y, 3f, row.height), main);
        Rect iconRect = new Rect(row.x - 1f, row.y + (row.height - 18f) * 0.5f, 18f, 18f);
        if (kind == 1)
        {
            // "OA" in the same style as the theme title logo, on the theme background
            Color bg = t.bgMode == 1 ? t.solid : Color.Lerp(t.top, t.bottom, 0.5f);
            bg.a = 1f;
            GUI.DrawTexture(iconRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, bg, 0f, 4f);
            GUI.DrawTexture(iconRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, Color.Lerp(bg, Color.black, 0.5f), 1f, 4f);
            OuterAIThemedEditor.DrawMiniLogo(iconRect, "OA", 11);
        }
        else GUI.DrawTexture(iconRect, IconFor(kind, main), ScaleMode.ScaleToFit, true);

        if (style == null) style = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 9 };
        Vector2 size = style.CalcSize(new GUIContent(label));
        Rect pill = new Rect(row.xMax - size.x - 28f, row.y + 2f, size.x + 10f, row.height - 4f);
        GUI.DrawTexture(pill, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, main, 0f, 4f);
        GUI.DrawTexture(pill, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, Color.Lerp(main, Color.black, 0.6f), 1f, 4f);
        float lum = 0.2126f * main.r + 0.7152f * main.g + 0.0722f * main.b;
        style.normal.textColor = lum > 0.55f ? new Color(0.07f, 0.07f, 0.10f) : Color.white;
        GUI.Label(pill, label, style);
    }

    // ---------------- procedural icons (no image files needed) ----------------
    private const int Px = 48;
    private static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

    private static Texture2D IconFor(int kind, Color main)
    {
        string key = kind + ColorUtility.ToHtmlStringRGB(main);
        Texture2D tex;
        if (icons.TryGetValue(key, out tex) && tex != null) return tex;
        tex = Build(kind, main);
        icons[key] = tex;
        return tex;
    }

    private static float Cov(float d) { return Mathf.Clamp01(0.5f - d * Px); }
    private static float Circle(Vector2 p, float cx, float cy, float r) { return (p - new Vector2(cx, cy)).magnitude - r; }
    private static float Ellipse(Vector2 p, float cx, float cy, float rx, float ry)
    {
        Vector2 q = new Vector2((p.x - cx) / rx, (p.y - cy) / ry);
        return (q.magnitude - 1f) * Mathf.Min(rx, ry);
    }
    private static float Box(Vector2 p, float cx, float cy, float hx, float hy, float rad)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - cx) - hx + rad, Mathf.Abs(p.y - cy) - hy + rad);
        return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - rad;
    }
    private static float Seg(Vector2 p, float ax, float ay, float bx, float by, float r)
    {
        Vector2 a = new Vector2(ax, ay), pa = p - a, ba = new Vector2(bx, by) - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }
    private static void Over(ref Color c, Color col, float cov)
    {
        if (cov <= 0f) return;
        float a = cov + c.a * (1f - cov);
        Vector3 rgb = (new Vector3(col.r, col.g, col.b) * cov + new Vector3(c.r, c.g, c.b) * c.a * (1f - cov)) / a;
        c = new Color(rgb.x, rgb.y, rgb.z, a);
    }

    private static Texture2D Build(int kind, Color main)
    {
        Color dark = Color.Lerp(main, Color.black, 0.65f);
        float lum = 0.2126f * main.r + 0.7152f * main.g + 0.0722f * main.b;
        Color fg = lum > 0.6f ? new Color(0.07f, 0.07f, 0.10f) : Color.white;
        Texture2D tex = new Texture2D(Px, Px, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color[] px = new Color[Px * Px];
        for (int y = 0; y < Px; y++)
        for (int x = 0; x < Px; x++)
        {
            Vector2 p = new Vector2((x + 0.5f) / Px, (y + 0.5f) / Px);
            Color c = new Color(0f, 0f, 0f, 0f);
            Over(ref c, dark, Cov(Box(p, 0.5f, 0.5f, 0.5f, 0.5f, 0.2f)));
            Over(ref c, main, Cov(Box(p, 0.5f, 0.5f, 0.45f, 0.45f, 0.16f)));
            if (kind == 0) // manager: sliders
            {
                for (int i = 0; i < 3; i++)
                {
                    float yy = 0.3f + i * 0.2f, kx = i == 0 ? 0.64f : (i == 1 ? 0.36f : 0.58f);
                    Over(ref c, fg, Cov(Seg(p, 0.2f, yy, 0.8f, yy, 0.035f)));
                    Over(ref c, dark, Cov(Circle(p, kx, yy, 0.115f)));
                    Over(ref c, fg, Cov(Circle(p, kx, yy, 0.085f)));
                }
            }
            else if (kind == 1) // monster: eye
            {
                Over(ref c, fg, Cov(Ellipse(p, 0.5f, 0.5f, 0.33f, 0.2f)));
                Over(ref c, dark, Cov(Circle(p, 0.5f, 0.5f, 0.125f)));
                Over(ref c, fg, Cov(Circle(p, 0.54f, 0.54f, 0.035f)));
            }
            else if (kind == 2) // walk: two footprints
            {
                Over(ref c, fg, Cov(Ellipse(p, 0.36f, 0.38f, 0.1f, 0.145f)));
                Over(ref c, fg, Cov(Circle(p, 0.36f, 0.19f, 0.06f)));
                Over(ref c, fg, Cov(Ellipse(p, 0.64f, 0.62f, 0.1f, 0.145f)));
                Over(ref c, fg, Cov(Circle(p, 0.64f, 0.43f, 0.06f)));
            }
            else if (kind == 3) // kill: skull
            {
                Over(ref c, fg, Cov(Circle(p, 0.5f, 0.58f, 0.25f)));
                Over(ref c, fg, Cov(Box(p, 0.5f, 0.3f, 0.14f, 0.1f, 0.04f)));
                Over(ref c, main, Cov(Circle(p, 0.4f, 0.58f, 0.075f)));
                Over(ref c, main, Cov(Circle(p, 0.6f, 0.58f, 0.075f)));
                Over(ref c, main, Cov(Seg(p, 0.45f, 0.3f, 0.45f, 0.24f, 0.012f)));
                Over(ref c, main, Cov(Seg(p, 0.55f, 0.3f, 0.55f, 0.24f, 0.012f)));
            }
            else // detect: exclamation mark
            {
                Over(ref c, fg, Cov(Seg(p, 0.5f, 0.45f, 0.5f, 0.76f, 0.065f)));
                Over(ref c, fg, Cov(Circle(p, 0.5f, 0.26f, 0.07f)));
            }
            px[y * Px + x] = c;
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        return tex;
    }
}

internal class OuterAISetupEngine
{
internal enum Sev { Ok, Info, Warn, Error }
internal class Item { public Sev sev; public string msg; public Object target; }
internal OuterAI ai;
internal bool matchDurations = true;
internal bool createNavMesh = true;
internal bool maxPerformance = true;
internal bool setupPlayers = true;
internal readonly List<Item> items = new List<Item>();
internal readonly List<string> log = new List<string>();
internal int Errors { get { return items.Count(i => i.sev == Sev.Error); } }
internal int Warnings { get { return items.Count(i => i.sev == Sev.Warn); } }
internal static GUIStyle helpStyle, itemStyle;
// true while the Outer AI Manager inspector draws results: clicking a message pings the object instead of selecting it (so the manager stays open).
internal static bool pingOnly;
internal static void EnsureStyles()
{
if (helpStyle == null)
helpStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 13, wordWrap = true, padding = new RectOffset(8, 8, 4, 4) };
if (itemStyle == null)
itemStyle = new GUIStyle(EditorStyles.helpBox) { fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 8, 8) };
}
private static Texture SevIcon(Sev s)
{
string n = s == Sev.Error ? "console.erroricon.sml" : s == Sev.Warn ? "console.warnicon.sml" : s == Sev.Info ? "console.infoicon.sml" : null;
return n == null ? null : EditorGUIUtility.IconContent(n).image;
}
internal void DrawLog()
{
EnsureStyles();
foreach (string l in log) GUILayout.Label("  + " + l, helpStyle);
}
internal void DrawItems(bool full)
{
EnsureStyles();
if (!full && log.Count > 0)
foreach (string l in log) GUILayout.Label("  + " + l, helpStyle);
int e = items.Count(i => i.sev == Sev.Error), w = items.Count(i => i.sev == Sev.Warn);
EditorGUILayout.LabelField("Errors: " + e + "   Warnings: " + w, EditorStyles.boldLabel);
foreach (Item it in items)
{
if (!full && it.sev < Sev.Warn) continue;
GUILayout.Label(new GUIContent((it.sev == Sev.Ok ? "OK: " : "") + it.msg, SevIcon(it.sev)), itemStyle);
Rect r = GUILayoutUtility.GetLastRect();
if (it.target != null && Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
{ if (!pingOnly) Selection.activeObject = it.target; EditorGUIUtility.PingObject(it.target); }
}
}
internal static T[] FindAll<T>() where T : Object
{
return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
}
private void Add(Sev s, string m, Object t = null) { items.Add(new Item { sev = s, msg = m, target = t }); }
private void Did(string m) { log.Add(m); }
private static bool Related(Transform a, Transform b) { return a.IsChildOf(b) || b.IsChildOf(a); }
private static int Depth(Transform t, Transform root)
{
int d = 0;
while (t != null && t != root) { t = t.parent; d++; }
return d;
}
private static Transform Pick(Transform root, string[] keys, List<Transform> blocked)
{
Transform best = null;
int bestDepth = int.MaxValue;
foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
{
if (t == root) continue;
string n = t.name.ToLowerInvariant();
if (!keys.Any(k => n.Contains(k))) continue;
if (blocked.Any(b => b != null && Related(t, b))) continue;
int d = Depth(t, root);
if (d < bestDepth) { best = t; bestDepth = d; }
}
return best;
}
private static Animator AnimOf(GameObject go) { return go == null ? null : go.GetComponentInChildren<Animator>(true); }
private static AnimatorController BaseController(Animator a)
{
if (a == null || a.runtimeAnimatorController == null) return null;
RuntimeAnimatorController rc = a.runtimeAnimatorController;
AnimatorOverrideController oc = rc as AnimatorOverrideController;
return (oc != null ? oc.runtimeAnimatorController : rc) as AnimatorController;
}
private static AnimationClip DefaultClip(Animator a)
{
if (a == null || a.runtimeAnimatorController == null) return null;
RuntimeAnimatorController rc = a.runtimeAnimatorController;
AnimatorOverrideController oc = rc as AnimatorOverrideController;
AnimatorController ac = BaseController(a);
if (ac != null && ac.layers.Length > 0)
{
AnimatorState st = ac.layers[0].stateMachine.defaultState;
AnimationClip c = st != null ? st.motion as AnimationClip : null;
if (c != null)
{
if (oc != null) { AnimationClip o = oc[c]; if (o != null) c = o; }
return c;
}
}
AnimationClip[] all = rc.animationClips;
return all.Length > 0 ? all[0] : null;
}
private static bool Loops(AnimationClip c) { return c != null && AnimationUtility.GetAnimationClipSettings(c).loopTime; }
private static bool IsStandaloneAnim(AnimationClip c)
{
return c != null && AssetDatabase.GetAssetPath(c).EndsWith(".anim");
}
private static void SetLoop(AnimationClip c, bool loop)
{
AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(c);
s.loopTime = loop;
AnimationUtility.SetAnimationClipSettings(c, s);
EditorUtility.SetDirty(c);
}
private static float Snap(float v) { return Mathf.Max(0.1f, Mathf.Round(v * 20f) / 20f); }
internal void AutoSetup()
{
log.Clear();
if (ai == null) return;
Undo.RegisterFullObjectHierarchyUndo(ai.gameObject, "Outer AI Auto Setup");
Undo.RecordObject(ai, "Outer AI Auto Setup");
Transform root = ai.transform;
NavMeshAgent agentComp = ai.GetComponent<NavMeshAgent>();
if (agentComp == null) { agentComp = Undo.AddComponent<NavMeshAgent>(ai.gameObject); Did("Added NavMeshAgent"); }
ApplyAgentPreset(agentComp);
if (ai.agent != agentComp) { ai.agent = agentComp; Did("Agent field = NavMeshAgent"); }
if (createNavMesh && !HasNavMesh()) CreateNavMesh();
PhotonView pv = ai.GetComponentInParent<PhotonView>();
if (pv == null) { pv = Undo.AddComponent<PhotonView>(ai.gameObject); Did("Added PhotonView"); }
Undo.RecordObject(pv, "Outer AI Auto Setup");
if (pv.ObservedComponents == null) pv.ObservedComponents = new List<Component>();
if (!pv.ObservedComponents.Contains(ai)) { pv.ObservedComponents.Add(ai); Did("PhotonView now observes OuterAI"); }
if (pv.Synchronization == ViewSynchronization.Off) { pv.Synchronization = ViewSynchronization.UnreliableOnChange; Did("PhotonView sync set to Unreliable On Change"); }
PhotonTransformView ptv = ai.GetComponent<PhotonTransformView>();
if (ptv == null) { ptv = Undo.AddComponent<PhotonTransformView>(ai.gameObject); Did("Added PhotonTransformView"); }
Undo.RecordObject(ptv, "Outer AI Auto Setup");
ptv.m_SynchronizePosition = true;
ptv.m_SynchronizeRotation = true;
ptv.m_SynchronizeScale = false;
EditorUtility.SetDirty(ptv);
if (!pv.ObservedComponents.Contains(ptv)) { pv.ObservedComponents.Add(ptv); Did("PhotonView now observes PhotonTransformView"); }
EditorUtility.SetDirty(pv);
if (!string.IsNullOrEmpty(ai.tagString) && !InternalEditorUtility.tags.Contains(ai.tagString))
{ InternalEditorUtility.AddTag(ai.tagString); Did("Created missing tag '" + ai.tagString + "'"); }
List<Transform> taken = new List<Transform>();
if (ai.KillObject != null) taken.Add(ai.KillObject.transform);
if (ai.DetectObject != null) taken.Add(ai.DetectObject.transform);
if (ai.WalkObject != null) taken.Add(ai.WalkObject.transform);
if (ai.KillObject == null)
{
Transform t = Pick(root, new[] { "kill", "jumpscare", "scare", "death" }, taken);
if (t != null) { ai.KillObject = t.gameObject; taken.Add(t); Did("KillObject = " + t.name); }
}
if (ai.DetectObject == null)
{
Transform t = Pick(root, new[] { "detect", "spot", "notice", "alert", "scream" }, taken);
if (t != null) { ai.DetectObject = t.gameObject; taken.Add(t); Did("DetectObject = " + t.name); }
}
if (ai.WalkObject == null)
{
Transform t = Pick(root, new[] { "walk", "run", "chase", "idle", "model", "body", "mesh" }, taken);
if (t == null)
foreach (Animator a in root.GetComponentsInChildren<Animator>(true))
if (!taken.Any(b => Related(a.transform, b))) { t = a.transform; break; }
if (t != null) { ai.WalkObject = t.gameObject; taken.Add(t); Did("WalkObject = " + t.name); }
}
if (ai.DetectObject != null && !ai.UseDetectionAnim) { ai.UseDetectionAnim = true; Did("Enabled Use Detection Animation"); }
if (ai.KillObject != null && ai.KillObject.activeSelf) { ai.KillObject.SetActive(false); Did("Hid KillObject"); }
if (ai.DetectObject != null && ai.DetectObject.activeSelf) { ai.DetectObject.SetActive(false); Did("Hid DetectObject"); }
if (ai.WalkObject != null && !ai.WalkObject.activeSelf) { ai.WalkObject.SetActive(true); Did("Showed WalkObject"); }
if (ai.JumpscareCamera == null)
{
Camera c = ai.KillObject != null ? ai.KillObject.GetComponentInChildren<Camera>(true) : null;
if (c == null) c = root.GetComponentInChildren<Camera>(true);
if (c != null) { ai.JumpscareCamera = c; Did("JumpscareCamera = " + c.name); }
}
if (ai.JumpscareCamera != null && ai.JumpscareCamera.enabled)
{ Undo.RecordObject(ai.JumpscareCamera, "Outer AI Auto Setup"); ai.JumpscareCamera.enabled = false; Did("Disabled JumpscareCamera (code enables it)"); }
if (ai.PlayerMainCamera == null)
{
Camera c = Camera.main;
if (c == null || c.transform.IsChildOf(root))
c = FindAll<Camera>().FirstOrDefault(x => !x.transform.IsChildOf(root) && x != ai.JumpscareCamera && x.enabled);
if (c != null) { ai.PlayerMainCamera = c; Did("PlayerMainCamera = " + c.name); }
}
if (ai.RespawnPoint == null)
{
Transform rp = FindAll<Transform>().FirstOrDefault(t => !t.IsChildOf(root) && t.name.ToLowerInvariant().Contains("respawn"))
?? FindAll<Transform>().FirstOrDefault(t => !t.IsChildOf(root) && t.name.ToLowerInvariant().Contains("spawn"));
if (rp == null)
{
GameObject g = new GameObject("RespawnPoint (move me)");
Undo.RegisterCreatedObjectUndo(g, "Outer AI Auto Setup");
g.transform.position = root.position;
rp = g.transform;
Did("Created 'RespawnPoint (move me)' at the monster - MOVE IT to where caught players should wait");
}
else Did("RespawnPoint = " + rp.name);
ai.RespawnPoint = rp;
}
AudioSource[] srcs = root.GetComponentsInChildren<AudioSource>(true);
List<AudioSource> usedSrc = new List<AudioSource>();
AudioSource Find(string[] keys)
{
AudioSource r = srcs.FirstOrDefault(s => !usedSrc.Contains(s) && keys.Any(k => s.name.ToLowerInvariant().Contains(k)));
if (r != null) usedSrc.Add(r);
return r;
}
foreach (AudioSource s in new[] { ai.IdleSound, ai.ChaseSound, ai.DetectSound, ai.FirstPersonKillAudio, ai.NetworkedKillAudio })
if (s != null) usedSrc.Add(s);
if (ai.FirstPersonKillAudio == null) { ai.FirstPersonKillAudio = Find(new[] { "first", "fp", "local", "caught", "self" }); if (ai.FirstPersonKillAudio) Did("FirstPersonKillAudio = " + ai.FirstPersonKillAudio.name); }
if (ai.NetworkedKillAudio == null) { ai.NetworkedKillAudio = Find(new[] { "net", "world", "remote", "other", "3d" }); if (ai.NetworkedKillAudio) Did("NetworkedKillAudio = " + ai.NetworkedKillAudio.name); }
string[] killKeys = { "kill", "jumpscare", "scare" };
if (ai.FirstPersonKillAudio == null) { ai.FirstPersonKillAudio = Find(killKeys); if (ai.FirstPersonKillAudio) Did("FirstPersonKillAudio = " + ai.FirstPersonKillAudio.name); }
if (ai.NetworkedKillAudio == null) { ai.NetworkedKillAudio = Find(killKeys); if (ai.NetworkedKillAudio) Did("NetworkedKillAudio = " + ai.NetworkedKillAudio.name); }
if (ai.DetectSound == null) { ai.DetectSound = Find(new[] { "detect", "spot", "notice", "alert", "scream" }); if (ai.DetectSound) Did("DetectSound = " + ai.DetectSound.name); }
if (ai.IdleSound == null) { ai.IdleSound = Find(new[] { "idle", "ambient", "breath" }); if (ai.IdleSound) Did("IdleSound = " + ai.IdleSound.name); }
if (ai.ChaseSound == null) { ai.ChaseSound = Find(new[] { "chase", "music" }); if (ai.ChaseSound) Did("ChaseSound = " + ai.ChaseSound.name); }
foreach (AudioSource s in new[] { ai.FirstPersonKillAudio, ai.NetworkedKillAudio, ai.DetectSound })
if (s != null && s.playOnAwake) { Undo.RecordObject(s, "Outer AI Auto Setup"); s.playOnAwake = false; Did("Play On Awake off on " + s.name); }
List<string> good = (ai.VisionBlockLayerNames ?? new string[0]).Where(n => LayerMask.NameToLayer(n) != -1).ToList();
if (ai.VisionBlockLayerNames == null || good.Count != ai.VisionBlockLayerNames.Length)
{
if (good.Count == 0) good.Add("Default");
ai.VisionBlockLayerNames = good.ToArray();
Did("Removed vision layers that don't exist");
}
if (Mathf.Approximately(ai.MonsterSpeedWander, 5f)) { ai.MonsterSpeedWander = 3.5f; Did("Wander speed 5 -> 3.5 (matches the agent preset)"); }
if (ai.MonsterSpeedChase <= ai.MonsterSpeedWander) { ai.MonsterSpeedChase = ai.MonsterSpeedWander * 1.5f; Did("Chase speed raised to " + ai.MonsterSpeedChase); }
foreach (GameObject go in new[] { ai.WalkObject, ai.KillObject, ai.DetectObject })
{
if (go == null) continue;
foreach (Animator a in go.GetComponentsInChildren<Animator>(true))
if (a.applyRootMotion) { Undo.RecordObject(a, "Outer AI Auto Setup"); a.applyRootMotion = false; Did("Root Motion off on " + a.name); }
}
AnimationClip wc = DefaultClip(AnimOf(ai.WalkObject));
if (wc != null && !Loops(wc) && IsStandaloneAnim(wc)) { SetLoop(wc, true); Did("Walk clip '" + wc.name + "' set to Loop Time"); }
if (matchDurations)
{
AnimationClip kc = DefaultClip(AnimOf(ai.KillObject));
if (kc != null && kc.length > 0.1f && Mathf.Abs(ai.JumpscareDuration - kc.length) > 0.25f)
{ ai.JumpscareDuration = Snap(kc.length); Did("JumpscareDuration = " + ai.JumpscareDuration); }
AnimationClip dc = DefaultClip(AnimOf(ai.DetectObject));
if (ai.UseDetectionAnim && dc != null && dc.length > 0.1f && Mathf.Abs(ai.DetectDuration - dc.length) > 0.25f)
{ ai.DetectDuration = Snap(dc.length); Did("DetectDuration = " + ai.DetectDuration); }
}
AnimatorController wac = BaseController(AnimOf(ai.WalkObject));
if (wac != null && !ai.UseAnimParams)
{
string sp = null, rn = null;
foreach (AnimatorControllerParameter p in wac.parameters)
{
string n = p.name.ToLowerInvariant();
if (p.type == AnimatorControllerParameterType.Float && n.Contains("speed")) sp = sp ?? p.name;
if ((p.type == AnimatorControllerParameterType.Bool || p.type == AnimatorControllerParameterType.Float) &&
(n.Contains("run") || n.Contains("chase"))) rn = rn ?? p.name;
}
if (sp != null || rn != null)
{
ai.UseAnimParams = true; ai.AnimSpeedParam = sp ?? ""; ai.AnimRunParam = rn ?? "";
Did("Walk/Run animation on (Speed='" + ai.AnimSpeedParam + "', Run='" + ai.AnimRunParam + "')");
}
}
if (maxPerformance) ApplyMaxPerformance();
if (setupPlayers) SetupPlayerScripts();
EditorUtility.SetDirty(ai);
PrefabUtility.RecordPrefabInstancePropertyModifications(ai);
if (!Application.isPlaying && ai.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(ai.gameObject.scene);
if (log.Count == 0) Did("Nothing needed changing");
Check();
}
private void ApplyMaxPerformance()
{
Undo.RecordObject(ai, "Outer AI Auto Setup");
ai.UseAntiLag = true; ai.NearChecksPerSecond = 6f; ai.FarChecksPerSecond = 1.5f; ai.ChaseChecksPerSecond = 6f;
ai.MaxPlayersPerCheck = 2; ai.MaxHeavyChecks = 3; ai.GlobalThinkBudgetPerFrame = 2; ai.PlayerScanInterval = 1.5f;
ai.FootstepChecksPerSecond = 20; ai.SyncMoveThreshold = 0.05f; ai.CullAnimatorsWhenHidden = true; ai.SnapDistance = 5f;
EditorUtility.SetDirty(ai);
Did("Anti Lag set to Max Performance");
}
private static bool IsLocoComp(MonoBehaviour mb) { return LocalPlayerTeleport.IsLoco(mb); }
private void SetupPlayerScripts()
{
foreach (MonoBehaviour mb in FindAll<MonoBehaviour>())
{
if (mb == null || !mb.gameObject.scene.IsValid() || !IsLocoComp(mb)) continue;
SetupRig(mb.gameObject, false);
}
foreach (PhotonView pv in FindAll<PhotonView>())
{
if (pv == null || !pv.gameObject.scene.IsValid() || pv.GetComponent<OuterAI>() != null) continue;
if (LooksLikePlayer(pv.gameObject)) SetupHandler(pv.gameObject, false);
}
int touched = 0;
foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
{
string path = AssetDatabase.GUIDToAssetPath(guid);
if (!path.StartsWith("Assets/") || path.IndexOf("/Resources/", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
if (asset == null || asset.GetComponent<PhotonView>() == null || asset.GetComponent<OuterAI>() != null) continue;
bool needsHandler = asset.GetComponent<PlayerDeathHandler>() == null && LooksLikePlayer(asset);
bool needsRig = false;
foreach (MonoBehaviour mb in asset.GetComponentsInChildren<MonoBehaviour>(true))
if (IsLocoComp(mb) && mb.GetComponent<LocalPlayerTeleport>() == null) { needsRig = true; break; }
if (!needsHandler && !needsRig) continue;
GameObject contents = PrefabUtility.LoadPrefabContents(path);
try
{
if (needsHandler) SetupHandler(contents, true);
if (needsRig)
foreach (MonoBehaviour mb in contents.GetComponentsInChildren<MonoBehaviour>(true))
if (IsLocoComp(mb)) SetupRig(mb.gameObject, true);
PrefabUtility.SaveAsPrefabAsset(contents, path);
touched++;
}
finally { PrefabUtility.UnloadPrefabContents(contents); }
}
if (touched > 0) Did("Updated " + touched + " player prefab(s) in Resources");
if (FindAll<PlayerDeathHandler>().Length == 0 && touched == 0)
Did("No Photon player found to add PlayerDeathHandler to. Put it (plus a PhotonView) on your networked player prefab, or run Auto Setup again once the player is in the scene.");
}
private bool LooksLikePlayer(GameObject go)
{
if (go.GetComponent<PlayerDeathHandler>() != null) return true;
if (go.GetComponent<Photon.Voice.PUN.PhotonVoiceView>() != null) return true;
try { if (!string.IsNullOrEmpty(ai.tagString) && go.CompareTag(ai.tagString)) return true; }
catch (UnityException) { }
return false;
}
private void SetupRig(GameObject go, bool inPrefab)
{
LocalPlayerTeleport t = go.GetComponent<LocalPlayerTeleport>();
if (t == null)
{
t = inPrefab ? go.AddComponent<LocalPlayerTeleport>() : Undo.AddComponent<LocalPlayerTeleport>(go);
Did("Added LocalPlayerTeleport to '" + go.name + "'");
}
MonoBehaviour loco = LocalPlayerTeleport.FindLoco(go);
if (loco != null && t.LocomotionScript != loco)
{
if (!inPrefab) Undo.RecordObject(t, "Outer AI Auto Setup");
t.LocomotionScript = loco;
EditorUtility.SetDirty(t);
}
if (go.GetComponent<Rigidbody>() == null) Did("Warning: '" + go.name + "' has no Rigidbody (the Gorilla rig normally has one).");
}
private void SetupHandler(GameObject go, bool inPrefab)
{
PlayerDeathHandler h = go.GetComponent<PlayerDeathHandler>();
if (h == null)
{
h = inPrefab ? go.AddComponent<PlayerDeathHandler>() : Undo.AddComponent<PlayerDeathHandler>(go);
Did("Added PlayerDeathHandler to '" + go.name + "'");
}
if (h.PlayerModelObjects == null) h.PlayerModelObjects = new List<GameObject>();
h.PlayerModelObjects.RemoveAll(x => x == null);
if (h.PlayerModelObjects.Count == 0)
{
foreach (Transform c in go.transform)
{
if (c.GetComponentInChildren<Renderer>(true) == null) continue;
if (c.GetComponentInChildren<Camera>(true) != null) continue;
h.PlayerModelObjects.Add(c.gameObject);
}
if (h.PlayerModelObjects.Count == 0 && go.GetComponent<Renderer>() != null) h.PlayerModelObjects.Add(go);
if (h.PlayerModelObjects.Count > 0) Did("PlayerModelObjects filled with " + h.PlayerModelObjects.Count + " object(s) on '" + go.name + "' - check it hides your player body");
}
if (go.GetComponentInParent<PhotonView>() == null) { if (inPrefab) go.AddComponent<PhotonView>(); else Undo.AddComponent<PhotonView>(go); Did("Added PhotonView to '" + go.name + "'"); }
EditorUtility.SetDirty(h);
}
private static int HumanoidId()
{
int n = NavMesh.GetSettingsCount();
for (int i = 0; i < n; i++)
{
NavMeshBuildSettings s = NavMesh.GetSettingsByIndex(i);
if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == "Humanoid") return s.agentTypeID;
}
return NavMesh.GetSettingsByIndex(0).agentTypeID;
}
private static bool HasNavMesh() { return NavMesh.CalculateTriangulation().vertices.Length > 0; }
private static bool Near(float a, float b) { return Mathf.Abs(a - b) < 0.001f; }
private static bool AgentMatches(NavMeshAgent a)
{
return a.agentTypeID == HumanoidId() && Near(a.baseOffset, 0.81f) && Near(a.speed, 3.5f) &&
Near(a.angularSpeed, 120f) && Near(a.acceleration, 9999f) && Near(a.stoppingDistance, 0f) &&
!a.autoBraking && Near(a.radius, 0.68f) && Near(a.height, 2.34f) &&
a.obstacleAvoidanceType == ObstacleAvoidanceType.HighQualityObstacleAvoidance &&
a.avoidancePriority == 50 && a.autoTraverseOffMeshLink && a.autoRepath && a.areaMask == NavMesh.AllAreas;
}
private void ApplyAgentPreset(NavMeshAgent a)
{
if (a == null || AgentMatches(a)) return;
Undo.RecordObject(a, "Outer AI Auto Setup");
a.agentTypeID = HumanoidId();
a.baseOffset = 0.81f;
a.speed = 3.5f;
a.angularSpeed = 120f;
a.acceleration = 9999f;
a.stoppingDistance = 0f;
a.autoBraking = false;
a.radius = 0.68f;
a.height = 2.34f;
a.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
a.avoidancePriority = 50;
a.autoTraverseOffMeshLink = true;
a.autoRepath = true;
a.areaMask = NavMesh.AllAreas;
EditorUtility.SetDirty(a);
Did("NavMeshAgent set to your preset (Humanoid, offset 0.81, speed 3.5, accel 9999, radius 0.68, height 2.34, High Quality, priority 50)");
}
private static System.Type FindType(string full)
{
foreach (System.Reflection.Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
System.Type t = asm.GetType(full);
if (t != null) return t;
}
return null;
}
internal void CreateNavMesh()
{
List<GameObject> hidden = new List<GameObject>();
foreach (OuterAI o in FindAll<OuterAI>()) if (o.gameObject.activeSelf) hidden.Add(o.gameObject);
if (ai != null && ai.gameObject.activeSelf && !hidden.Contains(ai.gameObject)) hidden.Add(ai.gameObject);
try
{
foreach (GameObject g in GameObject.FindGameObjectsWithTag(ai != null ? ai.tagString : "Player"))
if (g.activeSelf && !hidden.Contains(g)) hidden.Add(g);
}
catch (UnityException) { }
foreach (GameObject g in hidden) g.SetActive(false);
try { CreateNavMeshCore(); }
finally { foreach (GameObject g in hidden) if (g != null) g.SetActive(true); }
}
private void CreateNavMeshCore()
{
System.Type surfType = FindType("Unity.AI.Navigation.NavMeshSurface");
if (surfType != null)
{
try
{
Component surf = Resources.FindObjectsOfTypeAll(surfType).Cast<Component>()
.FirstOrDefault(c => c != null && c.gameObject.scene.IsValid());
if (surf == null)
{
GameObject go = new GameObject("NavMesh Surface (Outer AI)");
Undo.RegisterCreatedObjectUndo(go, "Create NavMesh");
surf = Undo.AddComponent(go, surfType);
surfType.GetProperty("agentTypeID").SetValue(surf, HumanoidId(), null);
Did("Created a NavMesh Surface object (Humanoid)");
}
surfType.GetMethod("BuildNavMesh", System.Type.EmptyTypes).Invoke(surf, null);
UnityEngine.Object data = surfType.GetProperty("navMeshData").GetValue(surf, null) as UnityEngine.Object;
if (data != null && !AssetDatabase.Contains(data))
{
string scenePath = surf.gameObject.scene.path;
string dir = string.IsNullOrEmpty(scenePath) ? "Assets" : System.IO.Path.GetDirectoryName(scenePath).Replace('\\', '/');
string nm = string.IsNullOrEmpty(scenePath) ? "OuterAI" : System.IO.Path.GetFileNameWithoutExtension(scenePath);
string path = AssetDatabase.GenerateUniqueAssetPath(dir + "/" + nm + "-NavMesh.asset");
AssetDatabase.CreateAsset(data, path);
Did("Saved NavMesh data: " + path);
}
EditorUtility.SetDirty(surf);
if (surf.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(surf.gameObject.scene);
Did("Baked the NavMesh");
return;
}
catch (System.Exception e) { Did("NavMesh Surface bake failed: " + e.Message); }
}
System.Type nb = typeof(Editor).Assembly.GetType("UnityEditor.AI.NavMeshBuilder");
System.Reflection.MethodInfo m = nb != null
? nb.GetMethod("BuildNavMesh", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, System.Type.EmptyTypes, null)
: null;
if (m != null)
{
try { m.Invoke(null, null); Did("Baked the NavMesh (built-in baker). Save the scene to keep it."); return; }
catch (System.Exception e) { Did("Built-in NavMesh bake failed: " + e.Message); }
}
UnityEditor.PackageManager.Client.Add("com.unity.ai.navigation");
Did("AI Navigation package was missing - installing it now. When Unity finishes compiling, press 'Create / Rebake NavMesh' again.");
}
internal void Check()
{
items.Clear();
if (ai == null) { Add(Sev.Info, "Select an OuterAI object first."); return; }
Transform root = ai.transform;
NavMeshAgent ag = ai.GetComponent<NavMeshAgent>();
if (ag == null) Add(Sev.Error, "No NavMeshAgent on the monster root. OuterAI.Start() needs one.", ai);
else
{
if (ai.agent != ag) Add(Sev.Warn, "The Agent field is empty. Auto Setup fills it in.", ai);
if (!AgentMatches(ag))
Add(Sev.Warn, "NavMeshAgent settings differ from your preset (Humanoid, offset 0.81, speed 3.5, accel 9999, radius 0.68, height 2.34...). Auto Setup fixes this.", ag);
if (!HasNavMesh())
Add(Sev.Error, "No NavMesh exists in this scene. Press 'Create / Rebake NavMesh' (or Auto Setup).", ai);
else
{
NavMeshHit h;
if (!NavMesh.SamplePosition(root.position, out h, 2f, NavMesh.AllAreas))
Add(Sev.Warn, "Monster is not standing on the NavMesh (nothing within 2m). Move it onto the blue area.", ai);
else Add(Sev.Ok, "NavMeshAgent + NavMesh found.");
}
}
PhotonView pv = ai.GetComponentInParent<PhotonView>();
if (pv == null) Add(Sev.Error, "No PhotonView. This AI requires Photon.", ai);
else if (pv.ObservedComponents == null || !pv.ObservedComponents.Contains(ai))
Add(Sev.Error, "PhotonView does not observe OuterAI, so position won't sync. Press Auto Setup.", pv);
else Add(Sev.Ok, "PhotonView observes OuterAI.");
if (string.IsNullOrEmpty(ai.tagString) || !InternalEditorUtility.tags.Contains(ai.tagString))
Add(Sev.Error, "Tag '" + ai.tagString + "' is not defined in Tags & Layers.");
else
{
GameObject[] players = GameObject.FindGameObjectsWithTag(ai.tagString);
if (players.Length == 0)
Add(Sev.Info, "No objects tagged '" + ai.tagString + "' in this scene. Fine if players spawn from a prefab - make sure that prefab has the tag.");
PlayerDeathHandler[] handlers = FindAll<PlayerDeathHandler>();
if (handlers.Length == 0)
Add(Sev.Info, "No PlayerDeathHandler in this scene. Fine if it is on your Photon player prefab (needs PlayerDeathHandler + PhotonView).");
foreach (PlayerDeathHandler hd in handlers)
if (hd.GetComponentInParent<PhotonView>() == null)
Add(Sev.Error, "PlayerDeathHandler on '" + hd.name + "' has no PhotonView on it or its parents.", hd);
}
if (FindAll<LocalPlayerTeleport>().Length == 0)
Add(Sev.Info, "No LocalPlayerTeleport in scene (Auto Setup adds it to the Gorilla rig).");
if (ai.UseRandomRoaming)
{ if (ai.RandomRoamingRadius < 1f) Add(Sev.Warn, "Random roaming radius is tiny.", ai); }
else if (ai.points == null || ai.points.Length == 0) Add(Sev.Error, "No patrol points and Random Roaming is off. It will never move.", ai);
else if (ai.points.Any(p => p == null)) Add(Sev.Error, "A patrol point slot is empty.", ai);
else Add(Sev.Ok, "Patrol points: " + ai.points.Length);
if (ai.MonsterSpeedChase <= ai.MonsterSpeedWander)
Add(Sev.Warn, "Chase speed (" + ai.MonsterSpeedChase + ") is not faster than wander speed (" + ai.MonsterSpeedWander + ") - no visible walk/run difference.", ai);
else Add(Sev.Ok, "Walk speed " + ai.MonsterSpeedWander + " / run speed " + ai.MonsterSpeedChase);
if (ai.ConeVisionRange > ai.ChaseSphereRange) Add(Sev.Info, "Cone range is larger than the chase sphere; it will lose you inside the cone range edge.", ai);
if (ai.VisionBlockLayerNames != null)
foreach (string n in ai.VisionBlockLayerNames)
if (LayerMask.NameToLayer(n) == -1) Add(Sev.Error, "Vision block layer '" + n + "' doesn't exist.", ai);
if (ai.UseBlindAudioAI && FindAll<PhotonVoiceView>().Length == 0)
Add(Sev.Warn, "Blind Audio AI is on but no PhotonVoiceView exists in this scene (players need one to be heard).", ai);
CheckWalk();
CheckKill();
CheckDetect();
CheckAudio();
if (ai.UseFootsteps)
{
if (ai.LeftHand == null || ai.RightHand == null) Add(Sev.Warn, "Footsteps on but Left/Right Hand/Foot not assigned.", ai);
if (ai.LeftStepSource == null || ai.RightStepSource == null) Add(Sev.Warn, "Footsteps on but a step AudioSource is missing.", ai);
if ((ai.DefaultStepSounds == null || ai.DefaultStepSounds.Length == 0) && (ai.StepSounds == null || ai.StepSounds.Length == 0))
Add(Sev.Warn, "Footsteps on but no step sounds assigned.", ai);
}
if (items.All(i => i.sev == Sev.Ok || i.sev == Sev.Info)) Add(Sev.Ok, "No problems found.");
}
private void CheckWalk()
{
if (ai.WalkObject == null) { Add(Sev.Error, "WalkObject is empty.", ai); return; }
Animator a = AnimOf(ai.WalkObject);
if (a == null) { Add(Sev.Warn, "WalkObject has no Animator - the monster will glide with no animation.", ai.WalkObject); return; }
if (a.runtimeAnimatorController == null) { Add(Sev.Error, "Walk Animator has no Controller.", a); return; }
if (a.applyRootMotion) Add(Sev.Warn, "Walk Animator has Apply Root Motion on; it fights the NavMeshAgent.", a);
AnimationClip c = DefaultClip(a);
if (c == null) Add(Sev.Error, "Walk controller has no clips.", a);
else if (!Loops(c)) Add(Sev.Warn, "Walk default clip '" + c.name + "' does not loop - it freezes after one cycle." +
(IsStandaloneAnim(c) ? " (Auto Setup fixes this)" : " Tick Loop Time in the model's Animation import tab."), c);
else Add(Sev.Ok, "Walk animation loops: " + c.name);
AnimatorController ac = BaseController(a);
if (ac != null)
{
bool hasRunState = ac.layers.Any(l => l.stateMachine.states.Any(s => s.state.name.ToLowerInvariant().Contains("run") || s.state.name.ToLowerInvariant().Contains("chase")));
bool hasParam = ac.parameters.Any(p => { string n = p.name.ToLowerInvariant(); return n.Contains("speed") || n.Contains("run") || n.Contains("chase"); });
bool driver = ai.UseAnimParams;
if (hasRunState && !hasParam) Add(Sev.Warn, "Controller has a run/chase state but no Speed/Run parameter, so it can never switch to it.", ac);
else if (hasParam && !driver) Add(Sev.Warn, "Controller has Speed/Run parameters but nothing feeds them. Press Auto Setup to turn on Walk/Run animation.", ai);
else if (hasParam && driver) Add(Sev.Ok, "Walk/Run switching is driven by OuterAI.");
else if (ai.MonsterSpeedChase > ai.MonsterSpeedWander * 1.3f)
Add(Sev.Info, "Same animation plays walking and chasing. For a separate run: add a 'Run' (bool) parameter + run state to the Walk controller, then Auto Setup.", ac);
}
}
private void CheckKill()
{
if (ai.KillObject == null) { Add(Sev.Error, "KillObject (jumpscare model) is empty.", ai); return; }
if (ai.WalkObject != null && ai.KillObject == ai.WalkObject) Add(Sev.Error, "Kill and Walk are the same object.", ai);
else if (ai.WalkObject != null && Related(ai.KillObject.transform, ai.WalkObject.transform))
Add(Sev.Error, "Kill and Walk objects are nested. Hiding Walk would hide Kill. Make them siblings.", ai.KillObject);
if (ai.KillObject.activeSelf) Add(Sev.Warn, "KillObject is active in the scene; it should start disabled.", ai.KillObject);
Animator a = AnimOf(ai.KillObject);
if (a == null) Add(Sev.Warn, "KillObject has no Animator - the jumpscare is a still pose.", ai.KillObject);
else if (a.runtimeAnimatorController == null) Add(Sev.Error, "Kill Animator has no Controller.", a);
else
{
AnimationClip c = DefaultClip(a);
if (c == null) Add(Sev.Error, "Kill controller has no clips.", a);
else if (c.length < ai.JumpscareDuration - 0.25f && Loops(c) == false)
Add(Sev.Info, "Kill clip is " + c.length.ToString("0.00") + "s but jumpscare lasts " + ai.JumpscareDuration + "s - it holds the last frame for the rest.", c);
else if (c.length > ai.JumpscareDuration + 0.25f)
Add(Sev.Warn, "Kill clip is " + c.length.ToString("0.00") + "s but JumpscareDuration is " + ai.JumpscareDuration + "s - the animation gets cut off.", c);
else Add(Sev.Ok, "Kill animation fits the jumpscare duration.");
if (a.applyRootMotion) Add(Sev.Warn, "Kill Animator has Apply Root Motion on.", a);
}
if (ai.JumpscareCamera == null) Add(Sev.Error, "JumpscareCamera is empty (put a Camera inside the Kill object).", ai);
else if (ai.JumpscareCamera.enabled) Add(Sev.Warn, "JumpscareCamera is enabled; it should start disabled.", ai.JumpscareCamera);
if (ai.PlayerMainCamera == null) Add(Sev.Info, "PlayerMainCamera is empty. Fine for runtime-spawned players, but the kill sound then won't attach to the caught player's camera.", ai);
if (ai.RespawnPoint == null) Add(Sev.Error, "RespawnPoint is empty - caught players have nowhere to be teleported.", ai);
else if (ai.RespawnPoint.IsChildOf(ai.transform)) Add(Sev.Warn, "RespawnPoint is a child of the monster, so it moves with it.", ai.RespawnPoint);
if (ai.KillDistanceRadius < 0.5f) Add(Sev.Warn, "Kill distance is very small (" + ai.KillDistanceRadius + ").", ai);
if (ai.JumpscareDuration < 0.5f) Add(Sev.Warn, "Jumpscare duration is very short.", ai);
}
private void CheckDetect()
{
if (!ai.UseDetectionAnim)
{
if (ai.DetectObject != null) Add(Sev.Info, "DetectObject is set but Use Detection Animation is off.", ai);
return;
}
if (ai.DetectObject == null) { Add(Sev.Error, "Use Detection Animation is on but DetectObject is empty.", ai); return; }
if (ai.WalkObject != null && Related(ai.DetectObject.transform, ai.WalkObject.transform))
Add(Sev.Error, "Detect and Walk objects are nested/same. Make them siblings.", ai.DetectObject);
if (ai.DetectObject.activeSelf) Add(Sev.Warn, "DetectObject is active in the scene; it should start disabled.", ai.DetectObject);
Animator a = AnimOf(ai.DetectObject);
if (a == null || a.runtimeAnimatorController == null) Add(Sev.Warn, "Detect object has no Animator/Controller - nothing will animate.", ai.DetectObject);
else
{
AnimationClip c = DefaultClip(a);
if (c != null && c.length > ai.DetectDuration + 0.25f)
Add(Sev.Warn, "Detect clip is " + c.length.ToString("0.00") + "s but DetectDuration is " + ai.DetectDuration + "s - it gets cut off.", c);
else Add(Sev.Ok, "Detect animation fits.");
}
if (ai.DetectSound == null) Add(Sev.Info, "No DetectSound assigned.", ai);
else if (ai.DetectSound.playOnAwake) Add(Sev.Warn, "DetectSound has Play On Awake on.", ai.DetectSound);
if (ai.DetectCooldown < ai.DetectDuration) Add(Sev.Info, "Detect cooldown is shorter than the animation.", ai);
}
private void CheckAudio()
{
if (ai.FirstPersonKillAudio == null && ai.NetworkedKillAudio == null) Add(Sev.Info, "No jumpscare sounds assigned.", ai);
if (ai.FirstPersonKillAudio != null && ai.FirstPersonKillAudio == ai.NetworkedKillAudio)
Add(Sev.Warn, "First-person and networked kill audio are the same source; use two.", ai);
if (ai.FirstPersonKillAudio != null && ai.FirstPersonKillAudio.playOnAwake) Add(Sev.Warn, "FirstPersonKillAudio has Play On Awake on.", ai.FirstPersonKillAudio);
if (ai.NetworkedKillAudio != null && ai.NetworkedKillAudio.playOnAwake) Add(Sev.Warn, "NetworkedKillAudio has Play On Awake on.", ai.NetworkedKillAudio);
if (ai.IdleSound == null && ai.ChaseSound == null) Add(Sev.Info, "No idle/chase sounds assigned (optional).", ai);
}
}
#endif
