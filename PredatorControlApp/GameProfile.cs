namespace PredatorControlApp
{
    public class GameProfile
    {
        public string ExecutableName { get; set; } = "";
        public string DisplayName { get; set; } = "";    

        public byte PowerMode { get; set; } = 0x01;      
        public byte FanMode { get; set; } = 0x01;         

        public int RefreshRate { get; set; } = -1;        

        public int BatteryLimit { get; set; } = -1;       

        public int RgbMode { get; set; } = -1;            
        public int RgbBrightness { get; set; } = -1;      
        public int RgbSpeed { get; set; } = -1;           
        public int RgbR { get; set; } = -1;               
        public int RgbG { get; set; } = -1;
        public int RgbB { get; set; } = -1;
    }

    public class DashboardSnapshot
    {
        public byte PowerMode { get; set; }
        public byte FanMode { get; set; }
        public int RefreshRate { get; set; }
        public int BatteryLimit { get; set; }
        public int RgbMode { get; set; }
        public int RgbBrightness { get; set; }
        public int RgbSpeed { get; set; }
        public int RgbR { get; set; }
        public int RgbG { get; set; }
        public int RgbB { get; set; }
    }
}
