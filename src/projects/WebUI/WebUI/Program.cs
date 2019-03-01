using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebUI.Data;
using WebUI.Models;
using WebUI.Utils;

namespace WebUI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("hosting.json", optional: true)
                .AddCommandLine(args)
                .Build();

            Config.App = new Config(WebUIConfig.AppInfoConfigFile);
            Console.WriteLine($"configApp = {Config.App.Obj}");



            return WebHost.CreateDefaultBuilder(args)
                .UseConfiguration(config)
                .CaptureStartupErrors(true)
                .UseKestrel(options =>
                {
                    /*
                    options.Listen(IPAddress.Any, 443, listenOptions =>
                    {
                        listenOptions.UseHttps("server.pfx");
                    });*/
                })
                .UseStartup<Startup>()
                .Build();
        }
        /*
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();*/
    }
}
