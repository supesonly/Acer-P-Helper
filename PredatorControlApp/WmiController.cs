using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PredatorControlApp
{
    [SupportedOSPlatform("windows")]
    public class WmiController : IDisposable
    {
        private ManagementObject? _cachedObj;
        private readonly object _lock = new();

        [DllImport("powrprof.dll")]
        private static extern uint PowerSetActiveOverlayScheme(Guid scheme);

        private static readonly Guid OVERLAY_EFFICIENCY = new("961cc777-2547-4f9d-8174-7d86181b8a7a");
        private static readonly Guid OVERLAY_BALANCED = new("00000000-0000-0000-0000-000000000000");
        private static readonly Guid OVERLAY_PERFORMANCE = new("ded574b5-45a0-4f42-8737-46345c09c238");

        private byte _lastR = 0, _lastG = 150, _lastB = 255;
        private byte _brightness = 100;
        private byte _speed = 5;       
        private byte _direction = 0;   
        private int _lastMode = 3;     

        public byte LastR => _lastR;
        public byte LastG => _lastG;
        public byte LastB => _lastB;
        public byte Brightness => _brightness;
        public byte Speed => _speed;
        public byte Direction => _direction;
        public int LastRgbMode => _lastMode;

        private ManagementObject? GetWmiObject()
        {
            lock (_lock)
            {
                if (_cachedObj != null) return _cachedObj;
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM AcerGamingFunction");
                    _cachedObj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                }
                catch { _cachedObj = null; }
                return _cachedObj;
            }
        }

        private void InvalidateCache()
        {
            lock (_lock) { _cachedObj = null; }
        }

        private (bool success, ulong output) SendCommand(string method, ulong input)
        {
            try
            {
                var obj = GetWmiObject();
                if (obj == null) return (false, 0);

                using var inParams = obj.GetMethodParameters(method);
                inParams["gmInput"] = input;
                using var outParams = obj.InvokeMethod(method, inParams, null);
                ulong result = Convert.ToUInt64(outParams["gmOutput"]);
                return ((result & 0xFF) == 0, result);
            }
            catch
            {
                InvalidateCache();
                return (false, 0);
            }
        }

        private bool SendLedCommand(byte[] payload)
        {
            try
            {
                var obj = GetWmiObject();
                if (obj == null) return false;

                using var inParams = obj.GetMethodParameters("SetGamingKBBacklight");
                inParams["gmInput"] = payload;
                using var outParams = obj.InvokeMethod("SetGamingKBBacklight", inParams, null);
                ulong result = Convert.ToUInt64(outParams["gmOutput"]);
                return (result & 0xFF) == 0;
            }
            catch
            {
                InvalidateCache();
                return false;
            }
        }

        private int GetSensorReading(ulong sensorId)
        {
            try
            {
                var obj = GetWmiObject();
                if (obj == null) return 0;

                using var inParams = obj.GetMethodParameters("GetGamingSysInfo");
                inParams["gmInput"] = (ulong)(0x0001 | (sensorId << 8));
                using var outParams = obj.InvokeMethod("GetGamingSysInfo", inParams, null);
                var raw = (ulong)outParams["gmOutput"];
                if ((raw & 0xFF) == 0) return (int)((raw >> 8) & 0xFFFF);
            }
            catch { InvalidateCache(); }
            return 0;
        }

        public void SetPowerMode(byte mode)
        {
            SendCommand("SetGamingMiscSetting", (ulong)0x0B | ((ulong)mode << 8));
            SyncWindowsPowerMode(mode);
        }

        public void SetFanBehavior(byte mode) =>
            SendCommand("SetGamingFanBehavior", (ulong)(0x09 | ((ulong)mode << 16) | ((ulong)mode << 22)));

        public void SetRgbMode(int mode, byte r, byte g, byte b, byte brightness, byte speed, byte direction)
        {
            _lastR = r; _lastG = g; _lastB = b;
            _brightness = brightness;
            _speed = speed;
            _direction = direction;
            _lastMode = mode;
            ApplyLightingMode(mode);
        }

        public void SetBrightness(byte brightness)
        {
            _brightness = brightness;
            ApplyLightingMode(_lastMode);
        }

        public void SetSpeed(byte speed)
        {
            _speed = speed;
            ApplyLightingMode(_lastMode);
        }

        public void SetDirection(byte direction)
        {
            _direction = direction;
            ApplyLightingMode(_lastMode);
        }

        public void SetStaticColor(byte r, byte g, byte b, byte brightness)
        {
            _lastR = r; _lastG = g; _lastB = b;
            _brightness = brightness;
            _lastMode = 0;
            ApplyLightingMode(0);
        }

        private void ApplyLightingMode(int mode)
        {
            SendCommand("SetGamingLEDBehavior", 0x07ul);
            Thread.Sleep(50);

            if (mode != 2 && mode != 3)
            {
                ulong zonePayload = 0x06ul | (0x0Ful << 8)
                    | ((ulong)_lastR << 16) | ((ulong)_lastG << 24) | ((ulong)_lastB << 32);
                SendCommand("SetGamingLEDBehavior", zonePayload);
                Thread.Sleep(20);
            }

            byte[] payload = new byte[16];
            payload[0] = (byte)mode;     
            payload[1] = _speed;         
            payload[2] = _brightness;    
            payload[3] = _direction;     
            payload[5] = _lastR;
            payload[6] = _lastG;
            payload[7] = _lastB;
            payload[9] = 1;              
            SendLedCommand(payload);
        }

        public int CpuTemp => GetSensorReading(0x01);
        public int GpuTemp => GetSensorReading(0x0A);

        private void SyncWindowsPowerMode(byte acerMode)
        {
            try
            {
                Guid overlay = acerMode switch
                {
                    0x00 or 0x06 => OVERLAY_EFFICIENCY,
                    0x04 or 0x05 => OVERLAY_PERFORMANCE,
                    _ => OVERLAY_BALANCED
                };
                PowerSetActiveOverlayScheme(overlay);
            }
            catch { }
        }

        private ManagementObject? GetBatteryControlObject()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM BatteryControl");
                return searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        public bool SetBatteryChargeLimit(bool enable)
        {
            try
            {
                using var obj = GetBatteryControlObject();
                if (obj == null) return false;

                using var inParams = obj.GetMethodParameters("SetBatteryHealthControl");
                inParams["uBatteryNo"] = (byte)1;
                inParams["uFunctionMask"] = (byte)1;
                inParams["uFunctionStatus"] = (byte)(enable ? 1 : 0);
                inParams["uReservedIn"] = new byte[] { 0, 0, 0, 0, 0 };

                using var outParams = obj.InvokeMethod("SetBatteryHealthControl", inParams, null);
                ushort result = Convert.ToUInt16(outParams["uReturn"]);
                return result == 0;
            }
            catch
            {
                return false;
            }
        }
        public bool IsBatteryControlSupported()
        {
            try
            {
                using var obj = GetBatteryControlObject();
                return obj != null;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _cachedObj?.Dispose();
                _cachedObj = null;
            }
        }
    }
}