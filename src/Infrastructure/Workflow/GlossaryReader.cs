using ClosedXML.Excel;

namespace Vid2Sub.Infrastructure.Workflow;

public sealed class GlossaryReader
{
    public Task<Glossary> ReadAsync(
        string path,
        string sheet,
        string keyColumn,
        string translationColumn,
        string? notesColumn,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Workbook not found", path);
        }

        using var workbook = new XLWorkbook(path);
        if (!workbook.TryGetWorksheet(sheet, out var worksheet))
        {
            throw new InvalidDataException($"Sheet not found: {sheet}");
        }

        var header = worksheet.FirstRowUsed()
            ?? throw new InvalidDataException($"Sheet has no header row: {sheet}");
        var columnMap = BuildColumnMap(header);
        var keyIndex = ResolveColumn(columnMap, keyColumn);
        var translationIndex = ResolveColumn(columnMap, translationColumn);
        int? notesIndex = string.IsNullOrWhiteSpace(notesColumn)
            ? null
            : ResolveColumn(columnMap, notesColumn);

        var entries = new List<GlossaryEntry>();
        var warnings = new List<ToolWarning>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? header.RowNumber();

        for (var rowNumber = header.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            var key = WorkbookInspector.NormalizeCell(row.Cell(keyIndex).GetString());
            if (string.IsNullOrWhiteSpace(key))
            {
                warnings.Add(new ToolWarning(
                    GlossaryWarningCodes.EmptyKey,
                    $"Skipped empty key at row {rowNumber}.",
                    rowNumber.ToString()));
                continue;
            }

            if (!seenKeys.Add(key))
            {
                warnings.Add(new ToolWarning(
                    GlossaryWarningCodes.DuplicateKey,
                    $"Skipped duplicate key at row {rowNumber}: {key}",
                    key));
                continue;
            }

            var translation = WorkbookInspector.NormalizeCell(row.Cell(translationIndex).GetString());
            var notes = notesIndex is null
                ? null
                : WorkbookInspector.NormalizeCell(row.Cell(notesIndex.Value).GetString());

            entries.Add(new GlossaryEntry(key, translation, string.IsNullOrEmpty(notes) ? null : notes));
        }

        return Task.FromResult(new Glossary(entries, warnings));
    }

    private static Dictionary<string, int> BuildColumnMap(IXLRow header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in header.CellsUsed())
        {
            var name = WorkbookInspector.NormalizeCell(cell.GetString());
            if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name))
            {
                map.Add(name, cell.Address.ColumnNumber);
            }
        }

        return map;
    }

    private static int ResolveColumn(IReadOnlyDictionary<string, int> columnMap, string name)
    {
        if (columnMap.TryGetValue(name.Trim(), out var index))
        {
            return index;
        }

        throw new InvalidDataException($"Column not found: {name}");
    }
}
