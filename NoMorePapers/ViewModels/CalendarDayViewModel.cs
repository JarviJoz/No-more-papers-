using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using NoMorePapers.Models;

namespace NoMorePapers.ViewModels
{
    public sealed class CalendarDayViewModel
    {
        public DateTime Date { get; }
        public string DayNumber => Date.Day.ToString();
        public bool IsCurrentMonth { get; }
        public bool IsToday => Date.Date == DateTime.Today;
        public bool IsPast => Date.Date < DateTime.Today;
        public bool IsSelected { get; set; }
        public bool HasReminders { get; }
        public Brush Foreground => IsPast
            ? new SolidColorBrush(Color.FromRgb(150, 150, 150))
            : new SolidColorBrush(Color.FromRgb(31, 49, 87));
        public Brush Background => IsSelected
            ? new SolidColorBrush(Color.FromRgb(255, 162, 166))
            : IsToday
                ? new SolidColorBrush(Color.FromRgb(255, 232, 232))
                : Brushes.Transparent;

        public CalendarDayViewModel(DateTime date, DateTime month, IEnumerable<Reminder> reminders, DateTime selectedDate)
        {
            Date = date.Date;
            IsCurrentMonth = date.Month == month.Month && date.Year == month.Year;
            IsSelected = date.Date == selectedDate.Date;
            HasReminders = reminders.Any(reminder => reminder.ScheduledAt.Date == date.Date);
        }
    }
}
