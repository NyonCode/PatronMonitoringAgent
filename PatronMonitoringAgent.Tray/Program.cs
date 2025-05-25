using System;
using System.Windows.Forms;

namespace PatronMonitoringAgent.Tray
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var tray = new TrayApplicationContext())
            {
                Application.Run(tray);
            }
        }
    }
}