using System;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace PatronMonitoringAgent.Tray
{
    static class Program
    {
        private static Mutex _mutex = null;

        [STAThread]
        static void Main()
        {
            bool isAdmin = IsAdministrator();
            string appName = isAdmin ? "PatronMontiringAgent_Admin" : "PatronMontiringAgent_User";

            bool createdNew;
            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("Aplikace již běží v této úrovni oprávnění.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var tray = new TrayApplicationContext())
            {
                Application.Run(tray);
            }
        }

        static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}