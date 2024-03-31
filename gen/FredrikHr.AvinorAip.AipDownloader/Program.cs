using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

RootCommand cliRoot = new("Avinor AIP Downloader");
cliRoot.SetHandler(static (InvocationContext context) =>
{
    IHost host = context.GetHost();
    IServiceProvider sp = host.Services;
    var runner = sp.GetRequiredService<CliRunner>();
    return runner.RunCommand();
});
CommandLineBuilder cliBuilder = new(cliRoot);
var cliPipeline = cliBuilder
    .UseDefaults()
    .UseHost(Host.CreateDefaultBuilder, ConfigureHost)
    .Build();
return await cliPipeline.InvokeAsync(args ?? Array.Empty<string>())
    .ConfigureAwait(continueOnCapturedContext: false);

void ConfigureHost(IHostBuilder host)
{
    var cliContext = host.GetInvocationContext();
    var cliParseResult = cliContext.ParseResult;
    host.ConfigureServices(static (hostContext, services) =>
    {
        services.AddSingleton<CliRunner>();
    });
}
