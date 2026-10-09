using NoMorePapers.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NoMorePapers.ViewModels
{
    public class SiblingSearchEntry : ObservableObject
    {
        private string _searchText = string.Empty;
        private Student? _selectedStudent;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    SelectedStudent = null;
                    SearchTextChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public Student? SelectedStudent
        {
            get => _selectedStudent;
            set
            {
                if (SetProperty(ref _selectedStudent, value))
                {
                    OnPropertyChanged(nameof(HasSelectedStudent));
                }
            }
        }

        public ObservableCollection<Student> Suggestions { get; } = new();

        public event EventHandler? SearchTextChanged;

        public bool HasMatches => Suggestions.Count > 0;
        public bool HasSelectedStudent => SelectedStudent != null;

        public void SetSuggestions(IEnumerable<Student> students)
        {
            Suggestions.Clear();
            foreach (var student in students)
            {
                Suggestions.Add(student);
            }

            OnPropertyChanged(nameof(HasMatches));
        }
    }
}
