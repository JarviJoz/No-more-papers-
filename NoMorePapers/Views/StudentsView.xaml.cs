using System.Windows.Controls;
using System.Windows.Input;
using NoMorePapers.ViewModels;

namespace NoMorePapers.Views
{
    public partial class StudentsView : UserControl
    {
        public StudentsView()
        {
            InitializeComponent();
        }

        private void StudentsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is StudentsViewModel viewModel && viewModel.SelectedStudent != null)
            {
                viewModel.OpenStudentProfileCommand.Execute(null);
            }
        }
    }
}
