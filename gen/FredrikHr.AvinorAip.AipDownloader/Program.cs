using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using System.Data;
using System.Net;
using System.Xml;
using System.Xml.Resolvers;

using FredrikHr.AvinorAip.AipDownloader;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

CliDefinition cliDef = new();
CommandLineBuilder cliBuilder = new(cliDef.CliCommand);
var cliPipeline = cliBuilder
    .UseDefaults()
    .UseHost(Host.CreateDefaultBuilder, ConfigureHost)
    .Build();
return await cliPipeline.InvokeAsync(args ?? [])
    .ConfigureAwait(continueOnCapturedContext: false);

void ConfigureHost(IHostBuilder host)
{
    host.ConfigureServices(ConfigureServices);

    cliDef.ConfigureHost(host);
}

static void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<CookieContainer>();
    services.AddHttpClient<AipHttpClient>()
        .ConfigurePrimaryHttpMessageHandler((serviceProvider) =>
        {
            var cookies = serviceProvider.GetRequiredService<CookieContainer>();
            return new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                UseCookies = true,
                CookieContainer = cookies,
            };
        });
    services.AddSingleton<XmlResolver>(new XmlPreloadedResolver(XmlKnownDtds.Xhtml10));
    services.AddOptions<XmlReaderSettings>()
        .PostConfigure<XmlResolver>((xmlReaderSettings, xmlResolver) =>
        {
            xmlReaderSettings.XmlResolver = xmlResolver;
            xmlReaderSettings.DtdProcessing = DtdProcessing.Parse;
        });
    services.AddSingleton<AipSDDataSet>();
}
