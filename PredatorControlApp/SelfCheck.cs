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
        }
    }
}
