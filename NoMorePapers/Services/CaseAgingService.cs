using NoMorePapers.Models;
using System;
using System.Windows.Media;

namespace NoMorePapers.Services
{
    public sealed record CaseAgingInfo(int Days, string Level, Color Color, string Description)
    {
        public Brush Brush => new SolidColorBrush(Color);
    }

    public static class CaseAgingService
    {
        public static CaseAgingInfo Calculate(StudentCase studentCase, DateTime? now = null)
        {
            if (studentCase.Status is "Finalizado" or "Cancelado")
            {
                return new CaseAgingInfo(0, "Detenido", Colors.Green, "Seguimiento detenido");
            }

            if (!IsTracked(studentCase))
            {
                return new CaseAgingInfo(0, "Detenido", Colors.Gray, "Seguimiento no activo");
            }

            var currentTime = now ?? DateTime.Now;
            var days = Math.Max(0, (currentTime.Date - studentCase.UpdatedAt.Date).Days);

            return days switch
            {
                < 7 => new CaseAgingInfo(days, "Azul", Color.FromRgb(3, 169, 244), "Actualizado recientemente"),
                < 15 => new CaseAgingInfo(days, "Amarillo", Colors.Gold, "7 días sin modificación"),
                < 30 => new CaseAgingInfo(days, "Naranja", Colors.DarkOrange, "15 días sin modificación"),
                _ => new CaseAgingInfo(days, "Rojo", Colors.Red, "30 días o más sin modificación")
            };
        }

        public static bool IsTracked(StudentCase studentCase) =>
            studentCase.Status is "En Proceso" or "Pendiente";
    }
}
