using System.Xml;
using System.Xml.Linq;

namespace FredrikHr.AvinorAip.AipDownloader.Model;

public class AipXHtmlDocument
{
    public const string XHtmlPrefix = "xhtml";
    public const string XHtmlUrn = "http://www.w3.org/1999/xhtml";

    public required Uri DocumentUri { get; init; }
    public IXmlNamespaceResolver? NamespaceResolver { get; init; }
    public required XDocument Dom { get; init; }
}
