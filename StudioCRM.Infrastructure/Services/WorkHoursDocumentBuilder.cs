using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using StudioCRM.Domain.Enums;

namespace StudioCRM.Infrastructure.Services;

internal sealed class WorkHoursDocumentModel
{
    public string TrainerFirstName { get; set; } = string.Empty;
    public string TrainerLastName { get; set; } = string.Empty;
    public TrainerContractType ContractType { get; set; }
    public string? ContractNumber { get; set; }
    public DateTime? ContractSignedAt { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal HourlyRate { get; set; }
    public List<WorkHoursDocumentRow> Rows { get; set; } = new();
}

internal sealed class WorkHoursDocumentRow
{
    public DateTime Date { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public decimal Hours { get; set; }
    public decimal TwoToOneBonusUnits { get; set; }
    public decimal ThreeToOneBonusUnits { get; set; }
    public decimal FourToOneBonusUnits { get; set; }
}

internal static class WorkHoursDocumentBuilder
{
    private const string WordprocessingMainNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const int B2BRowsBeforeTotalsPerPage = 35;
    private const int ZlecenieRowsBeforeTotalsPerPage = 27;

    public static byte[] Build(WorkHoursDocumentModel model)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var includeFirstPageHeader = model.ContractType == TrainerContractType.B2B;

            WriteEntry(archive, "[Content_Types].xml", BuildContentTypesXml(includeFirstPageHeader));
            WriteEntry(archive, "_rels/.rels", BuildPackageRelationshipsXml());
            WriteEntry(archive, "word/document.xml", BuildDocumentXml(model));

            if (includeFirstPageHeader)
            {
                WriteEntry(archive, "word/_rels/document.xml.rels", BuildDocumentRelationshipsXml());
                WriteEntry(archive, "word/header1.xml", BuildB2BHeaderXml(model));
            }
        }

        return stream.ToArray();
    }

    private static string BuildDocumentXml(WorkHoursDocumentModel model)
    {
        var body = model.ContractType == TrainerContractType.Zlecenie
            ? BuildZlecenieBody(model)
            : BuildB2BBody(model);

        return $"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="{WordprocessingMainNamespace}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <w:body>
    {body}
    {BuildSectionProperties(model.ContractType)}
  </w:body>
</w:document>
""";
    }

    private static string BuildSectionProperties(TrainerContractType contractType)
    {
        if (contractType == TrainerContractType.B2B)
        {
            return """
<w:sectPr>
  <w:headerReference w:type="first" r:id="rId1"/>
  <w:titlePg/>
  <w:pgSz w:w="11910" w:h="16840"/>
  <w:pgMar w:top="1580" w:right="1275" w:bottom="760" w:left="992" w:header="433" w:footer="574" w:gutter="0"/>
  <w:pgNumType w:start="1"/>
  <w:cols w:space="708"/>
</w:sectPr>
""";
        }

        return """
<w:sectPr>
  <w:pgSz w:w="11910" w:h="16840"/>
  <w:pgMar w:top="760" w:right="992" w:bottom="760" w:left="992" w:header="433" w:footer="574" w:gutter="0"/>
  <w:pgNumType w:start="1"/>
  <w:cols w:space="708"/>
</w:sectPr>
""";
    }

    private static string BuildB2BHeaderXml(WorkHoursDocumentModel model)
    {
        var monthStart = new DateTime(model.Year, model.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        return $"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:hdr xmlns:w="{WordprocessingMainNamespace}">
  {Paragraph("EWIDENCJA CZASU PRACY", alignment: "center", bold: true, size: 24, spacingAfter: 0)}
  {Paragraph($"za okres od {FormatPolishDate(monthStart)} do {FormatPolishDate(monthEnd)}", alignment: "center", size: 20, spacingAfter: 0)}
  {Paragraph("nr str.: 1", alignment: "right", size: 20, spacingAfter: 0)}
  {Paragraph($"do umowy o świadczenie usług zawartej w dniu {FormatPolishDate(model.ContractSignedAt)}", alignment: "center", size: 20, spacingAfter: 0)}
</w:hdr>
""";
    }

