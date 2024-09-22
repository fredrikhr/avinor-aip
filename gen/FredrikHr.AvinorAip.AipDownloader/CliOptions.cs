using System.CommandLine.Parsing;

namespace FredrikHr.AvinorAip.AipDownloader;

internal class CliOptions(CliDefinition definition, ParseResult cli)
{
    public string LanguageCode { get; } =
        cli.GetValueForOption(definition.LanguageOption)!;
}
