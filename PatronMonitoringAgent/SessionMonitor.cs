using PatronMonitoringAgent.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Security.Principal;

namespace PatronMonitoringAgent
{
    public class SessionMonitor : ISessionMonitor
    {
        public SessionInfo GetCurrentSession()
        {
            var user = Environment.UserName;
            var sessionStart = GetUserLogonTime();
            var mappedDrives = DriveMonitorForCurrentUser();
            var accessiblePaths = GetAccessiblePaths();

            return new SessionInfo
            {
                User = user,
                SessionStart = sessionStart,
                MappedDrives = mappedDrives,
                AccessiblePaths = accessiblePaths
            };
        }

        private DateTime GetUserLogonTime()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_LogonSession WHERE LogonType = 2 OR LogonType = 10"))
                {
                    foreach (ManagementObject session in searcher.Get())
                    {
                        var logonId = session["LogonId"].ToString();
                        var startTime = ManagementDateTimeConverter.ToDateTime(session["StartTime"].ToString());

                        // Najdi session, která patří aktuálnímu uživateli
                        using (var relSearcher = new ManagementObjectSearcher(
                            $"ASSOCIATORS OF {{Win32_LogonSession.LogonId={logonId}}} WHERE AssocClass=Win32_LoggedOnUser Role=Dependent"))
                        {
                            foreach (ManagementObject rel in relSearcher.Get())
                            {
                                var account = (string)rel["Name"];
                                var domain = (string)rel["Domain"];
                                var currentUser = WindowsIdentity.GetCurrent();
                                if (string.Equals(account, currentUser.Name.Split('\\').Last(), StringComparison.OrdinalIgnoreCase))
                                {
                                    return startTime;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            // Fallback (pokud nejde zjistit): aktuální čas
            return DateTime.Now;
        }

        private List<MappedDrive> DriveMonitorForCurrentUser()
        {
            
            try
            {
                var result = new List<MappedDrive>();
                foreach (var drive in Environment.GetLogicalDrives())
                {
                    // Získání síťových jednotek
                    var driveInfo = new System.IO.DriveInfo(drive);
                    if (driveInfo.DriveType == System.IO.DriveType.Network)
                    {
                        result.Add(new MappedDrive
                        {
                            Letter = drive.Replace("\\", ""),
                            Path = driveInfo.Name
                        });
                    }
                }
                return result;
            }
            catch
            {
                return new List<MappedDrive>();
            }
        }

        private List<string> GetAccessiblePaths()
        {
            var paths = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };
            return paths;
        }
    }
}