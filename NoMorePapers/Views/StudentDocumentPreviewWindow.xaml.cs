using NoMorePapers.Models;
using NoMorePapers.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace NoMorePapers.Views
{
    public partial class StudentDocumentPreviewWindow : Window
    {
        private readonly StudentDocument _document;
        private double _imageWidth;
        private double _imageHeight;

        public StudentDocumentPreviewWindow(StudentDocument document)
        {
            InitializeComponent();
            _document = document;
            DocumentNameText.Text = document.FileName;
            Loaded += OnWindowLoaded;
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            await LoadPreviewAsync();
        }

        private async Task LoadPreviewAsync()
        {
            if (!File.Exists(_document.FilePath))
            {
                ShowPreviewError("El archivo almacenado no está disponible.");
                return;
            }

            if (_document.FileExtension is ".jpg" or ".jpeg" or ".png")
            {
                LoadImagePreview();
                return;
            }

            if (_document.FileExtension is not ".pdf" and not ".docx" and not ".xlsx" and not ".pptx")
            {
                ShowPreviewError("No se puede previsualizar este tipo de archivo.");
                return;
            }

            try
            {
                PreviewMessageText.Text = "Cargando previsualización...";
                PreviewMessageText.Visibility = Visibility.Visible;
                await DocumentWebView.EnsureCoreWebView2Async();
                DocumentWebView.Visibility = Visibility.Visible;
                PreviewMessageText.Visibility = Visibility.Collapsed;
                ZoomSlider.IsEnabled = true;

                if (_document.FileExtension == ".pdf")
                {
                    DocumentWebView.CoreWebView2.Navigate(new Uri(_document.FilePath, UriKind.Absolute).AbsoluteUri);
                }
                else
                {
                    var html = await Task.Run(() => StudentDocumentPreviewBuilder.BuildOfficePreview(_document.FilePath, _document.FileExtension));
                    DocumentWebView.NavigateToString(html);
                }
            }
            catch (Exception)
            {
                DocumentWebView.Visibility = Visibility.Collapsed;
                ZoomSlider.IsEnabled = false;
                ShowPreviewError("No se pudo cargar la previsualización. Puede abrir el documento con la aplicación predeterminada de Windows.");
            }
        }

        private void LoadImagePreview()
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 1800;
                bitmap.UriSource = new Uri(_document.FilePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                DocumentImage.Source = bitmap;
                _imageWidth = bitmap.PixelWidth;
                _imageHeight = bitmap.PixelHeight;
                DocumentImage.Width = _imageWidth;
                DocumentImage.Height = _imageHeight;
                ImageScrollViewer.Visibility = Visibility.Visible;
                PreviewMessageText.Visibility = Visibility.Collapsed;
                ZoomSlider.IsEnabled = true;
            }
            catch (Exception)
            {
                ShowPreviewError("No se pudo cargar la imagen. Puede abrir el documento con la aplicación predeterminada de Windows.");
            }
        }

        private void ShowPreviewError(string message)
        {
            PreviewMessageText.Text = message;
            PreviewMessageText.Visibility = Visibility.Visible;
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_imageWidth > 0 && _imageHeight > 0)
            {
                DocumentImage.Width = _imageWidth * ZoomSlider.Value;
                DocumentImage.Height = _imageHeight * ZoomSlider.Value;
            }

            if (DocumentWebView.CoreWebView2 != null)
            {
                DocumentWebView.ZoomFactor = ZoomSlider.Value;
            }
        }

        private void OpenWithWindows_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(_document.FilePath))
                {
                    throw new FileNotFoundException();
                }

                Process.Start(new ProcessStartInfo(_document.FilePath) { UseShellExecute = true });
            }
            catch (Exception)
            {
                MessageBox.Show("No se puede abrir el documento con Windows.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
