using Microsoft.VisualBasic;
using Microsoft.Win32;
using MaterialDesignThemes.Wpf;
using NoMorePapers.ViewModels;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace NoMorePapers.Views
{
    /// <summary>
    /// Lógica de interacción para AddEditCaseWindow.xaml
    /// </summary>
    public partial class AddEditCaseWindow : Window
    {
        private RichTextBox Editor => (RichTextBox)FindName("FollowUpEditor");
        private Image? _selectedImage;
        private ImageResizeAdorner? _imageResizeAdorner;
        private PackIconKind? _pendingAvatarKind;
        private BitmapImage? _pendingAvatarImage;
        private byte[]? _pendingAvatarBytes;
        private PackIconKind _avatarBeforeMenuKind;
        private Brush? _avatarBeforeMenuForeground;
        private BitmapImage? _avatarBeforeMenuImage;
        private bool _avatarBeforeMenuUsesImage;
        private bool _hasSelectedAvatar;

        private void AvatarMale_Click(object sender, RoutedEventArgs e) => SelectPredefinedAvatar(PackIconKind.Account);

        private void AvatarFemale_Click(object sender, RoutedEventArgs e) => SelectPredefinedAvatar(PackIconKind.AccountOutline);

        public AddEditCaseWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void EditCaseNavigation_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavigation(EditCaseNavigationButton);
            SetActiveTab(InfoTabButton);
            CaseInformationPanel.BringIntoView();
        }

        private void FollowUpsNavigation_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavigation(FollowUpsNavigationButton);
            SetActiveTab(FollowUpsTabButton);
            FollowUpSection.BringIntoView();
        }

        private void ObservationsNavigation_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNavigation(ObservationsNavigationButton);
            SetActiveTab(InfoTabButton);
            ObservationsCard.BringIntoView();
        }

        private void InfoTab_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTab(InfoTabButton);
            SetActiveNavigation(EditCaseNavigationButton);
            CaseInformationPanel.BringIntoView();
        }

        private void FollowUpsTab_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTab(FollowUpsTabButton);
            SetActiveNavigation(FollowUpsNavigationButton);
            FollowUpSection.BringIntoView();
        }

        private void SetActiveNavigation(Button activeButton)
        {
            foreach (var button in new[]
            {
                EditCaseNavigationButton,
                FollowUpsNavigationButton,
                ObservationsNavigationButton
            })
            {
                button.Tag = button == activeButton ? "Active" : null;
            }
        }

        private void SetActiveTab(Button activeButton)
        {
            InfoTabButton.Tag = activeButton == InfoTabButton ? "Active" : null;
            FollowUpsTabButton.Tag = activeButton == FollowUpsTabButton ? "Active" : null;
        }

        private void SelectPredefinedAvatar(PackIconKind kind)
        {
            _pendingAvatarImage = null;
            _pendingAvatarBytes = null;
            _pendingAvatarKind = kind;
            PreviewPendingAvatar();
        }

        private void AvatarEditButton_Click(object sender, RoutedEventArgs e)
        {
            _avatarBeforeMenuUsesImage = StudentPhoto.Visibility == Visibility.Visible;
            _avatarBeforeMenuImage = StudentPhoto.Source as BitmapImage;
            _avatarBeforeMenuKind = DefaultAvatarIcon.Kind;
            _avatarBeforeMenuForeground = DefaultAvatarIcon.Foreground;
            _pendingAvatarKind = null;
            _pendingAvatarImage = null;
            _pendingAvatarBytes = null;
            AvatarMenu.IsOpen = true;
        }

        private void AvatarOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                _pendingAvatarImage = null;
                _pendingAvatarKind = button.Tag?.ToString() == "Female"
                    ? PackIconKind.AccountOutline
                    : PackIconKind.Account;
                _pendingAvatarBytes = null;
                PreviewPendingAvatar();
            }
        }

        private void SelectAvatarFromDevice_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp|Todos los archivos|*.*",
                Title = "Seleccionar imagen del dispositivo"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var image = new BitmapImage();
            using (var stream = File.OpenRead(dialog.FileName))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
            }
            image.Freeze();

            _pendingAvatarImage = image;
            _pendingAvatarBytes = File.ReadAllBytes(dialog.FileName);
            _pendingAvatarKind = null;
            PreviewPendingAvatar();
        }

        private void ApplyAvatar_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingAvatarImage != null)
            {
                StudentPhoto.Source = _pendingAvatarImage;
                StudentPhoto.Visibility = Visibility.Visible;
                DefaultAvatarIcon.Visibility = Visibility.Collapsed;
                _hasSelectedAvatar = true;
                UpdateViewModelAvatar("Custom", _pendingAvatarBytes);
            }
            else if (_pendingAvatarKind.HasValue)
            {
                DefaultAvatarIcon.Kind = _pendingAvatarKind.Value;
                DefaultAvatarIcon.Foreground = _pendingAvatarKind == PackIconKind.AccountOutline
                    ? new SolidColorBrush(Color.FromRgb(237, 138, 180))
                    : new SolidColorBrush(Color.FromRgb(107, 164, 122));
                DefaultAvatarIcon.Visibility = Visibility.Visible;
                StudentPhoto.Visibility = Visibility.Collapsed;
                _hasSelectedAvatar = true;
                UpdateViewModelAvatar(_pendingAvatarKind == PackIconKind.AccountOutline ? "Female" : "Male", null);
            }

            _pendingAvatarImage = null;
            _pendingAvatarBytes = null;
            _pendingAvatarKind = null;
            AvatarMenu.IsOpen = false;
        }

        private void CancelAvatar_Click(object sender, RoutedEventArgs e)
        {
            if (_avatarBeforeMenuUsesImage && _avatarBeforeMenuImage != null)
            {
                StudentPhoto.Source = _avatarBeforeMenuImage;
                StudentPhoto.Visibility = Visibility.Visible;
                DefaultAvatarIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                DefaultAvatarIcon.Kind = _avatarBeforeMenuKind;
                DefaultAvatarIcon.Foreground = _avatarBeforeMenuForeground ?? Brushes.LightSlateGray;
                DefaultAvatarIcon.Visibility = Visibility.Visible;
                StudentPhoto.Visibility = Visibility.Collapsed;
            }

            _hasSelectedAvatar = _avatarBeforeMenuUsesImage || _avatarBeforeMenuKind != PackIconKind.Account;
            RemoveAvatarButton.Visibility = _hasSelectedAvatar ? Visibility.Visible : Visibility.Collapsed;
            _pendingAvatarImage = null;
            _pendingAvatarBytes = null;
            _pendingAvatarKind = null;
            AvatarMenu.IsOpen = false;
        }

        private void RemoveAvatar_Click(object sender, RoutedEventArgs e)
        {
            StudentPhoto.Source = null;
            StudentPhoto.Visibility = Visibility.Collapsed;
            DefaultAvatarIcon.Kind = PackIconKind.Account;
            DefaultAvatarIcon.Foreground = new SolidColorBrush(Color.FromRgb(120, 144, 156));
            DefaultAvatarIcon.Visibility = Visibility.Visible;
            _hasSelectedAvatar = false;
            _pendingAvatarImage = null;
            _pendingAvatarBytes = null;
            _pendingAvatarKind = null;
            UpdateViewModelAvatar("DefaultMale", null);
            AvatarMenu.IsOpen = false;
        }

        private void PreviewPendingAvatar()
        {
            if (_pendingAvatarImage != null)
            {
                StudentPhoto.Source = _pendingAvatarImage;
                StudentPhoto.Visibility = Visibility.Visible;
                DefaultAvatarIcon.Visibility = Visibility.Collapsed;
            }
            else if (_pendingAvatarKind.HasValue)
            {
                DefaultAvatarIcon.Kind = _pendingAvatarKind.Value;
                DefaultAvatarIcon.Foreground = _pendingAvatarKind == PackIconKind.AccountOutline
                    ? new SolidColorBrush(Color.FromRgb(237, 138, 180))
                    : new SolidColorBrush(Color.FromRgb(107, 164, 122));
                DefaultAvatarIcon.Visibility = Visibility.Visible;
                StudentPhoto.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplySavedAvatar(AddEditCaseViewModel viewModel)
        {
            if (viewModel.AvatarImage is { Length: > 0 })
            {
                var image = CreateBitmapImage(viewModel.AvatarImage);
                StudentPhoto.Source = image;
                StudentPhoto.Visibility = Visibility.Visible;
                DefaultAvatarIcon.Visibility = Visibility.Collapsed;
                _hasSelectedAvatar = true;
            }
            else if (viewModel.AvatarType == "Female")
            {
                DefaultAvatarIcon.Kind = PackIconKind.AccountOutline;
                DefaultAvatarIcon.Foreground = new SolidColorBrush(Color.FromRgb(237, 138, 180));
                DefaultAvatarIcon.Visibility = Visibility.Visible;
                StudentPhoto.Visibility = Visibility.Collapsed;
                _hasSelectedAvatar = true;
            }
            else
            {
                DefaultAvatarIcon.Kind = PackIconKind.Account;
                DefaultAvatarIcon.Foreground = viewModel.AvatarType == "Male"
                    ? new SolidColorBrush(Color.FromRgb(107, 164, 122))
                    : new SolidColorBrush(Color.FromRgb(120, 144, 156));
                DefaultAvatarIcon.Visibility = Visibility.Visible;
                StudentPhoto.Visibility = Visibility.Collapsed;
                _hasSelectedAvatar = false;
            }

            RemoveAvatarButton.Visibility = _hasSelectedAvatar ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateViewModelAvatar(string type, byte[]? image)
        {
            if (DataContext is AddEditCaseViewModel viewModel)
            {
                viewModel.AvatarType = type;
                viewModel.AvatarImage = image;
            }

            RemoveAvatarButton.Visibility = _hasSelectedAvatar ? Visibility.Visible : Visibility.Collapsed;
        }

        private static BitmapImage CreateBitmapImage(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is AddEditCaseViewModel oldViewModel)
            {
                oldViewModel.FollowUpOpened -= OnFollowUpOpened;
            }

            if (e.NewValue is AddEditCaseViewModel newViewModel)
            {
                newViewModel.FollowUpOpened += OnFollowUpOpened;
                ApplySavedAvatar(newViewModel);
            }
        }

        private void OnFollowUpOpened(FollowUpViewModel followUp)
        {
            if (FollowUpLayoutPanel.Children.Contains(FollowUpEditorPanel))
            {
                FollowUpLayoutPanel.Children.Remove(FollowUpEditorPanel);
            }

            var editorIndex = Math.Min(followUp.Number, FollowUpLayoutPanel.Children.Count);
            FollowUpLayoutPanel.Children.Insert(editorIndex, FollowUpEditorPanel);

            ClearImageSelection();
            Editor.Document = new FlowDocument();

            if (!string.IsNullOrWhiteSpace(followUp.ContentRtf))
            {
                var range = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd);
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(followUp.ContentRtf));
                range.Load(stream, DataFormats.Rtf);
                AttachImageHandlers();
            }
        }

        private void AttachImageHandlers()
        {
            foreach (var block in Editor.Document.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    AttachImageHandlers(paragraph.Inlines);
                }
            }
        }

        private void AttachImageHandlers(InlineCollection inlines)
        {
            foreach (var inline in inlines)
            {
                if (inline is InlineUIContainer container && container.Child is Image image)
                {
                    image.MouseLeftButtonDown -= Image_MouseLeftButtonDown;
                    image.MouseLeftButtonDown += Image_MouseLeftButtonDown;
                }
                else if (inline is Span span)
                {
                    AttachImageHandlers(span.Inlines);
                }
            }
        }

        private void SaveFollowUp_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AddEditCaseViewModel viewModel)
            {
                _ = viewModel.SaveActiveFollowUpAsync(SerializeEditor());
            }
        }

        private string SerializeEditor()
        {
            var range = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd);
            using var stream = new MemoryStream();
            range.Save(stream, DataFormats.Rtf);
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }

        private void Signature_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AddEditCaseViewModel viewModel || viewModel.ActiveFollowUp == null)
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp|Todos los archivos|*.*",
                Title = "Seleccionar firma"
            };

            if (dialog.ShowDialog() == true)
            {
                var imageBytes = File.ReadAllBytes(dialog.FileName);
                viewModel.ActiveFollowUp.SignatureImage = imageBytes;
                viewModel.ActiveFollowUp.SignatureFileName = Path.GetFileName(dialog.FileName);
                InsertImageAtCaret(imageBytes);
            }
        }

        private void InsertImageAtCaret(byte[] imageBytes)
        {
            var bitmap = new BitmapImage();
            using (var stream = new MemoryStream(imageBytes))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            bitmap.Freeze();

            var image = new Image
            {
                Source = bitmap,
                Width = Math.Min(bitmap.PixelWidth, 320),
                Height = Math.Min(bitmap.PixelHeight, 180),
                Stretch = Stretch.Uniform,
                ToolTip = "Arrastre la esquina para redimensionar. Ctrl + rueda también permite cambiar el tamaño."
            };
            image.MouseLeftButtonDown += Image_MouseLeftButtonDown;

            var container = new InlineUIContainer(image, Editor.CaretPosition);
            Editor.CaretPosition = container.ElementEnd;
            SelectImage(image);
            Editor.Focus();
        }

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image image)
            {
                SelectImage(image);
                e.Handled = true;
            }
        }

        private void SelectImage(Image image)
        {
            ClearImageSelection();

            _selectedImage = image;
            _selectedImage.Opacity = 0.85;

            if (AdornerLayer.GetAdornerLayer(image) is AdornerLayer adornerLayer)
            {
                _imageResizeAdorner = new ImageResizeAdorner(image);
                adornerLayer.Add(_imageResizeAdorner);
            }
        }

        private void ClearImageSelection()
        {
            if (_imageResizeAdorner != null && AdornerLayer.GetAdornerLayer(_imageResizeAdorner.AdornedElement) is AdornerLayer adornerLayer)
            {
                adornerLayer.Remove(_imageResizeAdorner);
            }

            if (_selectedImage != null)
            {
                _selectedImage.Opacity = 1;
            }

            _imageResizeAdorner = null;
            _selectedImage = null;
        }

        private void FollowUpEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_selectedImage == null || (e.Key != Key.Back && e.Key != Key.Delete))
            {
                return;
            }

            if (_selectedImage.Parent is InlineUIContainer container && container.Parent is Paragraph paragraph)
            {
                paragraph.Inlines.Remove(container);
                ClearImageSelection();
                Editor.Focus();
                e.Handled = true;
            }
        }

        private void FollowUpEditor_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not Image)
            {
                ClearImageSelection();
            }
        }

        private void FollowUpEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_selectedImage == null || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                return;
            }

            var factor = e.Delta > 0 ? 1.1 : 0.9;
            var width = Math.Max(40, _selectedImage.Width * factor);
            var height = Math.Max(40, _selectedImage.Height * factor);
            _selectedImage.Width = width;
            _selectedImage.Height = height;
            e.Handled = true;
        }

        private void ExecuteEditingCommand(RoutedCommand command)
        {
            command.Execute(null, Editor);
            Editor.Focus();
        }

        private void Bold_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.ToggleBold);
        private void Italic_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.ToggleItalic);
        private void Underline_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.ToggleUnderline);
        private void StrikeThrough_Click(object sender, RoutedEventArgs e)
        {
            Editor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Strikethrough);
            Editor.Focus();
        }
        private void Bullets_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.ToggleBullets);
        private void Numbering_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.ToggleNumbering);
        private void AlignLeft_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.AlignLeft);
        private void AlignCenter_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.AlignCenter);
        private void AlignRight_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.AlignRight);
        private void AlignJustify_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(EditingCommands.AlignJustify);
        private void Undo_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(ApplicationCommands.Undo);
        private void Redo_Click(object sender, RoutedEventArgs e) => ExecuteEditingCommand(ApplicationCommands.Redo);

        private sealed class ImageResizeAdorner : Adorner
        {
            private const double HandleSize = 12;
            private readonly Image _image;
            private readonly Thumb _resizeThumb;
            private readonly double _aspectRatio;

            public ImageResizeAdorner(Image image) : base(image)
            {
                _image = image;
                _aspectRatio = image.Width / image.Height;
                _resizeThumb = new Thumb
                {
                    Width = HandleSize,
                    Height = HandleSize,
                    Background = Brushes.White,
                    BorderBrush = Brushes.DodgerBlue,
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.SizeNWSE,
                    ToolTip = "Arrastrar para redimensionar"
                };
                _resizeThumb.DragDelta += ResizeThumb_DragDelta;
                AddVisualChild(_resizeThumb);
            }

            protected override int VisualChildrenCount => 1;

            protected override Visual GetVisualChild(int index)
            {
                if (index != 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _resizeThumb;
            }

            protected override Size MeasureOverride(Size constraint)
            {
                _resizeThumb.Measure(constraint);
                return _image.DesiredSize;
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                _resizeThumb.Arrange(new Rect(
                    Math.Max(0, finalSize.Width - HandleSize),
                    Math.Max(0, finalSize.Height - HandleSize),
                    HandleSize,
                    HandleSize));
                return finalSize;
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                drawingContext.DrawRectangle(null, new Pen(Brushes.DodgerBlue, 1) { DashStyle = DashStyles.Dash },
                    new Rect(0, 0, AdornedElement.RenderSize.Width, AdornedElement.RenderSize.Height));
            }

            private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
            {
                var width = Math.Max(40, _image.Width + e.HorizontalChange);
                var height = Math.Max(40, width / _aspectRatio);
                _image.Width = width;
                _image.Height = height;
                InvalidateArrange();
            }
        }

        private void Highlight_Click(object sender, RoutedEventArgs e)
        {
            Editor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Yellow);
            Editor.Focus();
        }

        private void TextColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string colorName && ColorConverter.ConvertFromString(colorName) is Color color)
            {
                Editor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color));
                Editor.Focus();
            }
        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            if (Editor.Selection.IsEmpty)
            {
                return;
            }

            var url = Interaction.InputBox("Escriba la dirección del enlace:", "Insertar enlace", "https://");
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                Editor.Selection.ApplyPropertyValue(Hyperlink.NavigateUriProperty, uri);
                Editor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
                Editor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Blue);
                Editor.Focus();
            }
        }
    }
}
