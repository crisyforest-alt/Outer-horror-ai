// THIS IS A FREE AI MADE BY OUTER, IF YOU HAD TO PAY FOR THIS, YOU HAVE BEEN SCAMMED.
// THIS WATERMARK IS NOT NEEDED, BUT IT WOULD BE HELPFUL IF YOU KEPT THE WATER MARK IN IF YOU ARE SHARING IT TO OTHERS
// IF YOU HAVE ANY OTHER ISSUES, JOIN THIS SERVER FOR HELP: https://discord.gg/heDBJGkJUA
// OUTER AI UPDATE LOG + ONE-CLICK UPDATER (editor only).
//
// UPDATE LOG: OuterAI.cs has a value called UpdateLogVersion. Every time scripts reload, this file compares it with the value
// this project saw last. If they are different (new script imported), the Update Log opens ONCE and the value is saved.
//
// ONE-CLICK UPDATER: on startup it reads version.json from the GitHub repo set below. If GitHub has a newer version, the
// Update Log shows an "Update Now" button. It downloads every script in version.json, backs up your old ones, and replaces them.
//
// TO RELEASE AN UPDATE: 1) change the text in "UPDATE LOG CONTENT" below  2) change UpdateLogVersion in OuterAI.cs
//   3) upload the new scripts to the GitHub repo  4) change "version" in version.json on GitHub to the same text.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Networking;

// ============================== UPDATE LOG CONTENT (edit this) ==============================
internal static class OuterAIUpdateData
{
    internal const string Title = "OUTER AI MANAGER UPDATE";
    internal const string Date = "10/28/2026";

    internal struct Block
    {
        public string Head, Body; // Body == null means this is a group header like ADDITIONS
        public Block(string head, string body) { Head = head; Body = body; }
    }

    internal static readonly Block[] Blocks =
    {
        new Block("ADDITIONS", null),

        new Block("ONE-CLICK UPDATES",
            "Outer AI now checks GitHub when Unity opens. When a new version is out, press Update Now on this screen and it downloads and replaces the scripts for you. Your old scripts are backed up first, and Restore Previous Version puts them back."),

        new Block("NEW OUTER AI MANAGER",
            "Put the OuterAIManager script on any GameObject (or use the new Outer AI menu at the top). Manage lots of monsters in one place: tick them, run Auto Setup, bulk edit speeds and ranges, copy settings, change gizmos and themes."),

        new Block("NEW OUTER AI MENU",
            "A new Outer AI menu in the top bar. Open the manager, set up or check every monster in the scene, rebake the NavMesh, and toggle Hierarchy colors."),

        new Block("HIERARCHY COLORS AND ICONS",
            "Monsters, the manager and the Walk, Kill and Detect objects now get colors, badges and icons in the Hierarchy that match your theme. Turn it off in the Outer AI menu."),

        new Block("TERMS OF SERVICE",
            "The first time you import Outer AI, and any time the Terms of Service change, a pop up asks you to agree to the TOS before the Update Log shows."),

        new Block("NEW UPDATE LOG",
            "This screen. It opens once whenever a new version of Outer AI is imported. You can open it any time from Outer AI > Update Log."),
    };
}

// ============================== the check that runs in the background ==============================
[InitializeOnLoad]
internal static class OuterAIUpdateCheck
{
    internal const string Key = "OuterAI.UpdateLogSeen";

    static OuterAIUpdateCheck()
    {
        EditorApplication.delayCall += Check;
    }

    // The value written inside the OuterAI script (found by name, so nothing breaks if an old OuterAI.cs is still there).
    internal static string ScriptVersion()
    {
        FieldInfo f = typeof(OuterAI).GetField("UpdateLogVersion", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (f == null) return null;
        object v = f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null);
        return v as string;
    }

    internal static void ShowLogIfNew()
    {
        string current = ScriptVersion();
        if (string.IsNullOrEmpty(current)) return;
        // Saved per project (not shared between projects), so a new project that imports Outer AI sees it too.
        string seen = EditorUserSettings.GetConfigValue(Key);
        if (seen != current)
        {
            EditorUserSettings.SetConfigValue(Key, current); // switch the value so it only shows once
            OuterAIUpdateLogWindow.Open();
        }
    }

