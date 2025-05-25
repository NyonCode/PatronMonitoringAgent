using System;
using System.Drawing;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer; // Explicitní určení, který Timer se má použít

namespace PatronMonitoringAgent.Tray
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Timer _heartbeatTimer; // Používá se System.Windows.Forms.Timer
        private readonly string _heartbeatFile = "tray_heartbeat.txt"; // Cesta může být upravena např. do %APPDATA%
        private readonly int _heartbeatIntervalMs = 30_000;

        public TrayApplicationContext()
        {
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Zobraz stav", null, (s, e) => ShowStatus());
            contextMenu.Items.Add("Restartovat agenta", null, (s, e) => RestartAgent());
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Konec", null, (s, e) => ExitTray());

            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Information,
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
            // Zde lze načítat stav z IPC/registry/souboru dle potřeby
            string status = "Agent běží v pořádku.\n" +
                            $"Heartbeat: {DateTime.UtcNow:HH:mm:ss}\n" +
                            $"User: {Environment.UserName}";
            MessageBox.Show(status, "Stav EMS agenta", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show("Agent byl restartován.", "EMS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Chyba při restartu služby:\n" + ex.Message, "Chyba", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExitTray()
        {
            _heartbeatTimer.Stop();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _heartbeatTimer?.Dispose();
                _notifyIcon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}