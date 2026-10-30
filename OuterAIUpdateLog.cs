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

        new Block("ACCOUNTS",
            "After agreeing to the TOS you make an Outer AI account (a username, and an optional password). See and change your account any time from Outer AI > Account. Your password stays on your computer and is never sent anywhere."),

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
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (OuterAITos.Blocked) return; // still verifying or TOS not agreed: it shows later, when unlocked
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
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) { OuterAIUser.MarkVerified(); return; }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Check; return; }

        // Every copy gets its own code the first time it loads, before anything is agreed to. It stays on this computer until they agree.
        OuterAIUser.EnsureCode();

        // Give this copy of Outer AI its own copy ID by writing it into the scripts, like an update does. Unity then recompiles once.
        if (OuterAICopy.Ensure()) { EditorApplication.delayCall += Check; return; }

        // Order: startup check, then TOS, then account / sign in, then the Update Log.
        OuterAITos.Next();

        OuterAIUser.VerifyNow();    // ban check (locks Outer AI until it answers)
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
    // The scripts that make up an update (same list the Dev window publishes). Add a new script here to ship it.
    internal static readonly string[] FileNames = { "OuterAI.cs", "OuterAIManager.cs", "OuterAIUpdateLog.cs", "LocalPlayerTeleport.cs", "PlayerDeathHandler.cs", "SkinSlotMap.cs" };
    internal static readonly string[] FileClasses = { "OuterAI", "OuterAIManager", "OuterAIUpdateLogWindow", "LocalPlayerTeleport", "PlayerDeathHandler", "" };
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
        if (OuterAICopy.FromEmbedded().Length > 0)
        {
            EditorUtility.DisplayDialog("A copy ID is inside OuterAI.cs", "Open Outer AI > Developer > Dev Test Window and press 'Remove copy ID from this project' first. Otherwise every download from GitHub would carry your copy ID.", "OK");
            return;
        }
        string[] names = FileNames;
        string[] classes = FileClasses;
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

    // True while anything still needs doing before Outer AI may be used: startup check, TOS, account, sign in, ban.
    // (Cached for a quarter of a second because the Hierarchy asks for it on every row.)
    private static double evalAt = -1;
    private static bool evalVal;

    internal static bool Blocked
    {
        get
        {
            double t = EditorApplication.timeSinceStartup;
            if (evalAt < 0 || t < evalAt || t - evalAt > 0.25)
            {
                evalVal = OuterAIUser.Banned || OuterAIUser.Verifying || TosNeeded || OuterAIAccount.NeedsSetup || OuterAIAccount.NeedsLogin;
                evalAt = t;
            }
            return evalVal;
        }
    }

    internal static void Invalidate() { evalAt = -1; }

    internal static bool TosNeeded { get { return !string.IsNullOrEmpty(Pending) || NeedsAccept(LocalVersion()); } }

    // Opens whatever window is needed next: TOS, then account, then sign in.
    internal static void Prompt()
    {
        if (OuterAIUser.Banned) { OuterAIUser.ShowBannedDialog(); return; }
        if (TosNeeded) { Require(string.IsNullOrEmpty(Pending) ? LocalVersion() : Pending); return; }
        if (OuterAIUser.Verifying)
        {
            Debug.Log("Outer AI: still doing the quick startup check. Try again in a moment.");
            return;
        }
        if (OuterAIAccount.NeedsSetup || OuterAIAccount.NeedsLogin) OuterAIAccountWindow.Open();
    }

    // The one place that decides what happens next after something finishes.
    internal static void Next()
    {
        if (OuterAIUser.Banned || OuterAIUser.Verifying) return;
        Invalidate();
        if (Blocked) { Prompt(); return; }
        OuterAIUpdateCheck.ShowLogIfNew();
    }

    // Use at the top of any action: returns true (and shows the window that is needed) if the action must stop.
    internal static bool Guard()
    {
        if (!Blocked) return false;
        Prompt();
        return true;
    }

    internal static void DrawBlocked()
    {
        if (OuterAIUser.Banned)
        {
            EditorGUILayout.HelpBox("This copy of Outer AI has been disabled by the developer. Your code: " + OuterAIUser.ExistingCode(), MessageType.Error);
            return;
        }
        if (OuterAIUser.Verifying)
        {
            EditorGUILayout.HelpBox("Outer AI is doing a quick check when Unity opens. It unlocks in a moment.", MessageType.Info);
            if (!TosNeeded) return;
        }
        if (TosNeeded)
        {
            EditorGUILayout.HelpBox("Outer AI is locked until you agree to the Terms of Service.", MessageType.Warning);
            if (GUILayout.Button("Open Terms Of Service Agreement", GUILayout.Height(28f))) Prompt();
            return;
        }
        if (OuterAIAccount.NeedsSetup)
        {
            EditorGUILayout.HelpBox("Create your Outer AI account to unlock.", MessageType.Warning);
            if (GUILayout.Button("Create Account", GUILayout.Height(28f))) Prompt();
            return;
        }
        if (OuterAIAccount.NeedsLogin)
        {
            EditorGUILayout.HelpBox("Sign in to your Outer AI account to unlock.", MessageType.Warning);
            if (GUILayout.Button("Sign In", GUILayout.Height(28f))) Prompt();
        }
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
        if (OuterAIUser.Banned) { OuterAIUser.ShowBannedDialog(); return; }
        Pending = version.Trim();
        Invalidate();
        OuterAITosWindow.Open();
    }

    internal static void Accept()
    {
        if (!string.IsNullOrEmpty(Pending)) EditorUserSettings.SetConfigValue(Key, Pending);
        Pending = "";
        Invalidate();
        OuterAIUser.Announce();
        InternalEditorUtility.RepaintAllViews();
        EditorApplication.RepaintHierarchyWindow();
        SceneView.RepaintAll();
    }

    [MenuItem("Outer AI/Terms of Service", false, 61)]
    private static void MenuOpen() { if (!Guard()) Application.OpenURL(Url); }
}

