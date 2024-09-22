using System.Data;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

using FredrikHr.AvinorAip.AipDownloader.Model;

namespace FredrikHr.AvinorAip.AipDownloader;

public class AipSDDataSet : DataSet
{
    private static readonly char[] semiSeparator = [';'];
    private const string rowIdName = "ROWID";
    private const string relsTableName = "TRELATIONSHIPS";

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1002: Do not expose generic lists",
        Justification = nameof(Enumerable.Distinct))]
    public List<DataRow> LoadXHtmlTableRow(
        XElement tableRowElement,
        IXmlNamespaceResolver? xmlNsRes,
        bool skipRelationships = false
        )
    {
        string xhtml = AipXHtmlDocument.XHtmlPrefix;
        IEnumerable<XElement> sdElems = tableRowElement.XPathSelectElements(
            $""".//{xhtml}:span[@class = "SD"]""",
            xmlNsRes
            );
        var sdDataRows = sdElems
            .Select(sdSpan => LoadXHtmlTableDataSD(sdSpan, xmlNsRes))
            .Distinct()
            .ToList();
        if (!skipRelationships)
        {
            for (int i = 0; i < sdDataRows.Count; i++)
            {
                DataRow sdDataRow = sdDataRows[i];
                foreach (DataRow sdRelatedDataRow in sdDataRows.Take(i).Concat(sdDataRows.Skip(i + 1)))
                {
                    CreateSDRelationshipRow(sdDataRow, sdRelatedDataRow);
                }
            }
        }
        return sdDataRows;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000: Dispose objects before losing scope",
        Justification = nameof(DataSet))]
    private DataRow LoadXHtmlTableDataSD(
        XElement sdSpanElement,
        IXmlNamespaceResolver? xmlNsRes
        )
    {
        string xhtml = AipXHtmlDocument.XHtmlPrefix;
        string sdString = sdSpanElement.Value;
        XElement sdParamsSpanElem = sdSpanElement.XPathSelectElement(
            $"""following-sibling::{xhtml}:span[@class = "sdParams"]""",
            xmlNsRes
            ) ?? throw new InvalidOperationException();
        string sdParamsString = sdParamsSpanElem.Value;
        var sdParamsParts = sdParamsString.Split(semiSeparator, count: 3);
        string sdTableName = sdParamsParts[0];
        string sdColumnName = sdParamsParts[1];
        int sdTableRowId = int.Parse(sdParamsParts[2], NumberStyles.Integer, CultureInfo.InvariantCulture);

        DataTable dataTable = Tables[sdTableName] ?? CreateSDDataTable(sdTableName);
        DataColumn dataColumn = dataTable.Columns[sdColumnName] ?? CreateSDDataColumn(dataTable, sdColumnName);
        DataRow dataRow = dataTable.Rows.Find(sdTableRowId) ?? CreateSDDataRow(dataTable, sdTableRowId);
        dataRow[dataColumn] = sdString;
        return dataRow;
    }

    public DataTable CreateSDDataTable(string sdTableName)
    {
        string sdCaption = sdTableName[1..];
        DataTable dataTable = Tables.Add(sdTableName);
        dataTable.ExtendedProperties[nameof(DataColumn.Caption)] = sdCaption;
        DataColumn idColumn = dataTable.Columns.Add(rowIdName, typeof(int));
        idColumn.Unique = true;
        dataTable.PrimaryKey = [idColumn];
        return dataTable;
    }

    private static DataColumn CreateSDDataColumn(DataTable dataTable, string sdColumnName)
    {
        DataColumn dataColumn = dataTable.Columns.Add(sdColumnName);
        return dataColumn;
    }

    private static DataRow CreateSDDataRow(DataTable dataTable, int sdTableRowId)
    {
        DataColumn idColumn = dataTable.Columns[rowIdName] ??
            throw new InvalidOperationException($"SD DataTable does not contain required column '{rowIdName}'.");
        DataRow dataRow = dataTable.NewRow();
        dataRow[idColumn] = sdTableRowId;
        dataTable.Rows.Add(dataRow);
        return dataRow;
    }

    public DataRow CreateSDRelationshipRow(
        DataRow sourceRow,
        DataRow relatedRow
        )
    {
        ArgumentNullException.ThrowIfNull(sourceRow);
        ArgumentNullException.ThrowIfNull(relatedRow);
        DataTable relsTable = GetOrCreateSDRelationshipTable();
        object[] relsRowValues = [
            sourceRow.Table.TableName,
            sourceRow[rowIdName],
            relatedRow.Table.TableName,
            relatedRow[rowIdName],
            ];
        DataRow relsRow = relsTable.Rows.Find(relsRowValues) ??
            NewRelsDataRow(relsTable, relsRowValues);
        return relsRow;

        static DataRow NewRelsDataRow(DataTable dataTable, object[] rowValues)
        {
            DataRow dataRow = dataTable.NewRow();
            dataRow.ItemArray = rowValues;
            dataTable.Rows.Add(dataRow);
            return dataRow;
        }
    }

    private DataTable GetOrCreateSDRelationshipTable()
    {
        if (Tables[relsTableName] is DataTable relsTable)
            return relsTable;
        relsTable = Tables.Add(relsTableName);
        DataColumn[] relsColumns = [
            relsTable.Columns.Add("TSOURCE", typeof(string)),
            relsTable.Columns.Add("ROWID_SOURCE", typeof(int)),
            relsTable.Columns.Add("TRELATED", typeof(string)),
            relsTable.Columns.Add("ROWID_RELATED", typeof(int)),
            ];
        foreach (DataColumn relsColumn in relsColumns)
        {
            relsColumn.AllowDBNull = false;
        }
        relsTable.PrimaryKey = relsColumns;
        return relsTable;
    }

    public void ReplaceSDRelationshipTableWithDataSetRelations()
    {
        var relsTable = GetOrCreateSDRelationshipTable();
        var relsGroups = relsTable.AsEnumerable()
            .GroupBy(
                static dataRow => (
                    sourceDataTable: (string)dataRow["TSOURCE"],
                    relatedDataTable: (string)dataRow["TRELATED"]
                ),
                static (key, dataRows) => (
                    key.sourceDataTable,
                    key.relatedDataTable,
                    dataRows: dataRows.ToList(),
                    //allSingleRelatedRow: dataRows.GroupBy(
                    //    static dataRow => (int)dataRow["ROWID_SOURCE"],
                    //    (sourceDataRow, dataRows) => dataRows.Count()
                    //    ).All(count => count <= 1),
                    perSourceRelatedCounts: new Dictionary<int, int>(
                        dataRows.GroupBy(
                            static dataRow => (int)dataRow["ROWID_SOURCE"],
                            (sourceDataRow, dataRows) => KeyValuePair.Create(sourceDataRow, dataRows.Count())
                        )
                        .Where(kvp => kvp.Value > 1)
                    )
                )
            )
            .OrderBy(g => g.sourceDataTable)
            .ThenBy(g => g.relatedDataTable)
            .ToList();
        foreach (var (sourceDataTableName, relatedDataTableName, relsDataRows, perSourceRelatedCounts) in relsGroups)
        {
            bool isChildToParentRelation = perSourceRelatedCounts.Count == 0;
            if (sourceDataTableName == relatedDataTableName)
            {
                foreach (var relsDataRow in relsDataRows)
                    if (relsDataRow.RowState != DataRowState.Detached)
                        relsTable.Rows.Remove(relsDataRow);
            }
            else if (isChildToParentRelation)
            {
                var sourceDataTable = Tables[sourceDataTableName]!;
                var sourceFkDataColumn = sourceDataTable.Columns[relatedDataTableName] ??
                    sourceDataTable.Columns.Add(relatedDataTableName, typeof(int));
                var relatedDataTable = Tables[relatedDataTableName]!;
                var relatedRowIdColumn = relatedDataTable.Columns[rowIdName]!;
                var sourceToParentRelation = sourceDataTable.ParentRelations
                    .Add(
                        $"{sourceDataTableName}_FK_{relatedDataTableName}",
                        relatedRowIdColumn,
                        sourceFkDataColumn,
                        createConstraints: true
                        );
                foreach (var relsDataRow in relsDataRows)
                {
                    object sourceRowIdValue = relsDataRow["ROWID_SOURCE"];
                    object relatedRowIdValue = relsDataRow["ROWID_RELATED"];
                    DataRow sourceDataRow = sourceDataTable.Rows.Find(sourceRowIdValue)!;
                    sourceDataRow[sourceFkDataColumn] = relatedRowIdValue;
                    relsTable.Rows.Remove(relsDataRow);
                }
            }
            else if (
                relsGroups.Any(g =>
                    g.relatedDataTable == sourceDataTableName &&
                    g.sourceDataTable == relatedDataTableName &&
                    g.perSourceRelatedCounts.Count != 0
                ) == false)
            {
                foreach (var relsDataRow in relsDataRows)
                    relsTable.Rows.Remove(relsDataRow);
            }
            else
            {
                

                string m2mRelsTableName = $"M2M{sourceDataTableName}_{relatedDataTableName}";
                string revm2mRelsTableName = $"M2M{relatedDataTableName}_{sourceDataTableName}";
                DataTable m2mRelsDataTable = Tables[m2mRelsTableName] ??
                    Tables[revm2mRelsTableName] ??
                    SetupM2MRelationTable(m2mRelsTableName, Tables[sourceDataTableName]!, Tables[relatedDataTableName]!);
                var sourceFkDataColumn = m2mRelsDataTable.Columns[sourceDataTableName]!;
                var relatedFkDataColumn = m2mRelsDataTable.Columns[relatedDataTableName]!;
                foreach (var relsDataRow in relsDataRows)
                {
                    object sourceRowIdValue = relsDataRow["ROWID_SOURCE"];
                    object relatedRowIdValue = relsDataRow["ROWID_RELATED"];
                    object[] m2mRelDataRowValues = new object[2];
                    m2mRelDataRowValues[sourceFkDataColumn.Ordinal] = sourceRowIdValue;
                    m2mRelDataRowValues[relatedFkDataColumn.Ordinal] = relatedRowIdValue;
                    if (m2mRelsDataTable.Rows.Find(m2mRelDataRowValues) is not DataRow m2mRelDataRow)
                    {
                        m2mRelDataRow = m2mRelsDataTable.NewRow();
                        m2mRelDataRow.ItemArray = m2mRelDataRowValues;
                        m2mRelsDataTable.Rows.Add(m2mRelDataRow);
                    }
                    relsTable.Rows.Remove(relsDataRow);
                }

                static DataTable SetupM2MRelationTable(string m2mRelsTableName, DataTable sourceDataTable, DataTable relatedDataTable)
                {
                    DataTable m2mRelsDataTable = sourceDataTable.DataSet!.Tables.Add(m2mRelsTableName);
                    DataColumn sourceFkDataColumn = m2mRelsDataTable
                        .Columns.Add(sourceDataTable.TableName, typeof(int));
                    DataColumn relatedFkDataColumn = m2mRelsDataTable
                        .Columns.Add(relatedDataTable.TableName, typeof(int));
                    m2mRelsDataTable.PrimaryKey = [sourceFkDataColumn, relatedFkDataColumn];
                    DataColumn sourceRowIdColumn = sourceDataTable.Columns[rowIdName]!;
                    DataColumn relatedRowIdColumn = relatedDataTable.Columns[rowIdName]!;
                    m2mRelsDataTable.ParentRelations.Add(
                        $"{m2mRelsTableName}_FK_{sourceDataTable.TableName}",
                        sourceRowIdColumn,
                        sourceFkDataColumn,
                        createConstraints: true
                        );
                    m2mRelsDataTable.ParentRelations.Add(
                        $"{m2mRelsTableName}_FK_{relatedDataTable.TableName}",
                        relatedRowIdColumn,
                        relatedFkDataColumn,
                        createConstraints: true
                        );
                    return m2mRelsDataTable;
                }
            }
        }
        System.Diagnostics.Debug.Assert(relsTable.Rows.Count == 0);
        Tables.Remove(relsTable);
    }
}
