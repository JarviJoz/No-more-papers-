using NoMorePapers.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace NoMorePapers.Helpers
{
    public static class DocumentHelper
    {
        public static void PrintStudentReport(StudentCase studentCase)
        {
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                FlowDocument doc = CreateFlowDocument(studentCase);
                // Darle tamaño al documento basado en el área imprimible
                doc.PageHeight = printDialog.PrintableAreaHeight;
                doc.PageWidth = printDialog.PrintableAreaWidth;
                doc.PagePadding = new Thickness(50);
                doc.ColumnGap = 0;
                doc.ColumnWidth = printDialog.PrintableAreaWidth;

                IDocumentPaginatorSource idpSource = doc;
                printDialog.PrintDocument(idpSource.DocumentPaginator, $"Reporte_{studentCase.Id}");
            }
        }

        private static FlowDocument CreateFlowDocument(StudentCase c)
        {
            FlowDocument doc = new FlowDocument();
            doc.FontFamily = new FontFamily("Segoe UI");

            // Título
            Paragraph title = new Paragraph(new Run("REPORTE DE CASO PSICOEDUCATIVO"))
            {
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            doc.Blocks.Add(title);

            // Información General
            doc.Blocks.Add(CreateSectionHeader("INFORMACIÓN DEL ESTUDIANTE"));
            doc.Blocks.Add(CreateParagraph($"ID del Caso: {c.Id}"));
            doc.Blocks.Add(CreateParagraph($"Nombre Completo: {c.StudentName}"));
            doc.Blocks.Add(CreateParagraph($"Edad: {c.Age} años  |  Grado: {c.Grade}  |  Grupo: {c.Group}  |  Jornada: {c.Shift}"));

            // Acudiente
            doc.Blocks.Add(CreateSectionHeader("DATOS DEL ACUDIENTE"));
            doc.Blocks.Add(CreateParagraph($"Nombre: {c.GuardianName ?? "No registrado"}"));
            doc.Blocks.Add(CreateParagraph($"Parentesco: {c.GuardianRelation}  |  Teléfono: {c.GuardianPhone ?? "N/A"}"));

            // Detalles del Caso
            doc.Blocks.Add(CreateSectionHeader("DETALLES DEL CASO"));
            doc.Blocks.Add(CreateParagraph($"Estado Actual: {c.Status}"));
            doc.Blocks.Add(CreateParagraph($"Fecha de Creación: {c.CreatedAt:dd/MM/yyyy}  |  Última Actualización: {c.UpdatedAt:dd/MM/yyyy}"));
            doc.Blocks.Add(CreateParagraph($"Categorías Implicadas: {c.Categories}"));

            doc.Blocks.Add(CreateSectionHeader("MOTIVO DE REMISIÓN"));
            doc.Blocks.Add(CreateParagraph(c.Reason));

            doc.Blocks.Add(CreateSectionHeader("OBSERVACIONES Y NOTAS DEL PROCESO"));
            doc.Blocks.Add(CreateParagraph(string.IsNullOrWhiteSpace(c.Observations) ? "Sin observaciones registradas." : c.Observations));

            return doc;
        }

        private static Paragraph CreateSectionHeader(string text)
        {
            return new Paragraph(new Run(text))
            {
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.DarkSlateBlue,
                Margin = new Thickness(0, 15, 0, 5),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
        }

        private static Paragraph CreateParagraph(string text)
        {
            return new Paragraph(new Run(text))
            {
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 2)
            };
        }
    }
}