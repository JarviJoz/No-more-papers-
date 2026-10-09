using NoMorePapers.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NoMorePapers.Helpers
{
    public static class ExportHelper
    {
        public static async Task ExportCasesToCsvAsync(string filePath, IEnumerable<StudentCase> cases)
        {
            var csv = new StringBuilder();

            // Cabeceras
            csv.AppendLine("ID,ESTUDIANTE,EDAD,GRADO,GRUPO,ACUDIENTE,PARENTESCO,MOTIVO,CATEGORIAS,ESTADO,CREACION,ACTUALIZACION");

            foreach (var c in cases)
            {
                // Escapar comas y comillas en campos de texto libre
                var name = EscapeCsv(c.StudentName);
                var guardian = EscapeCsv(c.GuardianName ?? "");
                var reason = EscapeCsv(c.Reason);
                var categories = EscapeCsv(c.Categories);

                var line = $"{c.Id},{name},{c.Age},{c.Grade},{c.Group},{guardian},{c.GuardianRelation},{reason},{categories},{c.Status},{c.CreatedAt:dd/MM/yyyy},{c.UpdatedAt:dd/MM/yyyy}";
                csv.AppendLine(line);
            }

            // Guardar con codificación UTF8 con BOM para que Excel reconozca las tildes y eñes
            await File.WriteAllTextAsync(filePath, csv.ToString(), new UTF8Encoding(true));
        }

        private static string EscapeCsv(string field)
        {
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }
            return field;
        }
    }
}