    private static void Check()
    {
        if (Application.isBatchMode) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Check; return; }

        // Terms of Service first. The Update Log waits until the TOS is accepted.
        string tos = OuterAITos.LocalVersion();
        if (OuterAITos.NeedsAccept(tos)) OuterAITos.Require(tos);
        else ShowLogIfNew();

        OuterAIUpdater.Check(true); // quiet check of GitHub
    }
}

// ============================== the GitHub updater ==============================
[System.Serializable] internal class OuterAIManifestFile { public string name; public string sha256; }
[System.Serializable] internal class OuterAIManifest { public string version; public string tosVersion; public string notes; public OuterAIManifestFile[] files; }

internal static class OuterAIUpdater
{
    // ---- where the update files live (raw GitHub). Change these if you rename the repo. ----
    internal const string Owner = "crisyforest-alt";
    internal const string Repo = "Outer-horror-ai";
    internal const string Branch = "main";
    internal static string BaseUrl { get { return "https://raw.githubusercontent.com/" + Owner + "/" + Repo + "/" + Branch + "/"; } }

    internal enum State { Idle, Checking, UpToDate, Available, Downloading, Done, Error }
    internal static State state = State.Idle;
    internal static string message = "";
    internal static string remoteVersion = "";
    internal static string remoteNotes = "";
    internal static System.Action Changed;

    private const string PromptKey = "OuterAI.UpdatePrompted";
    private static OuterAIManifest manifest;
    private static UnityWebRequest req;
    private static bool silent;
    private static int fileIndex;
    private static List<byte[]> downloaded;

    private static void Notify() { if (Changed != null) Changed(); }
    private static void Fail(string msg) { state = State.Error; message = msg; Notify(); }
    private static string Root { get { return Directory.GetParent(Application.dataPath).FullName; } }
    private static string BackupDir { get { return Path.Combine(Path.Combine(Root, "Library"), "OuterAIBackup"); } }
    private static string MapPath { get { return Path.Combine(BackupDir, "map.txt"); } }
    private static bool ValidName(string n) { return !string.IsNullOrEmpty(n) && Regex.IsMatch(n, @"^[A-Za-z0-9_\-]+\.cs$"); }

    internal static bool IsNewer(string remote, string local)
    {
        if (string.IsNullOrEmpty(remote)) return false;
        if (string.IsNullOrEmpty(local)) return true;
        System.Version a, b;
        if (System.Version.TryParse(Pad(remote), out a) && System.Version.TryParse(Pad(local), out b)) return a > b;
        return remote != local;
    }
    private static string Pad(string v) { return v.Contains(".") ? v : v + ".0"; }

