using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace ICOforge
{
    public partial class MainWindow : FluentWindow
    {
        private readonly IconConverterService _converterService = new();
        private readonly ObservableCollection<string> _fileList = new();
        private string _customOutputPath = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
        }

        private void InitializeApplication()
        {
            var iconUri = new Uri("pack://application:,,,/assets/icons/icoforge.ico");
            this.Icon = new BitmapImage(iconUri);
            TitleBar.Icon = new ImageIcon { Source = new BitmapImage(iconUri) { DecodePixelWidth = 32 } };

            FileListView.ItemsSource = _fileList;
            _fileList.CollectionChanged += (s, e) => UpdateDropZoneState();

            _customOutputPath = NativeMethods.GetDownloadsPath();
            CustomLocationText.Text = Path.GetFileName(_customOutputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            var colorOptions = new List<int> { 8, 16, 32, 64, 128, 256 };
            ColorCountComboBox.ItemsSource = colorOptions;
            ColorCountComboBox.SelectedIndex = 5;

            UpdateDropZoneState();
        }

        private void UpdateDropZoneState()
        {
            bool hasFiles = _fileList.Any();
            DropZoneText.Visibility = hasFiles ? Visibility.Collapsed : Visibility.Visible;
            FileListView.IsHitTestVisible = hasFiles;
        }

        #region --- User Interaction: File Management ---

        private void AddFilesMenuItem_Click(object sender, RoutedEventArgs e) => SelectFiles();
        private void AddFolderMenuItem_Click(object sender, RoutedEventArgs e) => SelectFolder();
        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e) => DeleteSelectedFiles();
        private void ClearMenuItem_Click(object sender, RoutedEventArgs e) => _fileList.Clear();

        private void FileListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                DeleteSelectedFiles();
            }
        }

        private async void DropZone_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Border || e.OriginalSource is System.Windows.Controls.TextBlock)
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    var choiceBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "Input Selection",
                        PrimaryButtonText = "Files",
                        SecondaryButtonText = "Folder",
                        CloseButtonText = "Cancel"
                    };

                    var result = await choiceBox.ShowDialogAsync();

                    if (result == Wpf.Ui.Controls.MessageBoxResult.Primary) SelectFiles();
                    else if (result == Wpf.Ui.Controls.MessageBoxResult.Secondary) SelectFolder();
                }
            }
        }

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                AddFilesToList(files);
            }
        }

        private void SelectFiles()
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.svg;*.webp|All files|*.*",
                Title = "Select Image Files"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                AddFilesToList(openFileDialog.FileNames);
            }
        }

        private void SelectFolder()
        {
            var dialog = new CommonOpenFileDialog { IsFolderPicker = true, Title = "Select a folder containing images" };

            if (dialog.ShowDialog(new WindowInteropHelper(this).Handle) == CommonFileDialogResult.Ok)
            {
                string[] validExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".svg", ".webp" };
                var files = Directory.EnumerateFiles(dialog.FileName, "*.*", SearchOption.AllDirectories)
                                     .Where(f => validExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
                AddFilesToList(files);
            }
        }

        private void DeleteSelectedFiles()
        {
            var selectedItems = FileListView.SelectedItems.Cast<string>().ToList();
            foreach (var item in selectedItems)
            {
                _fileList.Remove(item);
            }
        }

        private void AddFilesToList(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                if (!_fileList.Contains(file))
                {
                    _fileList.Add(file);
                }
            }
        }

        #endregion

        #region --- User Interaction: Options ---

        private void OpenColorPicker_MouseDown(object sender, MouseButtonEventArgs e)
        {
            using var colorDialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
            try
            {
                var wpfColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(SvgColorTextBox.Text);
                colorDialog.Color = System.Drawing.Color.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
            }
            catch { /* Ignore invalid hex */ }

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var selectedColor = colorDialog.Color;
                SvgColorTextBox.Text = $"#{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";
            }
        }

        private void BrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            OutputCustom.IsChecked = true;
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select a custom output folder",
                InitialDirectory = _customOutputPath
            };

            if (dialog.ShowDialog(new WindowInteropHelper(this).Handle) == CommonFileDialogResult.Ok)
            {
                _customOutputPath = dialog.FileName;
                CustomLocationText.Text = Path.GetFileName(_customOutputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
        }

        #endregion

        #region --- Conversion Process ---
        private async void ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            if (!AreInputsValid(isIcoConversion: true)) return;

            await RunConversionProcessAsync("ICO", async (outputDir) =>
            {
                var filesToProcess = new List<string>(_fileList);
                var svgHexColor = EnableSvgColorizationCheckBox.IsChecked == true ? SvgColorTextBox.Text : string.Empty;
                var optimizationOptions = GetPngOptimizationOptions();

                var result = await _converterService.ConvertImagesToIcoAsync(filesToProcess, GetSelectedSizes(), svgHexColor, optimizationOptions, outputDir, new Progress<IconConversionProgress>(UpdateProgress));
                HandleIcoConversionResult(result, outputDir);
            });
        }

        private async void FaviconPackButton_Click(object sender, RoutedEventArgs e) => await HandleFaviconCreation(FileListView.SelectedItem as string);
        private async void FaviconPackMenuItem_Click(object sender, RoutedEventArgs e) => await HandleFaviconCreation((sender as System.Windows.Controls.MenuItem)?.DataContext as string);

        private async Task HandleFaviconCreation(string? inputFile)
        {
            var sourceFile = inputFile ?? _fileList.FirstOrDefault();
            if (!AreInputsValid(isIcoConversion: false, singleFile: sourceFile)) return;

            await RunConversionProcessAsync("Favicon", async (outputDir) =>
            {
                var svgHexColor = EnableSvgColorizationCheckBox.IsChecked == true ? SvgColorTextBox.Text : string.Empty;
                var optimizationOptions = GetPngOptimizationOptions();

                string? warning = await _converterService.CreateFaviconPackAsync(sourceFile!, svgHexColor, optimizationOptions, outputDir, new Progress<IconConversionProgress>(UpdateProgress));

                var message = new StringBuilder();
                message.AppendLine($"Favicon pack created successfully in:\n{outputDir}");
                if (!string.IsNullOrEmpty(warning))
                {
                    message.AppendLine($"\n{warning}");
                }
                ShowMessageBox(message.ToString(), "Favicon Pack Created");
                Process.Start("explorer.exe", outputDir);
            });
        }

        private PngOptimizationOptions GetPngOptimizationOptions()
        {
            return new PngOptimizationOptions(
                UseLossyCompressionCheckBox.IsChecked == true,
                UseLosslessCompressionCheckBox.IsChecked == true,
                (int)(ColorCountComboBox.SelectedItem ?? 256)
            );
        }

        private async Task RunConversionProcessAsync(string type, Func<string, Task> conversionAction)
        {
            if (!TryGetOutputDirectory(type, out string outputDirectory)) return;

            ProcessingOverlay.Visibility = Visibility.Visible;
            try
            {
                await conversionAction(outputDirectory);
            }
            catch (Exception ex)
            {
                ShowMessageBox($"An unexpected error occurred during conversion: {ex.Message}", "Fatal Error");
            }
            finally
            {
                ProcessingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private bool AreInputsValid(bool isIcoConversion, string? singleFile = null)
        {
            var files = isIcoConversion ? _fileList.ToList() : (singleFile != null ? new List<string> { singleFile } : new List<string>());
            if (!files.Any())
            {
                ShowMessageBox("Please add or select a file to convert.", "No File");
                return false;
            }
            if (isIcoConversion && !GetSelectedSizes().Any())
            {
                ShowMessageBox("Please select at least one icon size.", "No Sizes Selected");
                return false;
            }
            return true;
        }

        private bool TryGetOutputDirectory(string type, out string outputDirectory)
        {
            string? baseOutputPath = (OutputCustom.IsChecked == true) ? _customOutputPath : Path.GetDirectoryName(_fileList.First());

            if (string.IsNullOrEmpty(baseOutputPath))
            {
                ShowMessageBox("Could not determine the output directory.", "Output Error");
                outputDirectory = string.Empty;
                return false;
            }

            string timestamp = DateTime.Now.ToString("yyMMdd-HHmmss");
            outputDirectory = Path.Combine(baseOutputPath, $"ICOforge-{type}-{timestamp}");
            Directory.CreateDirectory(outputDirectory);
            return true;
        }

        private void HandleIcoConversionResult(ConversionResult result, string outputDirectory)
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("Conversion complete!");
            messageBuilder.AppendLine($"Successful: {result.SuccessfulFiles.Count}, Failed: {result.FailedFiles.Count}.");

            if (result.FailedFiles.Any())
            {
                messageBuilder.AppendLine("\nFailures:");
                string failedFilesSummary = string.Join("\n", result.FailedFiles.Take(10).Select(f => $"- {f.File}: {f.Error}"));
                messageBuilder.AppendLine(failedFilesSummary);
                if (result.FailedFiles.Count > 10)
                {
                    messageBuilder.AppendLine("- ...and more.");
                }
            }

            ShowMessageBox(messageBuilder.ToString(), "Conversion Finished");

            if (result.SuccessfulFiles.Any())
            {
                Process.Start("explorer.exe", outputDirectory);
            }
        }

        private void UpdateProgress(IconConversionProgress progress)
        {
            ConversionProgressBar.Value = progress.Percentage;
            ProgressFileText.Text = progress.CurrentFile;
        }

        private List<int> GetSelectedSizes()
        {
            var sizes = new List<int>();
            if (Size16.IsChecked == true) sizes.Add(16);
            if (Size24.IsChecked == true) sizes.Add(24);
            if (Size32.IsChecked == true) sizes.Add(32);
            if (Size48.IsChecked == true) sizes.Add(48);
            if (Size64.IsChecked == true) sizes.Add(64);
            if (Size72.IsChecked == true) sizes.Add(72);
            if (Size96.IsChecked == true) sizes.Add(96);
            if (Size128.IsChecked == true) sizes.Add(128);
            if (Size256.IsChecked == true) sizes.Add(256);
            return sizes;
        }

        private void ShowMessageBox(string message, string title)
        {
            Dispatcher.Invoke(() =>
            {
                var messageBox = new Wpf.Ui.Controls.MessageBox { Title = title, Content = message, CloseButtonText = "OK" };
                messageBox.ShowDialogAsync();
            });
        }
        #endregion
    }
}