// Gives each person (computer) a random 5 character code, and tells the Discord webhook when they agree to the TOS.
// The code is saved with EditorPrefs, so the same person keeps the same code in every project.
[System.Serializable] internal class OuterAIWebhookMsg { public string content; }
[System.Serializable] internal class OuterAIRelayMsg { public string code; public string text; }

internal static class OuterAIUser
{
    // BEST: deploy the relay (OuterAIRelay-worker.js) and paste its URL here. Then no Discord link is in this script at all.
    internal const string RelayUrl = "";
    // FALLBACK (only used while RelayUrl is empty): the webhook, scrambled so it is not readable in plain text.
    private static readonly byte[] HookBlob = { 7, 67, 85, 33, 11, 23, 78, 70, 59, 2, 64, 26, 0, 69, 69, 127, 27, 66, 12, 70, 62, 27, 90, 86, 24, 82, 67, 57, 23, 66, 10, 26, 112, 90, 6, 79, 90, 6, 18, 104, 76, 30, 88, 93, 110, 83, 4, 77, 95, 14, 19, 102, 87, 106, 19, 90, 14, 88, 82, 49, 86, 65, 69, 2, 12, 28, 53, 91, 53, 52, 113, 62, 95, 67, 78, 98, 42, 20, 83, 10, 12, 0, 124, 58, 89, 89, 114, 101, 11, 21, 0, 5, 49, 18, 70, 30, 12, 90, 106, 41, 29, 127, 20, 62, 19, 4, 30, 26, 33, 127, 18, 97, 34, 116, 2, 49, 51, 42, 81, 20, 4 };
    private static readonly byte[] HookKey = { 111, 55, 33, 81, 120, 45, 97, 105, 95, 107, 51, 121 };

    private static string Hook()
    {
        byte[] d = new byte[HookBlob.Length];
        for (int i = 0; i < d.Length; i++) d[i] = (byte)(HookBlob[i] ^ HookKey[i % HookKey.Length]);
        return Encoding.UTF8.GetString(d);
    }
    private const string CodeKey = "OuterAI.UserCode";
    private static UnityWebRequest req;

    // ---- ban list: a plain list of banned codes online. Add a code there and that person is locked out. ----
    // Paste the link of your published Google Sheet (CSV) here. Leave it empty to use banned.txt in the GitHub repo instead.
    internal const string BanListUrl = "";
    private const string BannedKey = "OuterAI.Banned";
    private static int bannedCache = -1;

    // ---- startup verification: Outer AI stays locked once per Unity launch until the GitHub ban check finishes ----
    // (SessionState survives script reloads, so it only locks again when Unity is closed and opened.)
    private const string VerifiedKey = "OuterAI.SessionVerified";
    private static int verifiedCache = -1;

    internal static bool Verifying
    {
        get
        {
            if (verifiedCache < 0) verifiedCache = SessionState.GetBool(VerifiedKey, false) ? 1 : 0;
            return verifiedCache == 0;
        }
    }

    // Called when the check ends. If GitHub could not be reached it unlocks anyway (a ban that was saved earlier still applies).
    internal static void MarkVerified()
    {
        if (!Verifying) return;
        verifiedCache = 1;
        SessionState.SetBool(VerifiedKey, true);
        InternalEditorUtility.RepaintAllViews();
        EditorApplication.RepaintHierarchyWindow();
        SceneView.RepaintAll();
        OuterAITos.Invalidate();
        EditorApplication.delayCall += OuterAITos.Next;
    }

    internal static void BeginVerify()
    {
        verifiedCache = 0;
        OuterAITos.Invalidate();
        SessionState.SetBool(VerifiedKey, false);
        InternalEditorUtility.RepaintAllViews();
        EditorApplication.RepaintHierarchyWindow();
        SceneView.RepaintAll();
    }

    internal static bool Banned
    {
        get
        {
            if (bannedCache < 0) bannedCache = EditorPrefs.GetBool(BannedKey, false) ? 1 : 0;
            return bannedCache == 1;
        }
    }

    internal static void SetBanned(bool v)
    {
        bannedCache = v ? 1 : 0;
        OuterAITos.Invalidate();
        EditorPrefs.SetBool(BannedKey, v);
        InternalEditorUtility.RepaintAllViews();
        EditorApplication.RepaintHierarchyWindow();
        SceneView.RepaintAll();
    }

    // The saved code, without making a new one.
    internal static string ExistingCode()
    {
        string c = EditorPrefs.GetString(CodeKey, "");
        return c.Length == 5 ? c : "";
    }

