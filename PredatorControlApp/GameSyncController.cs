using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;

namespace PredatorControlApp
{
    [SupportedOSPlatform("windows")]
    public class GameSyncController : IDisposable
    {
        private readonly System.Windows.Forms.Timer _pollTimer;
        private readonly List<GameProfile> _profiles = new();
        private readonly string _savePath;

        private bool _enabled;
        private string? _activeExe;                   
        private DashboardSnapshot? _savedSnapshot;    

        public event Action<GameProfile>? GameDetected;

        public event Action<DashboardSnapshot>? GameExited;

        public event Action<bool>? EnabledChanged;

        public bool IsEnabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    if (_enabled)
                        _pollTimer.Start();
                    else
                    {
                        _pollTimer.Stop();
                        if (_activeExe != null && _savedSnapshot != null)
                        {
                            GameExited?.Invoke(_savedSnapshot);
                            _activeExe = null;
                            _savedSnapshot = null;
                        }
                    }
                    EnabledChanged?.Invoke(_enabled);
                    Save();
                }
            }
        }

        public IReadOnlyList<GameProfile> Profiles => _profiles.AsReadOnly();
        public string? ActiveGameExe => _activeExe;

        public GameSyncController()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "PredatorControl");
            Directory.CreateDirectory(dir);
            _savePath = Path.Combine(dir, "game_sync.json");

            _pollTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _pollTimer.Tick += PollProcesses;

            Load();

            if (_enabled)
                _pollTimer.Start();
        }

        public void AddProfile(GameProfile profile)
        {
            _profiles.RemoveAll(p => p.ExecutableName.Equals(profile.ExecutableName, StringComparison.OrdinalIgnoreCase));
            _profiles.Add(profile);
            Save();
        }

        public void RemoveProfile(string exeName)
        {
            _profiles.RemoveAll(p => p.ExecutableName.Equals(exeName, StringComparison.OrdinalIgnoreCase));

            if (_activeExe != null && _activeExe.Equals(exeName, StringComparison.OrdinalIgnoreCase))
            {
                if (_savedSnapshot != null)
                    GameExited?.Invoke(_savedSnapshot);
                _activeExe = null;
                _savedSnapshot = null;
            }
            Save();
        }

        public void UpdateProfile(GameProfile profile)
        {
            var idx = _profiles.FindIndex(p => p.ExecutableName.Equals(profile.ExecutableName, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                _profiles[idx] = profile;
            else
                _profiles.Add(profile);
            Save();
        }

        public void SetPreGameSnapshot(DashboardSnapshot snapshot)
        {
            _savedSnapshot = snapshot;
        }

        private bool IsProcessActive(string nameWithoutExe)
        {
            try
            {
                var matches = Process.GetProcessesByName(nameWithoutExe);
                if (matches.Length == 0) return false;

                bool isBrowser = nameWithoutExe.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
                                 nameWithoutExe.Equals("brave", StringComparison.OrdinalIgnoreCase) ||
                                 nameWithoutExe.Equals("msedge", StringComparison.OrdinalIgnoreCase);

                bool active = false;
                if (isBrowser)
                {
                    active = matches.Any(p => p.MainWindowHandle != IntPtr.Zero);
                }
                else
                {
                    active = true;
                }

                foreach (var p in matches) p.Dispose();
                return active;
            }
            catch
            {
                return false;
            }
        }

        private void PollProcesses(object? sender, EventArgs e)
        {
            if (!_enabled || _profiles.Count == 0) return;

            if (_activeExe != null)
            {
                string nameWithoutExe = Path.GetFileNameWithoutExtension(_activeExe);
                bool stillRunning = IsProcessActive(nameWithoutExe);

                if (!stillRunning)
                {
                    var snapshot = _savedSnapshot;
                    _activeExe = null;
                    _savedSnapshot = null;
                    if (snapshot != null)
                        GameExited?.Invoke(snapshot);
                }
            }
            else
            {
                foreach (var profile in _profiles)
                {
                    string nameWithoutExe = Path.GetFileNameWithoutExtension(profile.ExecutableName);
                    if (IsProcessActive(nameWithoutExe))
                    {
                        _activeExe = profile.ExecutableName;
                        GameDetected?.Invoke(profile);
                        break;
                    }
                }
            }
        }

        #region Persistence

        private void Save()
        {
            try
            {
                var data = new GameSyncData
                {
                    Enabled = _enabled,
                    Profiles = _profiles
                };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_savePath, json);
            }
            catch { }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_savePath)) return;
                var json = File.ReadAllText(_savePath);
                var data = JsonSerializer.Deserialize<GameSyncData>(json);
                if (data != null)
                {
                    _enabled = data.Enabled;
                    _profiles.Clear();
                    if (data.Profiles != null)
                        _profiles.AddRange(data.Profiles);
                }
            }
            catch { }
        }

        private class GameSyncData
        {
            public bool Enabled { get; set; }
            public List<GameProfile>? Profiles { get; set; }
        }

        #endregion

        public void Dispose()
        {
            _pollTimer.Stop();
            _pollTimer.Dispose();
        }
    }
}
