using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using NoMorePapers.Models;
using NoMorePapers.Services;
using NoMorePapers.ViewModels;

namespace NoMorePapers.Views
{
    /// <summary>
    /// Lógica de interacción para WorkspaceView.xaml
    /// </summary>
    public partial class WorkspaceView : UserControl
    {
        private const double ExpandedSidebarWidth = 218;
        private const double CollapsedSidebarWidth = 72;

        public WorkspaceView()
        {
            InitializeComponent();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not WorkspaceViewModel viewModel)
            {
                return;
            }

            var settingsWindow = new SettingsWindow(
                App.ServiceProvider.GetRequiredService<AppSettingsService>(),
                viewModel.CurrentProfile,
                App.ServiceProvider.GetRequiredService<IProfileService>(),
                App.ServiceProvider.GetRequiredService<MainViewModel>())
            {
                Owner = Window.GetWindow(this)
            };
            settingsWindow.ShowDialog();
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow(App.ServiceProvider.GetRequiredService<ContactSubmissionService>())
            {
                Owner = Window.GetWindow(this)
            };
            aboutWindow.ShowDialog();
        }

        private void WorkspaceView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is WorkspaceViewModel viewModel)
            {
                SidebarBorder.Width = viewModel.IsSidebarExpanded ? ExpandedSidebarWidth : CollapsedSidebarWidth;
                App.ServiceProvider.GetRequiredService<AppSettingsService>().ApplyVisualPreferences();
            }
        }

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not WorkspaceViewModel viewModel)
            {
                return;
            }

            viewModel.IsSidebarExpanded = !viewModel.IsSidebarExpanded;
            SidebarBorder.BeginAnimation(
                WidthProperty,
                new DoubleAnimation(
                    SidebarBorder.ActualWidth,
                    viewModel.IsSidebarExpanded ? ExpandedSidebarWidth : CollapsedSidebarWidth,
                    TimeSpan.FromMilliseconds(250))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
        }

        private void SortCasesButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu is ContextMenu contextMenu)
            {
                contextMenu.PlacementTarget = button;
                contextMenu.IsOpen = true;
            }
        }

        private void CasesDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not WorkspaceViewModel viewModel || e.OriginalSource is not DependencyObject source)
            {
                return;
            }

            var row = FindParent<DataGridRow>(source);
            if (row?.Item is not StudentCase studentCase)
            {
                return;
            }

            var isCheckboxClick = source is CheckBox || FindParent<CheckBox>(source) != null;
            var wasSelected = studentCase.IsSelected;
            viewModel.SelectedCase = studentCase;

            if (!wasSelected)
            {
                studentCase.IsSelected = true;
            }
            else if (!isCheckboxClick && e.ClickCount == 1)
            {
                viewModel.ToggleSelectionCommand.Execute(studentCase);
            }
        }

        private void CasesDataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not DataGrid dataGrid || e.OriginalSource is not DependencyObject source)
            {
                return;
            }

            var row = FindParent<DataGridRow>(source);
            if (row?.Item is StudentCase studentCase && DataContext is WorkspaceViewModel viewModel)
            {
                viewModel.SelectedCase = studentCase;
            }
        }

        private void ReminderListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBox listBox || listBox.SelectedItem is not Reminder reminder)
            {
                return;
            }

            var detailsWindow = new ReminderDetailsWindow(
                reminder,
                DataContext is WorkspaceViewModel viewModel
                    ? () => viewModel.DeleteReminderFromDetailsAsync(reminder)
                    : () => Task.CompletedTask)
            {
                Owner = Window.GetWindow(this)
            };
            detailsWindow.ShowDialog();
        }

        private void ReminderTimePicker_SelectedTimeChanged(object sender, RoutedPropertyChangedEventArgs<DateTime?> e)
        {
            if (DataContext is WorkspaceViewModel viewModel && sender is TimePicker timePicker)
            {
                viewModel.ReminderSelectedTime = timePicker.SelectedTime;
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T matchingParent)
                {
                    return matchingParent;
                }

                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }

        private async void CasesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.WorkspaceViewModel viewModel && viewModel.SelectedCase != null)
            {
                await viewModel.OpenEditorAsync(viewModel.SelectedCase);
            }
        }
    }

    public sealed class CategoryDisplayConverter : IValueConverter
    {
        private static readonly IReadOnlyDictionary<string, string> CategoryEmojis = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Emocional"] = "💜",
            ["Académico"] = "📚",
            ["Familiar"] = "👨‍👩‍👧",
            ["Conductual"] = "⚠️"
        };

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string categories || string.IsNullOrWhiteSpace(categories))
            {
                return value ?? string.Empty;
            }

            return string.Join(", ", categories.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(category => CategoryEmojis.TryGetValue(category, out var emoji) ? $"{emoji} {category}" : category));
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
