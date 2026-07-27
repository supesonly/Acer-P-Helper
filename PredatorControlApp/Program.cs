namespace PredatorControlApp
{
    internal static class Program
    {
        private static readonly HashSet<string> _reported = new();
        private static bool _dialogOpen;

        private static string LogPath
        {
            get
            {
                var dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(dir)) dir = Path.GetTempPath();
                return Path.Combine(dir, "PredatorControl", "crash.log");
            }
        }

        internal static void Report(Exception ex, bool fatal)
        {
            string key = $"{ex.GetType().FullName}|{ex.StackTrace}";

            try
            {
                var path = LogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, $"[{DateTime.Now:u}] {(fatal ? "FATAL" : "ERROR")} {ex}\n\n");
            }
            catch { }

            if (_dialogOpen || !_reported.Add(key)) return;

            _dialogOpen = true;
            try
            {
                MessageBox.Show(
                    $"{(fatal ? "Fatal" : "Error")}: {ex.Message}\n\nLogged to:\n{LogPath}",
                    "Predator Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
            finally { _dialogOpen = false; }
        }

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            SelfCheck.Run();

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Report(e.Exception, false);

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex) Report(ex, true);
            };

            Form1 form;
            try
            {
                form = new Form1();
            }
            catch (Exception ex)
            {
                Report(ex, true);
                return;
            }

            Application.Run(form);
        }
    }
}