    internal static void ApplyBanList(string[] list)
    {
        string code = ExistingCode();
        List<string> copies = OuterAICopy.AllIds();
        bool inList = false;
        if (list != null)
            foreach (string x in list)
            {
                if (x == null) continue;
                string y = x.Trim();
                if (code.Length > 0 && string.Equals(y, code, System.StringComparison.OrdinalIgnoreCase)) inList = true;
                foreach (string c in copies) if (string.Equals(y, c, System.StringComparison.OrdinalIgnoreCase)) inList = true;
            }
        if (inList == Banned) return;
        SetBanned(inList);
        if (inList) EditorApplication.delayCall += ShowBannedDialog;
    }

    internal static void ShowBannedDialog()
    {
        EditorUtility.DisplayDialog("Outer AI disabled", "This copy of Outer AI has been disabled by the developer.\nYour code: " + ExistingCode() + "\nIf you think this is a mistake, contact the developer.", "OK");
    }

    private static UnityWebRequest banReq;

    internal static string BanUrl()
    {
        string u = string.IsNullOrEmpty(BanListUrl) ? OuterAIUpdater.BaseUrl + "banned.txt" : BanListUrl;
        return u + (u.Contains("?") ? "&" : "?") + "t=" + System.DateTime.UtcNow.Ticks;
    }

    // Reads every 5 character code out of the list (works with a text file, or a Google Sheet CSV).
    internal static string[] ParseCodes(string text)
    {
        List<string> r = new List<string>();
        foreach (string t in Regex.Split(text ?? "", @"[\s\uFEFF,;""']+"))
            if (t.Length >= 5 && t.Length <= 12 && Regex.IsMatch(t, "^[A-Za-z0-9]{5,12}$")) r.Add(t.ToUpperInvariant());
        return r.ToArray();
    }

    // Starts the ban check. Outer AI stays locked until it ends. Offline or error = unlock (a saved ban still applies).
    // ---- developer access: the codes in devs.txt (edited from the Dev window) get dev access ----
    private const string DevKey = "OuterAI.DevAccess";
    private static int devCache = -1;
    private static UnityWebRequest devReq;
    private static System.Action devDone;

    internal static bool DevAccess
    {
        get
        {
            if (devCache < 0) devCache = EditorPrefs.GetBool(DevKey, false) ? 1 : 0;
            return devCache == 1;
        }
    }

    // Reads devs.txt and keeps this computer's dev access in step with it. Offline or errors change nothing.
    internal static void CheckDevList(System.Action done)
    {
        if (devReq != null) { devDone += done; return; }
        devDone = done;
        devReq = UnityWebRequest.Get(OuterAIUpdater.BaseUrl + "devs.txt?t=" + System.DateTime.UtcNow.Ticks);
        devReq.timeout = 10;
        devReq.SetRequestHeader("Cache-Control", "no-cache");
        devReq.SendWebRequest();
        EditorApplication.update += PollDev;
    }

    private static void PollDev()
    {
        if (devReq == null) { EditorApplication.update -= PollDev; return; }
        if (!devReq.isDone) return;
        EditorApplication.update -= PollDev;
        UnityWebRequest r = devReq;
        devReq = null;
        System.Action cb = devDone;
        devDone = null;
        try
        {
            bool known = false, ok = false;
            if (r.result == UnityWebRequest.Result.Success)
            {
                known = true;
                string code = ExistingCode();
                if (code.Length > 0)
                    foreach (string c in ParseCodes(r.downloadHandler.text))
                        if (string.Equals(c, code, System.StringComparison.OrdinalIgnoreCase)) ok = true;
            }
            else if (r.responseCode == 404) known = true; // no devs.txt yet = nobody has dev access
            if (known && ok != DevAccess)
            {
                devCache = ok ? 1 : 0;
                EditorPrefs.SetBool(DevKey, ok);
                InternalEditorUtility.RepaintAllViews();
            }
        }
        finally { r.Dispose(); }
        if (cb != null) cb();
    }

    internal static void VerifyNow()
    {
        if (banReq != null) return;
        // Someone who agreed before codes existed has no code yet: make one now so they can be banned (and tell the webhook).
        if (!Announced && !string.IsNullOrEmpty(OuterAITos.Accepted())) Announce();
        ReportCopyId();
        banReq = UnityWebRequest.Get(BanUrl());
        banReq.timeout = 10;
        banReq.SetRequestHeader("Cache-Control", "no-cache");
        banReq.SendWebRequest();
        EditorApplication.update += PollBan;
        CheckDevList(null);
    }

    private static void PollBan()
    {
        if (banReq == null) { EditorApplication.update -= PollBan; return; }
        if (!banReq.isDone) return;
        EditorApplication.update -= PollBan;
        UnityWebRequest r = banReq;
        banReq = null;
        try
        {
            if (r.result == UnityWebRequest.Result.Success)
            {
                string[] codes = ParseCodes(r.downloadHandler.text);
                ApplyBanList(codes);
                Debug.Log("Outer AI: ban check finished. " + codes.Length + " banned code(s) online. This computer's code: " + ExistingCode() + " -> " + (Banned ? "BANNED" : "not banned") + ".");
            }
            else if (r.responseCode == 404)
            {
                ApplyBanList(new string[0]); // no ban list file yet = nobody banned
                Debug.Log("Outer AI: ban check finished (no banned.txt found = nobody banned). This computer's code: " + ExistingCode() + " -> " + (Banned ? "BANNED" : "not banned") + ".");
            }
            else Debug.LogWarning("Outer AI: ban check could not finish (error " + r.responseCode + ": " + r.error + "). A ban saved earlier stays in place. URL: " + BanUrl());
        }
        finally { r.Dispose(); MarkVerified(); }
    }

