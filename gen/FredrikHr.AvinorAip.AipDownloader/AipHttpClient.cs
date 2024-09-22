using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

using FredrikHr.AvinorAip.AipDownloader.Model;

using Microsoft.Extensions.Options;

namespace FredrikHr.AvinorAip.AipDownloader;

public partial class AipHttpClient
{
    private readonly HttpClient httpClient;
    private readonly IOptions<XmlReaderSettings> xmlReaderOptions;

    public AipHttpClient(
        HttpClient httpClient,
        IOptions<XmlReaderSettings> xmlReaderOptions)
    {
        this.httpClient = httpClient;
        this.xmlReaderOptions = xmlReaderOptions;

        Enr2 = new(this);
        Enr4 = new(this);
    }

    public AipEnr2HttpClient Enr2 { get; }
    public AipEnr4HttpClient Enr4 { get; }

    public async Task<AipPublication> GetAipPublicationAsync(
        string languageCode,
        CancellationToken cancelToken = default)
    {
        string requUriString = FormattableString.Invariant(
            $"https://ais.avinor.no/{Uri.EscapeDataString(languageCode)}/AIP"
            );
        Uri requUri = new(requUriString);
        AipXHtmlDocument respAipDocument = await GetAipXHtmlDocumentAsync(
            requUri,
            cancelToken
            ).ConfigureAwait(continueOnCapturedContext: false);
        XDocument respDoc = respAipDocument.Dom;
        Uri respUri = respAipDocument.DocumentUri;
        IXmlNamespaceResolver? respXmlNs = respAipDocument.NamespaceResolver;

        const string xhtml = AipXHtmlDocument.XHtmlPrefix;
        const string xhtmlNs = AipXHtmlDocument.XHtmlUrn;
        XElement respAipPubTrElem = respDoc.XPathSelectElement(
            $"/{xhtml}:html/{xhtml}:body//{xhtml}:table/{xhtml}:tbody/{xhtml}:tr",
            respXmlNs
            ) ?? throw new InvalidOperationException();
        XElement[] respAipPubTdElems = respAipPubTrElem
            .Elements(XName.Get("td", xhtmlNs))
            .ToArray();
        XElement respAipPubEffectiveDateElem = respAipPubTdElems[0]
            .Element(XName.Get("a", xhtmlNs))
            ?? throw new InvalidOperationException();
        Uri respAipPubIndexUri =
            new(respUri, respAipPubEffectiveDateElem.Attribute("href")?.Value);
        string respAipPubIndexFileName = respAipPubIndexUri.Segments.Last();
        string respAipPubCultureTag = GetAipFileNameCultureTagRegex()
            .Match(respAipPubIndexFileName).Value;

        return new()
        {
            IndexUri = respAipPubIndexUri,
            EffectiveDate = DateOnly.Parse(
                respAipPubEffectiveDateElem.Value,
                CultureInfo.InvariantCulture),
            PublicationDate = DateOnly.Parse(
                respAipPubTdElems[1].Value,
                CultureInfo.InvariantCulture),
            Reason = respAipPubTdElems[2].Value,
            CultureTag = respAipPubCultureTag,
        };
    }

