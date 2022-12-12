using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Json;
using SFTPToS3Sync.BackgroundServices;
using SFTPToS3Sync.Domains;
using SFTPToS3Sync.Helper;
using System;

namespace SFTPToS3Sync
{
    class Program
    {
        public static readonly string Namespace = typeof(Program).Namespace;
        public static readonly string AppName = Namespace.LastIndexOf('.') >= 0
                                            ? Namespace.Substring(Namespace.LastIndexOf('.', Namespace.LastIndexOf('.') - 1) + 1)
                                            : Namespace;
        static void Main(string[] args)
        {
            try
            {
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception Message: {ex.Message} and StackTrace: {ex.StackTrace}");
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging((hostBuilderContext, logging) => {
                    var isDevelopment = hostBuilderContext.HostingEnvironment.IsDevelopment();
                    var appConfiguration = hostBuilderContext.Configuration;

                })
                .ConfigureServices((hostContext, services) => {
                    services.AddSingleton<IS3Connector, S3Connector>();
                    services.AddSingleton<ILocalConnector, LocalConnector>();
                    services.AddSingleton<ISFTPConnector, SFTPConnector>();
                    services.AddSingleton<MongoDBConnector>();
                    services.AddHostedService<FolderSync>();
                });
    }
}
