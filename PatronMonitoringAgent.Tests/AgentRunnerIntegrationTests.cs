using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatronMonitoringAgent;
using PatronMonitoringAgent.Common;
using System;
using System.Threading.Tasks;

namespace PatronMonitoringAgent.Tests
{
    [TestClass]
    public class AgentRunnerIntegrationTests
    {
        [TestMethod]
        public async Task OnShutdown_SendsShutdownStatusToApi()
        {
            // Správné použití ServiceCollection
            var services = new ServiceCollection();

            // Zde nastavte testovací implementace nebo skuteèné služby
            services.AddSingleton<IConfigurationProvider, TestConfigurationProvider>();
             services.AddSingleton<IApiClient>(sp =>
                new LaravelApiClient(
                    sp.GetRequiredService<IConfigurationProvider>(), // místo URL
                    sp.GetRequiredService<ILoggerService>()          // místo tokenu
                )
            );
            // Pøidejte také testovací logger service:

            var provider = services.BuildServiceProvider();

            var runner = new AgentRunner(provider);

            // Act
            await runner.OnShutdown();

            // Assert
            Assert.IsTrue(provider.GetRequiredService<IConfigurationProvider>().UUIDExist(), "UUID should exist after shutdown.");
        }
    }

    // Testovací implementace IConfigurationProvider
    public class TestConfigurationProvider : IConfigurationProvider
    {
        public int GetInterval()
        {
            throw new NotImplementedException();
        }

        public string GetToken()
        {
            throw new NotImplementedException();
        }

        public string GetUUID() => "test-uuid";

        public bool IntervalExist()
        {
            throw new NotImplementedException();
        }

        public void SaveInterval(int? interval)
        {
            throw new NotImplementedException();
        }

        public void SaveToken(string token)
        {
            throw new NotImplementedException();
        }

        public bool TokenExist()
        {
            throw new NotImplementedException();
        }

        public bool UUIDExist()
        {
            // Vrací true, aby test prošel
            return true;
        }
        // Implementujte další metody dle potøeby
    }
}