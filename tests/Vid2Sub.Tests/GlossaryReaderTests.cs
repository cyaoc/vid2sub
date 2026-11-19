using System.IO.Compression;
using Vid2Sub.Infrastructure.Correction;

namespace Vid2Sub.Tests;

public class GlossaryReaderTests
{
    private const string Namespace = "xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"";

    /// <summary>A two-language glossary with the oddities of real workbooks: furigana, rich text, gaps and repeats.</summary>
    private const string TwoLanguages = """
        <row r="1"><c r="A1" t="inlineStr"><is><t>ja</t></is></c><c r="B1" t="inlineStr"><is><t>zh</t></is></c><c r="C1" t="inlineStr"><is><t>note</t></is></c></row>
        <row r="2"><c r="A2" t="s"><v>0</v></c><c r="B2" t="s"><v>1</v></c></row>
        <row r="3"><c r="A3"/><c r="B3"/></row>
        <row r="4"><c r="A4" t="inlineStr"><is><t> kintone </t></is></c><c r="C4"><v>2024</v></c></row>
        <row r="5"><c r="B5" t="inlineStr"><is><t>只有中文</t></is></c></row>
        <row r="6"><c r="A6" t="inlineStr"><is><t>kintone</t></is></c><c r="B6" t="inlineStr"><is><t>重复的一行</t></is></c></row>
        """;

    private const string SharedStrings = """
        <si><t>多様な個性を重視</t><rPh sb="0" eb="2"><t>タヨウ</t></rPh><phoneticPr fontId="1"/></si>
        <si><r><t>重视</t></r><r><t>多彩个性 </t></r></si>
        """;

    [Theory]
    [InlineData("ja", new[] { "多様な個性を重視", "kintone" })]
    [InlineData(" ZH ", new[] { "重视多彩个性", "只有中文", "重复的一行" })]
    // Row 4 has no B cell: its C value must still be read as the third column.
    [InlineData("note", new[] { "2024" })]
    public void The_named_column_is_read_and_the_others_are_left_out(string column, string[] expected)
    {
        using var temp = new TempDirectory();

        Assert.Equal(expected, GlossaryReader.Read(Workbook(temp, TwoLanguages), column));
    }

    [Theory]
    [InlineData(null, "has several columns (ja, zh, note); name the one that holds the terms with -G")]
    [InlineData("en", "no terms in a column named \"en\"; its columns are: ja, zh, note")]
    public void A_workbook_with_several_columns_needs_one_of_them_named(string? column, string expectedMessage)
    {
        using var temp = new TempDirectory();

        var failure = Assert.Throws<InvalidDataException>(() => GlossaryReader.Read(Workbook(temp, TwoLanguages), column));

        Assert.Contains(expectedMessage, failure.Message);
    }

    [Fact]
    public void A_single_column_sheet_does_not_excuse_naming_the_column_of_another_sheet()
    {
        using var temp = new TempDirectory();
        const string notes = """<row r="1"><c r="A1" t="inlineStr"><is><t>how to use this workbook</t></is></c></row>""";

        // Read without a column, the notes would come back as the whole glossary and every term would be lost.
        var failure = Assert.Throws<InvalidDataException>(() => GlossaryReader.Read(Workbook(temp, TwoLanguages, notes)));
        Assert.Contains("has several columns (ja, zh, note)", failure.Message);

        Assert.Equal(["多様な個性を重視", "kintone"], GlossaryReader.Read(Workbook(temp, TwoLanguages, notes), "ja"));
    }

    [Fact]
    public void A_single_column_workbook_needs_no_column_name_even_when_the_column_is_not_the_first()
    {
        using var temp = new TempDirectory();
        var path = Workbook(temp, """
            <row r="1"><c r="B1" t="inlineStr"><is><t>サイボウズ</t></is></c></row>
            <row r="2"><c r="B2" t="inlineStr"><is><t>kintone</t></is></c></row>
            """);

        Assert.Equal(["サイボウズ", "kintone"], GlossaryReader.Read(path));
    }

    [Fact]
    public void A_cell_with_several_lines_gives_one_term_per_line()
    {
        using var temp = new TempDirectory();
        var path = Workbook(temp, """
            <row r="1"><c r="A1" t="inlineStr"><is><t>ja</t></is></c><c r="B1" t="inlineStr"><is><t>zh</t></is></c></row>
            <row r="2"><c r="A2" t="inlineStr"><is><t>kintone&#10; キントーン&#13;&#10;&#10;きんとーん</t></is></c><c r="B2" t="inlineStr"><is><t>第一行&#10;第二行</t></is></c></row>
            <row r="3"><c r="A3" t="inlineStr"><is><t>A&#9;B</t></is></c><c r="B3" t="inlineStr"><is><t>x</t></is></c></row>
            """);

        // The line break inside B2 must not start a new row either, or A3 would be read from the wrong place.
        Assert.Equal(["kintone", "キントーン", "きんとーん", "A B"], GlossaryReader.Read(path, "ja"));
    }

    [Fact]
    public void A_text_file_holds_one_term_per_line()
    {
        using var temp = new TempDirectory();

        Assert.Equal(["サイボウズ", "kintone"], GlossaryReader.Read(temp.File("terms.txt", "サイボウズ\r\n\r\n  kintone  \nサイボウズ\n"), "ignored"));
    }

    [Fact]
    public void An_empty_workbook_is_reported()
    {
        using var temp = new TempDirectory();

        Assert.Contains("is empty", Assert.Throws<InvalidDataException>(() => GlossaryReader.Read(Workbook(temp, ""))).Message);
    }

    private static string Workbook(TempDirectory temp, string rows, string? secondSheetRows = null)
    {
        var path = Path.Combine(temp.Path, "glossary.XLSX");
        File.Delete(path);
        using var workbook = ZipFile.Open(path, ZipArchiveMode.Create);
        Write(workbook, "xl/sharedStrings.xml", $"<sst {Namespace}>{SharedStrings}</sst>");
        Write(workbook, "xl/worksheets/sheet1.xml", $"<worksheet {Namespace}><sheetData>{rows}</sheetData></worksheet>");
        if (secondSheetRows is not null)
        {
            Write(workbook, "xl/worksheets/sheet2.xml", $"<worksheet {Namespace}><sheetData>{secondSheetRows}</sheetData></worksheet>");
        }

        return path;
    }

    private static void Write(ZipArchive workbook, string name, string xml)
    {
        using var writer = new StreamWriter(workbook.CreateEntry(name).Open());
        writer.Write(xml);
    }
}
