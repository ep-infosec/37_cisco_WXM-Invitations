using Microsoft.AspNetCore.Razor.Hosting;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace InvitationNotification
{
    class Program
    {
        public static IConfigurationRoot Configuration;
        private static readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private static ApplicationLog log;
        private static ManualResetEvent resetEvent = new ManualResetEvent(false);

        static async Task Main(string[] args)
        {

            try
            {
                Console.WriteLine("Process started");
                var configuration = new ConfigurationBuilder()
                                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                                    .AddJsonFile("appsettings.json", false, true)
                                    .Build();

                AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);

                string appLogpath = configuration["ApplicationLogpath"];
                log = new ApplicationLog(appLogpath);

                log.logMessage = $" {DateTime.UtcNow.ToString()}    Process started\n";
                NotificationHandler n = new NotificationHandler(configuration, log, cancellationTokenSource.Token);
                await n.NotificationWorker();

                //Console.ReadLine();

                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    // Cancel the cancellation to allow the program to shutdown cleanly.
                    eventArgs.Cancel = true;
                    resetEvent.Set();
                };
                resetEvent.WaitOne();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in Main()" + e.Message);
            }
        }

        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            cancellationTokenSource.Cancel();

            log.logMessage += "Application exiting!\n";
            log.AddLogsToFile(DateTime.UtcNow);

            Console.WriteLine("exit");
        }


    }

}
