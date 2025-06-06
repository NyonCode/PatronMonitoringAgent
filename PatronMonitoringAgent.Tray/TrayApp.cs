using PatronMonitoringAgent.Tray.Properties;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer; // Explicitní určení, který Timer se má použít

namespace PatronMonitoringAgent.Tray
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;

        private Icon _fallbackIcon = SystemIcons.Shield;

        /// <summary>
        /// Gets or sets the fallback icon used when no valid icon file is found.
        /// </summary>
        public Icon FallbackIcon
        {
            get => _fallbackIcon;
            set => _fallbackIcon = value ?? SystemIcons.Shield;
        }

        private readonly Timer _heartbeatTimer; // Používá se System.Windows.Forms.Timer
        private readonly string _heartbeatFile = "tray_heartbeat.txt"; // Cesta může být upravena např. do %APPDATA%
        private readonly int _heartbeatIntervalMs = 30_000;



        public TrayApplicationContext()
        {
            var contextMenu = new ContextMenuStrip();
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");

            //contextMenu.Items.Add("Zobraz stav", null, (s, e) => ShowStatus());

            if (IsAdministrator())
            {
                contextMenu.Items.Add("Restartovat službu", null, (s, e) => RestartAgent());
                contextMenu.Items.Add(new ToolStripSeparator());
                contextMenu.Items.Add("Konec", null, (s, e) => ExitTray());
            }

            _notifyIcon = new NotifyIcon
            {
                Icon = AdminIconResovler(),
                ContextMenuStrip = contextMenu,
                Text = "Patron Monitoring Agent",
                Visible = true
            };

            _notifyIcon.DoubleClick += (s, e) => ShowStatus();

            // Periodický heartbeat pro watchdog
            _heartbeatTimer = new Timer
            {
                Interval = _heartbeatIntervalMs,
                Enabled = true
            };
            _heartbeatTimer.Tick += (s, e) => WriteTrayHeartbeat();
            _heartbeatTimer.Start();

            // První zápis hned po startu
            WriteTrayHeartbeat();
        }

        private void WriteTrayHeartbeat()
        {
            try
            {
                File.WriteAllText(_heartbeatFile, DateTime.UtcNow.ToString("o"));
            }
            catch
            {
                // Ignoruj chyby, watchdog je robustní
            }
        }

        private void ShowStatus()
        {
            var sessionMonitor = new PatronMonitoringAgent.SessionMonitor();
            var session = sessionMonitor.GetCurrentSession();

            string info = $"Uživatel: {session.User}\n" +
                          $"Začátek relace: {session.SessionStart}\n" +
                          $"Stav: {(IsAdministrator() ? "Správce" : "Uživatel")}\n" +
                          $"Verze: {Application.ProductVersion}\n" +
                          $"Stav služby: {(new ServiceController("PatronMonitoringAgent").Status == ServiceControllerStatus.Running ? "Běží" : "Zastavena")}\n" +
                          $"Připojené disky: {string.Join(", ", session.MappedDrives.Select(d => d.Letter))} \n";

            MessageBox.Show(info, "Informace o relaci", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RestartAgent()
        {
            try
            {
                // Restart Windows služby (musí mít práva správce!)
                using (var sc = new ServiceController("PatronMonitoringAgent"))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
                    }
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                }
                MessageBox.Show("Agent byl restartován.", "PAtron Monitorin Agent", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Chyba při restartu služby:\n" + ex.Message, "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void ExitTray()
        {
            _heartbeatTimer.Stop();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Application.Exit();
        }

        /// <summary>
        /// Cleans up resources when the application context is disposed.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _heartbeatTimer?.Dispose();
                _notifyIcon?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Gets the icon from the given path, or returns a fallback icon.
        /// </summary>
        /// <param name="iconPath">The file path to the icon.</param>
        /// <returns>
        /// Icon object from the specified file, or the fallback icon if the file is not found.
        /// </returns>
        private Icon GetIcon(string iconPath = null)
        {
            const string fallbackIconFileName = "icon.ico";

            if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
            {
                string fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fallbackIconFileName);
                if (File.Exists(fallbackPath))
                {
                    return new Icon(fallbackPath, 32, 32);
                }

                return FallbackIcon;
            }

            return new Icon(iconPath, 32, 32);
        }

        private Icon AdminIconResovler()
        {
            if (! IsAdministrator())
            {
                return GetIcon();
                
            }

            return FallbackIcon;

        }

    }
}