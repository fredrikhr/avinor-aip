namespace FredrikHr.AvinorAip.AipDownloader.Model;

public class AipPublication
{
    public required DateOnly EffectiveDate { get; init; }
    public required DateOnly PublicationDate { get; init; }
    public string? Reason { get; init; }
    public required Uri IndexUri { get; init; }
    public required string CultureTag { get; init; }
}
