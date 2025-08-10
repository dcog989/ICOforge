using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.WindowsAPICodePack.Dialogs;
using Wpf.Ui.Controls;

namespace ICOforge
{
    public partial class MainWindow : FluentWindow, IDialogService
    {
        private readonly MainWindowViewModel _viewModel;
        private static readonly string[] ValidImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".svg", ".webp", ".tif", ".tiff" };

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel(this);
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

        private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.SelectedFiles = FileListView.SelectedItems.Cast<string>().ToList();
        }

        private void AboutPanel_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var aboutWindow = new AboutWindow { Owner = this };
            aboutWindow.ShowDialog();
        }

        public IEnumerable<string>? ShowOpenFileDialog()
        {
            var dialog = new CommonOpenFileDialog
            {
                Multiselect = true,
                Title = "Select Image Files"
            };
            var filterExtensions = string.Join(";", ValidImageExtensions.Select(ext => $"*{ext}"));
            dialog.Filters.Add(new CommonFileDialogFilter("Image Files", filterExtensions));
            dialog.Filters.Add(new CommonFileDialogFilter("All files", "*.*"));

            return dialog.ShowDialog(new WindowInteropHelper(this).Handle) == CommonFileDialogResult.Ok
                ? dialog.FileNames
                : null;
        }

        public string? ShowFolderPickerDialog()
        {
            var dialog = new CommonOpenFileDialog { IsFolderPicker = true, Title = "Select a folder containing images" };
            return dialog.ShowDialog(new WindowInteropHelper(this).Handle) == CommonFileDialogResult.Ok
                ? dialog.FileName
                : null;
        }

        public string? ShowSaveDialog()
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select a custom output folder",
                InitialDirectory = _viewModel.Options.CustomOutputPath
            };
            return dialog.ShowDialog(new WindowInteropHelper(this).Handle) == CommonFileDialogResult.Ok
                ? dialog.FileName
                : null;
        }

        public string? ShowColorPickerDialog(string initialColor)
        {
            using var colorDialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
            try
            {
                var wpfColor = (Color)ColorConverter.ConvertFromString(initialColor);
                colorDialog.Color = System.Drawing.Color.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
            }
            catch { /* Ignore invalid hex */ }

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var selectedColor = colorDialog.Color;
                return $"#{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";
            }
            return null;
        }

        public void ShowAnalysisReport(string report, string title)
        {
            Dispatcher.Invoke(() =>
            {
                var scrollViewer = new ScrollViewer
                {
                    Content = new System.Windows.Controls.TextBox
                    {
                        Text = report,
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

        public void ShowMessageBox(string message, string title)
        {
            Dispatcher.Invoke(() =>
            {
                var messageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = title,
                    Content = message,
                    CloseButtonText = "OK"
                };
                _ = messageBox.ShowDialogAsync();
            });
        }

        public void OpenInExplorer(string path)
        {
            try
            {
                Process.Start("explorer.exe", path);
            }
            catch (Exception ex)
            {
                ShowMessageBox($"Could not open path: {path}\nError: {ex.Message}", "Explorer Error");
            }
        }
    }
}