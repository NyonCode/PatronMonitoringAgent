using System;
using System.Collections.Generic;

namespace PatronMonitoringAgent.Common
{
    public class ApiResponse
    {
        public string Status { get; set; }
        public int? Interval { get; set; }
        public string Token { get; set; }
        public string UpdateUrl { get; set; }
        public List<RemoteCommand> RemoteCommands { get; set; }
    }

    public class RemoteCommand
    {
        public string Type { get; set; }
        public string Command { get; set; }
        public string Url { get; set; }
    }

    public class MappedDrive
    {
        public string Letter { get; set; }
        public string Path { get; set; }
    }

    public class SystemInfo
    {
        public string OS { get; set; }
        public string CPU { get; set; }
        public string RAM { get; set; }
        public string GPU { get; set; }
        public List<DiskUsage> Disks { get; set; }
    }

    public class DiskInfo
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public string Free { get; set; }
        public string Type { get; set; }
    }

    public class SystemUsage
    {
        public double CpuUsagePercent { get; set; }
        public double RamUsagePercent { get; set; }
        public double GpuUsagePercent { get; set; }
        public List<DiskUsage> Disks { get; set; }
    }

    public class SystemNetworkInfo
    {
        public string Address { get; set; }
        public string SubnetMask { get; set; }
        public string Gateway { get; set; }
        public string[] Dns { get; set; }
        public string MacAddress { get; set; }
    }

    public class DiskUsage
    {
        public string Name { get; set; }
        public double UsagePercent { get; set; }
        public string Free { get; set; }
        public string Size { get; set; }
    }

    public class SystemLogEntry
    {
        public string Source { get; set; }
        public string EntryType { get; set; }
        public DateTime Time { get; set; }
        public string Message { get; set; }
    }

    public class SessionInfo
    {
        public string User { get; set; }
        public DateTime SessionStart { get; set; }
        public List<MappedDrive> MappedDrives { get; set; }
        public List<string> AccessiblePaths { get; set; }
    }
}