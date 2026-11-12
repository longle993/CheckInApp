using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using CheckInApp.Application;
namespace CheckInApp.Infrastructure;

// Small, bounded OpenXML interchange: text cells preserve codes and ISO dates.
public static class ExcelFile
{
    static readonly XNamespace N = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public static byte[] Write(IEnumerable<string[]> rows)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Add(string path, string xml)
            {
                using var w = new StreamWriter(zip.CreateEntry(path).Open());
                w.Write(xml);
            }
            Add(
                "[Content_Types].xml",
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Add(
                "_rels/.rels",
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Add(
                "xl/workbook.xml",
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Data\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add(
                "xl/_rels/workbook.xml.rels",
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            var number = 0;
            var data = new XElement(
                N + "sheetData",
                rows.Select(row => new XElement(
                N + "row",
                new XAttribute("r", ++number),
                row.Select((s, i) => new XElement(
                N + "c",
                new XAttribute("r", $"{(char)('A' + i)}{number}"),
                new XAttribute("t", "inlineStr"),
                new XElement(N + "is", new XElement(N + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), s)))))));
            Add(
                "xl/worksheets/sheet1.xml",
                new XElement(
                N + "worksheet",
                new XElement(
                N + "cols",
                new XElement(
                N + "col",
                new XAttribute("min", 1),
                new XAttribute("max", 12),
                new XAttribute("width", 28),
                new XAttribute("customWidth", 1))),
                data).ToString());
        }
        return output.ToArray();
    }
    public static List<Dictionary<string, string>> Read(byte[] bytes)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes));
            if (zip.Entries.Count > 100 || zip.Entries.Sum(e => e.Length) > 20_000_000)
                throw new InvalidDataException();
            XDocument Xml(string name)
            {
                using var stream = zip.GetEntry(name)!.Open();
                using var reader = XmlReader.Create(
                    stream,
                    new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 20_000_000 });
                return XDocument.Load(reader);
            }
            var shared = zip.GetEntry("xl/sharedStrings.xml") == null
                ? []
                : Xml("xl/sharedStrings.xml").Descendants(N + "si").Select(x => string.Concat(x.Descendants(N + "t").Select(t => t.Value)))
                .ToArray();
            var rows = Xml("xl/worksheets/sheet1.xml").Descendants(N + "row").ToList();
            if (rows.Count > 1001 || rows.Count < 1)
                throw new InvalidDataException();
            Dictionary<string, string> Cells(XElement row) => row.Elements(N + "c").ToDictionary(
                c => new string(((string?)c.Attribute("r") ?? "").TakeWhile(char.IsLetter).ToArray()),
                c =>
            {
                if (c.Element(N + "f") != null)
                    throw new InvalidDataException("Không nhập ô công thức.");
                var value = c.Element(N + "v")?.Value ?? string.Concat(c.Descendants(N + "t").Select(x => x.Value));
                return (string?)c.Attribute("t") == "s" ? shared[int.Parse(value)] : value;
            });
            var headers = Cells(rows[0]);
            if (headers.Count > 12 || headers.Values.Distinct().Count() != headers.Count)
                throw new InvalidDataException();
            return rows.Skip(1).Select(row => { var cells = Cells(row); return headers.ToDictionary(h => h.Value, h => cells.GetValueOrDefault(h.Key, "")); })
                .Where(r => r.Values.Any(v => v.Length > 0))
                .ToList();
        }
        catch (Exception e) when (e is not AppException)
        {
            throw new AppException(
            400,
            "EXCEL_INVALID",
            "File .xlsx không hợp lệ; tối đa 1.000 dòng, không dùng công thức. Giữ nguyên tiêu đề và ngày yyyy-MM-dd dạng Text.");
        }
    }
}
