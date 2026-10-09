using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NoMorePapers.Models
{
    public sealed class CaseNotification : ObservableObject
    {
        public string Title { get; init; } = string.Empty;
        public string StudentName { get; init; } = string.Empty;
        public string CaseId { get; init; } = string.Empty;
        public string Grade { get; init; } = string.Empty;
        public string Categories { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string UpdatedAt { get; init; } = string.Empty;
        public string TimeWithoutModification { get; init; } = string.Empty;
        public string Level { get; init; } = string.Empty;
        public Brush ColorBrush { get; init; } = Brushes.Transparent;

        private bool _isRead;
        public bool IsRead
        {
            get => _isRead;
            set => SetProperty(ref _isRead, value);
        }

        public string Details =>
            $"Estudiante: {StudentName}\nID: {CaseId}\nGrado: {Grade}\nCategoría: {Categories}\n" +
            $"Estado: {Status}\nÚltima modificación: {UpdatedAt}\n" +
            $"Tiempo sin modificación: {TimeWithoutModification}\nNivel de seguimiento: {Level}";
    }
}
