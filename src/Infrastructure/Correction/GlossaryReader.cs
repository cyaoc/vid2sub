using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Vid2Sub.Infrastructure.Correction;

/// <summary>
/// Reads the correct spellings of names and terms for the corrector. A glossary workbook usually pairs each
/// term with its translation; only the column in the language of the subtitles helps a proofreader, so that
/// one column is taken and the rest is left out.
/// </summary>
public static class GlossaryReader
{
    private static readonly XNamespace Sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    /// <summary>
    /// Returns the terms without duplicates. A text file holds one term per line. In an .xlsx workbook the first
    /// row names the columns and <paramref name="column"/> picks one of them; a workbook with a single column
    /// needs no name. A cell that holds several lines gives one term per line.
    /// </summary>
    public static IReadOnlyList<string> Read(string path, string? column = null)
    {
        if (!path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Terms(File.ReadLines(path));
        }

        // For each sheet, its rows and the columns that hold anything.
        var sheets = Sheets(path)
            .Select(rows => (Rows: rows, Used: Enumerable.Range(0, rows.Max(row => row.Count)).Where(index => rows.Any(row => Cell(row, index).Length > 0)).ToList()))
            .ToList();
        var severalColumns = sheets.Where(sheet => sheet.Used.Count > 1).ToList();

        // Without a name there is nothing to choose only when no sheet has a second column: a sheet of notes
        // beside the sheet of terms must not be taken for the glossary. In a single-column workbook the first
        // row may be a header or a term; a header among the terms does no harm.
        var cells = string.IsNullOrWhiteSpace(column)
            ? severalColumns.Count > 0 ? [] : sheets.SelectMany(sheet => sheet.Rows.Select(row => Cell(row, sheet.Used[0])))
            : from sheet in sheets
              let chosen = sheet.Rows[0].FindIndex(name => string.Equals(name, column.Trim(), StringComparison.OrdinalIgnoreCase))
              where chosen >= 0
              from row in sheet.Rows.Skip(1)
              select Cell(row, chosen);

        var terms = Terms(cells);
        if (terms.Count > 0)
        {
            return terms;
        }

        static string Headers(IEnumerable<(List<List<string>> Rows, List<int> Used)> of) =>
            string.Join(", ", of.SelectMany(sheet => sheet.Rows[0]).Where(name => name.Length > 0).Distinct());

        var file = Path.GetFileName(path);
        throw new InvalidDataException(
            sheets.Count == 0 ? $"the glossary {file} is empty"
            : string.IsNullOrWhiteSpace(column) ? $"the glossary {file} has several columns ({Headers(severalColumns)}); name the one that holds the terms with -G / --glossary-column or correction.glossary_column"
            : $"the glossary {file} has no terms in a column named \"{column}\"; its columns are: {Headers(sheets)}");
    }

    // Several terms written into one cell with line breaks count separately; a tab would read as a column to the model.
    private static List<string> Terms(IEnumerable<string> cells) =>
        [.. cells.SelectMany(cell => cell.ReplaceLineEndings("\n").Split('\n'))
            .Select(term => term.Replace('\t', ' ').Trim())
            .Where(term => term.Length > 0)
            .Distinct()];

    private static string Cell(List<string> row, int column) => column < row.Count ? row[column] : "";

    /// <summary>Every sheet that has content, as rows of cells in their columns; empty rows are left out.</summary>
    private static List<List<List<string>>> Sheets(string path)
    {
        // An .xlsx file is a zip of XML parts, so the standard library reads it without an Excel package.
        using var workbook = ZipFile.OpenRead(path);
        var shared = workbook.GetEntry("xl/sharedStrings.xml") is { } strings
            ? Load(strings).Descendants(Sheet + "si").Select(Text).ToList()
            : [];

        return [.. workbook.Entries
            .Where(entry => entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal))
            .Select(entry => Load(entry).Descendants(Sheet + "row").Select(row => Cells(row, shared)).Where(row => row.Any(cell => cell.Length > 0)).ToList())
            .Where(rows => rows.Count > 0)];
    }

    // Excel leaves empty cells out of the file, so each value is placed by its own reference ("C5" is the third column).
    private static List<string> Cells(XElement row, List<string> shared)
    {
        var cells = new List<string>();
        foreach (var cell in row.Elements(Sheet + "c"))
        {
            var letters = ((string?)cell.Attribute("r") ?? "").TakeWhile(char.IsAsciiLetterUpper).ToList();
            var column = letters.Count == 0 ? cells.Count : letters.Aggregate(0, (index, letter) => index * 26 + letter - 'A' + 1) - 1;
            while (cells.Count <= column)
            {
                cells.Add("");
            }

            cells[column] = Value(cell, shared);
        }

        return cells;
    }

    private static string Value(XElement cell, List<string> shared) => ((string?)cell.Attribute("t") switch
    {
        "s" => shared[int.Parse((string)cell.Element(Sheet + "v")!, CultureInfo.InvariantCulture)],
        "inlineStr" => Text(cell),
        _ => (string?)cell.Element(Sheet + "v") ?? ""
    }).Trim();

    // Excel keeps the furigana of Japanese cells in rPh elements; they are reading aids, not part of the value.
    private static string Text(XElement element) =>
        string.Concat(element.Descendants(Sheet + "t").Where(text => text.Parent?.Name != Sheet + "rPh").Select(text => text.Value));

    private static XDocument Load(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }
}
