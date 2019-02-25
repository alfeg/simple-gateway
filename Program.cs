using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ProxyKit;

namespace Alfeg.SimpleGateway
{
    internal class ApiMap
    {
        public UpstreamHost Bind { get; set; }
        public UpstreamHost Target { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (File.Exists("gateway.json"))
            {
                var map = JsonConvert.DeserializeObject<ApiMap[]>(File.ReadAllText("gateway.json"));
                CreateWebHostBuilder(map).Build().Run();
            }
            else
            {
                Console.WriteLine("Expecting gateway.json config file.");
            }

        }

        public static IWebHostBuilder CreateWebHostBuilder(ApiMap[] args) =>
            WebHost.CreateDefaultBuilder()
                .UseHttpSys(ops =>
                {
                    foreach (var map in args)
                    {
                        ops.UrlPrefixes.Add(map.Bind.ToString());
                    }

                    Startup.Map = args;
                })
                .PreferHostingUrls(true)
                .ConfigureLogging(l => { l.SetMinimumLevel(LogLevel.Information); })
                .UseStartup<Startup>();
    }

    public class Startup
    {
        internal static ApiMap[] Map { get; set; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddProxy();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.RunProxy(context =>
            {
                var req = context.Request;
                var target = Map.FirstOrDefault(m => 
                    m.Bind.ToString().Equals(
                        $"{req.Scheme}://{req.Host.Host}:{req.Host.Port ?? 80}{req.PathBase}", 
                        StringComparison.OrdinalIgnoreCase));
                
                return context
                    .ForwardTo(target.Target)
                    .AddXForwardedHeaders()
                    .Send();
            });

        }
    }
}
