using System.Data;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

using FredrikHr.AvinorAip.AipDownloader.Model;

namespace FredrikHr.AvinorAip.AipDownloader;

public class AipEnr2HttpClient(AipHttpClient aipClient)
{
    private const string xhtml = AipXHtmlDocument.XHtmlPrefix;

    public async Task LoadEnr2Dot1AtsAirspaces(
        Uri aipEnr2Dot1Uri,
        AipSDDataSet aipDataSet,
        CancellationToken cancelToken = default
        )
    {
        ArgumentNullException.ThrowIfNull(aipDataSet);
        AipXHtmlDocument xhtmlInfo = await aipClient.GetAipXHtmlDocumentAsync(
            aipEnr2Dot1Uri,
            cancelToken
            ).ConfigureAwait(continueOnCapturedContext: false);
        XDocument xdoc = xhtmlInfo.Dom;
        IXmlNamespaceResolver? xmlNsRes = xhtmlInfo.NamespaceResolver;

        IEnumerable<XElement> enr2dot1Rows = xdoc.XPathSelectElements(
            $"""/{xhtml}:html/{xhtml}:body//{xhtml}:div[@id = "ENR-2.1"]""" +
            $"""/{xhtml}:table/{xhtml}:tbody/{xhtml}:tr""",
            xmlNsRes
            );
        DataRow airspaceDataRow = null!;
        foreach (var airspaceRowElement in enr2dot1Rows)
        {
            var airspaceSubDataRows = aipDataSet
                .LoadXHtmlTableRow(airspaceRowElement, xmlNsRes, skipRelationships: true);
            var aispaceSubDataRowGroups = airspaceSubDataRows
                .GroupBy(airspaceSubDataRow => airspaceSubDataRow.Table);
            DataRow? airspacePolygonRow = null;
            foreach (var airspaceDataRowGrouping in aispaceSubDataRowGroups)
            {
                var tableName = airspaceDataRowGrouping.Key.TableName;
                switch (tableName)
                {
                    case "TAIRSPACE":
                        airspaceDataRow = airspaceDataRowGrouping.Single();
                        break;

                    case "TAIRSPACE_VERTEX":
                        DataColumn airspaceVertexRowIdDataColumn = airspaceDataRowGrouping.Key.PrimaryKey.Single();
                        DataColumn airspaceVertexNextDataColumn =
                            GetOrCreateAirspaceVertexNextDataColumn(airspaceDataRowGrouping.Key);
                        object? airspaceVertexFirstDataRowId = null;
                        DataRow? airspaceVertexPreviousDataRow = null;
                        foreach (var airspaceVertexCurrentDataRow in airspaceDataRowGrouping)
                        {
                            object airspaceVertexCurrentDataRowId = airspaceVertexCurrentDataRow[airspaceVertexRowIdDataColumn];
                            airspaceVertexFirstDataRowId ??= airspaceVertexCurrentDataRowId;
                            if (airspaceVertexPreviousDataRow is null)
                            {
                                airspaceVertexPreviousDataRow = airspaceVertexCurrentDataRow;
                                continue;
                            }

                            airspaceVertexPreviousDataRow[airspaceVertexNextDataColumn] =
                                airspaceVertexCurrentDataRowId;

                            airspaceVertexPreviousDataRow = airspaceVertexCurrentDataRow;
                        }
                        if (airspaceVertexPreviousDataRow is not null)
                        {
                            airspaceVertexPreviousDataRow[airspaceVertexNextDataColumn] =
                                airspaceVertexFirstDataRowId;
                        }
                        goto case "TAIRSPACE_VOLUME";

                    case "TAIRSPACE_VOLUME":
                    case "TAIRSPACE_LAYER_CLASS":
                        airspacePolygonRow ??= CreateAirspacePolygonDataRow(airspaceDataRow);
                        foreach (var airspaceGroupedDataRow in airspaceDataRowGrouping)
                        {
                            aipDataSet.CreateSDRelationshipRow(airspaceGroupedDataRow, airspacePolygonRow);
                            aipDataSet.CreateSDRelationshipRow(airspacePolygonRow, airspaceGroupedDataRow);
                        }
                        break;
                }
            }

            foreach (var airspaceSubDataRow in airspaceSubDataRows)
            {
                aipDataSet.CreateSDRelationshipRow(airspaceSubDataRow, airspaceDataRow);
                aipDataSet.CreateSDRelationshipRow(airspaceDataRow, airspaceSubDataRow);
            }
        }
    }

    private static DataRow CreateAirspacePolygonDataRow(DataRow airspaceDataRow)
    {
        var dataSet = airspaceDataRow.Table.DataSet as AipSDDataSet ??
            throw new InvalidOperationException($"No DataSet specified in {airspaceDataRow.Table.TableName} data row");
        DataTable airspacePolygonDataTable = GetOrCreateAirspacePolygonDataTable(dataSet);
        DataRow airspacePolygonDataRow = airspacePolygonDataTable.NewRow();
        airspacePolygonDataTable.Rows.Add(airspacePolygonDataRow);
        dataSet.CreateSDRelationshipRow(airspacePolygonDataRow, airspaceDataRow);
        dataSet.CreateSDRelationshipRow(airspaceDataRow, airspacePolygonDataRow);
        return airspacePolygonDataRow;
    }

    private static DataTable GetOrCreateAirspacePolygonDataTable(AipSDDataSet dataSet)
    {
        const string dataTableName = "TAIRSPACE_POLYGON";
        if (dataSet.Tables[dataTableName] is DataTable dataTable)
            return dataTable;
        dataTable = dataSet.CreateSDDataTable(dataTableName);
        DataColumn rowIdDataColumn = dataTable.Columns[0];
        rowIdDataColumn.AutoIncrement = true;
        rowIdDataColumn.AutoIncrementSeed = -1;
        rowIdDataColumn.AutoIncrementStep = -1;
        return dataTable;
    }

    private static DataColumn GetOrCreateAirspaceVertexNextDataColumn(DataTable dataTable)
    {
        string dataColumnName = $"NEXT_{dataTable.TableName}";
        if (dataTable.Columns[dataColumnName] is DataColumn dataColumn)
            return dataColumn;
        DataColumn rowIdColumn = dataTable.PrimaryKey.Single();
        dataColumn = dataTable.Columns.Add(dataColumnName, rowIdColumn.DataType);
        dataTable.ChildRelations.Add(
            $"{dataTable.TableName}_FK_{dataColumnName}",
            rowIdColumn,
            dataColumn,
            createConstraints: true
            );
        return dataColumn;
    }
}
