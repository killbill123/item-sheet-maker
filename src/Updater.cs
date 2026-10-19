using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace ItemSheetMaker
{
    /// <summary>
    /// Checks GitHub Releases for a newer version, asks the user, downloads the installer and runs it silently.
    /// The installer replaces the app and starts it again.
    /// </summary>
    static class Updater
    {
        const string Repo = "killbill123/item-sheet-maker";
        const string ApiUrl = "https://api.github.com/repos/" + Repo + "/releases/latest";

        public static Version Current { get { return Assembly.GetExecutingAssembly().GetName().Version; } }

        class ReleaseInfo { public Version Version; public string Tag; public string SetupUrl; public string Notes; }

        /// <summary>Run a check in the background. When manual is false, stays quiet unless an update exists.</summary>
        public static void CheckAsync(Form owner, bool manual)
        {
            var t = new Thread(() =>
            {
                ReleaseInfo info = null; string error = null;
                try { info = FetchLatest(); } catch (Exception ex) { error = ex.Message; }
                if (owner.IsDisposed) return;
                try { owner.BeginInvoke((Action)(() => OnChecked(owner, info, error, manual))); } catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        static ReleaseInfo FetchLatest()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
            string json;
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "ItemSheetMaker/" + Current;
                wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                wc.Encoding = System.Text.Encoding.UTF8;
                json = wc.DownloadString(ApiUrl);
            }
            var tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
            var vm = Regex.Match(tag, @"(\d+)\.(\d+)(?:\.(\d+))?");
            if (!vm.Success) return null;
            var ver = new Version(int.Parse(vm.Groups[1].Value), int.Parse(vm.Groups[2].Value),
                vm.Groups[3].Success ? int.Parse(vm.Groups[3].Value) : 0);
            string setup = null;
            foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\""))
            {
                var u = m.Groups[1].Value;
                if (u.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && u.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0)
                { setup = u; break; }
            }
            var notes = Regex.Match(json, "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"").Groups[1].Value;
            notes = Regex.Replace(Regex.Unescape(notes), "(?m)^#+\\s*", "").Replace("**", "");
            if (notes.Length > 600) notes = notes.Substring(0, 600) + "...";
            return new ReleaseInfo { Version = ver, Tag = tag, SetupUrl = setup, Notes = notes };
        }

        static void OnChecked(Form owner, ReleaseInfo info, string error, bool manual)
        {
            var cur = new Version(Current.Major, Current.Minor, Math.Max(0, Current.Build));
            if (info == null)
            {
                if (manual) MessageBox.Show(owner, "Could not check for updates.\n\n" + (error ?? "No release information found."),
                    "Item Sheet Maker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (info.Version <= cur || info.SetupUrl == null)
            {
                if (manual) MessageBox.Show(owner, "You have the latest version (" + Short(cur) + ").",
                    "Item Sheet Maker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!manual && Settings.Get("skipVersion") == info.Version.ToString()) return;

            var msg = "A new version of Item Sheet Maker is available.\n\n" +
                      "Installed:  " + Short(cur) + "\nNew:          " + Short(info.Version) + "\n" +
                      (string.IsNullOrWhiteSpace(info.Notes) ? "" : "\nWhat's new:\n" + info.Notes.Trim() + "\n") +
                      "\nUpdate now? The app will close and reopen when the update is done.\n\n" +
                      "(Yes = update now,  No = later,  Cancel = skip this version)";
            var r = MessageBox.Show(owner, msg, "Update available", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
            if (r == DialogResult.Cancel) { Settings.Set("skipVersion", info.Version.ToString()); return; }
            if (r != DialogResult.Yes) return;
            DownloadAndInstall(owner, info);
        }

        static string Short(Version v) { return v.Major + "." + v.Minor + (v.Build > 0 ? "." + v.Build : ""); }

        static void DownloadAndInstall(Form owner, ReleaseInfo info)
        {
            var path = Path.Combine(Path.GetTempPath(), "ItemSheetMaker-Setup-" + info.Version + ".exe");
            var dlg = new Form
            {
                Text = "Updating Item Sheet Maker", Width = 420, Height = 140, FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, ControlBox = false,
                BackColor = MainForm.Panel, ForeColor = MainForm.Text1
            };
            var lbl = new Label { Text = "Downloading version " + Short(info.Version) + "...", Left = 16, Top = 16, Width = 380 };
            var bar = new ProgressBar { Left = 16, Top = 44, Width = 372, Height = 18 };
            dlg.Controls.Add(lbl); dlg.Controls.Add(bar);

            var wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "ItemSheetMaker/" + Current;
            wc.DownloadProgressChanged += (s, e) => { bar.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage)); };
            wc.DownloadFileCompleted += (s, e) =>
            {
                wc.Dispose();
                if (e.Error != null || e.Cancelled)
                {
                    dlg.Close();
                    MessageBox.Show(owner, "Download failed:\n" + (e.Error != null ? e.Error.Message : "cancelled"),
                        "Item Sheet Maker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                lbl.Text = "Installing...";
                try
                {
                    // /S = silent; the installer closes this app, replaces it and starts the new version.
                    Process.Start(new ProcessStartInfo(path, "/S /UPDATE") { UseShellExecute = true });
                    Application.Exit();
                }
                catch (Exception ex)
                {
                    dlg.Close();
                    MessageBox.Show(owner, "Could not start the installer:\n" + ex.Message, "Item Sheet Maker",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            dlg.Shown += (s, e) =>
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                wc.DownloadFileAsync(new Uri(info.SetupUrl), path);
            };
            dlg.ShowDialog(owner);
        }
    }

    /// <summary>Tiny key=value settings file in %AppData%\ItemSheetMaker\settings.txt.</summary>
    static class Settings
    {
        static string FilePath
        {
            get
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ItemSheetMaker");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.txt");
            }
        }

        public static string Get(string key)
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i > 0 && line.Substring(0, i) == key) return line.Substring(i + 1);
                }
            }
            catch { }
            return null;
        }

        public static void Set(string key, string value)
        {
            try
            {
                var lines = File.Exists(FilePath) ? new System.Collections.Generic.List<string>(File.ReadAllLines(FilePath))
                                                  : new System.Collections.Generic.List<string>();
                lines.RemoveAll(l => l.StartsWith(key + "="));
                lines.Add(key + "=" + value);
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch { }
        }
    }
}
