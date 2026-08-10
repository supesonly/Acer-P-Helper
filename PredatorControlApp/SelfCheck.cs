using System.Diagnostics;

namespace PredatorControlApp
{
    internal static class SelfCheck
    {
        [Conditional("DEBUG")]
        public static void Run()
        {
            var messy = new List<Point> { new(500, 500), new(10, -20), new(60, 60), new(60, 61) };
            var n = FanCurveGraph.Normalize(messy);

            Debug.Assert(n.Count == messy.Count, "Normalize dropped points");
            Debug.Assert(n[0].X == 30 && n[^1].X == 100, "Normalize must pin first/last temp");
            for (int i = 0; i < n.Count; i++)
            {
                Debug.Assert(n[i].Y is >= 0 and <= 100, "speed out of range");
                Debug.Assert(n[i].X is >= 30 and <= 100, "temp out of range");
                Debug.Assert(i == 0 || n[i - 1].X <= n[i].X, "Normalize must sort by temp");
            }

            Debug.Assert(FanCurveGraph.Normalize(null).Count >= 2, "null must yield default curve");
            Debug.Assert(FanCurveGraph.Normalize(new List<Point> { new(50, 50) }).Count >= 2,
                "single point must yield default curve");

            Debug.Assert(Form1.BatteryProfileValues.Length == 4, "battery table size");
            Debug.Assert(Form1.BatteryProfileValues[3] == 0x06, "battery Eco must map to 0x06");
            Debug.Assert(Form1.AcProfileValues.Length == 5, "AC table size");
            Debug.Assert(Form1.AcProfileValues[4] == 0x05, "AC Turbo must map to 0x05");

            CheckPowerLineDebounce();
        }

        private static void CheckPowerLineDebounce()
        {
            bool? pending = null;
            int ticks = 0;
            bool? state = false;

            state = Form1.DebouncePowerLine(PowerLineStatus.Online, state, ref pending, ref ticks);
            Debug.Assert(state == false, "one Online sample must not flip state");

            state = Form1.DebouncePowerLine(PowerLineStatus.Offline, state, ref pending, ref ticks);
            Debug.Assert(state == false, "a contradicting sample must restart the count");

            state = Form1.DebouncePowerLine(PowerLineStatus.Online, state, ref pending, ref ticks);
            state = Form1.DebouncePowerLine(PowerLineStatus.Online, state, ref pending, ref ticks);
            Debug.Assert(state == true, "two agreeing samples must flip state");

            state = Form1.DebouncePowerLine(PowerLineStatus.Unknown, state, ref pending, ref ticks);
            Debug.Assert(state == true, "Unknown must not change state");
            Debug.Assert(ticks == 0 && pending == null, "Unknown must reset the count");

            bool? unknownStart = null;
            pending = null;
            ticks = 0;
            unknownStart = Form1.DebouncePowerLine(PowerLineStatus.Offline, unknownStart, ref pending, ref ticks);
            Debug.Assert(unknownStart == null, "unresolved state must stay unresolved after one sample");
            unknownStart = Form1.DebouncePowerLine(PowerLineStatus.Offline, unknownStart, ref pending, ref ticks);
            Debug.Assert(unknownStart == false, "unresolved state must resolve after two agreeing samples");

            CheckUpdater();
        }

        [Conditional("DEBUG")]
        private static void CheckUpdater()
        {
            Debug.Assert(Updater.TryParseTag("v1.3.1", out var tag) && tag == new Version(1, 3, 1), "tag must parse without the v");
            Debug.Assert(!Updater.TryParseTag("nightly", out _), "junk tags must be rejected");
            Debug.Assert(Updater.Current.Revision == -1, "Current must be Major.Minor.Build so tags compare cleanly");

            using var doc = System.Text.Json.JsonDocument.Parse("""
                {"assets":[
                    {"name":"PredatorControl-standalone-win-x64.exe","browser_download_url":"http://x/big.exe"},
                    {"name":"PredatorControl-win-x64.exe","browser_download_url":"http://x/light.exe"}]}
                """);
            var rel = doc.RootElement;
            Debug.Assert(Updater.PickAsset(rel, true) == "http://x/big.exe", "self-contained build must take the standalone asset");
            Debug.Assert(Updater.PickAsset(rel, false) == "http://x/light.exe", "framework-dependent build must take the light asset");

            using var noMatch = System.Text.Json.JsonDocument.Parse("""
                {"assets":[{"name":"only-light.exe","browser_download_url":"http://x/only.exe"}]}
                """);
            Debug.Assert(Updater.PickAsset(noMatch.RootElement, true) == "http://x/only.exe", "must fall back to any .exe");

            using var none = System.Text.Json.JsonDocument.Parse("""{"assets":[{"name":"notes.txt","browser_download_url":"http://x/n"}]}""");
            Debug.Assert(Updater.PickAsset(none.RootElement, true) == null, "non-exe assets must be ignored");
        }
    }
}