    private static string Sha256(byte[] data)
    {
        using (SHA256 sha = SHA256.Create())
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte x in sha.ComputeHash(data)) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }
    }

    // ------------------------------------------------------------ 1) check
    internal static void Check(bool quiet)
    {
        if (state == State.Checking || state == State.Downloading) return;
        silent = quiet;
        state = State.Checking;
        message = "Checking GitHub for updates...";
        Notify();
        req = UnityWebRequest.Get(BaseUrl + "version.json?t=" + System.DateTime.UtcNow.Ticks);
        req.timeout = 10;
        req.SetRequestHeader("Cache-Control", "no-cache");
        req.SendWebRequest();
        EditorApplication.update += PollCheck;
    }

    private static void PollCheck()
    {
        if (req == null) { EditorApplication.update -= PollCheck; return; }
        if (!req.isDone) return;
        EditorApplication.update -= PollCheck;
        UnityWebRequest r = req;
        req = null;
        try
        {
            if (r.result != UnityWebRequest.Result.Success) { Fail("Could not reach GitHub, so no update check was done. (" + r.error + ")"); return; }
            OuterAIManifest m = null;
            try { m = JsonUtility.FromJson<OuterAIManifest>(r.downloadHandler.text); } catch (System.Exception) { }
            if (m == null || string.IsNullOrEmpty(m.version) || m.files == null || m.files.Length == 0) { Fail("version.json on GitHub looks wrong, so the update was skipped."); return; }
            foreach (OuterAIManifestFile f in m.files)
                if (f == null || !ValidName(f.name)) { Fail("version.json has a file name that is not allowed, so the update was skipped."); return; }

            manifest = m;
            remoteVersion = m.version;
            remoteNotes = m.notes ?? "";
            // A new TOS on GitHub (version.json "tosVersion") pops up the agree window too, even without a script update.
            if (OuterAITos.NeedsAcceptRemote(m.tosVersion)) OuterAITos.Require(m.tosVersion);
            string local = OuterAIUpdateCheck.ScriptVersion();
            if (IsNewer(m.version, local))
            {
                state = State.Available;
                message = "";
                if (silent && EditorUserSettings.GetConfigValue(PromptKey) != m.version)
                {
                    EditorUserSettings.SetConfigValue(PromptKey, m.version); // only pop up once per new version
                    if (!EditorWindow.HasOpenInstances<OuterAIUpdateLogWindow>()) OuterAIUpdateLogWindow.Open();
                }
            }
            else { state = State.UpToDate; message = "You have the newest version (" + local + ")."; }
        }
        finally { r.Dispose(); Notify(); }
    }

    // ------------------------------------------------------------ 2) download everything, THEN replace
    internal static void StartUpdate()
    {
        if (state != State.Available || manifest == null) return;
        if (OuterAITos.Guard()) return;
        StringBuilder list = new StringBuilder();
        foreach (OuterAIManifestFile f in manifest.files) list.Append("  - ").Append(f.name).Append('\n');
        if (!EditorUtility.DisplayDialog("Update Outer AI to " + manifest.version + "?",
            "These scripts will be replaced:\n" + list +
            "\nYour current scripts are backed up first. If something goes wrong you can press 'Restore Previous Version' on the Update Log screen.",
            "Update", "Cancel")) return;

        state = State.Downloading;
        fileIndex = 0;
        downloaded = new List<byte[]>();
        StartFile();
    }

    private static void StartFile()
    {
        OuterAIManifestFile f = manifest.files[fileIndex];
        message = "Downloading " + f.name + " (" + (fileIndex + 1) + " of " + manifest.files.Length + ")...";
        Notify();
        req = UnityWebRequest.Get(BaseUrl + f.name + "?t=" + System.DateTime.UtcNow.Ticks);
        req.timeout = 20;
        req.SetRequestHeader("Cache-Control", "no-cache");
        req.SendWebRequest();
        EditorApplication.update += PollFile;
    }

    private static void PollFile()
    {
        if (req == null) { EditorApplication.update -= PollFile; return; }
        if (!req.isDone) return;
        EditorApplication.update -= PollFile;
        UnityWebRequest r = req;
        req = null;
        OuterAIManifestFile f = manifest.files[fileIndex];
        try
        {
            if (r.result != UnityWebRequest.Result.Success) { Fail("Download failed for " + f.name + ". Nothing was changed. (" + r.error + ")"); return; }
            byte[] data = r.downloadHandler.data;
            if (data == null || data.Length == 0 || data.Length > 5 * 1024 * 1024) { Fail(f.name + " has a bad size. Nothing was changed."); return; }
            if (!string.IsNullOrEmpty(f.sha256) && !string.Equals(Sha256(data), f.sha256.Trim(), System.StringComparison.OrdinalIgnoreCase))
            { Fail(f.name + " did not pass the file check (the hash does not match). Nothing was changed."); return; }
            downloaded.Add(data);
        }
        finally { r.Dispose(); }

        fileIndex++;
        if (fileIndex < manifest.files.Length) StartFile();
        else Apply();
    }

    // Where an existing script lives in the project: same file name, else the file that declares the same class, else Assets/OuterAI.
    internal static string FindLocal(string fileName, string text)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);
        foreach (string guid in AssetDatabase.FindAssets(stem + " t:MonoScript"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.StartsWith("Assets/") && Path.GetFileName(p) == fileName) return p;
        }
        if (!string.IsNullOrEmpty(text))
        {
            foreach (Match m in Regex.Matches(text, @"\bclass\s+([A-Za-z0-9_]+)"))
            {
                string cls = m.Groups[1].Value;
                foreach (MonoScript ms in MonoImporter.GetAllRuntimeMonoScripts())
                {
                    if (ms == null) continue;
                    System.Type t = ms.GetClass();
                    if (t == null || t.Name != cls) continue;
                    string p = AssetDatabase.GetAssetPath(ms);
                    if (p.StartsWith("Assets/")) return p;
                }
            }
        }
        return "Assets/OuterAI/" + fileName;
    }

    private static void Apply()
    {
        List<string> written = new List<string>();
        try
        {
            if (Directory.Exists(BackupDir)) Directory.Delete(BackupDir, true);
            Directory.CreateDirectory(BackupDir);

            List<string> map = new List<string>();
            map.Add("version=" + (OuterAIUpdateCheck.ScriptVersion() ?? ""));
            string[] targets = new string[manifest.files.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                string name = manifest.files[i].name;
                targets[i] = FindLocal(name, Encoding.UTF8.GetString(downloaded[i]));
                string full = Path.GetFullPath(Path.Combine(Root, targets[i]));
                if (File.Exists(full))
                {
                    string bn = i + "_" + name;
                    File.Copy(full, Path.Combine(BackupDir, bn), true);
                    map.Add(targets[i] + "|" + bn);
                }
                else map.Add(targets[i] + "|");
            }
            File.WriteAllLines(MapPath, map.ToArray());

            for (int i = 0; i < targets.Length; i++)
            {
                string full = Path.GetFullPath(Path.Combine(Root, targets[i]));
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, downloaded[i]);
                written.Add(targets[i]);
            }

            state = State.Done;
            message = "Updated to " + manifest.version + ". Unity is recompiling, the Update Log will open when it is done.";
            Notify();
            AssetDatabase.Refresh();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Outer AI update failed, putting your old scripts back: " + e.Message);
            Restore(true);
            Fail("The update failed, so your old scripts were put back. (" + e.Message + ")");
        }
    }

    // ------------------------------------------------------------ 3) restore the backup
    internal static bool HasBackup() { return File.Exists(MapPath); }

    internal static void Restore(bool quiet)
    {
        if (!HasBackup()) return;
        string[] lines = File.ReadAllLines(MapPath);
        string oldVersion = null;
        foreach (string line in lines)
        {
            if (line.StartsWith("version=")) { oldVersion = line.Substring(8); continue; }
            int bar = line.IndexOf('|');
            if (bar <= 0) continue;
            string asset = line.Substring(0, bar), bn = line.Substring(bar + 1);
            string full = Path.GetFullPath(Path.Combine(Root, asset));
            if (string.IsNullOrEmpty(bn)) { if (File.Exists(full)) AssetDatabase.DeleteAsset(asset); } // file did not exist before the update
            else
            {
                string src = Path.Combine(BackupDir, bn);
                if (File.Exists(src)) { Directory.CreateDirectory(Path.GetDirectoryName(full)); File.Copy(src, full, true); }
            }
        }
        if (!string.IsNullOrEmpty(oldVersion)) EditorUserSettings.SetConfigValue(OuterAIUpdateCheck.Key, oldVersion); // do not pop the log for the old version
        try { Directory.Delete(BackupDir, true); } catch (System.Exception) { }
        state = State.Idle;
        message = "Your previous version was restored.";
        Notify();
        AssetDatabase.Refresh();
        if (!quiet) Debug.Log("Outer AI: previous version restored.");
    }

    // ------------------------------------------------------------ menu
    [MenuItem("Outer AI/Check For Updates", false, 62)]
    private static void MenuCheck()
    {
        OuterAIUpdateLogWindow.Open();
        Check(false);
    }

    [MenuItem("Outer AI/Restore Previous Version", false, 63)]
    private static void MenuRestore()
    {
        if (EditorUtility.DisplayDialog("Restore previous version", "Put back the Outer AI scripts you had before the last update?", "Restore", "Cancel")) Restore(false);
    }

    [MenuItem("Outer AI/Restore Previous Version", true)]
    private static bool MenuRestoreCheck() { return HasBackup(); }

    // FOR THE AUTHOR: copies a ready version.json (with file checks) to your clipboard. Paste it into version.json on GitHub.
    [MenuItem("Outer AI/Developer/Copy version.json For GitHub", false, 100)]
    private static void MenuMakeManifest()
    {
        string[] names = { "OuterAI.cs", "OuterAIManager.cs", "OuterAIUpdateLog.cs", "LocalPlayerTeleport.cs", "PlayerDeathHandler.cs", "SkinSlotMap.cs" };
        string[] classes = { "OuterAI", "OuterAIManager", "OuterAIUpdateLogWindow", "LocalPlayerTeleport", "PlayerDeathHandler", "" };
        StringBuilder sb = new StringBuilder();
        sb.Append("{\n  \"version\": \"").Append(OuterAIUpdateCheck.ScriptVersion() ?? "1.0").Append("\",\n");
        sb.Append("  \"tosVersion\": \"").Append(OuterAITos.LocalVersion() ?? "1").Append("\",\n");
        sb.Append("  \"notes\": \"Write what is new here\",\n  \"files\": [\n");
        List<string> missing = new List<string>();
        for (int i = 0; i < names.Length; i++)
        {
            string path = FindLocal(names[i], string.IsNullOrEmpty(classes[i]) ? null : "class " + classes[i]);
            string full = Path.GetFullPath(Path.Combine(Root, path));
            string hash = "";
            if (File.Exists(full)) hash = Sha256(File.ReadAllBytes(full)); else missing.Add(names[i]);
            sb.Append("    { \"name\": \"").Append(names[i]).Append("\", \"sha256\": \"").Append(hash).Append("\" }").Append(i < names.Length - 1 ? "," : "").Append('\n');
        }
        sb.Append("  ]\n}\n");
        GUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("Outer AI: version.json copied to your clipboard. Paste it into version.json on GitHub. Upload the SAME script files you have now, with the names listed in it." +
                  (missing.Count > 0 ? " Could not find in the project: " + string.Join(", ", missing.ToArray()) : ""));
    }
}