    internal static string Code
    {
        get
        {
            string c = EditorPrefs.GetString(CodeKey, "");
            if (c.Length != 5) { c = Generate(); EditorPrefs.SetString(CodeKey, c); }
            return c;
        }
    }

    private static string Generate() { return RandomId(5); }

    internal static string RandomId(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 32 characters, no 0/O/1/I
        byte[] b = new byte[length];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < b.Length; i++) sb.Append(chars[b[i] % chars.Length]);
        return sb.ToString();
    }

    // This copy's ID (see OuterAICopy). Empty if it has none yet.
    private static string copyIdCache;

    internal static string CopyId()
    {
        if (!string.IsNullOrEmpty(copyIdCache)) return copyIdCache;
        List<string> ids = OuterAICopy.AllIds();
        copyIdCache = ids.Count > 0 ? ids[0] : "";
        return copyIdCache;
    }

    private const string AnnouncedKey = "OuterAI.Announced";
    private const string CopySentKey = "OuterAI.CopyIdSent";
    private static readonly Queue<string> outbox = new Queue<string>();

    // Makes this copy's code if it does not have one yet. Runs when Outer AI first loads, before the TOS.
    // The code stays on this computer; nothing is sent anywhere until the person agrees to the TOS.
    internal static void EnsureCode() { string c = Code; }

    internal static bool Announced { get { return EditorPrefs.GetBool(AnnouncedKey, false); } }

    // Called when the person clicks Agree. Never blocks or fails the agreement if Discord can't be reached.
    internal static void Announce()
    {
        string code = Code;
        Debug.Log("Outer AI: you agreed to the TOS. Your code is " + code);
        EditorPrefs.SetBool(AnnouncedKey, true);
        string copy = CopyId();
        if (copy.Length > 0) EditorPrefs.SetString(CopySentKey, copy);
        Post(code + " Has successfully agreed to the TOS and has been generated a code" + (copy.Length > 0 ? " (copy " + copy + ")" : ""));
    }

    // People who agreed to the TOS before their copy had an ID never sent it. This sends it once per ID (only after they agreed).
    internal static void ReportCopyId()
    {
        string copy = CopyId();
        if (copy.Length == 0 || string.IsNullOrEmpty(OuterAITos.Accepted())) return;
        if (EditorPrefs.GetString(CopySentKey, "") == copy) return;
        EditorPrefs.SetString(CopySentKey, copy);
        Post(Code + " Copy ID: " + copy);
    }

    // Sends a line to the developer's Discord (webhook, or the relay if RelayUrl is set). Messages are sent one at a time.
    internal static void Post(string text)
    {
        outbox.Enqueue(text);
        if (req == null) SendNext();
    }

    private static void SendNext()
    {
        if (outbox.Count == 0) return;
        string text = outbox.Dequeue();
        bool relay = !string.IsNullOrEmpty(RelayUrl);
        string json = relay
            ? JsonUtility.ToJson(new OuterAIRelayMsg { code = Code, text = text })
            : JsonUtility.ToJson(new OuterAIWebhookMsg { content = text });
        req = new UnityWebRequest(relay ? RelayUrl : Hook(), "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 10;
        req.SendWebRequest();
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (req == null) { EditorApplication.update -= Poll; return; }
        if (!req.isDone) return;
        EditorApplication.update -= Poll;
        if (req.result != UnityWebRequest.Result.Success) Debug.LogWarning("Outer AI: could not send a message to the developer (" + req.error + "). It does not affect you.");
        req.Dispose();
        req = null;
        SendNext();
    }
}

// ============================== copy ID (automatic watermark) ==============================
// Every copy of Outer AI gives itself an ID the first time it loads, and keeps it in three places:
//   1) a small script with a random name in a random folder under Assets/Plugins,
//   2) a line inside OuterAI.cs itself,
//   3) this computer's Unity settings.
// If one is deleted (or an update replaces OuterAI.cs) the next startup rebuilds it from the others, with the same ID.
// If scripts are shared with the ID inside, every copy carries it: ban that ID in banned.txt and they all lock.
// The ID is sent to the developer when the person agrees to the TOS (and the agree window says so).
internal static class OuterAICopy
{
    private const string TriedKey = "OuterAI.CopyIdTried";
    private const string ForceKey = "OuterAI.CopyIdForce";
    private const string LastKey = "OuterAI.CopyIdLast";
    private const string TypeName = "Zq7Kd2";           // class inside the random script
    private const string OldTypeName = "OuterAICopyId"; // the earlier build used this name
    private static readonly Regex Mark = new Regex("CopyMark\\s*=\\s*\"([A-Z0-9]{5,12})\"");

