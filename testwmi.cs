using System;
using System.Management;

class Program
{
    static void Main()
    {
        try {
            var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM BatteryControl");
            foreach (ManagementObject obj in searcher.Get()) {
                using var inParams = obj.GetMethodParameters("SetBatteryHealthControl");
                Console.WriteLine("SetBatteryHealthControl Parameters:");
                foreach (PropertyData prop in inParams.Properties) {
                    Console.WriteLine($"{prop.Name} : {prop.Type} (IsArray: {prop.IsArray})");
                }

                using var inParams2 = obj.GetMethodParameters("GetBatteryHealthControlStatus");
                Console.WriteLine("GetBatteryHealthControlStatus Parameters:");
                foreach (PropertyData prop in inParams2.Properties) {
                    Console.WriteLine($"{prop.Name} : {prop.Type} (IsArray: {prop.IsArray})");
                }
                break;
            }
        } catch (Exception ex) {
            Console.WriteLine(ex.Message);
        }
    }
}
