using DotNetEnv;
using WayFinaWebApp.Models;

namespace WayfinaMobileAppBot;

public class Program
{
    public static void Main(string[] args)
    {
        Env.Load();
        
        var settings = new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        };

        var builder = Host.CreateApplicationBuilder(settings);
        builder.Services.Configure<ZohoAppOptions>(builder.Configuration.GetSection("ZohoApp"));
        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();
        host.Run();
    }
}