    // The developer's project (it has the Dev window, unlocked with a GitHub token that can write to the repo) never gets an ID, or the GitHub upload would carry it.
    internal static bool DevMachine()
    {
        if (!EditorPrefs.GetBool("OuterAI.DevVerified", false)) return false; // the Dev window must be unlocked with a repo-write GitHub token
        foreach (Assembly a in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try { if (a.GetType("OuterAIDevWindow") != null) return true; } catch { }
        }
        return false;
    }

    private static string FromType(string typeName)
    {
        foreach (Assembly a in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type t = null;
            try { t = a.GetType(typeName); } catch { }
            if (t == null) continue;
            FieldInfo f = t.GetField("Id", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) return "";
            object v = f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null);
            string id = ((v as string) ?? "").Trim().ToUpperInvariant();
            return Regex.IsMatch(id, "^[A-Z0-9]{5,12}$") ? id : "";
        }
        return "";
    }

    private static string AssetPathOf(string fileName)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);
        foreach (string guid in AssetDatabase.FindAssets(stem))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith("/" + fileName)) return path;
        }
        return null;
    }

    private static string Full(string assetPath) { return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath)); }

    internal static string FromEmbedded()
    {
        try
        {
            string ap = AssetPathOf("OuterAI.cs");
            if (ap == null) return "";
            Match m = Mark.Match(File.ReadAllText(Full(ap)));
            return m.Success ? m.Groups[1].Value : "";
        }
        catch (System.Exception) { return ""; }
    }

    // Every ID this copy carries, in priority order (the first one is its main ID).
    internal static List<string> AllIds()
    {
        List<string> r = new List<string>();
        string[] found = { FromType(TypeName), FromType(OldTypeName), FromEmbedded(), EditorPrefs.GetString(LastKey, "") };
        foreach (string x in found)
            if (Regex.IsMatch(x ?? "", "^[A-Z0-9]{5,12}$") && !r.Contains(x)) r.Add(x);
        return r;
    }

    // Makes whatever is missing. Returns true if it wrote a file (Unity is about to recompile).
    internal static bool Ensure()
    {
        bool force = SessionState.GetBool(ForceKey, false);
        if (!force && (DevMachine() || SessionState.GetBool(TriedKey, false))) return false;
        SessionState.SetBool(TriedKey, true); // once per Unity launch, so it can never loop
        try
        {
            List<string> ids = AllIds();
            string id = ids.Count > 0 ? ids[0] : OuterAIUser.RandomId(8);
            bool wrote = false;
            if (FromType(TypeName).Length == 0 && FromType(OldTypeName).Length == 0) wrote |= WriteScript(id);
            if (FromEmbedded().Length == 0) wrote |= Embed(id);
            if (!Regex.IsMatch(EditorPrefs.GetString(LastKey, ""), "^[A-Z0-9]{5,12}$")) EditorPrefs.SetString(LastKey, id);
            return wrote;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Outer AI: could not give this copy an ID (" + e.Message + ").");
            return false;
        }
    }

    private static string RandomName() { return "_" + OuterAIUser.RandomId(9).ToLowerInvariant(); }

    private static bool WriteScript(string id)
    {
        string dir = "Assets/Plugins/" + RandomName();
        string asset = dir + "/" + RandomName() + ".cs";
        Directory.CreateDirectory(Full(dir));
        File.WriteAllText(Full(asset), "internal static class " + TypeName + "{internal const string Id=\"" + id + "\";}\n");
        AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceUpdate);
        return true;
    }

    private static bool Embed(string id)
    {
        string ap = AssetPathOf("OuterAI.cs");
        if (ap == null) return false;
        string full = Full(ap);
        string text = File.ReadAllText(full);
        Match anchor = Regex.Match(text, "public const string TosVersion\\s*=\\s*\"[^\"]*\";");
        if (!anchor.Success) anchor = Regex.Match(text, "public const string UpdateLogVersion\\s*=\\s*\"[^\"]*\";");
        if (!anchor.Success) return false;
        string nl = text.Contains("\r\n") ? "\r\n" : "\n";
        text = text.Insert(anchor.Index + anchor.Length, nl + "public const string CopyMark = \"" + id + "\";");
        File.WriteAllText(full, text);
        AssetDatabase.ImportAsset(ap, ImportAssetOptions.ForceUpdate);
        return true;
    }

    // Dev window: add an ID to this project to try it out (remove it again before uploading to GitHub).
    internal static void Test()
    {
        SessionState.SetBool(ForceKey, true);
        SessionState.SetBool(TriedKey, false);
        Ensure();
        SessionState.SetBool(ForceKey, false);
    }

    // Dev window: take every copy of the ID out of this project (the script, the line in OuterAI.cs, the saved setting).
    internal static void RemoveAll()
    {
        string root = Application.dataPath;
        foreach (string f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            FileInfo fi = new FileInfo(f);
            if (fi.Length > 400) continue;
            string t = File.ReadAllText(f);
            if (t.Contains("class " + TypeName) || t.Contains("class " + OldTypeName))
            {
                string rel = "Assets" + f.Substring(root.Length).Replace('\\', '/');
                AssetDatabase.DeleteAsset(rel);
            }
        }
        string ap = AssetPathOf("OuterAI.cs");
        if (ap != null)
        {
            string full = Full(ap);
            string text = File.ReadAllText(full);
            string cleaned = Regex.Replace(text, "(\r?\n)?public const string CopyMark = \"[A-Z0-9]*\";", "");
            if (cleaned != text) { File.WriteAllText(full, cleaned); AssetDatabase.ImportAsset(ap, ImportAssetOptions.ForceUpdate); }
        }
        EditorPrefs.DeleteKey(LastKey);
        SessionState.SetBool(TriedKey, true);
    }
}

