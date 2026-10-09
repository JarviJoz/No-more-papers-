using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Linq;
using System.Net;
using System.Text;
using Drawing = DocumentFormat.OpenXml.Drawing;

namespace NoMorePapers.Helpers
{
    public static class StudentDocumentPreviewBuilder
    {
        public static string BuildOfficePreview(string filePath, string extension)
        {
            var body = extension.ToLowerInvariant() switch
            {
                ".docx" => BuildWordPreview(filePath),
                ".xlsx" => BuildExcelPreview(filePath),
                ".pptx" => BuildPowerPointPreview(filePath),
                _ => throw new NotSupportedException("Formato Office no compatible.")
            };

            return $"<!DOCTYPE html><html lang='es'><head><meta charset='utf-8'><style>{Styles}</style></head><body>{body}</body></html>";
        }

        private static string BuildWordPreview(string filePath)
        {
            using var document = WordprocessingDocument.Open(filePath, false);
            var paragraphs = document.MainDocumentPart?.Document.Body?
                .Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
                .Select(paragraph => paragraph.InnerText)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList() ?? new System.Collections.Generic.List<string>();

            if (paragraphs.Count == 0)
            {
                return "<p>El documento no contiene texto para mostrar.</p>";
            }

            return string.Join(string.Empty, paragraphs.Select(text => $"<p>{Encode(text)}</p>"));
        }

        private static string BuildExcelPreview(string filePath)
        {
            using var document = SpreadsheetDocument.Open(filePath, false);
            var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("El libro no contiene hojas legibles.");
            var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
            var output = new StringBuilder();

            foreach (var sheet in workbookPart.Workbook.Sheets?.Elements<Sheet>() ?? Enumerable.Empty<Sheet>())
            {
                var sheetName = sheet.Name?.Value ?? "Hoja";
                if (sheet.Id?.Value == null || workbookPart.GetPartById(sheet.Id.Value) is not WorksheetPart worksheetPart)
                {
                    continue;
                }

                output.Append("<h2>").Append(Encode(sheetName)).Append("</h2><table>");
                foreach (var row in worksheetPart.Worksheet.Descendants<Row>())
                {
                    output.Append("<tr><th>").Append(row.RowIndex?.Value ?? 0).Append("</th>");
                    foreach (var cell in row.Elements<Cell>())
                    {
                        output.Append("<td>").Append(Encode(GetCellText(cell, sharedStrings))).Append("</td>");
                    }

                    output.Append("</tr>");
                }

                output.Append("</table>");
            }

            return output.Length == 0 ? "<p>El libro no contiene hojas legibles.</p>" : output.ToString();
        }

        private static string GetCellText(Cell cell, SharedStringTable? sharedStrings)
        {
            if (cell.DataType?.Value == CellValues.SharedString &&
                int.TryParse(cell.CellValue?.Text, out var sharedStringIndex) &&
                sharedStrings != null)
            {
                return sharedStrings.ElementAtOrDefault(sharedStringIndex)?.InnerText ?? string.Empty;
            }

            if (cell.DataType?.Value == CellValues.InlineString)
            {
                return cell.InlineString?.InnerText ?? string.Empty;
            }

            return cell.CellValue?.Text ?? string.Empty;
        }

        private static string BuildPowerPointPreview(string filePath)
        {
            using var document = PresentationDocument.Open(filePath, false);
            var presentationPart = document.PresentationPart ?? throw new InvalidOperationException("La presentación no se puede leer.");
            var slideIds = presentationPart.Presentation.SlideIdList?.Elements<SlideId>() ?? Enumerable.Empty<SlideId>();
            var output = new StringBuilder();
            var slideNumber = 0;

            foreach (var slideId in slideIds)
            {
                if (slideId.RelationshipId?.Value is not { } relationshipId ||
                    presentationPart.GetPartById(relationshipId) is not SlidePart slidePart)
                {
                    continue;
                }

                slideNumber++;
                output.Append("<section><h2>Diapositiva ").Append(slideNumber).Append("</h2>");
                var paragraphs = slidePart.Slide.Descendants<Drawing.Paragraph>()
                    .Select(paragraph => paragraph.InnerText)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToList();
                foreach (var paragraph in paragraphs)
                {
                    output.Append("<p>").Append(Encode(paragraph)).Append("</p>");
                }

                output.Append("</section>");
            }

            return output.Length == 0 ? "<p>La presentación no contiene texto legible.</p>" : output.ToString();
        }

        private static string Encode(string value) => WebUtility.HtmlEncode(value);

        private const string Styles = "body{font-family:Segoe UI,Arial,sans-serif;color:#24133F;padding:20px;line-height:1.5}h2{color:#6C5689;border-bottom:1px solid #E9DDF8;padding-bottom:6px}p{white-space:pre-wrap;margin:0 0 12px}section{margin-bottom:24px}table{border-collapse:collapse;width:100%;margin-bottom:24px}td,th{border:1px solid #D8CBEA;padding:6px 8px;text-align:left;white-space:pre-wrap}th{background:#E9DDF8;width:48px}";
    }
}
