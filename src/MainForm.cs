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
        private CheckBox emotes;
        private Button inspect, install, restore, browse;

        public MainForm()
        {
            Text = "BongoClaim · chest helper";
            ClientSize = new Size(670, 610);
            MinimumSize = new Size(670, 650);
            MaximumSize = new Size(870, 650);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Ink;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            var mark = TextLabel("ฅ", 31, Mint); mark.SetBounds(28, 22, 55, 60); Controls.Add(mark);
            var brand = TextLabel("BONGO CLAIM", 18, Color.White); brand.Font = new Font("Segoe UI", 18, FontStyle.Bold); brand.SetBounds(91, 28, 390, 35); Controls.Add(brand);
            var subtitle = TextLabel("Your cat keeps playing. Your chests get claimed.", 10, Muted); subtitle.SetBounds(92, 66, 490, 24); Controls.Add(subtitle);
            var badge = TextLabel("WINDOWS  /  v1.0", 8, Mint); badge.SetBounds(520, 34, 125, 22); Controls.Add(badge);
            var rule = new Panel { BackColor = PanelColor }; rule.SetBounds(30, 110, 610, 1); Controls.Add(rule);
            var heading = TextLabel("01   FIND YOUR GAME", 9, Muted); heading.SetBounds(30, 134, 400, 23); Controls.Add(heading);
            folder = new TextBox { BackColor = PanelColor, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Bongo Cat game folder" }; folder.SetBounds(30, 168, 493, 32); Controls.Add(folder);
            browse = MakeButton("Browse…", false, delegate { using (var dialog = new FolderBrowserDialog { Description = "Choose the folder containing BongoCat.exe", SelectedPath = folder.Text }) if (dialog.ShowDialog(this) == DialogResult.OK) { folder.Text = dialog.SelectedPath; CheckGame(); } }); browse.SetBounds(536, 165, 104, 34); Controls.Add(browse);
            var hint = TextLabel("Choose the BongoCat folder, not BongoCat_Data or Managed.", 9, Muted); hint.SetBounds(30, 207, 600, 22); Controls.Add(hint);
            var card = new Panel { BackColor = PanelColor }; card.SetBounds(30, 247, 610, 109); Controls.Add(card);
            var checkHeading = TextLabel("COMPATIBILITY CHECK", 8, Mint); checkHeading.SetBounds(17, 14, 570, 20); card.Controls.Add(checkHeading);
            status = TextLabel("Checking your installed build…", 10, Color.White); status.SetBounds(17, 39, 572, 45); card.Controls.Add(status);
            fingerprint = TextLabel("", 8, Muted); fingerprint.SetBounds(17, 86, 572, 17); card.Controls.Add(fingerprint);
            emotes = new CheckBox { Text = "Also claim emote chests (uses their normal in-game pets cost)", ForeColor = Muted, AutoSize = false, AccessibleName = "Also claim emote chests" }; emotes.SetBounds(30, 370, 610, 29); Controls.Add(emotes);
            var note = TextLabel("Normal chests only by default. Eligibility and cooldowns stay unchanged.", 9, Muted); note.SetBounds(30, 406, 610, 22); Controls.Add(note);
            inspect = MakeButton("Check build", false, delegate { CheckGame(); }); inspect.SetBounds(30, 447, 120, 44); Controls.Add(inspect);
            install = MakeButton("Enable auto claim  →", true, delegate { RunChange(false); }); install.SetBounds(164, 447, 282, 44); Controls.Add(install);
            restore = MakeButton("Restore original", false, delegate { RunChange(true); }); restore.SetBounds(460, 447, 180, 44); Controls.Add(restore);
            var foot = TextLabel("Exit Bongo Cat before enabling or restoring. A verified original backup\nis kept in your game folder. Steam updates may require reapplying.", 9, Muted); foot.SetBounds(30, 512, 610, 43); Controls.Add(foot);
            var github = new LinkLabel { Text = "Source & downloads ↗", LinkColor = Mint, ActiveLinkColor = Color.White, VisitedLinkColor = Mint, BackColor = Ink, Font = new Font("Segoe UI", 9), AutoSize = true }; github.SetBounds(30, 574, 230, 20); github.LinkClicked += delegate { Process.Start("https://github.com/NH4OH-35/bongo-claim"); }; Controls.Add(github);
            var offline = TextLabel("LOCAL TOOL · NO LOGIN · NO KEY SIMULATION", 8, Muted); offline.SetBounds(342, 576, 310, 20); Controls.Add(offline);
            Shown += delegate { folder.Text = FindGame(); CheckGame(); };
        }

        private Label TextLabel(string text, float size, Color color) { return new Label { Text = text, ForeColor = color, Font = new Font("Segoe UI", size), AutoSize = false }; }
        private Button MakeButton(string text, bool primary, EventHandler click)
        {
            var result = new Button { Text = text, BackColor = primary ? Mint : PanelColor, ForeColor = primary ? Ink : Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10, primary ? FontStyle.Bold : FontStyle.Regular) };
            result.FlatAppearance.BorderSize = 0; result.Click += click; return result;
        }

        private void Busy(bool busy) { foreach (var c in new Control[] { folder, browse, inspect, install, restore, emotes }) c.Enabled = !busy; UseWaitCursor = busy; }

        private void CheckGame()
        {
            try
            {
                var result = PatchEngine.Inspect(PatchEngine.GameAssembly(folder.Text));
                status.Text = result.Message; status.ForeColor = result.Supported ? Color.White : Color.FromArgb(255, 195, 154);
                fingerprint.Text = result.Hash == null ? "" : "BUILD FINGERPRINT  " + result.Hash.Substring(0, 16).ToUpperInvariant();
                install.Enabled = result.Supported && !result.Installed;
                restore.Enabled = File.Exists(Path.Combine(folder.Text, ".bongo-claim", "install.json"));
                if (result.Installed) emotes.Checked = result.Emotes;
            }
            catch (Exception ex) { status.Text = ex.Message; status.ForeColor = Color.FromArgb(255, 195, 154); install.Enabled = false; restore.Enabled = false; }
        }

        private void RunChange(bool undo)
        {
            string root = folder.Text; bool includeEmotes = emotes.Checked;
            Busy(true); status.Text = undo ? "Verifying backup and restoring the original…" : "Verifying your build, backing up and applying the patch…";
            var worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = undo ? PatchEngine.Restore(root) : PatchEngine.Install(root, includeEmotes); };
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
