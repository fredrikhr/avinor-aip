using System.CommandLine;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FredrikHr.AvinorAip.AipDownloader;

internal class CliDefinition
{
    public Command CliCommand { get; }
    public Option<string> LanguageOption { get; }

    public CliDefinition()
    {
        LanguageOption = new("--language")
        {
            Arity = ArgumentArity.ZeroOrOne,
        };
        LanguageOption.AddAlias("-l");
        LanguageOption.FromAmong("no", "en");
        LanguageOption.SetDefaultValue("en");

        CliCommand = new RootCommand("Avinor AIP Downloader")
        {
            LanguageOption,
        };
        CliCommand.SetHandler(InvokeCommandHandler);
    }

    public void ConfigureHost(IHostBuilder host)
    {
        host.ConfigureServices(ConfigureServices);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(this);
        services.AddSingleton<CliOptions>();
        services.AddHostedService<CliHostedService>();
    }

    private Task InvokeCommandHandler(InvocationContext context)
    {
        var host = context.GetHost();
        return host.WaitForShutdownAsync();
    }
}
