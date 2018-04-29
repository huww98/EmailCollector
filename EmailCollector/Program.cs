using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmailCollector.Models;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EmailCollector
{
    public class Program
    {
        public async static Task Main(string[] args)
        {
            var host = BuildWebHost(args);

            using (var serviceScope = host.Services.CreateScope())
            {
                var services = serviceScope.ServiceProvider;
                Task fetchTask = null;
                CancellationTokenSource doneSource = new CancellationTokenSource();
                var logger = services.GetRequiredService<ILogger<Program>>();

                try
                {
                    var context = services.GetRequiredService<EmailCollectorContext>();
                    await context.Database.MigrateAsync();
                }
                catch (Exception e)
                {
                    logger.LogError(e, "An error occurred while migrating database.");
                    throw;
                }

                var fetcher = ActivatorUtilities.CreateInstance<EmailFetcher>(services);
                fetchTask = fetcher.FetchEmailAsync(doneSource.Token);

                host.Run();

                doneSource.Cancel();

                try
                {
                    await fetchTask;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    logger.LogError(e, "An error occurred.");
                }
            }
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .Build();
    }
}