// ============================== Terms of Service ==============================
// Pops up the first time Outer AI is imported into a project, and again whenever the TOS version changes
// (TosVersion in OuterAI.cs, or "tosVersion" in version.json on GitHub). Saved per project.
internal static class OuterAITos
{
    internal const string Url = "https://sites.google.com/view/outerhorrorai/home/tos";
    internal const string Key = "OuterAI.TosAccepted";
    internal static string Pending = "";

    internal static string LocalVersion()
    {
        FieldInfo f = typeof(OuterAI).GetField("TosVersion", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (f == null) return "1";
        object v = f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null);
        return v as string ?? "1";
    }

    // True while the TOS has not been agreed to. Everything in Outer AI stays locked while this is true.
    internal static bool Blocked { get { return !string.IsNullOrEmpty(Pending) || NeedsAccept(LocalVersion()); } }

    internal static void Prompt() { Require(string.IsNullOrEmpty(Pending) ? LocalVersion() : Pending); }

    // Use at the top of any action: returns true (and shows the agree window) if the action must stop.
    internal static bool Guard()
    {
        if (!Blocked) return false;
        Prompt();
        return true;
    }

    internal static void DrawBlocked()
    {
        EditorGUILayout.HelpBox("Outer AI is locked until you agree to the Terms of Service.", MessageType.Warning);
        if (GUILayout.Button("Open Terms Of Service Agreement", GUILayout.Height(28f))) Prompt();
    }

