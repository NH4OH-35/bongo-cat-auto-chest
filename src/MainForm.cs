using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BongoClaim
{
    public sealed class MainForm : Form
    {
        private readonly Color Ink = Color.FromArgb(20, 25, 31), PanelColor = Color.FromArgb(29, 36, 43), Mint = Color.FromArgb(162, 238, 190), Muted = Color.FromArgb(156, 171, 181);
        private TextBox folder;
        private Label status, fingerprint;
        private Button inspect, install, restore, browse, startup;
        private bool isBusy;

        public MainForm()
        {
            Text = "Bongo Cat Auto Chest";
            ClientSize = new Size(670, 745);
            MinimumSize = new Size(670, 785);
            MaximumSize = new Size(870, 785);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Ink;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            var mark = TextLabel("ฅ", 31, Mint); mark.SetBounds(28, 22, 55, 60); Controls.Add(mark);
            var brand = TextLabel("BONGO CAT AUTO CHEST", 18, Color.White); brand.Font = new Font("Segoe UI", 18, FontStyle.Bold); brand.SetBounds(91, 28, 390, 35); Controls.Add(brand);
            var subtitle = TextLabel("Your cat keeps playing. Your chests get claimed.", 10, Muted); subtitle.SetBounds(92, 66, 490, 24); Controls.Add(subtitle);
            var badge = TextLabel("WINDOWS  /  v1.3", 8, Mint); badge.SetBounds(520, 34, 125, 22); Controls.Add(badge);
            var rule = new Panel { BackColor = PanelColor }; rule.SetBounds(30, 110, 610, 1); Controls.Add(rule);
            var heading = TextLabel("01   FIND YOUR GAME", 9, Muted); heading.SetBounds(30, 134, 400, 23); Controls.Add(heading);
            folder = new TextBox { BackColor = PanelColor, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Bongo Cat game folder" }; folder.SetBounds(30, 168, 493, 32); Controls.Add(folder);
            browse = MakeButton("Browse…", false, delegate { using (var dialog = new FolderBrowserDialog { Description = "Choose the folder containing BongoCat.exe", SelectedPath = folder.Text }) if (dialog.ShowDialog(this) == DialogResult.OK) { folder.Text = dialog.SelectedPath; CheckGame(); } }); browse.SetBounds(536, 165, 104, 34); Controls.Add(browse);
            var hint = TextLabel("Choose the BongoCat folder, not BongoCat_Data or Managed.", 9, Muted); hint.SetBounds(30, 207, 600, 22); Controls.Add(hint);
            var card = new Panel { BackColor = PanelColor }; card.SetBounds(30, 247, 610, 109); Controls.Add(card);
            var checkHeading = TextLabel("COMPATIBILITY CHECK", 8, Mint); checkHeading.SetBounds(17, 14, 570, 20); card.Controls.Add(checkHeading);
            status = TextLabel("Checking your installed build…", 10, Color.White); status.SetBounds(17, 39, 572, 45); card.Controls.Add(status);
            fingerprint = TextLabel("", 8, Muted); fingerprint.SetBounds(17, 86, 572, 17); card.Controls.Add(fingerprint);
            var controlsHeading = TextLabel("02   CONTROL IT IN THE GAME", 9, Muted); controlsHeading.SetBounds(30, 377, 610, 23); Controls.Add(controlsHeading);
            var controlsLocation = TextLabel("Settings > General", 12, Mint); controlsLocation.SetBounds(30, 408, 610, 27); Controls.Add(controlsLocation);
            var normalRow = TextLabel("Normal / emote auto claim · Stored tokens and claim buttons", 10, Color.White); normalRow.SetBounds(30, 443, 610, 24); Controls.Add(normalRow);
            var emoteRow = TextLabel("Keep pets when claiming · Edit pet score", 10, Color.White); emoteRow.SetBounds(30, 471, 610, 24); Controls.Add(emoteRow);
            var note = TextLabel("Menu shortcuts: F10 edit score / F7 normal / F9 emote.", 9, Muted); note.SetBounds(30, 506, 610, 22); Controls.Add(note);
            inspect = MakeButton("Check build", false, delegate { CheckGame(); }); inspect.SetBounds(30, 545, 120, 44); Controls.Add(inspect);
            install = MakeButton("Install auto claim  →", true, delegate { RunChange(false); }); install.SetBounds(164, 545, 282, 44); Controls.Add(install);
            restore = MakeButton("Restore original", false, delegate { RunChange(true); }); restore.SetBounds(460, 545, 180, 44); Controls.Add(restore);
            var foot = TextLabel("Exit Bongo Cat before installing or restoring. Claims consume real owned\ntokens. The helper can be closed after installation.", 9, Muted); foot.SetBounds(30, 604, 610, 43); Controls.Add(foot);
            startup = MakeButton("Enable startup repair", false, delegate { RunStartupChange(); }); startup.SetBounds(30, 655, 282, 38); Controls.Add(startup);
            var startupHint = TextLabel("Check and repair before starting Bongo Cat\nat Windows sign-in. Optional; exit game first.", 9, Muted); startupHint.SetBounds(326, 654, 314, 43); Controls.Add(startupHint);
            var github = new LinkLabel { Text = "Source & downloads ↗", LinkColor = Mint, ActiveLinkColor = Color.White, VisitedLinkColor = Mint, BackColor = Ink, Font = new Font("Segoe UI", 9), AutoSize = true }; github.SetBounds(30, 715, 230, 20); github.LinkClicked += delegate { Process.Start("https://github.com/NH4OH-35/bongo-cat-auto-chest"); }; Controls.Add(github);
            var offline = TextLabel("LOCAL TOOL · NO LOGIN · NO KEY SIMULATION", 8, Muted); offline.SetBounds(342, 717, 310, 20); Controls.Add(offline);
            Shown += delegate { folder.Text = FindGame(); CheckGame(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (isBusy) e.Cancel = true; };
        }

        private Label TextLabel(string text, float size, Color color) { return new Label { Text = text, ForeColor = color, Font = new Font("Segoe UI", size), AutoSize = false }; }
        private Button MakeButton(string text, bool primary, EventHandler click)
        {
            var result = new Button { Text = text, BackColor = primary ? Mint : PanelColor, ForeColor = primary ? Ink : Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10, primary ? FontStyle.Bold : FontStyle.Regular) };
            result.FlatAppearance.BorderSize = 0; result.Click += click; return result;
        }

        private void Busy(bool busy) { isBusy = busy; foreach (var c in new Control[] { folder, browse, inspect, install, restore, startup }) c.Enabled = !busy; UseWaitCursor = busy; }

        private void CheckGame()
        {
            if (isBusy) return;
            try
            {
                var result = PatchEngine.Inspect(PatchEngine.GameAssembly(folder.Text));
                status.Text = result.Message; status.ForeColor = result.Supported ? Color.White : Color.FromArgb(255, 195, 154);
                fingerprint.Text = result.Hash == null ? "" : "BUILD FINGERPRINT  " + result.Hash.Substring(0, 16).ToUpperInvariant();
                install.Text = result.Installed ? (result.OwnedTokenControls ? "Controls installed" : "Upgrade controls  →") : "Install auto claim  →";
                install.Enabled = result.Supported && (!result.Installed || !result.OwnedTokenControls);
                restore.Enabled = File.Exists(Path.Combine(folder.Text, ".bongo-claim", "install.json"));
                startup.Text = StartupLauncher.EnabledFor(folder.Text) ? "Disable startup repair" : "Enable startup repair";
                startup.Enabled = StartupLauncher.EnabledFor(folder.Text) || (result.Supported && result.OwnedTokenControls);
            }
            catch (Exception ex) { status.Text = ex.Message; status.ForeColor = Color.FromArgb(255, 195, 154); install.Enabled = false; restore.Enabled = false; startup.Enabled = false; }
        }

        private void RunChange(bool undo)
        {
            if (isBusy) return;
            string root = folder.Text;
            Busy(true); status.Text = undo ? "Verifying backup and restoring the original…" : "Verifying your build, backing up and applying the patch…";
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                if (undo) { PatchEngine.EnsureGameClosed(root); StartupLauncher.Disable(root); e.Result = PatchEngine.Restore(root); }
                else e.Result = PatchEngine.Install(root, true);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                Busy(false); CheckGame();
                status.Text = e.Error == null ? (string)e.Result : e.Error.Message;
                status.ForeColor = e.Error == null ? Mint : Color.FromArgb(255, 195, 154);
                worker.Dispose();
            };
            worker.RunWorkerAsync();
        }

        private void RunStartupChange()
        {
            if (isBusy) return;
            string root = folder.Text;
            bool disable = StartupLauncher.EnabledFor(root);
            Busy(true); status.Text = disable ? "Restoring your previous startup choice…" : "Saving a startup launcher and verifying the installed controls…";
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = disable ? StartupLauncher.Disable(root) : StartupLauncher.Enable(root); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                Busy(false); CheckGame();
                status.Text = e.Error == null ? (string)e.Result : e.Error.Message;
                status.ForeColor = e.Error == null ? Mint : Color.FromArgb(255, 195, 154);
                worker.Dispose();
            };
            worker.RunWorkerAsync();
        }
        public static string FindGame()
        {
            foreach (var process in Process.GetProcessesByName("BongoCat"))
            {
                using (process) { try { return Path.GetDirectoryName(process.MainModule.FileName); } catch { } }
            }
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var keyPath in new[] { @"HKEY_CURRENT_USER\Software\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam" })
            {
                var steam = Registry.GetValue(keyPath, "SteamPath", null) as string ?? Registry.GetValue(keyPath, "InstallPath", null) as string;
                if (!String.IsNullOrEmpty(steam)) libraries.Add(steam);
            }
            libraries.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            foreach (var steam in libraries.ToArray())
            {
                var file = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(file)) continue;
                foreach (Match match in Regex.Matches(File.ReadAllText(file), "\"path\"\\s*\"([^\"]+)\"")) libraries.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
            }
            foreach (var library in libraries)
            {
                var game = Path.Combine(library, "steamapps", "common", "BongoCat");
                if (File.Exists(Path.Combine(game, "BongoCat.exe"))) return game;
            }
            return "";
        }
    }
}
