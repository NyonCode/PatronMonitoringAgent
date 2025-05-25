using System.Collections.Generic;
using System.IO;
using PatronMonitoringAgent.Common;

namespace PatronMonitoringAgent
{
    public class DriveMonitor : IDriveMonitor
    {
        public IEnumerable<MappedDrive> GetMappedDrives()
        {
            var drives = new List<MappedDrive>();
            foreach (var d in DriveInfo.GetDrives())
            {
                if (d.DriveType == DriveType.Network)
                {
                    try
                    {
                        drives.Add(new MappedDrive { Letter = d.Name, Path = d.RootDirectory.FullName });
                    }
                    catch { }
                }
            }
            return drives;
        }
    }
}