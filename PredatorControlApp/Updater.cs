using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace PredatorControlApp
{
    internal sealed record UpdateInfo(Version Version, string Tag, string Notes, string DownloadUrl);

    [SupportedOSPlatform("windows")]
    internal static class Updater
    {
        private const string ReleasesApi = "https://api.github.com/repos/supesonly/Acer-P-Helper/releases";
        private const string RegPath = @"SOFTWARE\PredatorControl";
        private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

        #region Version

        internal static Version Current => Norm(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));

        internal static string CurrentText => Current.ToString(3);

        private static Version Norm(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

        internal static bool TryParseTag(string tag, out Version v)
        {
            v = new Version(0, 0, 0);
            if (string.IsNullOrEmpty(tag)) return false;
            if (!Version.TryParse(tag.TrimStart('v', 'V').Trim(), out var parsed)) return false;
            v = Norm(parsed);
            return true;
        }

        #endregion

        #region Check

        internal static async Task<UpdateInfo?> CheckAsync()
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("PredatorControl");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using var doc = JsonDocument.Parse(await http.GetStringAsync(ReleasesApi));

            var current = Current;
            UpdateInfo? newest = null;
            var notes = new StringBuilder();

            foreach (var rel in doc.RootElement.EnumerateArray())
            {
                if (Flag(rel, "draft") || Flag(rel, "prerelease")) continue;
                if (!TryParseTag(Str(rel, "tag_name"), out var v) || v <= current) continue;

                notes.AppendLine($"â”€â”€ {Str(rel, "name", $"v{v.ToString(3)}")} â”€â”€")
                     .AppendLine(Str(rel, "body").Trim().Replace("\r\n", "\n").Replace("\n", Environment.NewLine))
                     .AppendLine();

                if (newest == null && PickAsset(rel, IsSelfContained) is string url)
                    newest = new UpdateInfo(v, Str(rel, "tag_name"), "", url);
            }

            return newest == null ? null : newest with { Notes = notes.ToString().TrimEnd() };
        }

        private static bool Flag(JsonElement e, string name) =>
            e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

        private static string Str(JsonElement e, string name, string fallback = "") =>
            e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? fallback : fallback;

        private static bool IsSelfContained =>
            !RuntimeEnvironment.GetRuntimeDirectory().Replace('/', '\\').Contains(@"\dotnet\shared\", OIC);

        internal static string? PickAsset(JsonElement rel, bool wantSelfContained)
        {
            if (!rel.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;

            string? fallback = null;
            foreach (var a in assets.EnumerateArray())
            {
                string name = Str(a, "name");
                if (!name.EndsWith(".exe", OIC)) continue;

                string url = Str(a, "browser_download_url");
                if (url.Length == 0) continue;

                bool selfContained = name.Contains("standalone", OIC) || name.Contains("self", OIC);
                if (selfContained == wantSelfContained) return url;
                fallback ??= url;
            }
            return fallback;
        }

        #endregion

        #region Apply

        internal static async Task ApplyAsync(UpdateInfo info)
        {
            string target = Environment.ProcessPath ?? Application.ExecutablePath;
            string staged = Path.Combine(Path.GetTempPath(), "PredatorControl-update.exe");

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("PredatorControl");
                using var src = await http.GetStreamAsync(info.DownloadUrl);
                using var dst = File.Create(staged);
                await src.CopyToAsync(dst);
            }

            if (new FileInfo(staged).Length < 100_000)
                throw new IOException("Downloaded file looks truncated.");

            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegPath);
                key.SetValue("UpdateNotes", info.Notes);
                key.SetValue("UpdateNotesVersion", info.Version.ToString(3));
            }
            catch { }

            int pid = Environment.ProcessId;
            string script = Path.Combine(Path.GetTempPath(), "PredatorControl-update.cmd");
            File.WriteAllText(script, $"""
                @echo off
                chcp 65001 >nul
                :wait
                tasklist /fi "PID eq {pid}" /nh | find "{pid}" >nul
                if not errorlevel 1 (
                    timeout /t 1 /nobreak >nul
                    goto wait
                )
                move /y "{staged}" "{target}" >nul
                start "" "{target}"
                (goto) 2>nul & del "%~f0"
                """, new UTF8Encoding(false));

            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath()
            });
        }

        #endregion

        #region Post-update notes

        internal static void ShowPendingNotes(IWin32Window owner)
        {
            string? notes, version;
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegPath);
                notes = key.GetValue("UpdateNotes") as string;
                version = key.GetValue("UpdateNotesVersion") as string;
                if (string.IsNullOrWhiteSpace(notes)) return;
                key.DeleteValue("UpdateNotes", false);
                key.DeleteValue("UpdateNotesVersion", false);
            }
            catch { return; }

            if (Version.TryParse(version, out var v) && Norm(v) > Current)
            {
                MessageBox.Show(owner, $"The update to v{version} could not be installed â€” you're still on v{CurrentText}.\n\n" +
                    "This usually means the app file was locked or write-protected. Try again, or download it manually from GitHub.",
                    "Predator Control", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowNotes(owner, $"Updated to v{version}", "What's new", notes!, confirm: false);
        }

        #endregion

        #region Dialog

        private static readonly Color FormBg = Color.FromArgb(22, 22, 26);
        private static readonly Color PanelBg = Color.FromArgb(30, 30, 34);
        private static readonly Color HeaderColor = Color.FromArgb(120, 120, 135);
        private static readonly Color TextColor = Color.FromArgb(210, 210, 215);

        internal static bool ShowNotes(IWin32Window owner, string title, string subtitle, string notes, bool confirm)
        {
            using var dlg = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                BackColor = FormBg,
                ClientSize = new Size(460, 400),
                DialogResult = DialogResult.Cancel
            };

            dlg.Controls.Add(new Label
            {
                Text = subtitle,
                Location = new Point(20, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = TextColor,
                BackColor = Color.Transparent
            });

            var txt = new TextBox
            {
                Text = notes,
                Location = new Point(20, 48),
                Size = new Size(420, 280),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = PanelBg,
                ForeColor = HeaderColor,
                Font = new Font("Segoe UI", 9f)
            };
            dlg.Controls.Add(txt);

            var btnPrimary = new PredatorButton
            {
                Text = confirm ? "Update Now" : "Close",
                Location = new Point(confirm ? 250 : 330, 344),
                Size = new Size(110, 34)
            };
            btnPrimary.Click += (s, e) => { dlg.DialogResult = DialogResult.OK; dlg.Close(); };
            dlg.Controls.Add(btnPrimary);

            if (confirm)
            {
                var btnLater = new PredatorButton
                {
                    Text = "Later",
                    Location = new Point(130, 344),
                    Size = new Size(110, 34)
                };
                btnLater.Click += (s, e) => dlg.Close();
                dlg.Controls.Add(btnLater);
            }

            dlg.Shown += (s, e) => txt.Select(0, 0);
            return dlg.ShowDialog(owner) == DialogResult.OK;
        }

        #endregion
    }
}
