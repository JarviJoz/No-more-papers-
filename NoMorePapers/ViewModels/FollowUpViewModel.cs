using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace NoMorePapers.ViewModels
{
    public partial class FollowUpViewModel : ObservableObject
    {
        public FollowUpViewModel(int number)
        {
            Number = number;
        }

        public int Number { get; }
        public string Title => $"SEGUIMIENTO #{Number}";

        [ObservableProperty]
        private string _contentRtf = string.Empty;

        [ObservableProperty]
        private DateTime? _followUpDate;

        [ObservableProperty]
        private byte[]? _signatureImage;

        [ObservableProperty]
        private string _signatureFileName = string.Empty;

        [ObservableProperty]
        private DateTime? _savedAt;

        [ObservableProperty]
        private bool _isOpen;

        public bool HasSavedContent =>
            !string.IsNullOrWhiteSpace(ContentRtf) ||
            FollowUpDate.HasValue ||
            SignatureImage is { Length: > 0 };

        partial void OnContentRtfChanged(string value) => OnPropertyChanged(nameof(HasSavedContent));
        partial void OnFollowUpDateChanged(DateTime? value) => OnPropertyChanged(nameof(HasSavedContent));
        partial void OnSignatureImageChanged(byte[]? value) => OnPropertyChanged(nameof(HasSavedContent));
    }
}
