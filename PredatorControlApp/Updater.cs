using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

                notes.AppendLine($"--- {Str(rel, "name", $"v{v.ToString(3)}")} ---")
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
                MessageBox.Show(owner, $"The update to v{version} could not be installed - you're still on v{CurrentText}.\n\n" +
                    "This usually means the app file was locked or write-protected. Try again, or download it manually from GitHub.",
                    "Predator Control", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowNotes(owner, $"Updated to v{version}", "What's new", notes!, confirm: false);
        }

        #endregion

        #region Dialog

        private static readonly Color FormBg = Color.FromArgb(22, 22, 26);
        private static readonly Color TitleBarBg = Color.FromArgb(18, 18, 21);
        private static readonly Color PanelBg = Color.FromArgb(30, 30, 34);
        private static readonly Color SeparatorColor = Color.FromArgb(40, 40, 44);
        private static readonly Color TitleTextColor = Color.FromArgb(200, 200, 205);
        private static readonly Color CloseHoverColor = Color.FromArgb(220, 50, 50);
        private static readonly Color BodyColor = Color.FromArgb(165, 165, 175);
        private static readonly Color TextColor = Color.FromArgb(210, 210, 215);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private static void Drag(Form dlg, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(dlg.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        internal static string Plain(string markdown)
        {
            var sb = new StringBuilder();
            bool blank = false;

            foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.TrimEnd();

                if (line.Length == 0)
                {
                    blank = sb.Length > 0;
                    continue;
                }

                if (blank) sb.AppendLine();
                blank = false;

                line = Regex.Replace(line, @"^\s{0,3}#{1,6}\s*", "");
                line = Regex.Replace(line, @"^\s*[-*+]\s+", "\u2022 ");
                line = Regex.Replace(line, @"\[([^\]]+)\]\([^)]*\)", "$1");
                line = Regex.Replace(line, @"`([^`]*)`", "$1");
                line = line.Replace("**", "").Replace("__", "");

                sb.AppendLine(line);
            }

            return sb.ToString().Trim();
        }

        internal static bool ShowNotes(IWin32Window owner, string title, string subtitle, string notes, bool confirm)
        {
            const int W = 520, Bar = 36, Pad = 20, BtnW = 120, BtnH = 34, BodyH = 300;
            int bodyY = Bar + 72;
            int btnY = bodyY + BodyH + Pad;

            using var dlg = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                KeyPreview = true,
                BackColor = FormBg,
                ClientSize = new Size(W, btnY + BtnH + Pad),
                DialogResult = DialogResult.Cancel
            };

            var bar = new Panel { Location = Point.Empty, Size = new Size(W, Bar), BackColor = TitleBarBg };
            bar.MouseDown += (s, e) => Drag(dlg, e);
            dlg.Controls.Add(bar);

            var lblTitle = new Label
            {
                Text = title,
                Location = new Point(14, 9),
                AutoSize = true,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TitleTextColor,
                BackColor = Color.Transparent
            };
            lblTitle.MouseDown += (s, e) => Drag(dlg, e);
            bar.Controls.Add(lblTitle);

            var lblClose = new Label
            {
                Text = "\u2715",
                Location = new Point(W - Bar, 0),
                Size = new Size(Bar, Bar),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10f),
                ForeColor = TitleTextColor,
                Cursor = Cursors.Hand
            };
            lblClose.MouseEnter += (s, e) => lblClose.ForeColor = CloseHoverColor;
            lblClose.MouseLeave += (s, e) => lblClose.ForeColor = TitleTextColor;
            lblClose.Click += (s, e) => dlg.Close();
            bar.Controls.Add(lblClose);

            dlg.Controls.Add(new Panel
            {
                Location = new Point(0, Bar),
                Size = new Size(W, 1),
                BackColor = SeparatorColor
            });

            dlg.Controls.Add(new Label
            {
                Text = subtitle,
                Location = new Point(Pad, Bar + 16),
                Size = new Size(W - Pad * 2, 48),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TextColor,
                BackColor = Color.Transparent
            });

            var txt = new TextBox
            {
                Text = Plain(notes),
                Location = new Point(Pad, bodyY),
                Size = new Size(W - Pad * 2, BodyH),
                Multiline = true,
                ReadOnly = true,
                TabStop = false,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = PanelBg,
                ForeColor = BodyColor,
                Font = new Font("Segoe UI", 9.5f)
            };
            dlg.Controls.Add(txt);

            var btnPrimary = new PredatorButton
            {
                Text = confirm ? "Update Now" : "Close",
                Location = new Point(W - Pad - BtnW, btnY),
                Size = new Size(BtnW, BtnH)
            };
            btnPrimary.Click += (s, e) => { dlg.DialogResult = DialogResult.OK; dlg.Close(); };
            dlg.Controls.Add(btnPrimary);

            if (confirm)
            {
                var btnLater = new PredatorButton
                {
                    Text = "Later",
                    Location = new Point(W - Pad - BtnW * 2 - 12, btnY),
                    Size = new Size(BtnW, BtnH)
                };
                btnLater.Click += (s, e) => dlg.Close();
                dlg.Controls.Add(btnLater);
            }

            dlg.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) dlg.Close(); };
            dlg.Shown += (s, e) =>
            {
                txt.Select(0, 0);
                dlg.ActiveControl = btnPrimary;
            };

            return dlg.ShowDialog(owner) == DialogResult.OK;
        }

        #endregion
    }
}
