using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace ICOforge
{
    public partial class MainWindow : FluentWindow
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly IcoAnalyzerService _analyzerService = new();
        private static readonly string[] ValidImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".svg", ".webp", ".tif", ".tiff" };

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel
            {
                ShowMessageBoxAction = ShowMessageBox,
                OpenInExplorerAction = (path) => Process.Start("explorer.exe", path)
            };
            DataContext = _viewModel;

            Loaded += OnMainWindowLoaded;
        }

        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            InitializeApplicationIcon();
        }

        private void InitializeApplicationIcon()
        {
            var iconUri = new Uri("pack://application:,,,/assets/icons/icoforge.ico");
            try
            {
                BitmapDecoder decoder = BitmapDecoder.Create(iconUri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var bestFrame = decoder.Frames.OrderByDescending(f => f.Width).FirstOrDefault();
                if (bestFrame != null)
                {
                    TitleBar.Icon = new ImageIcon { Source = bestFrame };
                    LogoImage.Source = bestFrame;
                }
            }
            catch
            {
                var fallbackSource = BitmapFrame.Create(iconUri);
                TitleBar.Icon = new ImageIcon { Source = fallbackSource };
                LogoImage.Source = fallbackSource;
            }
        }

        private void AddFilesMenuItem_Click(object sender, RoutedEventArgs e) => SelectFiles();
        private void AddFolderMenuItem_Click(object sender, RoutedEventArgs e) => SelectFolder();

        private void FileListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                _viewModel.DeleteSelectedFilesCommand.Execute(null);
            }
        }

        private void DropZone_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.ContextMenu != null)
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    border.ContextMenu.PlacementTarget = border;
                    border.ContextMenu.IsOpen = true;
                    e.Handled = true;
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
                _viewModel.AddFilesToList(files);
            }
        }

        private void SelectFiles()
        {
            var dialog = new CommonOpenFileDialog
            {
                Multiselect = true,
                Title = "Select Image Files"
            };

            var filterExtensions = string.Join(";", ValidImageExtensions.Select(ext => ext.TrimStart('.')));
            dialog.Filters.Add(new CommonFileDialogFilter("Image Files", filterExtensions));
            dialog.Filters.Add(new CommonFileDialogFilter("All files", "*.*"));

            if (dialog.ShowDialog(new WindowInteropHelper(this).Handle) == CommonFileDialogResult.Ok)
            {
                _viewModel.AddFilesToList(dialog.FileNames);
            }
        }

        private void SelectFolder()
        {
            var dialog = new CommonOpenFileDialog { IsFolderPicker = true, Title = "Select a folder containing images" };

            if (dialog.ShowDialog(new WindowInteropHelper(this).Handle) == CommonFileDialogResult.Ok)
            {
                var files = Directory.EnumerateFiles(dialog.FileName, "*.*", SearchOption.AllDirectories)
                                     .Where(f => ValidImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
                _viewModel.AddFilesToList(files);
            }
        }

        private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel == null) return;

            var selectedItems = FileListView.SelectedItems.Cast<string>().ToList();
            _viewModel.SelectedFiles = selectedItems;

            bool isSingleIco = selectedItems.Count == 1 &&
                               Path.GetExtension(selectedItems.FirstOrDefault() ?? string.Empty)
                                   .Equals(".ico", StringComparison.OrdinalIgnoreCase);
            AnalyzeMenuItem.IsEnabled = isSingleIco;
        }

        private void OpenColorPicker_MouseDown(object sender, MouseButtonEventArgs e)
        {
            using var colorDialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
            try
            {
                var wpfColor = (Color)ColorConverter.ConvertFromString(_viewModel.Options.SvgColor);
                colorDialog.Color = System.Drawing.Color.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
            }
            catch { /* Ignore invalid hex */ }

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var selectedColor = colorDialog.Color;
                _viewModel.Options.SvgColor = $"#{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";
            }
        }

        private void BrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Options.IsOutputToSource = false;
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select a custom output folder",
                InitialDirectory = _viewModel.Options.CustomOutputPath
            };

            if (dialog.ShowDialog(new WindowInteropHelper(this).Handle) == CommonFileDialogResult.Ok)
            {
                _viewModel.Options.CustomOutputPath = dialog.FileName;
            }
        }

        private void AnalyzeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is not string filePath) return;

            try
            {
                var report = _analyzerService.Analyze(filePath);
                var reportText = FormatAnalysisReport(report);
                ShowMessageBox(reportText, $"Analysis for {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                ShowMessageBox($"Failed to analyze ICO file: {ex.Message}", "Analysis Error");
            }
        }

        private string FormatAnalysisReport(IcoAnalysisReport report)
        {
            var sb = new StringBuilder();

            var allFormats = report.Entries.Select(e => e.Format).Distinct().ToList();
            var hasTransparency = report.Entries.Any(e => e.HasTransparency);

            sb.AppendLine("--------- SUMMARY ---------");
            sb.AppendLine($"File Size: {FormatBytes(report.FileSize)}");
            sb.AppendLine($"Layers: {report.Directory?.Count ?? 0}");
            sb.AppendLine($"Formats: {string.Join(", ", allFormats)}");
            sb.AppendLine($"Transparency: {(hasTransparency ? "Yes" : "No")}");
            sb.AppendLine();

            sb.AppendLine("--------- ICONDIR (Header) ---------");
            if (report.Directory != null)
            {
                sb.AppendLine($"idReserved: {report.Directory.Reserved} (Must be 0)");
                sb.AppendLine($"idType:     {report.Directory.Type} (1=ICO, 2=CUR)");
                sb.AppendLine($"idCount:    {report.Directory.Count} (Number of images)");
            }
            sb.AppendLine();

            sb.AppendLine("--------- ICONDIRENTRY (Image Directory) ---------");
            int index = 0;
            foreach (var entry in report.Entries)
            {
                sb.AppendLine($"\n[ ENTRY {index} ]");
                sb.AppendLine($"  Dimensions:    {entry.Width}x{entry.Height} pixels");
                sb.AppendLine($"  Bit Depth:     {entry.BitCount} bpp");
                sb.AppendLine($"  Format:        {entry.Format}");
                sb.AppendLine($"  --- Raw Values ---");
                sb.AppendLine($"  bWidth:        {entry.RawWidth} (0 means 256)");
                sb.AppendLine($"  bHeight:       {entry.RawHeight} (0 means 256)");
                sb.AppendLine($"  bColorCount:   {entry.ColorCountPalette} (0 if no palette or >256 colors)");
                sb.AppendLine($"  bReserved:     {entry.Reserved} (Should be 0)");
                sb.AppendLine($"  wPlanes:       {entry.Planes} (Color planes, should be 0 or 1)");
                sb.AppendLine($"  wBitCount:     {entry.BitCount} (Bits per pixel)");
                sb.AppendLine($"  dwBytesInRes:  {entry.BytesInRes} bytes (Size of image data)");
                sb.AppendLine($"  dwImageOffset: {entry.ImageOffset} (Offset to image data)");
                index++;
            }

            return sb.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (bytes == 0) return "0 " + suf[0];
            long absBytes = Math.Abs(bytes);
            int place = Convert.ToInt32(Math.Floor(Math.Log(absBytes, 1024)));
            double num = Math.Round(absBytes / Math.Pow(1024, place), 2);
            return (Math.Sign(bytes) * num) + " " + suf[place];
        }

        private void ShowMessageBox(string message, string title)
        {
            Dispatcher.Invoke(() =>
            {
                var scrollViewer = new ScrollViewer
                {
                    Content = new System.Windows.Controls.TextBox
                    {
                        Text = message,
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.NoWrap,
                        AcceptsReturn = true,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize = 12
                    },
                    MaxHeight = 400,
                    MaxWidth = 600
                };

                var messageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = title,
                    Content = scrollViewer,
                    CloseButtonText = "OK"
                };
                _ = messageBox.ShowDialogAsync();
            });
        }

        private void AboutPanel_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var aboutWindow = new AboutWindow
            {
                Owner = this
            };
            aboutWindow.ShowDialog();
        }
    }
}