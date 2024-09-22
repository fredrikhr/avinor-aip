using System.Diagnostics;

namespace FredrikHr.AvinorAip.AipDownloader.Model;

[DebuggerDisplay($"{{{nameof(Id)},nq}} {{{nameof(Title)},nq}}")]
public class AipNavigationMenuItem()
{
    public required string Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public required Uri Uri { get; init; }
}