// ============================== accounts ==============================
// Local to this computer (EditorPrefs). The password is never saved as text and never sent anywhere (only a salted PBKDF2 hash is kept).
// The username and this copy's code ARE sent to the developer's Discord when the account is created, renamed or deleted.
internal static class OuterAIAccount
{
    private const string NameKey = "OuterAI.Acct.Name", SaltKey = "OuterAI.Acct.Salt", HashKey = "OuterAI.Acct.Hash",
                         CreatedKey = "OuterAI.Acct.Created", SignedInKey = "OuterAI.Acct.SignedIn";

    internal static string Name { get { return EditorPrefs.GetString(NameKey, ""); } }
    internal static bool HasAccount { get { return Name.Length > 0; } }
    internal static bool HasPassword { get { return EditorPrefs.GetString(HashKey, "").Length > 0; } }
    internal static string Created { get { return EditorPrefs.GetString(CreatedKey, ""); } }
    internal static bool SignedIn { get { return SessionState.GetBool(SignedInKey, false); } }
    internal static bool NeedsSetup { get { return !HasAccount; } }
    internal static bool NeedsLogin { get { return HasAccount && HasPassword && !SignedIn; } }

    internal static string CheckName(string n)
    {
        if (n == null) n = "";
        if (n.Length < 3 || n.Length > 20) return "Username must be 3 to 20 characters.";
        if (!Regex.IsMatch(n, "^[A-Za-z0-9_.-]+$")) return "Use only letters, numbers, _ . and - (no spaces).";
        return "";
    }

    internal static string CheckNewPassword(string p, string again)
    {
        if (string.IsNullOrEmpty(p)) return ""; // no password is allowed
        if (p.Length < 4) return "Password must be at least 4 characters.";
        if (p != again) return "The two passwords do not match.";
        return "";
    }

    private static string HashOf(string pw, string saltB64)
    {
        using (Rfc2898DeriveBytes d = new Rfc2898DeriveBytes(pw, System.Convert.FromBase64String(saltB64), 10000))
            return System.Convert.ToBase64String(d.GetBytes(32));
    }

    internal static bool PasswordOk(string pw)
    {
        if (!HasPassword) return true;
        string a = HashOf(pw ?? "", EditorPrefs.GetString(SaltKey, ""));
        string b = EditorPrefs.GetString(HashKey, "");
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    private static void StorePassword(string pw)
    {
        if (string.IsNullOrEmpty(pw)) { EditorPrefs.DeleteKey(SaltKey); EditorPrefs.DeleteKey(HashKey); return; }
        byte[] salt = new byte[16];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
        string s = System.Convert.ToBase64String(salt);
        EditorPrefs.SetString(SaltKey, s);
        EditorPrefs.SetString(HashKey, HashOf(pw, s));
    }

    private static void Changed()
    {
        OuterAITos.Invalidate();
        InternalEditorUtility.RepaintAllViews();
        EditorApplication.RepaintHierarchyWindow();
        SceneView.RepaintAll();
    }

    // Each returns "" when it worked, otherwise the message to show.
    internal static string Create(string name, string pw, string again)
    {
        string e = CheckName(name);
        if (e.Length == 0) e = CheckNewPassword(pw, again);
        if (e.Length > 0) return e;
        EditorPrefs.SetString(NameKey, name);
        EditorPrefs.SetString(CreatedKey, System.DateTime.UtcNow.ToString("yyyy-MM-dd"));
        StorePassword(pw);
        SessionState.SetBool(SignedInKey, true);
        OuterAIUser.Post("Account created: " + name + " (code " + OuterAIUser.Code + ")");
        Changed();
        return "";
    }

    internal static string SignIn(string pw)
    {
        if (!PasswordOk(pw)) return "Wrong password.";
        SessionState.SetBool(SignedInKey, true);
        Changed();
        return "";
    }

    internal static void SignOut() { SessionState.SetBool(SignedInKey, false); Changed(); }

    internal static string Rename(string newName)
    {
        string e = CheckName(newName);
        if (e.Length > 0) return e;
        if (newName == Name) return "That is already your username.";
        string old = Name;
        EditorPrefs.SetString(NameKey, newName);
        OuterAIUser.Post("Account renamed: " + old + " -> " + newName + " (code " + OuterAIUser.Code + ")");
        Changed();
        return "";
    }

    internal static string ChangePassword(string current, string pw, string again)
    {
        if (!PasswordOk(current)) return "Your current password is wrong.";
        string e = CheckNewPassword(pw, again);
        if (e.Length > 0) return e;
        StorePassword(pw);
        Changed();
        return "";
    }

    internal static string Delete(string pw)
    {
        if (!PasswordOk(pw)) return "Wrong password.";
        Wipe(true);
        return "";
    }

    internal static void Wipe(bool tellDeveloper)
    {
        string n = Name;
        if (tellDeveloper && n.Length > 0) OuterAIUser.Post("Account deleted: " + n + " (code " + OuterAIUser.Code + ")");
        EditorPrefs.DeleteKey(NameKey); EditorPrefs.DeleteKey(CreatedKey); EditorPrefs.DeleteKey(SaltKey); EditorPrefs.DeleteKey(HashKey);
        SessionState.SetBool(SignedInKey, false);
        Changed();
    }
}

// The account window. It is the pop up for new people (create account), for people with a password (sign in),
// and the place to see and change account info.
public class OuterAIAccountWindow : EditorWindow
{
    private string nameIn = "", pw1 = "", pw2 = "", cur = "", newName = "", error = "", ok = "";
    private Vector2 scroll;
    private GUIStyle lab, val, small, body;

