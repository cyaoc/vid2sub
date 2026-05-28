using ClosedXML.Excel;

namespace Vid2Sub.Infrastructure.Workflow;

public sealed class WorkbookInspector
{
    public Task<WorkbookInspection> InspectAsync(
        string path,
        int sampleRowCount = 3,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Workbook not found", path);
        }

        using var workbook = new XLWorkbook(path);
        var sheets = workbook.Worksheets
            .Select(sheet => InspectSheet(sheet, sampleRowCount))
            .ToList();

        return Task.FromResult(new WorkbookInspection(sheets));
    }

    private static WorkbookSheetInfo InspectSheet(IXLWorksheet sheet, int sampleRowCount)
    {
        var headerRow = sheet.FirstRowUsed();
        if (headerRow is null)
        {
            return new WorkbookSheetInfo(sheet.Name, [], []);
        }

        var columns = headerRow.CellsUsed()
            .Select(cell => new WorkbookColumn(NormalizeCell(cell.GetString()), cell.Address.ColumnNumber))
            .Where(column => !string.IsNullOrEmpty(column.Name))
            .ToList();

        var sampleRows = new List<IReadOnlyDictionary<string, string>>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        for (var rowNumber = headerRow.RowNumber() + 1;
             rowNumber <= lastRow && sampleRows.Count < sampleRowCount;
             rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hasValue = false;
            foreach (var column in columns)
            {
                var value = NormalizeCell(row.Cell(column.Number).GetString());
                if (!string.IsNullOrEmpty(value))
                {
                    hasValue = true;
                }

                values[column.Name] = value;
            }

            if (hasValue)
            {
                sampleRows.Add(values);
            }
        }

        return new WorkbookSheetInfo(sheet.Name, columns.Select(column => column.Name).ToList(), sampleRows);
    }

    internal static string NormalizeCell(string? value) => value?.Trim() ?? string.Empty;

    private sealed record WorkbookColumn(string Name, int Number);
}