    public async Task<AipIndex> GetAipIndexAsync(
        AipPublication aipPublication,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(aipPublication);

        Uri requUri = aipPublication.IndexUri;
        AipXHtmlDocument respAipDocument = await GetAipXHtmlDocumentAsync(
            requUri,
            cancelToken
            ).ConfigureAwait(continueOnCapturedContext: false);
        XDocument respDoc = respAipDocument.Dom;
        Uri respUri = respAipDocument.DocumentUri;
        IXmlNamespaceResolver? respXmlNs = respAipDocument.NamespaceResolver;

        const string xhtml = AipXHtmlDocument.XHtmlPrefix;
        string respNavFrameHref = respDoc.XPathSelectElement(
            $"""/{xhtml}:html/{xhtml}:frameset/{xhtml}:frame[@name = "eAISNavigationBase"]""",
            respXmlNs)?.Attribute("src")?.Value
            ?? throw new InvalidOperationException();
        Uri respNavFramesetUri = new(respUri, respNavFrameHref);
        string respContentFrameHref = respDoc.XPathSelectElement(
            $"""/{xhtml}:html/{xhtml}:frameset/{xhtml}:frame[@name = "eAISContent"]""",
            respXmlNs)?.Attribute("src")?.Value
            ?? throw new InvalidOperationException();
        Uri respCoverUri = new(respUri, respContentFrameHref);

        AipXHtmlDocument respAipNavFrameset =
            await GetAipXHtmlDocumentAsync(respNavFramesetUri, cancelToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        respNavFrameHref = respAipNavFrameset.Dom.XPathSelectElement(
            $"""/{xhtml}:html/{xhtml}:frameset/{xhtml}:frame[@name = "eAISNavigation"]""",
            respXmlNs)?.Attribute("src")?.Value
            ?? throw new InvalidOperationException();
        Uri respNavUri = new(respAipNavFrameset.DocumentUri, respNavFrameHref);

        return new()
        {
            MenuUri = respNavUri,
            CoverUri = respCoverUri,
        };
    }

    public async Task<ReadOnlyDictionary<string, AipNavigationMenuItem>> GetAipNavigationMenuAsync(
        AipIndex aipIndex,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(aipIndex);

        Uri requUri = aipIndex.MenuUri;
        AipXHtmlDocument respAipXhtmlDoc =
            await GetAipXHtmlDocumentAsync(requUri, cancelToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        Uri respBaseUri = respAipXhtmlDoc.DocumentUri;
        const string xhtml = AipXHtmlDocument.XHtmlPrefix;
        IEnumerable<XElement> respMenuAnchorElems = respAipXhtmlDoc.Dom
            .XPathSelectElements(
                $"""/{xhtml}:html/{xhtml}:body//{xhtml}:div[not(@class = "tab")]/{xhtml}:a[not(@class = "Plus")]""",
                respAipXhtmlDoc.NamespaceResolver
            );
        Dictionary<string, AipNavigationMenuItem> items =
            new(StringComparer.InvariantCultureIgnoreCase);
        foreach (XElement respMenuAnchorElem in respMenuAnchorElems)
        {
            string id = respMenuAnchorElem.Attribute("id")?.Value!;
            string title = respMenuAnchorElem.Attribute("title")?.Value!;
            string href = respMenuAnchorElem.Attribute("href")?.Value!;
            Uri uri = new(respBaseUri, href);
            items[id] = new() { Id = id, Title = title, Uri = uri };
        }
        return items.AsReadOnly();
    }

    public async Task<AipXHtmlDocument> GetAipXHtmlDocumentAsync(
        Uri requUri,
        CancellationToken cancelToken = default)
    {
        using HttpResponseMessage respMsg = await httpClient.GetAsync(
            requUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancelToken
            ).ConfigureAwait(continueOnCapturedContext: false);
        respMsg.EnsureSuccessStatusCode();
        HttpContent respHttpContent = respMsg.Content;
        Uri respUri = respMsg.RequestMessage?.RequestUri ?? requUri;
        string respUriString = respUri.ToString();
        XmlReaderSettings xmlReaderSettings = xmlReaderOptions.Value;
        using Stream respStream = await respHttpContent
            .ReadAsStreamAsync(cancelToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        using XmlReader respXmlReader = TryGetHttpContentEncoding(respHttpContent, out var respEnc)
            ? XmlReader.Create(
                new StreamReader(respStream, respEnc),
                xmlReaderSettings,
                respUriString
                )
            : XmlReader.Create(
                respStream,
                xmlReaderSettings,
                respUriString
                );
        var respXhtmlDocument = XDocument.Load(respXmlReader);
        XmlNamespaceManager respXmlNsMgr = new(respXmlReader.NameTable);
        respXmlNsMgr.AddNamespace(
            AipXHtmlDocument.XHtmlPrefix,
            AipXHtmlDocument.XHtmlUrn);
        return new()
        {
            DocumentUri = respUri,
            Dom = respXhtmlDocument,
            NamespaceResolver = respXmlNsMgr,
        };
    }

    private static bool TryGetHttpContentEncoding(
        HttpContent content,
        [NotNullWhen(true)] out Encoding? encoding)
    {
        var ct = content.Headers.ContentType;
        if (ct?.CharSet is string charset)
        {
            try
            {
                encoding = Encoding.GetEncoding(charset);
                return true;
            }
            catch (ArgumentException) { }
        }

        encoding = null;
        return false;
    }

    [GeneratedRegex("""(?<=^index-).+(?=\.html$)""",
        RegexOptions.Singleline |
        RegexOptions.IgnoreCase
        )]
    private static partial Regex GetAipFileNameCultureTagRegex();

    public async Task<int> LoadAipXHtmlDocumentTableLevel1Async(
        Uri requUri,
        string tableRowsXPath,
        AipSDDataSet aipDataSet,
        CancellationToken cancelToken = default
        )
    {
        AipXHtmlDocument xhtmlInfo = await GetAipXHtmlDocumentAsync(
            requUri,
            cancelToken
            ).ConfigureAwait(continueOnCapturedContext: false);
        XDocument xdoc = xhtmlInfo.Dom;
        IXmlNamespaceResolver? xmlNsRes = xhtmlInfo.NamespaceResolver;
        IEnumerable<XElement> tableRows = xdoc.XPathSelectElements(
            tableRowsXPath,
            xmlNsRes
            );
        return tableRows
            .Select(tableRowElement => aipDataSet.LoadXHtmlTableRow(tableRowElement, xmlNsRes))
            .Sum(dataRows => dataRows.Count);
    }
}
