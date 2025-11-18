using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ICOforge.Services;
using ICOforge.ViewModels;
using Wpf.Ui.Controls;

namespace ICOforge.Views
{
    public partial class MainWindow : FluentWindow, IDialogService
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly ModernDialogService _dialogService;

        public MainWindow()
        {
            InitializeComponent();
            _dialogService = new ModernDialogService(this);
            _viewModel = new MainWindowViewModel(_dialogService);
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
            return _dialogService.ShowOpenFileDialog();
        }

        public string? ShowFolderPickerDialog()
        {
            return _dialogService.ShowFolderPickerDialog();
        }

        public string? ShowSaveDialog()
        {
            return _dialogService.ShowSaveDialog();
        }

        public string? ShowColorPickerDialog(string initialColor)
        {
            return _dialogService.ShowColorPickerDialog(initialColor);
        }

        public void ShowAnalysisReport(string report, string title)
        {
            _dialogService.ShowAnalysisReport(report, title);
        }

        public void ShowMessageBox(string message, string title)
        {
            _dialogService.ShowMessageBox(message, title);
        }

        public void OpenInExplorer(string path)
        {
            _dialogService.OpenInExplorer(path);
        }
    }
}