    internal static string Accepted() { return EditorUserSettings.GetConfigValue(Key) ?? ""; }

    internal static bool NeedsAccept(string required)
    {
        return !string.IsNullOrEmpty(required) && OuterAIUpdater.IsNewer(required, Accepted());
    }

    // GitHub check: ANY different value (higher or lower) from what this project accepted pops it up.
    internal static bool NeedsAcceptRemote(string remote)
    {
        return !string.IsNullOrEmpty(remote) && remote.Trim() != Accepted();
    }

    internal static void Require(string version)
    {
        Pending = version.Trim();
        OuterAITosWindow.Open();
    }

    internal static void Accept()
    {
        if (!string.IsNullOrEmpty(Pending)) EditorUserSettings.SetConfigValue(Key, Pending);
        Pending = "";
        InternalEditorUtility.RepaintAllViews();
        EditorApplication.RepaintHierarchyWindow();
        SceneView.RepaintAll();
    }

    [MenuItem("Outer AI/Terms of Service", false, 61)]
    private static void MenuOpen() { if (!Guard()) Application.OpenURL(Url); }
}

// Stops Play mode in the editor (for scenes that use Outer AI) until the TOS is agreed to.
[InitializeOnLoad]
internal static class OuterAIPlayGuard
{
    static OuterAIPlayGuard() { EditorApplication.playModeStateChanged += OnState; }