    private static string BuildB2BBody(WorkHoursDocumentModel model)
    {
        var contractorRows = new List<TableRow>
        {
            new([
                Cell("Nazwisko:", shaded: true, bold: true),
                Cell(model.TrainerLastName),
                Cell("Pierwsze imię:", shaded: true, bold: true),
                Cell(model.TrainerFirstName)
            ], Height: 270)
        };

        var hourRows = BuildBaseB2BHourHeaderRows();

        foreach (var row in model.Rows)
        {
            hourRows.Add(new([
                Cell(row.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)),
                Cell(FormatTime(row.StartAt)),
                Cell(FormatTime(row.EndAt)),
                Cell(FormatHoursAsTime(row.Hours)),
                Cell(string.Empty)
            ], Height: 270));
        }

        PadRowsToPage(hourRows, B2BRowsBeforeTotalsPerPage, columns: 5);

        var totalHours = model.Rows.Sum(r => r.Hours);
        hourRows.Add(TotalRow("Razem strona", FormatHoursAsTime(totalHours), columns: 5));
        hourRows.Add(TotalRow("Z przeniesienia", string.Empty, columns: 5));
        hourRows.Add(TotalRow("RAZEM", FormatHoursAsTime(totalHours), columns: 5));

        return string.Concat(
            Paragraph("WYKONAWCA:", bold: true, size: 26, spacingAfter: 3),
            Table(contractorRows, [1193, 3224, 1402, 3723], tableWidthAuto: true, indent: 35, borderSize: 8, cellMargin: 0),
            Paragraph(string.Empty, spacingAfter: 30),
            Table(hourRows, [1193, 1032, 1033, 1160, 5126], tableWidthAuto: true, indent: 35, borderSize: 8, cellMargin: 0),
            SignatureBlock("podpis Wykonawcy", "podpis Zamawiającego"));
    }

    private static List<TableRow> BuildBaseB2BHourHeaderRows()
    {
        return
        [
            new([
                Cell("dzień", shaded: true, bold: true),
                Cell("godzina", span: 2, shaded: true, bold: true),
                Cell("liczba godzin", shaded: true, bold: true),
                Cell(string.Empty, shaded: true)
            ], Height: 224),
            new([
                Cell(string.Empty, shaded: true),
                Cell("od", shaded: true, bold: true),
                Cell("do", shaded: true, bold: true),
                Cell(string.Empty, shaded: true),
                Cell(string.Empty, shaded: true)
            ], Height: 270),
            new([
                Cell("1", shaded: true, bold: true),
                Cell("2", shaded: true, bold: true),
                Cell("3", shaded: true, bold: true),
                Cell("4", shaded: true, bold: true),
                Cell("5", shaded: true, bold: true)
            ], Height: 138),
            new([
                Cell(string.Empty, shaded: true),
                Cell("hh : mm", shaded: true),
                Cell("hh : mm", shaded: true),
                Cell("hh : mm", shaded: true),
                Cell(string.Empty, shaded: true)
            ], Height: 138)
        ];
    }

    private static string BuildZlecenieBody(WorkHoursDocumentModel model)
    {
        var monthStart = new DateTime(model.Year, model.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var contractorRows = new List<TableRow>
        {
            new([
                Cell("Nazwisko:", shaded: true, bold: true),
                Cell(model.TrainerLastName),
                Cell("Pierwsze imię:", shaded: true, bold: true),
                Cell(model.TrainerFirstName)
            ], Height: 270)
        };

        var hourRows = BuildBaseZlecenieHourHeaderRows();

        foreach (var row in model.Rows)
        {
            hourRows.Add(new([
                Cell(row.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)),
                Cell(FormatTime(row.StartAt)),
                Cell(FormatTime(row.EndAt)),
                Cell(FormatHoursAsTime(row.Hours)),
                Cell(FormatBonusUnits(row.TwoToOneBonusUnits)),
                Cell(FormatBonusUnits(row.ThreeToOneBonusUnits)),
                Cell(FormatBonusUnits(row.FourToOneBonusUnits))
            ], Height: 270));
        }

        PadRowsToPage(hourRows, ZlecenieRowsBeforeTotalsPerPage, columns: 7);

        var totalHours = model.Rows.Sum(r => r.Hours);
        var totalTwoToOne = model.Rows.Sum(r => r.TwoToOneBonusUnits);
        var totalThreeToOne = model.Rows.Sum(r => r.ThreeToOneBonusUnits);
        var totalFourToOne = model.Rows.Sum(r => r.FourToOneBonusUnits);

        hourRows.Add(ZlecenieTotalRow("Razem strona", totalHours, totalTwoToOne, totalThreeToOne, totalFourToOne));
        hourRows.Add(ZlecenieTotalRow("Z przeniesienia", null, null, null, null));
        hourRows.Add(ZlecenieTotalRow("RAZEM", totalHours, totalTwoToOne, totalThreeToOne, totalFourToOne));

        return string.Concat(
            Paragraph("str 1", alignment: "right", bold: true, size: 20, spacingAfter: 0),
            Paragraph("EWIDENCJA CZASU PRACY", alignment: "center", bold: true, size: 24, spacingAfter: 0),
            Paragraph($"za okres od {FormatShortDate(monthStart)} do {FormatShortDate(monthEnd)}", alignment: "center", size: 20, spacingAfter: 0),
            Paragraph($"do umowy zlecenia nr {ValueOrBlank(model.ContractNumber)} zawartej w dniu {FormatShortDate(model.ContractSignedAt)}", alignment: "center", size: 20, spacingAfter: 60),
            Paragraph("ZLECENIOBIORCA:", bold: true, size: 24, spacingAfter: 3),
            Table(contractorRows, [1193, 3224, 1402, 3723], tableWidthAuto: true, indent: 35, borderSize: 8, cellMargin: 0),
            Paragraph(string.Empty, spacingAfter: 30),
            Table(hourRows, [1193, 1032, 1033, 1160, 1708, 1709, 1709], tableWidthAuto: true, indent: 35, borderSize: 8, cellMargin: 0),
            Paragraph("Uwagi:", alignment: "center", bold: true, spacingAfter: 0),
            Paragraph($"Stawka godzinowa - {FormatCurrency(model.HourlyRate)} zł", alignment: "center", spacingAfter: 0),
            Paragraph($"Trening semipersonalny 2:1 - {FormatCurrency(ResolveRoundedBonus(model.HourlyRate, 1.6m))} zł", alignment: "center", spacingAfter: 0),
            Paragraph($"Trening semipersonalny 3:1 - {FormatCurrency(ResolveRoundedBonus(model.HourlyRate, 2.2m))} zł", alignment: "center", spacingAfter: 0),
            Paragraph($"Trening semipersonalny 4:1 - {FormatCurrency(ResolveRoundedBonus(model.HourlyRate, 2.66m))} zł", alignment: "center", spacingAfter: 20),
            SignatureBlock("podpis zleceniodawcy", "podpis zleceniobiorcy"));
    }

    private static List<TableRow> BuildBaseZlecenieHourHeaderRows()
    {
        return
        [
            new([
                Cell("dzień", shaded: true, bold: true),
                Cell("godzina", span: 2, shaded: true, bold: true),
                Cell("liczba godzin", shaded: true, bold: true),
                Cell("treningi semipersonalne", span: 3, shaded: true, bold: true)
            ], Height: 224),
            new([
                Cell(string.Empty, shaded: true),
                Cell("od", shaded: true, bold: true),
                Cell("do", shaded: true, bold: true),
                Cell(string.Empty, shaded: true),
                Cell("2 do 1", shaded: true, bold: true),
                Cell("3 do 1", shaded: true, bold: true),
                Cell("4 do 1", shaded: true, bold: true)
            ], Height: 270),
            new([
                Cell("1", shaded: true, bold: true),
                Cell("2", shaded: true, bold: true),
                Cell("3", shaded: true, bold: true),
                Cell("4", shaded: true, bold: true),
                Cell("5", shaded: true, bold: true),
                Cell("6", shaded: true, bold: true),
                Cell("7", shaded: true, bold: true)
            ], Height: 138),
            new([
                Cell(string.Empty, shaded: true),
                Cell("hh : mm", shaded: true),
                Cell("hh : mm", shaded: true),
                Cell("hh : mm", shaded: true),
                Cell(string.Empty, shaded: true),
                Cell(string.Empty, shaded: true),
                Cell(string.Empty, shaded: true)
            ], Height: 138)
        ];
    }

    private static void PadRowsToPage(List<TableRow> rows, int rowsBeforeTotalsPerPage, int columns)
    {
        var targetRows = rows.Count <= rowsBeforeTotalsPerPage
            ? rowsBeforeTotalsPerPage
            : (int)Math.Ceiling(rows.Count / (decimal)rowsBeforeTotalsPerPage) * rowsBeforeTotalsPerPage;

        while (rows.Count < targetRows)
        {
            rows.Add(new TableRow(
                Enumerable.Range(0, columns)
                    .Select(_ => Cell(string.Empty))
                    .ToList(),
                Height: 270));
        }
    }

    private static string SignatureBlock(string leftLabel, string rightLabel)
    {
        var rows = new List<TableRow>
        {
            new([
                Cell("\n........................................"),
                Cell("\n........................................")
            ], Height: 540),
            new([
                Cell(leftLabel),
                Cell(rightLabel)
            ], Height: 270)
        };

        return Table(rows, [4771, 4771], borders: false, tableWidthAuto: true, indent: 35, cellMargin: 0);
    }

    private static TableRow TotalRow(string label, string value, int columns)
    {
        var cells = new List<TableCell>
        {
            Cell(label, span: columns - 2, shaded: true, bold: true),
            Cell(value, shaded: true, bold: true),
            Cell(string.Empty, shaded: true)
        };

        return new TableRow(cells, Height: 270);
    }

    private static TableRow ZlecenieTotalRow(
        string label,
        decimal? hours,
        decimal? twoToOne,
        decimal? threeToOne,
        decimal? fourToOne)
    {
        return new TableRow([
            Cell(label, span: 3, shaded: true, bold: true),
            Cell(hours.HasValue ? FormatHoursAsTime(hours.Value) : string.Empty, shaded: true, bold: true),
            Cell(twoToOne.HasValue ? FormatBonusUnits(twoToOne.Value) : string.Empty, shaded: true, bold: true),
            Cell(threeToOne.HasValue ? FormatBonusUnits(threeToOne.Value) : string.Empty, shaded: true, bold: true),
            Cell(fourToOne.HasValue ? FormatBonusUnits(fourToOne.Value) : string.Empty, shaded: true, bold: true)
        ], Height: 270);
    }

    private static string Table(
        List<TableRow> rows,
        int[] widths,
        bool borders = true,
        bool tableWidthAuto = false,
        int indent = 0,
        int borderSize = 6,
        int cellMargin = 80)
    {
        var borderXml = borders
            ? $"""
<w:tblBorders>
  <w:top w:val="single" w:sz="{borderSize}" w:space="0" w:color="000000"/>
  <w:left w:val="single" w:sz="{borderSize}" w:space="0" w:color="000000"/>
  <w:bottom w:val="single" w:sz="{borderSize}" w:space="0" w:color="000000"/>
  <w:right w:val="single" w:sz="{borderSize}" w:space="0" w:color="000000"/>
  <w:insideH w:val="single" w:sz="{borderSize}" w:space="0" w:color="000000"/>
  <w:insideV w:val="single" w:sz="{borderSize}" w:space="0" w:color="000000"/>
</w:tblBorders>
"""
            : """
<w:tblBorders>
  <w:top w:val="nil"/>
  <w:left w:val="nil"/>
  <w:bottom w:val="nil"/>
  <w:right w:val="nil"/>
  <w:insideH w:val="nil"/>
  <w:insideV w:val="nil"/>
</w:tblBorders>
""";

        var grid = string.Concat(widths.Select(width => $"<w:gridCol w:w=\"{width}\"/>"));
        var tableWidthXml = tableWidthAuto
            ? "<w:tblW w:w=\"0\" w:type=\"auto\"/>"
            : $"<w:tblW w:w=\"{widths.Sum()}\" w:type=\"dxa\"/>";
        var tableIndentXml = indent > 0
            ? $"<w:tblInd w:w=\"{indent}\" w:type=\"dxa\"/>"
            : string.Empty;
        var builder = new StringBuilder();
        builder.Append($"""
<w:tbl>
  <w:tblPr>
    {tableWidthXml}
    {tableIndentXml}
    <w:tblCellMar>
      <w:top w:w="{cellMargin}" w:type="dxa"/>
      <w:left w:w="{cellMargin}" w:type="dxa"/>
      <w:bottom w:w="{cellMargin}" w:type="dxa"/>
      <w:right w:w="{cellMargin}" w:type="dxa"/>
    </w:tblCellMar>
    {borderXml}
    <w:tblLayout w:type="fixed"/>
  </w:tblPr>
  <w:tblGrid>{grid}</w:tblGrid>
""");

        foreach (var row in rows)
        {
            var rowHeight = row.Height.HasValue
                ? $"<w:trPr><w:trHeight w:val=\"{row.Height.Value}\"/></w:trPr>"
                : string.Empty;
            builder.Append($"<w:tr>{rowHeight}");

            var gridIndex = 0;
            foreach (var cell in row.Cells)
            {
                builder.Append(RenderTableCell(cell, ResolveCellWidth(widths, gridIndex, cell.Span)));
                gridIndex += cell.Span;
            }

            builder.Append("</w:tr>");
        }

        builder.Append("</w:tbl>");
        return builder.ToString();
    }

    private static string RenderTableCell(TableCell cell, int width)
    {
        var span = cell.Span > 1 ? $"<w:gridSpan w:val=\"{cell.Span}\"/>" : string.Empty;
        var shading = cell.Shaded ? "<w:shd w:fill=\"D9D9D9\"/>" : string.Empty;

        return $"""
<w:tc>
  <w:tcPr><w:tcW w:w="{width}" w:type="dxa"/>{span}{shading}<w:vAlign w:val="center"/></w:tcPr>
  {Paragraph(cell.Text, alignment: "center", bold: cell.Bold, spacingAfter: 0)}
</w:tc>
""";
    }

    private static int ResolveCellWidth(int[] widths, int gridIndex, int span)
    {
        return widths
            .Skip(gridIndex)
            .Take(span)
            .Sum();
    }

    private static string Paragraph(
        string text,
        string alignment = "left",
        bool bold = false,
        int size = 22,
        int spacingAfter = 120)
    {
        var boldXml = bold ? "<w:b/>" : string.Empty;
        var lines = text.Split('\n');
        var textXml = string.Join("<w:br/>", lines.Select(line => $"<w:t xml:space=\"preserve\">{Escape(line)}</w:t>"));

        return $"""
<w:p>
  <w:pPr>
    <w:jc w:val="{alignment}"/>
    <w:spacing w:after="{spacingAfter}"/>
  </w:pPr>
  <w:r>
    <w:rPr>{boldXml}<w:sz w:val="{size}"/></w:rPr>
    {textXml}
  </w:r>
</w:p>
""";
    }

    private static TableCell Cell(string? text, int span = 1, bool shaded = false, bool bold = false)
    {
        return new TableCell(text ?? string.Empty, span, shaded, bold);
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string BuildContentTypesXml(bool includeHeader)
    {
        var headerOverride = includeHeader
            ? """
  <Override PartName="/word/header1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml"/>
"""
            : string.Empty;

        return $"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
{headerOverride}
</Types>
""";
    }

    private static string BuildPackageRelationshipsXml()
    {
        return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>
""";
    }

    private static string BuildDocumentRelationshipsXml()
    {
        return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/header" Target="header1.xml"/>
</Relationships>
""";
    }

    private static string Escape(string? value)
    {
        return SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
    }

    private static string FormatShortDate(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToString("M/d/yyyy", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static string FormatPolishDate(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static string FormatTime(DateTime value)
    {
        return value.ToString("H:mm", CultureInfo.InvariantCulture);
    }

    private static string FormatHoursAsTime(decimal hours)
    {
        var totalMinutes = (int)Math.Round(hours * 60m, MidpointRounding.AwayFromZero);
        return $"{totalMinutes / 60}:{totalMinutes % 60:00}";
    }

    private static string FormatCurrency(decimal value)
    {
        return Math.Round(value, 0, MidpointRounding.AwayFromZero)
            .ToString("0", CultureInfo.InvariantCulture);
    }

    private static string FormatBonusUnits(decimal value)
    {
        return value == 0
            ? string.Empty
            : value.ToString("0.##", CultureInfo.InvariantCulture).Replace(".", ",", StringComparison.Ordinal);
    }

    private static decimal ResolveRoundedBonus(decimal hourlyRate, decimal multiplier)
    {
        return Math.Round((multiplier - 1m) * hourlyRate, 0, MidpointRounding.AwayFromZero);
    }

    private static string ValueOrBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private sealed record TableRow(List<TableCell> Cells, int? Height = null);

    private sealed record TableCell(string Text, int Span, bool Shaded, bool Bold);
}
