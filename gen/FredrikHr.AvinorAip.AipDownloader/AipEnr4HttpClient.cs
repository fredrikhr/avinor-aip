using System.Xml.Linq;
using System.Xml;

using FredrikHr.AvinorAip.AipDownloader.Model;
using System.Xml.XPath;

namespace FredrikHr.AvinorAip.AipDownloader;

public class AipEnr4HttpClient(AipHttpClient aipClient)
{
    private const string xhtml = AipXHtmlDocument.XHtmlPrefix;

    public Task<int> LoadRadioNavigationAids(
        Uri aipEnr4Dot1Uri,
        AipSDDataSet aipDataSet,
        CancellationToken cancelToken = default
        ) => aipClient.LoadAipXHtmlDocumentTableLevel1Async(
            aipEnr4Dot1Uri,
            $"""/{xhtml}:html/{xhtml}:body//{xhtml}:div[@id = "ENR-4.1"]""" +
            $"""/{xhtml}:table/{xhtml}:tbody/{xhtml}:tr""",
            aipDataSet,
            cancelToken
            );
    
    public async Task LoadReportingWaypoints(
        Uri aipEnr4Dot4Uri,
        AipSDDataSet aipDataSet,
        CancellationToken cancelToken = default
        )
    {
        ArgumentNullException.ThrowIfNull(aipDataSet);
        AipXHtmlDocument xhtmlInfo = await aipClient.GetAipXHtmlDocumentAsync(
            aipEnr4Dot4Uri,
            cancelToken
            ).ConfigureAwait(continueOnCapturedContext: false);
        XDocument xdoc = xhtmlInfo.Dom;
        IXmlNamespaceResolver? xmlNsRes = xhtmlInfo.NamespaceResolver;

        IEnumerable<XElement> enr4dot4dot1Rows = xdoc.XPathSelectElements(
            $"""/{xhtml}:html/{xhtml}:body//{xhtml}:div[@id = "ENR-4.4"]""" +
            $"""/{xhtml}:div[@id = "ENR-4.4.1"]""" +
            $"""/{xhtml}:table/{xhtml}:tbody/{xhtml}:tr""",
            xmlNsRes
            );
        foreach(var enr4dot4dot1RowElement in enr4dot4dot1Rows)
        {
            _ = aipDataSet.LoadXHtmlTableRow(enr4dot4dot1RowElement, xmlNsRes);
        }
        
        IEnumerable<XElement> enr4dot4dot2SigpointRows = xdoc.XPathSelectElements(
            $"""/{xhtml}:html/{xhtml}:body//{xhtml}:div[@id = "ENR-4.4"]""" +
            $"""/{xhtml}:div[@id = "ENR-4.4.2"]""" +
            $"""/{xhtml}:table/{xhtml}:tbody/{xhtml}:tr[@class = "sigpoint"]""",
            xmlNsRes
            );
        foreach (var enr4dot4dot2SigpointRowElement in enr4dot4dot2SigpointRows)
        {
            var sigpointDataRows = aipDataSet.LoadXHtmlTableRow(enr4dot4dot2SigpointRowElement, xmlNsRes);
            IEnumerable<XElement> subRowsElements = enr4dot4dot2SigpointRowElement
                .NodesAfterSelf()
                .OfType<XElement>()
                .TakeWhile(trElem => trElem.Attribute("class")?.Value != "sigpoint");
            var subDataRows = subRowsElements
                .SelectMany(subRowElement => aipDataSet.LoadXHtmlTableRow(subRowElement, xmlNsRes))
                .ToList();
            foreach (var sigpointDataRow in sigpointDataRows)
            {
                foreach (var subDataRow in subDataRows)
                {
                    aipDataSet.CreateSDRelationshipRow(sigpointDataRow, subDataRow);
                    aipDataSet.CreateSDRelationshipRow(subDataRow, sigpointDataRow);
                }
            }
        }
    }
}