    private static void OnState(PlayModeStateChange s)
    {
        if (s != PlayModeStateChange.ExitingEditMode || !OuterAITos.Blocked) return;
        if (Object.FindObjectsByType<OuterAI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0) return;
        EditorApplication.isPlaying = false;
        Debug.LogWarning("Outer AI: Play mode was stopped. Agree to the Terms of Service first (Outer AI > Terms of Service).");
        EditorApplication.delayCall += OuterAITos.Prompt;
    }
}

public class OuterAITosWindow : EditorWindow
{
    private GUIStyle body, note;
    private bool updated;

    internal static void Open()
    {
        OuterAITosWindow w = GetWindow<OuterAITosWindow>(true, "Outer AI Terms of Service", true);
        w.minSize = w.maxSize = new Vector2(460f, 250f);
        Rect m = EditorGUIUtility.GetMainWindowPosition();
        w.position = new Rect(m.x + (m.width - 460f) * 0.5f, m.y + (m.height - 250f) * 0.5f, 460f, 250f);
        w.updated = !string.IsNullOrEmpty(OuterAITos.Accepted());
        w.ShowUtility();
        w.Focus();
    }

    private void OnGUI()
    {
        if (body == null)
        {
            body = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = true };
            body.normal.textColor = Color.white;
            note = new GUIStyle(body) { fontSize = 11, fontStyle = FontStyle.Italic };
            note.normal.textColor = new Color(0.86f, 0.86f, 0.88f);
        }
        OuterAIThemedEditor.ThemeData t = OuterAIThemedEditor.CurrentTheme;
        Rect full = new Rect(0f, 0f, position.width, position.height);
        Color accent = t.logo; accent.a = 1f;
        if (Event.current.type == EventType.Repaint)
        {
            OuterAIUpdateLogWindow.DrawBackground(full, t);
            Rect frame = new Rect(10f, 10f, full.width - 20f, full.height - 20f);
            GUI.DrawTexture(frame, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.58f), 0f, 16f);
            GUI.DrawTexture(frame, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, accent, 2f, 16f);
        }

        GUILayout.BeginArea(new Rect(26f, 22f, full.width - 52f, full.height - 44f));
        Rect tr = GUILayoutUtility.GetRect(0f, 40f, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint) OuterAIThemedEditor.DrawLogoText(tr, "TERMS OF SERVICE", 24);
        GUILayout.Space(8f);
        GUILayout.Label("By clicking agree, you agree to the TOS.", body);
        GUILayout.Label("Outer AI stays locked until you agree.", note);
        if (updated) GUILayout.Label("The Terms of Service were updated.", note);
        GUILayout.Space(6f);

        Color old = GUI.backgroundColor;
        GUI.backgroundColor = Color.Lerp(t.tabOn, Color.white, 0.15f);
        if (GUILayout.Button("Read the TOS", GUILayout.Height(26f))) Application.OpenURL(OuterAITos.Url);
        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Agree", GUILayout.Height(30f)))
        {
            OuterAITos.Accept();
            EditorApplication.delayCall += OuterAIUpdateCheck.ShowLogIfNew;
            Close();
        }
        if (GUILayout.Button("Decline", GUILayout.Height(30f))) Close();
        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = old;
        GUILayout.EndArea();
    }
}

// ============================== the screen ==============================
public class OuterAIUpdateLogWindow : EditorWindow
{
    private Vector2 scroll;
    private static Texture2D grad;
    private static Color gradTop, gradBottom;
    private GUIStyle head, group, body, date, bannerFill, bannerOutline;

