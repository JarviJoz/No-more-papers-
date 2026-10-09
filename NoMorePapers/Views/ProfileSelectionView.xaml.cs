using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using NoMorePapers.ViewModels;

namespace NoMorePapers.Views
{
    /// <summary>
    /// Lógica de interacción para ProfileSelectionView.xaml
    /// </summary>
    public partial class ProfileSelectionView : UserControl
    {
        public ProfileSelectionView()
        {
            InitializeComponent();
        }

        private void ProfilePasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not PasswordBox passwordBox)
            {
                return;
            }

            var itemsControl = FindParent<ItemsControl>(passwordBox);
            if (itemsControl?.DataContext is ProfileSelectionViewModel viewModel)
            {
                viewModel.PasswordInput = passwordBox.Password;
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
    }
}