    [MenuItem("Outer AI/Account", false, 60)]
    private static void MenuOpen() { if (!OuterAITos.Guard()) Open(); }

    internal static void Open()
    {
        OuterAIAccountWindow w = GetWindow<OuterAIAccountWindow>(true, "Outer AI Account", true);
        Rect m = EditorGUIUtility.GetMainWindowPosition();
        w.position = new Rect(m.x + (m.width - 460f) * 0.5f, m.y + (m.height - 430f) * 0.5f, 460f, 430f);
        w.newName = OuterAIAccount.Name;
        w.ShowUtility();
        w.Focus();
    }

    private static float Wanted()
    {
        if (!OuterAIAccount.HasAccount) return 440f;
        if (OuterAIAccount.NeedsLogin) return 300f;
        return 640f;
    }

    private void Styles()
    {
        if (body != null) return;
        body = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold, wordWrap = true };
        body.normal.textColor = Color.white;
        lab = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 11, wordWrap = true };
        lab.normal.textColor = Color.white;
        val = new GUIStyle(lab) { fontStyle = FontStyle.Normal };
        small = new GUIStyle(lab) { fontStyle = FontStyle.Italic, fontSize = 10 };
        small.normal.textColor = new Color(0.86f, 0.86f, 0.88f);
    }

    private void OnGUI()
    {
        Styles();
        float h = Wanted();
        if (maxSize.y != h || minSize.y != h) { minSize = new Vector2(460f, h); maxSize = new Vector2(460f, h); }

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

        bool setup = !OuterAIAccount.HasAccount;
        bool login = OuterAIAccount.NeedsLogin;
        string title = setup ? "CREATE YOUR ACCOUNT" : (login ? "SIGN IN" : "YOUR ACCOUNT");

        Event ev = Event.current;
        bool enter = ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter);

        GUILayout.BeginArea(new Rect(26f, 22f, full.width - 52f, full.height - 44f));
        Rect tr = GUILayoutUtility.GetRect(0f, 40f, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint) OuterAIThemedEditor.DrawLogoText(tr, title, 24);
        GUILayout.Space(6f);

        Color old = GUI.backgroundColor;
        GUI.backgroundColor = Color.Lerp(t.tabOn, Color.white, 0.15f);
        if (setup) DrawSetup(enter);
        else if (login) DrawLogin(enter);
        else DrawManage();
        GUI.backgroundColor = old;

        GUILayout.EndArea();
    }

    private void Msg()
    {
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        else if (!string.IsNullOrEmpty(ok)) EditorGUILayout.HelpBox(ok, MessageType.Info);
    }

    private void DrawSetup(bool enter)
    {
        GUILayout.Label("Pick a username for Outer AI.", body);
        GUILayout.Label("A password is optional. If you set one, you sign in each time Unity opens.", small);
        GUILayout.Space(6f);
        GUILayout.Label("Username", lab);
        nameIn = EditorGUILayout.TextField(nameIn);
        GUILayout.Label("Password (optional)", lab);
        pw1 = EditorGUILayout.PasswordField(pw1);
        GUILayout.Label("Confirm password", lab);
        pw2 = EditorGUILayout.PasswordField(pw2);
        GUILayout.Label("Your username and this copy's code are sent to the developer. Your password never leaves this computer.", small);
        Msg();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Create Account", GUILayout.Height(30f)) || enter)
        {
            ok = "";
            error = OuterAIAccount.Create(nameIn.Trim(), pw1, pw2);
            if (error.Length == 0)
            {
                newName = OuterAIAccount.Name;
                nameIn = pw1 = pw2 = cur = "";
                ok = "Account created. Outer AI is unlocked.";
                GUI.FocusControl(null);
                EditorApplication.delayCall += OuterAITos.Next;
            }
        }
    }

    private void DrawLogin(bool enter)
    {
        GUILayout.Label("Welcome back, " + OuterAIAccount.Name + ".", body);
        GUILayout.Space(8f);
        GUILayout.Label("Password", lab);
        cur = EditorGUILayout.PasswordField(cur);
        Msg();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Sign In", GUILayout.Height(30f)) || enter)
        {
            ok = "";
            error = OuterAIAccount.SignIn(cur);
            if (error.Length == 0)
            {
                cur = "";
                GUI.FocusControl(null);
                EditorApplication.delayCall += OuterAITos.Next;
            }
        }
        if (GUILayout.Button("Forgot password? Erase this account", EditorStyles.miniButton))
        {
            if (EditorUtility.DisplayDialog("Erase account", "This erases your account on this computer. You will have to create a new one.", "Erase", "Cancel"))
            {
                OuterAIAccount.Wipe(true);
                cur = "";
                error = ok = "";
                EditorApplication.delayCall += OuterAITos.Next;
            }
        }
    }

    private void Row(string a, string b)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(a, lab, GUILayout.Width(120f));
        GUILayout.Label(string.IsNullOrEmpty(b) ? "-" : b, val);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawManage()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        Row("Username", OuterAIAccount.Name);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Copy code", lab, GUILayout.Width(120f));
        GUILayout.Label(OuterAIUser.Code, val);
        if (GUILayout.Button("Copy", GUILayout.Width(50f))) EditorGUIUtility.systemCopyBuffer = OuterAIUser.Code;
        EditorGUILayout.EndHorizontal();
        if (OuterAIUser.CopyId().Length > 0) Row("Copy ID", OuterAIUser.CopyId());
        Row("Account created", OuterAIAccount.Created);
        Row("TOS agreed", OuterAITos.Accepted());
        Row("Password", OuterAIAccount.HasPassword ? "Set" : "Not set");
        GUILayout.Label("Your copy code identifies this Outer AI on this computer. Send it to the developer if you need help or want to appeal a ban.", small);

        GUILayout.Space(8f);
        GUILayout.Label("Change username", lab);
        EditorGUILayout.BeginHorizontal();
        newName = EditorGUILayout.TextField(newName);
        if (GUILayout.Button("Save", GUILayout.Width(60f)))
        {
            ok = "";
            error = OuterAIAccount.Rename(newName.Trim());
            if (error.Length == 0) ok = "Username changed.";
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label(OuterAIAccount.HasPassword ? "Change password" : "Set a password", lab);
        if (OuterAIAccount.HasPassword)
        {
            GUILayout.Label("Current password", val);
            cur = EditorGUILayout.PasswordField(cur);
        }
        GUILayout.Label(OuterAIAccount.HasPassword ? "New password (leave empty to remove it)" : "New password", val);
        pw1 = EditorGUILayout.PasswordField(pw1);
        GUILayout.Label("Confirm new password", val);
        pw2 = EditorGUILayout.PasswordField(pw2);
        if (GUILayout.Button(OuterAIAccount.HasPassword ? "Update Password" : "Set Password"))
        {
            ok = "";
            if (!OuterAIAccount.HasPassword && string.IsNullOrEmpty(pw1)) error = "Type a password first.";
            else
            {
                bool had = OuterAIAccount.HasPassword;
                error = OuterAIAccount.ChangePassword(cur, pw1, pw2);
                if (error.Length == 0)
                {
                    ok = !OuterAIAccount.HasPassword ? "Password removed." : (had ? "Password updated." : "Password set. You will sign in each time Unity opens.");
                    cur = pw1 = pw2 = "";
                    GUI.FocusControl(null);
                }
            }
        }

        GUILayout.Space(8f);
        Msg();
        if (OuterAIAccount.HasPassword && GUILayout.Button("Sign Out (locks Outer AI until you sign in)"))
        {
            OuterAIAccount.SignOut();
            error = ok = "";
            EditorApplication.delayCall += OuterAITos.Next;
        }
        if (GUILayout.Button("Delete Account"))
        {
            ok = "";
            if (OuterAIAccount.HasPassword && string.IsNullOrEmpty(cur)) error = "Type your current password in the box above to delete the account.";
            else if (EditorUtility.DisplayDialog("Delete account", "Delete your account? Outer AI stays locked until you make a new one.", "Delete", "Cancel"))
            {
                error = OuterAIAccount.Delete(cur);
                if (error.Length == 0) { cur = pw1 = pw2 = ""; error = ok = ""; EditorApplication.delayCall += OuterAITos.Next; }
            }
        }
        EditorGUILayout.EndScrollView();
    }
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
        Debug.LogWarning("Outer AI: Play mode was stopped because Outer AI is locked (TOS not agreed to, no account or not signed in, still doing the startup check, or disabled).");
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
        w.minSize = w.maxSize = new Vector2(460f, 280f);
        Rect m = EditorGUIUtility.GetMainWindowPosition();
        w.position = new Rect(m.x + (m.width - 460f) * 0.5f, m.y + (m.height - 280f) * 0.5f, 460f, 280f);
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
        GUILayout.Label("Agreeing sends two anonymous random codes (not your name) to the developer: one for you and one for this copy of Outer AI.", note);
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
            EditorApplication.delayCall += OuterAITos.Next;
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
            text = (OuterAIUser.DevAccess ? "DEVELOPER VERSION AVAILABLE: version " : "UPDATE AVAILABLE: version ") + OuterAIUpdater.remoteVersion + (string.IsNullOrEmpty(OuterAIUpdater.remoteNotes) ? "" : "\n" + OuterAIUpdater.remoteNotes);
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
            if (GUI.Button(new Rect(r.x + 10f, r.yMax - 34f, r.width - 20f, 26f), OuterAIUser.DevAccess ? "Get Developer Version" : "Update Now"))
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