    [MenuItem("Outer AI/Update Log", false, 60)]
    public static void Open()
    {
        if (OuterAITos.Guard()) return;
        OuterAIUpdateLogWindow w = GetWindow<OuterAIUpdateLogWindow>(true, "Outer AI Update Log", true);
        w.minSize = new Vector2(480f, 560f);
        Rect m = EditorGUIUtility.GetMainWindowPosition();
        float h = Mathf.Min(740f, Mathf.Max(560f, m.height - 80f));
        w.position = new Rect(m.x + (m.width - 600f) * 0.5f, m.y + (m.height - h) * 0.5f, 600f, h);
        w.Show();
        w.Focus();
    }

    private void OnEnable() { OuterAIUpdater.Changed += Repaint; }
    private void OnDisable() { OuterAIUpdater.Changed -= Repaint; }

    private void BuildStyles()
    {
        if (head != null) return;
        head = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = true };
        group = new GUIStyle(head) { fontSize = 17 };
        body = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 12, wordWrap = true };
        date = new GUIStyle(head) { fontSize = 14 };
        head.normal.textColor = group.normal.textColor = date.normal.textColor = Color.white;
        body.normal.textColor = new Color(0.86f, 0.86f, 0.88f);
        bannerFill = new GUIStyle(head) { fontSize = 30, alignment = TextAnchor.MiddleLeft };
        bannerFill.normal.textColor = Color.white;
        bannerOutline = new GUIStyle(bannerFill);
    }

    internal static void DrawBackground(Rect full, OuterAIThemedEditor.ThemeData t)
    {
        if (t.bgMode == 1)
        {
            Color c = t.solid; c.a = 1f;
            EditorGUI.DrawRect(full, c);
            return;
        }
        Color top = t.top, bottom = t.bottom; top.a = bottom.a = 1f;
        if (grad == null || gradTop != top || gradBottom != bottom)
        {
            if (grad == null) grad = new Texture2D(1, 64, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) grad.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            grad.Apply(false, false);
            gradTop = top; gradBottom = bottom;
        }
        GUI.DrawTexture(full, grad, ScaleMode.StretchToFill);
    }

    // The box under the date that talks about updates (checking, available, downloading, done, error).
    private void DrawUpdateBox(float width, Color accent)
    {
        OuterAIUpdater.State s = OuterAIUpdater.state;
        string text = null;
        bool button = false;
        if (s == OuterAIUpdater.State.Available)
        {
            text = "UPDATE AVAILABLE: version " + OuterAIUpdater.remoteVersion + (string.IsNullOrEmpty(OuterAIUpdater.remoteNotes) ? "" : "\n" + OuterAIUpdater.remoteNotes);
            button = true;
        }
        else if (s != OuterAIUpdater.State.Idle && !string.IsNullOrEmpty(OuterAIUpdater.message)) text = OuterAIUpdater.message;
        if (text == null) return;

        float th = body.CalcHeight(new GUIContent(text), width - 20f);
        float h = th + 14f + (button ? 38f : 0f);
        Rect r = GUILayoutUtility.GetRect(0f, h, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
        {
            Color fill = accent; fill.a = 0.18f;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, fill, 0f, 8f);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, accent, 1f, 8f);
        }
        GUIStyle ts = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter };
        ts.normal.textColor = Color.white;
        GUI.Label(new Rect(r.x + 10f, r.y + 7f, r.width - 20f, th), text, ts);
        if (button)
        {
            GUI.enabled = OuterAIUpdater.state == OuterAIUpdater.State.Available;
            if (GUI.Button(new Rect(r.x + 10f, r.yMax - 34f, r.width - 20f, 26f), "Update Now"))
                EditorApplication.delayCall += OuterAIUpdater.StartUpdate;
            GUI.enabled = true;
        }
        GUILayout.Space(6f);
    }

    private void OnGUI()
    {
        BuildStyles();
        OuterAIThemedEditor.ThemeData t = OuterAIThemedEditor.CurrentTheme;
        Rect full = new Rect(0f, 0f, position.width, position.height);
        Color accent = t.logo; accent.a = 1f;

        if (Event.current.type == EventType.Repaint)
        {
            DrawBackground(full, t);

            // dark panel with a theme-colored border (like the update log in the example)
            Rect frame = new Rect(12f, 40f, full.width - 24f, full.height - 54f);
            GUI.DrawTexture(frame, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.58f), 0f, 16f);
            GUI.DrawTexture(frame, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, accent, 2f, 16f);

            // slanted banner
            float bw = Mathf.Min(full.width * 0.62f, 340f);
            Handles.BeginGUI();
            Handles.color = new Color(0f, 0f, 0f, 0.65f);
            Handles.DrawAAConvexPolygon(new Vector3[] { new Vector3(0f, 14f), new Vector3(bw + 4f, 14f), new Vector3(bw - 24f, 68f), new Vector3(0f, 68f) });
            Handles.color = accent;
            Handles.DrawAAConvexPolygon(new Vector3[] { new Vector3(0f, 8f), new Vector3(bw, 8f), new Vector3(bw - 28f, 62f), new Vector3(0f, 62f) });
            Handles.EndGUI();
        }

        // banner text: white with a dark outline
        Rect br = new Rect(22f, 8f, 320f, 54f);
        bannerOutline.normal.textColor = Color.Lerp(accent, Color.black, 0.75f);
        for (int ox = -2; ox <= 2; ox++)
            for (int oy = -2; oy <= 2; oy++)
                if (ox != 0 || oy != 0) GUI.Label(new Rect(br.x + ox, br.y + oy, br.width, br.height), "UPDATE LOG", bannerOutline);
        GUI.Label(br, "UPDATE LOG", bannerFill);

        float areaW = full.width - 52f;
        GUILayout.BeginArea(new Rect(26f, 76f, areaW, full.height - 100f));

        // big title in the Outer AI theme logo style
        int size = Mathf.Clamp(Mathf.RoundToInt((full.width - 80f) / (OuterAIUpdateData.Title.Length * 0.74f)), 16, 36);
        Rect tr = GUILayoutUtility.GetRect(0f, size + 18f, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint) OuterAIThemedEditor.DrawLogoText(tr, OuterAIUpdateData.Title, size);
        GUILayout.Label(OuterAIUpdateData.Date, date);
        GUILayout.Space(6f);

        DrawUpdateBox(areaW, accent);

        scroll = EditorGUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
        foreach (OuterAIUpdateData.Block b in OuterAIUpdateData.Blocks)
        {
            if (b.Body == null)
            {
                GUILayout.Space(10f);
                GUILayout.Label(b.Head, group);
                Rect lr = GUILayoutUtility.GetLastRect();
                EditorGUI.DrawRect(new Rect(lr.center.x - 28f, lr.yMax, 56f, 2f), accent);
                GUILayout.Space(4f);
            }
            else
            {
                GUILayout.Space(14f);
                GUILayout.Label(b.Head, head);
                GUILayout.Label(b.Body, body);
            }
        }
        GUILayout.Space(10f);
        EditorGUILayout.EndScrollView();

        GUILayout.Space(6f);
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = Color.Lerp(t.tabOn, Color.white, 0.15f);

        EditorGUILayout.BeginHorizontal();
        bool busy = OuterAIUpdater.state == OuterAIUpdater.State.Checking || OuterAIUpdater.state == OuterAIUpdater.State.Downloading;
        GUI.enabled = !busy;
        if (GUILayout.Button("Check For Updates", GUILayout.Height(26f))) OuterAIUpdater.Check(false);
        GUI.enabled = !busy && OuterAIUpdater.HasBackup();
        if (GUILayout.Button("Restore Previous Version", GUILayout.Height(26f)))
            EditorApplication.delayCall += () =>
            {
                if (EditorUtility.DisplayDialog("Restore previous version", "Put back the Outer AI scripts you had before the last update?", "Restore", "Cancel")) OuterAIUpdater.Restore(false);
            };
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Close", GUILayout.Height(28f))) Close();
        if (GUILayout.Button("Open Outer AI Manager", GUILayout.Height(28f)))
        {
            EditorApplication.delayCall += () => OuterAIManagerTools.OpenOrCreate(null);
            Close();
        }
        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = old;
        GUILayout.EndArea();
    }
}
#endif
