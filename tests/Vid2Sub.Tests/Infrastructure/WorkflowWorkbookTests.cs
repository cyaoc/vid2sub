using ClosedXML.Excel;
using Vid2Sub.Infrastructure.Workflow;

namespace Vid2Sub.Tests.Infrastructure;

public sealed class WorkflowWorkbookTests
{
    [Fact]
    public async Task WorkbookInspector_ReturnsSheetsColumnsAndBoundedSamples()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Root, "glossary.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Terms");
            sheet.Cell(1, 1).Value = " Japanese ";
            sheet.Cell(1, 2).Value = " Chinese ";
            sheet.Cell(2, 1).Value = "猫";
            sheet.Cell(2, 2).Value = "猫";
            sheet.Cell(3, 1).Value = "犬";
            sheet.Cell(3, 2).Value = "狗";
            sheet.Cell(4, 1).Value = "鳥";
            sheet.Cell(4, 2).Value = "鸟";
            workbook.SaveAs(path);
        }

        var inspection = await new WorkbookInspector().InspectAsync(path, sampleRowCount: 2);

        var sheetInfo = Assert.Single(inspection.Sheets);
        Assert.Equal("Terms", sheetInfo.Name);
        Assert.Equal(["Japanese", "Chinese"], sheetInfo.Columns);
        Assert.Equal(2, sheetInfo.SampleRows.Count);
    }

    [Fact]
    public async Task WorkbookInspector_PreservesColumnAddressesWhenHeadersHaveGaps()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Root, "glossary.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Terms");
            sheet.Cell(1, 2).Value = "Japanese";
            sheet.Cell(1, 3).Value = "Chinese";
            sheet.Cell(2, 2).Value = "猫";
            sheet.Cell(2, 3).Value = "猫";
            workbook.SaveAs(path);
        }

        var inspection = await new WorkbookInspector().InspectAsync(path, sampleRowCount: 1);

        var sample = Assert.Single(Assert.Single(inspection.Sheets).SampleRows);
        Assert.Equal("猫", sample["Japanese"]);
        Assert.Equal("猫", sample["Chinese"]);
    }

    [Fact]
    public async Task GlossaryReader_WarnsForEmptyAndDuplicateKeys()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Root, "glossary.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Terms");
            sheet.Cell(1, 1).Value = "Japanese";
            sheet.Cell(1, 2).Value = "Chinese";
            sheet.Cell(2, 1).Value = "猫";
            sheet.Cell(2, 2).Value = "猫";
            sheet.Cell(3, 1).Value = "";
            sheet.Cell(3, 2).Value = "empty";
            sheet.Cell(4, 1).Value = "猫";
            sheet.Cell(4, 2).Value = "貓";
            workbook.SaveAs(path);
        }

        var glossary = await new GlossaryReader().ReadAsync(
            path,
            "Terms",
            "Japanese",
            "Chinese",
            notesColumn: null);

        Assert.Single(glossary.Entries);
        Assert.Contains(glossary.Warnings, warning => warning.Code == GlossaryWarningCodes.EmptyKey);
        Assert.Contains(glossary.Warnings, warning => warning.Code == GlossaryWarningCodes.DuplicateKey);
    }
}
