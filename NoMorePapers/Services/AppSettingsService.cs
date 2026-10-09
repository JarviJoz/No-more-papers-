using NoMorePapers.Models;
using MaterialDesignThemes.Wpf;
using System.IO;
using System.Media;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NoMorePapers.Services
{
    public sealed class AppSettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        private static readonly ConditionalWeakTable<FrameworkElement, FontSizeSnapshot> FontSizeSnapshots = new();
        private readonly string _settingsDirectory;
        private readonly string _settingsPath;
        private bool _removeLegacyTransparencySetting;

        public AppSettings Settings { get; private set; }

        public AppSettingsService()
        {
            _settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoMorePapers", "Settings");
            _settingsPath = Path.Combine(_settingsDirectory, "preferences.json");
            Settings = Load();
            if (_removeLegacyTransparencySetting)
            {
                Save();
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(_settingsDirectory);
            var temporaryPath = _settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(Settings, JsonOptions));
            File.Move(temporaryPath, _settingsPath, true);
            ApplyVisualPreferences();
        }

        public void Reset()
        {
            var customSoundPath = Settings.CustomSoundPath;
            Settings = new AppSettings();
            DeleteCustomSoundIfManaged(customSoundPath);
            Save();
        }

        public void Restore(AppSettings settings, byte[]? customSoundBytes)
        {
            var managedSoundPath = Path.Combine(_settingsDirectory, "notification.wav");
            DeleteCustomSoundIfManaged(Settings.CustomSoundPath);
            Settings = settings;
            if (customSoundBytes != null && !string.IsNullOrWhiteSpace(settings.CustomSoundPath))
            {
                Directory.CreateDirectory(_settingsDirectory);
                File.WriteAllBytes(managedSoundPath, customSoundBytes);
                Settings.CustomSoundPath = managedSoundPath;
            }
            else if (!string.IsNullOrWhiteSpace(settings.CustomSoundPath))
            {
                Settings.CustomSoundPath = null;
            }

            Save();
        }

        public string SetCustomSound(string sourcePath)
        {
            var fileInfo = new FileInfo(sourcePath);
            if (!fileInfo.Exists || fileInfo.Length == 0 || fileInfo.Length > 5 * 1024 * 1024 ||
                !string.Equals(fileInfo.Extension, ".wav", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Selecciona un archivo WAV válido de hasta 5 MB.");
            }

            using (var player = new SoundPlayer(sourcePath))
            {
                player.Load();
            }

            Directory.CreateDirectory(_settingsDirectory);
            var destination = Path.Combine(_settingsDirectory, "notification.wav");
            File.Copy(sourcePath, destination, true);
            Settings.CustomSoundPath = destination;
            Save();
            return destination;
        }

        public void RemoveCustomSound()
        {
            var customSoundPath = Settings.CustomSoundPath;
            Settings.CustomSoundPath = null;
            DeleteCustomSoundIfManaged(customSoundPath);
            Save();
        }

        public bool AreNotificationsEnabled
        {
            get
            {
                if (Settings.NotificationsEnabled)
                {
                    return true;
                }

                if (Settings.NotificationsDisabledUntil is null || Settings.NotificationsDisabledUntil <= DateTime.Now)
                {
                    Settings.NotificationsEnabled = true;
                    Settings.NotificationsDisabledUntil = null;
                    Save();
                    return true;
                }

                return false;
            }
        }

        public string CustomSoundDisplayName => Settings.CustomSoundPath is { } path && File.Exists(path)
            ? Path.GetFileName(path)
            : "Sonido predeterminado de la aplicación";

        public void ApplyVisualPreferences()
        {
            var application = Application.Current;
            if (application == null)
            {
                return;
            }

            var dark = Settings.Appearance == "Oscuro" ||
                (Settings.Appearance == "Seguir configuración del sistema" && IsSystemDarkMode());
            SetResourceColor(application, "AppBackgroundBrush", dark ? "#111827" : "#F4F7FC");
            SetResourceColor(application, "SurfaceBrush", dark ? "#1F2937" : "#FFFFFF");
            SetResourceColor(application, "SurfaceMutedBrush", dark ? "#273449" : "#F2F5FC");
            SetResourceColor(application, "TextPrimaryBrush", dark ? "#F3F4F6" : "#17264A");
            SetResourceColor(application, "TextSecondaryBrush", dark ? "#B8C2D4" : "#68799F");
            SetResourceColor(application, "BorderBrush", dark ? "#374151" : "#E3EAF7");
            SetResourceColor(application, "PrimaryDarkBrush", dark ? "#DCE6FF" : "#1B2E62");
            var accent = ParseColor(Settings.AccentColor, Color.FromRgb(0x31, 0x5B, 0xFA));
            var palette = new PaletteHelper();
            var materialTheme = palette.GetTheme();
            materialTheme.SetPrimaryColor(accent);
            palette.SetTheme(materialTheme);
            SetResourceColor(application, "PrimaryBrush", accent.ToString());
            var primaryHover = Blend(accent, Colors.Black, 0.12);
            SetResourceColor(application, "PrimaryHoverBrush", primaryHover.ToString());
            SetResourceColor(application, "PrimaryHoverForegroundBrush", GetContrastingTextColor(primaryHover).ToString());
            SetResourceColor(application, "PrimaryPressedBrush", Blend(accent, Colors.Black, 0.22).ToString());
            SetResourceColor(application, "PrimaryForegroundBrush", GetContrastingTextColor(accent).ToString());
            SetResourceColor(application, "PrimaryPressedForegroundBrush", GetContrastingTextColor(Blend(accent, Colors.Black, 0.22)).ToString());
            SetResourceColor(application, "PrimaryTextBrush", GetReadableAccent(accent, dark).ToString());
            var sidebar = Blend(accent, Color.FromRgb(0x10, 0x18, 0x27), 0.68);
            SetResourceColor(application, "SidebarBrush", sidebar.ToString());
            SetResourceColor(application, "SidebarHoverBrush", Blend(sidebar, accent, 0.22).ToString());
            SetResourceColor(application, "SidebarSelectedBrush", Blend(sidebar, accent, 0.42).ToString());

            var fontScale = Settings.FontSize switch
            {
                "Pequeño" => 0.9d,
                "Grande" => 1.12d,
                _ => 1d
            };
            if (application.MainWindow != null)
            {
                ApplyFontScale(application.MainWindow, fontScale);
            }
        }

        private AppSettings Load()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return new AppSettings();
                }

                var settingsJson = File.ReadAllText(_settingsPath);
                using var settingsDocument = JsonDocument.Parse(settingsJson);
                _removeLegacyTransparencySetting = settingsDocument.RootElement.TryGetProperty("Transparency", out _);
                var loaded = JsonSerializer.Deserialize<AppSettings>(settingsJson) ?? new AppSettings();
                loaded.NotificationVolume = Math.Clamp(loaded.NotificationVolume, 0, 100);
                if (loaded.FontSize is not ("Pequeño" or "Mediano" or "Grande"))
                {
                    loaded.FontSize = "Mediano";
                }

                if (loaded.Appearance is not ("Claro" or "Oscuro" or "Seguir configuración del sistema"))
                {
                    loaded.Appearance = "Claro";
                }

                var managedSoundPath = Path.Combine(_settingsDirectory, "notification.wav");
                if (loaded.CustomSoundPath is not { } soundPath ||
                    !string.Equals(Path.GetFullPath(soundPath), Path.GetFullPath(managedSoundPath), StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(soundPath))
                {
                    loaded.CustomSoundPath = null;
                }
                return loaded;
            }
            catch
            {
                return new AppSettings();
            }
        }

        private static Color ParseColor(string colorText, Color fallback)
        {
            return ColorConverter.ConvertFromString(colorText) is Color color ? color : fallback;
        }

        private static Color Blend(Color source, Color target, double amount)
        {
            return Color.FromRgb(
                (byte)Math.Round(source.R + (target.R - source.R) * amount),
                (byte)Math.Round(source.G + (target.G - source.G) * amount),
                (byte)Math.Round(source.B + (target.B - source.B) * amount));
        }

        private static Color GetContrastingTextColor(Color background)
        {
            var whiteContrast = GetContrastRatio(background, Colors.White);
            var darkText = Colors.Black;
            return whiteContrast >= GetContrastRatio(background, darkText) ? Colors.White : darkText;
        }

        private static Color GetReadableAccent(Color accent, bool dark)
        {
            var surface = dark ? Color.FromRgb(0x1F, 0x29, 0x37) : Colors.White;
            var target = dark ? Colors.White : Colors.Black;
            for (var amount = 0d; amount <= 1d; amount += 0.025)
            {
                var readableAccent = Blend(accent, target, amount);
                if (GetContrastRatio(readableAccent, surface) >= 4.5)
                {
                    return readableAccent;
                }
            }

            return target;
        }

        private static double GetContrastRatio(Color first, Color second)
        {
            var firstLuminance = GetRelativeLuminance(first);
            var secondLuminance = GetRelativeLuminance(second);
            return (Math.Max(firstLuminance, secondLuminance) + 0.05) /
                   (Math.Min(firstLuminance, secondLuminance) + 0.05);
        }

        private static double GetRelativeLuminance(Color color)
        {
            static double Linearize(byte channel)
            {
                var value = channel / 255d;
                return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Linearize(color.R) + 0.7152 * Linearize(color.G) + 0.0722 * Linearize(color.B);
        }

        private static void ApplyFontScale(DependencyObject root, double scale)
        {
            var elements = new List<FrameworkElement>();
            CollectFrameworkElements(root, elements);
            var fontElements = elements
                .Where(element => element is TextBlock or Control)
                .Select(element => (Element: element, Snapshot: FontSizeSnapshots.GetValue(
                    element,
                    _ => new FontSizeSnapshot(element is TextBlock textBlock ? textBlock.FontSize : ((Control)element).FontSize))))
                .ToList();

            foreach (var (element, snapshot) in fontElements)
            {
                if (element is TextBlock textBlock)
                {
                    textBlock.FontSize = snapshot.Value * scale;
                }
                else if (element is Control control)
                {
                    control.FontSize = snapshot.Value * scale;
                }
            }
        }

        private static void CollectFrameworkElements(DependencyObject parent, List<FrameworkElement> elements)
        {
            if (parent is FrameworkElement frameworkElement)
            {
                elements.Add(frameworkElement);
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                CollectFrameworkElements(VisualTreeHelper.GetChild(parent, index), elements);
            }
        }

        private sealed record FontSizeSnapshot(double Value);

        private static void SetResourceColor(Application application, string key, string colorText)
        {
            if (ColorConverter.ConvertFromString(colorText) is not Color color)
            {
                return;
            }

            if (application.Resources[key] is SolidColorBrush brush)
            {
                if (brush.IsFrozen)
                {
                    var mutableBrush = brush.Clone();
                    mutableBrush.Color = color;
                    application.Resources[key] = mutableBrush;
                }
                else
                {
                    brush.Color = color;
                }
            }
            else
            {
                application.Resources[key] = new SolidColorBrush(color);
            }
        }

        private static bool IsSystemDarkMode()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 1)) == 0;
            }
            catch
            {
                return false;
            }
        }

        private void DeleteCustomSoundIfManaged(string? path)
        {
            var managedSoundPath = Path.Combine(_settingsDirectory, "notification.wav");
            if (!string.IsNullOrWhiteSpace(path) &&
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(managedSoundPath), StringComparison.OrdinalIgnoreCase) &&
                File.Exists(managedSoundPath))
            {
                File.Delete(managedSoundPath);
            }
        }
    }
}