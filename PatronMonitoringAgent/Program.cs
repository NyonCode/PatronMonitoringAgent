using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace PatronMonitoringAgent
{
    internal static class Program
    {
        /// <summary>
        /// Hlavní vstupní bod aplikace.
        /// </summary>
        static void Main()
        {
            if (Environment.UserInteractive)
            {
                // Pro vývojáře: umožní spustit službu jako konzolovku
                var runner = new AgentRunner();
                runner.StartAsync();
                Console.WriteLine("Running... Press Enter to exit.");
                Console.ReadLine();
                runner.Stop();
            }
            else
            {
                ServiceBase.Run(new AgentService());
            }
        }
    }
}
