using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace BongoClaim
{
    // Optional launch-time repair; no resident watcher. The game's Autostart
    // off/on sequence restores its direct EXE entry. Enable this option again
    // afterwards to restore the checked launcher.
    public static class StartupLauncher
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunName = "BongoCat", HelperName = "BongoCatAutoChest.exe";
        private const string MutexName = @"Local\BongoCatAutoChest.Startup";

        [DataContract]
        private sealed class StartupState
        {
            [DataMember] public int Version = 1;
            [DataMember] public string Root, Command, OriginalValue;
            [DataMember] public int OriginalKind;
        }
        private sealed class RunValue { public string Value; public RegistryValueKind Kind; }

        public static string StartupDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BongoCatAutoChest"); } }
        public static string LogPath { get { return Path.Combine(StartupDirectory, "startup.log"); } }
        private static string StatePath { get { return Path.Combine(StartupDirectory, "startup.json"); } }
        private static string HelperPath { get { return Path.Combine(StartupDirectory, HelperName); } }

        private static string NormalizeRoot(string root)
        {
            if (String.IsNullOrWhiteSpace(root)) throw new ArgumentException("Choose the Bongo Cat game folder first.");
            var full = Path.GetFullPath(root);
            if (full.Length > Path.GetPathRoot(full).Length) full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (full.IndexOf('"') >= 0 || full.IndexOf('\r') >= 0 || full.IndexOf('\n') >= 0) throw new ArgumentException("The game folder contains an invalid command-line character.");
            return full;
        }
        private static string Quote(string value) { return "\"" + value + "\""; }
        private static string Command(string root) { return Quote(HelperPath) + " --launch " + Quote(root); }
        private static bool Equal(string left, string right) { return String.Equals(left, right, StringComparison.OrdinalIgnoreCase); }
        private static bool CurrentControls(Inspection result) { return result.Supported && result.Installed && result.RuntimeControls && result.OwnedTokenControls; }

        private static RunValue ReadRun()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                if (key == null) return new RunValue();
                var value = key.GetValue(RunName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (value == null) return new RunValue();
                var text = value as string;
                if (text == null) throw new InvalidOperationException("The Bongo Cat startup entry has an unfamiliar value. It was left unchanged.");
                return new RunValue { Value = text, Kind = key.GetValueKind(RunName) };
            }
        }
        private static bool SameRun(RunValue left, RunValue right) { return String.Equals(left.Value, right.Value, StringComparison.Ordinal) && (left.Value == null || left.Kind == right.Kind); }
        private static bool DirectGameCommand(string command, string root)
        {
            if (command == null) return true;
            var exe = Path.Combine(root, "BongoCat.exe");
            return Equal(command.Trim(), Quote(exe)) || Equal(command.Trim(), exe);
        }

        private static StartupState ReadState()
        {
            if (!File.Exists(StatePath)) return null;
            StartupState state;
            using (var stream = File.OpenRead(StatePath)) state = (StartupState)new DataContractJsonSerializer(typeof(StartupState)).ReadObject(stream);
            if (state == null || state.Version != 1 || !Equal(NormalizeRoot(state.Root), state.Root) || !String.Equals(state.Command, Command(state.Root), StringComparison.Ordinal)
                || !DirectGameCommand(state.OriginalValue, state.Root)
                || (state.OriginalValue != null && state.OriginalKind != (int)RegistryValueKind.String && state.OriginalKind != (int)RegistryValueKind.ExpandString))
                throw new InvalidOperationException("The saved startup backup is invalid. The startup entry was left unchanged.");
            return state;
        }
        private static void SaveState(StartupState state)
        {
            var pending = StatePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = File.Create(pending)) new DataContractJsonSerializer(typeof(StartupState)).WriteObject(stream, state);
                if (File.Exists(StatePath)) File.Replace(pending, StatePath, null); else File.Move(pending, StatePath);
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
        private static void CopyVerified(string source, string target)
        {
            if (!File.Exists(source)) throw new FileNotFoundException("Extract the whole release ZIP before enabling the startup launcher.", source);
            var expected = PatchEngine.Hash(source);
            if (Equal(Path.GetFullPath(source), Path.GetFullPath(target))) return;
            if (File.Exists(target) && Equal(PatchEngine.Hash(target), expected)) return;
            var pending = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(source, pending, false);
                if (!Equal(PatchEngine.Hash(pending), expected)) throw new IOException("The copied startup helper failed verification. The startup entry was left unchanged.");
                if (File.Exists(target)) File.Replace(pending, target, null); else File.Move(pending, target);
                if (!Equal(PatchEngine.Hash(target), expected)) throw new IOException("The startup helper failed verification. The startup entry was left unchanged.");
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
        private static bool TakeMutex(Mutex mutex)
        {
            try { return mutex.WaitOne(0); }
            catch (AbandonedMutexException) { return true; }
        }

        public static bool EnabledFor(string root)
        {
            if (String.IsNullOrWhiteSpace(root)) return false;
            return String.Equals(ReadRun().Value, Command(NormalizeRoot(root)), StringComparison.Ordinal);
        }
        public static string Enable(string root)
        {
            root = NormalizeRoot(root);
            PatchEngine.EnsureGameClosed(root);
            var inspection = PatchEngine.Inspect(PatchEngine.GameAssembly(root));
            if (!CurrentControls(inspection)) throw new InvalidOperationException("Install the supported current controls before enabling the startup launcher. " + inspection.Message);
            if (!File.Exists(Path.Combine(root, "BongoCat.exe"))) throw new InvalidOperationException("Choose the folder containing BongoCat.exe.");
            using (var mutex = new Mutex(false, MutexName))
            {
                if (!TakeMutex(mutex)) throw new InvalidOperationException("The startup launcher is busy. Try again after it finishes.");
                try
                {
                    var current = ReadRun();
                    var prior = ReadState();
                    if (prior != null && String.Equals(current.Value, prior.Command, StringComparison.Ordinal) && !Equal(root, prior.Root))
                        throw new InvalidOperationException("The startup launcher already belongs to another game folder. Disable it for that folder first.");
                    var owned = String.Equals(current.Value, Command(root), StringComparison.Ordinal);
                    if (owned && (prior == null || !Equal(root, prior.Root))) throw new InvalidOperationException("The startup entry has no matching original backup. It was left unchanged.");
                    if (!owned && !DirectGameCommand(current.Value, root)) throw new InvalidOperationException("The Bongo Cat startup entry points somewhere else. It was left unchanged.");
                    var sourceDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    Directory.CreateDirectory(StartupDirectory);
                    CopyVerified(Path.Combine(sourceDirectory, "Mono.Cecil.dll"), Path.Combine(StartupDirectory, "Mono.Cecil.dll"));
                    CopyVerified(Path.Combine(sourceDirectory, HelperName), HelperPath);
                    var state = owned ? prior : new StartupState { Root = root, Command = Command(root), OriginalValue = current.Value, OriginalKind = (int)current.Kind };
                    SaveState(state);
                    PatchEngine.EnsureGameClosed(root);
                    if (!SameRun(current, ReadRun())) throw new InvalidOperationException("The startup entry changed during setup. It was left unchanged; check it and try again.");
                    using (var key = Registry.CurrentUser.CreateSubKey(RunKey)) key.SetValue(RunName, state.Command, RegistryValueKind.String);
                    Log("Startup launcher enabled.");
                    return "Startup launcher enabled. It will check the patch before starting Bongo Cat through Steam at sign-in.";
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
        public static string Disable(string root)
        {
            root = NormalizeRoot(root);
            using (var mutex = new Mutex(false, MutexName))
            {
                if (!TakeMutex(mutex)) throw new InvalidOperationException("The startup launcher is busy. Try again after it finishes.");
                try
                {
                    var current = ReadRun();
                    if (!String.Equals(current.Value, Command(root), StringComparison.Ordinal)) return "The startup entry was already removed or changed. Nothing was changed.";
                    var state = ReadState();
                    if (state == null || !Equal(state.Root, root)) throw new InvalidOperationException("The original startup backup is missing. The startup entry was left unchanged.");
                    if (!SameRun(current, ReadRun())) throw new InvalidOperationException("The startup entry changed. Nothing was changed.");
                    using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (state.OriginalValue == null) key.DeleteValue(RunName, false);
                        else key.SetValue(RunName, state.OriginalValue, (RegistryValueKind)state.OriginalKind);
                    }
                    Log("Original startup setting restored.");
                    return "The previous Bongo Cat startup setting was restored.";
                }
                finally { mutex.ReleaseMutex(); }
            }
        }

        // Fail closed for missing or unfamiliar flags. Only the known fully
        // installed state (4) is allowed; update/staging states must finish.
        public static bool IsUpdatePending(string manifest)
        {
            if (manifest == null) return true;
            var match = Regex.Match(manifest, "\"StateFlags\"\\s*\"([0-9]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            ulong flags;
            return !match.Success || !UInt64.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out flags) || flags != 4;
        }
        private static void CheckSteamUpdate(string root)
        {
            var common = Directory.GetParent(root);
            var steamApps = common == null ? null : common.Parent;
            if (steamApps == null) return;
            var manifest = Path.Combine(steamApps.FullName, "appmanifest_3419430.acf");
            if (!File.Exists(manifest)) return;
            if (IsUpdatePending(File.ReadAllText(manifest)))
                throw new InvalidOperationException("Steam has a pending or incomplete Bongo Cat update. Let Steam finish updating, then launch through Bongo Cat Auto Chest again. No game files were changed by this check.");
        }
        public static Inspection Prepare(string root)
        {
            root = NormalizeRoot(root);
            PatchEngine.EnsureGameClosed(root);
            if (!File.Exists(Path.Combine(root, "BongoCat.exe"))) throw new InvalidOperationException("The Bongo Cat game folder is unavailable. Check that its drive is connected, then try again.");
            CheckSteamUpdate(root);
            var result = PatchEngine.Inspect(PatchEngine.GameAssembly(root));
            if (!result.Supported) throw new InvalidOperationException("The game cannot be started with the current patch: " + result.Message);
            if (!CurrentControls(result))
            {
                PatchEngine.Install(root, true);
                result = PatchEngine.Inspect(PatchEngine.GameAssembly(root));
            }
            if (!CurrentControls(result)) throw new InvalidOperationException("The patch could not be verified. The game was not started.");
            return result;
        }
        private static bool GameRunning(string root)
        {
            foreach (var process in Process.GetProcessesByName("BongoCat"))
                using (process)
                    try { if (Equal(Path.GetDirectoryName(process.MainModule.FileName), root)) return true; }
                    catch (System.ComponentModel.Win32Exception) { return true; }
                    catch (InvalidOperationException) { }
            return false;
        }
        private static string SteamExe()
        {
            var locations = new[] {
                Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string,
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string,
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string
            };
            foreach (var location in locations)
            {
                if (String.IsNullOrWhiteSpace(location)) continue;
                var exe = Path.GetFullPath(Path.Combine(location, "steam.exe"));
                if (File.Exists(exe)) return exe;
            }
            throw new InvalidOperationException("Steam could not be found. Open Steam once, then launch through Bongo Cat Auto Chest again.");
        }
        public static string Launch(string root)
        {
            root = NormalizeRoot(root);
            if (GameRunning(root)) return "Bongo Cat is already running. No files were changed.";
            using (var mutex = new Mutex(false, MutexName))
            {
                if (!TakeMutex(mutex)) return "The startup launcher is already working.";
                try
                {
                    if (GameRunning(root)) return "Bongo Cat is already running. No files were changed.";
                    var steam = SteamExe();
                    var prepared = Prepare(root);
                    CheckSteamUpdate(root);
                    Log("Patch verified; starting Bongo Cat through Steam.");
                    using (var process = Process.Start(new ProcessStartInfo(steam, "-applaunch 3419430") { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Path.GetDirectoryName(steam) })) { }
                    var wait = Stopwatch.StartNew();
                    while (!GameRunning(root) && wait.Elapsed < TimeSpan.FromSeconds(60)) Thread.Sleep(500);
                    if (!GameRunning(root)) throw new InvalidOperationException("Steam was opened, but Bongo Cat did not start within 60 seconds. Finish any Steam update or sign-in, then launch through Bongo Cat Auto Chest again.");
                    var after = PatchEngine.Inspect(PatchEngine.GameAssembly(root));
                    if (!CurrentControls(after) || !Equal(after.Hash, prepared.Hash))
                        throw new InvalidOperationException("Steam replaced the game files during launch. Exit Bongo Cat normally, finish its Steam update, then launch through Bongo Cat Auto Chest again. The running game was left untouched.");
                    Log("Bongo Cat started; the installed patch is verified.");
                    return "Bongo Cat started through Steam with the patch verified.";
                }
                catch (Exception ex) { Log("Startup stopped: " + ex.GetType().Name + ". Open the helper for the actionable error."); throw; }
                finally { mutex.ReleaseMutex(); }
            }
        }
        private static void Log(string status)
        {
            try
            {
                Directory.CreateDirectory(StartupDirectory);
                File.AppendAllText(LogPath, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " + status + Environment.NewLine, Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

