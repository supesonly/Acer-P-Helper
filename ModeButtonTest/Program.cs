using System;
using System.Management;
using System.Runtime.Versioning;

namespace ModeButtonTest
{
    [SupportedOSPlatform("windows")]
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("Acer Mode/Turbo Button WMI Event Listener Test");
            Console.WriteLine("==================================================");
            Console.WriteLine("Make sure to run this console app as ADMINISTRATOR!");
            Console.WriteLine("Once started, press the physical Mode / Turbo button.");
            Console.WriteLine("Press Ctrl+C or Enter to exit.");
            Console.WriteLine("--------------------------------------------------");

            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\WMI");
                scope.Connect();

                // Subscribe to AcerGenericEvent
                WqlEventQuery query = new WqlEventQuery("SELECT * FROM AcerGenericEvent");
                using ManagementEventWatcher watcher = new ManagementEventWatcher(scope, query);
                
                watcher.EventArrived += (sender, e) =>
                {
                    Console.WriteLine($"\n[Event Arrived at {DateTime.Now:HH:mm:ss.fff}]");
                    try
                    {
                        var eventObj = e.NewEvent;
                        string instanceName = eventObj["InstanceName"]?.ToString() ?? "Unknown";
                        Console.WriteLine($"Instance Name: {instanceName}");

                        if (eventObj["EventDetail"] is byte[] detail)
                        {
                            Console.Write("EventDetail (Hex): ");
                            foreach (byte b in detail)
                            {
                                Console.Write($"{b:X2} ");
                            }
                            Console.WriteLine();

                            Console.Write("EventDetail (Dec): ");
                            foreach (byte b in detail)
                            {
                                Console.Write($"{b} ");
                            }
                            Console.WriteLine();
                        }
                        else
                        {
                            Console.WriteLine("EventDetail is not a byte array or is null.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing event: {ex.Message}");
                    }
                };

                watcher.Start();
                Console.WriteLine("Listening for events... Press Enter to stop.");
                Console.ReadLine();
                watcher.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing event watcher: {ex.Message}");
                Console.WriteLine("Please run this tool as Administrator.");
            }
        }
    }
}
