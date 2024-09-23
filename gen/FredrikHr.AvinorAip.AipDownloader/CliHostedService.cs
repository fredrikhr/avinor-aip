using System.Data;

using FredrikHr.AvinorAip.AipDownloader;
using FredrikHr.AvinorAip.AipDownloader.Model;

using Microsoft.Extensions.Hosting;

internal class CliHostedService(
    AipHttpClient aipClient,
    CliOptions options,
    IHostApplicationLifetime lifetime,
    AipSDDataSet aipDataSet
    ) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        AipPublication pub = await aipClient
            .GetAipPublicationAsync(options.LanguageCode, stoppingToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        AipIndex index = await aipClient
            .GetAipIndexAsync(pub, stoppingToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        var menu = await aipClient
            .GetAipNavigationMenuAsync(index, stoppingToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var enr2dot1 = menu["ENR-2.1"];
        await aipClient.Enr2
            .LoadEnr2Dot1AtsAirspaces(enr2dot1.Uri, aipDataSet, stoppingToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var enr2dot2 = menu["ENR-2.2"];
        await aipClient.Enr2
            .LoadEnr2Dot2AtsAirspaces(enr2dot2.Uri, aipDataSet, stoppingToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var enr4dot1 = menu["ENR-4.1"];
        await aipClient.Enr4
            .LoadRadioNavigationAids(enr4dot1.Uri, aipDataSet, stoppingToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var enr4dot4 = menu["ENR-4.4"];
        await aipClient.Enr4
            .LoadReportingWaypoints(enr4dot4.Uri, aipDataSet, stoppingToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        aipDataSet.ReplaceSDRelationshipTableWithDataSetRelations();

        lifetime.StopApplication();
    }
}
