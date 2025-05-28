using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PatronMonitoringAgent.Common;
using LibreHardwareMonitor.Hardware;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

namespace PatronMonitoringAgent
{
    public class SystemMonitor : ISystemMonitor
    {
        private readonly Computer _computer;

        public SystemMonitor()
        {
            _computer = new Computer
            {
                IsGpuEnabled = true,
                IsCpuEnabled = true,
                IsMemoryEnabled = true
            };
            _computer.Open();
        }

        public SystemInfo GetSystemInfo()
        {
            return new SystemInfo
            {
                OS = Environment.OSVersion.ToString(),
                CPU = GetCpuName(),
                RAM = $"{(GetTotalMemory() / (1024 * 1024 * 1024))}GB",
                GPU = GetGpuName(),
                Disks = GetDiskUsages()
            };
        }

        public SystemUsage GetCurrentUsage()
        {
            return new SystemUsage
            {
                CpuUsagePercent = GetCpuUsagePercent(),
                RamUsagePercent = GetRamUsagePercent(),
                GpuUsagePercent = GetGpuUsagePercent(),
                Disks = GetDiskUsages()
            };
        }

        public SystemNetworkInfo GetNetworkInfo()
        {
            return new SystemNetworkInfo
            {
                Address = GetLocalIpAddress(),
                SubnetMask = GetSubnetMask(),
                Gateway = GetGatewayAddress(),
                Dns = GetDnsAddress(),
                MacAddress = GetMacAddress()
            };
        }

        public IEnumerable<SystemLogEntry> GetSystemLogs(int maxCount = 100)
        {
            var entries = new List<SystemLogEntry>();
            try
            {
                var log = new EventLog("System");
                foreach (EventLogEntry entry in log.Entries)
                {
                    if (entry.EntryType == EventLogEntryType.Error || entry.EntryType == EventLogEntryType.Warning)
                    {
                        entries.Add(new SystemLogEntry
                        {
                            Source = entry.Source,
                            EntryType = entry.EntryType.ToString(),
                            Time = entry.TimeGenerated,
                            Message = entry.Message
                        });
                        if (entries.Count >= maxCount)
                            break;
                    }
                }
            }
            catch { }
            return entries;
        }

        // Platform-specific implementations below (for demo, simplified):
        private string GetCpuName()
        {
            var cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            return cpu?.Name ?? "Unknown CPU";
        }

        private string GetGpuName()
        {
            var gpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd || h.HardwareType == HardwareType.GpuIntel);
            return gpu?.Name ?? "Unknown GPU";
        }

        private long GetTotalMemory()
        {
            // Použijeme Win32 API přes System.Management pro spolehlivé zjištění fyzické paměti RAM
            try
            {
                long total = 0;
                var searcher = new System.Management.ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get())
                {
                    total = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                    break;
                }
                return total;
            }
            catch
            {
                return 0;
            }
        }

        private double GetCpuUsagePercent()
        {
            try
            {
                using (var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
                {
                    cpuCounter.NextValue();
                    System.Threading.Thread.Sleep(500);
                    return Math.Round(cpuCounter.NextValue(), 1);
                }
            }
            catch { return 0.0; }
        }

        private double GetRamUsagePercent()
        {
            try
            {
                // Získání přesného využití fyzické paměti (nikoli committed)
                var total = GetTotalMemory();
                var available = new PerformanceCounter("Memory", "Available Bytes").NextValue();
                if (total > 0)
                {
                    double used = total - available;
                    double percent = (used / total) * 100.0;
                    return Math.Round(percent, 1);
                }
                return 0.0;
            }
            catch { return 0.0; }
        }

        private double GetGpuUsagePercent()
        {
            double? percent = null;
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.GpuNvidia || hardware.HardwareType == HardwareType.GpuAmd || hardware.HardwareType == HardwareType.GpuIntel)
                {
                    hardware.Update();
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core"))
                        {
                            percent = sensor.Value ?? percent;
                        }
                    }
                }
            }
            return percent.HasValue ? Math.Round(percent.Value, 1) : 0.0;
        }

        private List<DiskInfo> GetDisks()
        {
            var list = new List<DiskInfo>();
            foreach (var d in System.IO.DriveInfo.GetDrives())
            {
                if (d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
                {
                    list.Add(new DiskInfo
                    {
                        Name = d.Name,
                        Size = $"{d.TotalSize / (1024 * 1024 * 1024)}GB",
                        Free = $"{d.TotalFreeSpace / (1024 * 1024 * 1024)}GB",
                        Type = d.DriveFormat
                    });
                }
            }
            return list;
        }

        private List<DiskUsage> GetDiskUsages()
        {
            var list = new List<DiskUsage>();
            foreach (var d in System.IO.DriveInfo.GetDrives())
            {
                if (d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
                {
                    double usagePercent = 0;
                    try
                    {
                        usagePercent = Math.Round(100.0 * (d.TotalSize - d.TotalFreeSpace) / d.TotalSize, 1);
                    }
                    catch { }
                    list.Add(new DiskUsage
                    {
                        Name = d.Name,
                        Size = $"{d.TotalSize / (1024 * 1024 * 1024)}GB",
                        Free = $"{d.TotalFreeSpace / (1024 * 1024 * 1024)}GB",
                        UsagePercent = usagePercent
                    });
                }
            }
            return list;
        }

        private string GetLocalIpAddress()
        {
            string localIP = "N/A";
            try
            {
                foreach (var host in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (host.AddressFamily == AddressFamily.InterNetwork)
                    {
                        localIP = host.ToString();
                        break;
                    }
                }
            }
            catch { }
            return localIP;
        }

        private string GetMacAddress()
        {
            string macAddress = "N/A";
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
                if (nic != null)
                {
                    macAddress = nic.GetPhysicalAddress().ToString();
                }
            }
            catch { }
            return macAddress;

        }

        private string GetGatewayAddress()
        {
            string gateway = "N/A";
            try
            {
                var gateways = NetworkInterface.GetAllNetworkInterfaces()
                    .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                    .Where(g => g.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(g => g.Address.ToString())
                    .ToList();
                if (gateways.Any())
                {
                    gateway = gateways.FirstOrDefault();
                }
            }
            catch { }
            return gateway;
        }

        private string[] GetDnsAddress()
        {
            string[] dnsAddresses = new string[] { "N/A" };
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
                if (nic != null)
                {
                    var dnsList = nic.GetIPProperties().DnsAddresses
                        .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                        .Select(d => d.ToString())
                        .ToArray();
                    if (dnsList.Any())
                    {
                        dnsAddresses = dnsList;
                    }
                }
            }
            catch { }
            return dnsAddresses;
        }

        private string GetSubnetMask()
        {
            string subnetMask = "N/A";
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
                if (nic != null)
                {
                    var ipProps = nic.GetIPProperties();
                    var unicast = ipProps.UnicastAddresses.FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);
                    if (unicast != null)
                    {
                        subnetMask = unicast.IPv4Mask.ToString();
                    }
                }
            }
            catch { }
            return subnetMask;
        }
    }
}