using System;
using System.ServiceModel;
using MortalKombatXDLL.Services;
using MortalKombatXDLL.Contracts;
using System.ServiceModel.Description;

namespace LobbyServerHost
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Mortal Kombat X Lobby Server (Code-Based NetTcp)";
            var serviceInstance = new LobbyService();

            // Base address (0.0.0.0 listens on all interfaces). Change to localhost if local only.
            var baseAddress = new Uri("net.tcp://0.0.0.0:9090/LobbyService");

            using (var host = new ServiceHost(serviceInstance, baseAddress))
            {
                var binding = new NetTcpBinding(SecurityMode.None)
                {
                    MaxReceivedMessageSize = 10 * 1024 * 1024,
                    ReceiveTimeout = TimeSpan.FromMinutes(20),
                    SendTimeout = TimeSpan.FromMinutes(1),
                    OpenTimeout = TimeSpan.FromSeconds(10),
                    CloseTimeout = TimeSpan.FromSeconds(10)
                };

                host.AddServiceEndpoint(typeof(ILobbyService), binding, "");

                

                var smb = host.Description.Behaviors.Find<ServiceMetadataBehavior>();
                if (smb == null)
                {
                    smb = new ServiceMetadataBehavior();
                    host.Description.Behaviors.Add(smb);
                }

                // Optional MEX for tooling
                host.AddServiceEndpoint(typeof(IMetadataExchange), MetadataExchangeBindings.CreateMexTcpBinding(), "mex");

                var sdb = host.Description.Behaviors.Find<ServiceDebugBehavior>();
                if (sdb == null)
                {
                    sdb = new ServiceDebugBehavior { IncludeExceptionDetailInFaults = true };
                    host.Description.Behaviors.Add(sdb);
                }
                else
                {
                    sdb.IncludeExceptionDetailInFaults = true;
                }

                host.Open();
                Console.WriteLine("LobbyService running at " + baseAddress);
                Console.WriteLine("Press ENTER to stop.");
                Console.ReadLine();
            }
        }
    }
}
