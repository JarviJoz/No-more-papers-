using System.Windows.Controls;
using System.Windows.Input;
using NoMorePapers.ViewModels;

namespace NoMorePapers.Views
{
    public partial class StudentProfileView : UserControl
    {
        public StudentProfileView()
        {
            InitializeComponent();
        }

        private void CasesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is StudentProfileViewModel viewModel && viewModel.SelectedCase != null)
            {
                viewModel.OpenSelectedCaseCommand.Execute(null);
            }
        }
    }
}
