using System.Collections.ObjectModel;
using System.IO;
using ICOforge.Models;
using ICOforge.Services;
using ICOforge.Utilities;

namespace ICOforge.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IconConverterService _converterService = new();
        private readonly IcoAnalyzerService _analyzerService = new();
        private readonly IDialogService _dialogService;
        private readonly ConversionOrchestrator _orchestrator;
        private readonly HashSet<string> _fileSet = new(StringComparer.OrdinalIgnoreCase);

        private List<string> _selectedFiles = [];
        private bool _isProcessing;
        private int _conversionProgress;
        private string _progressFileText = "Initializing...";

        public ObservableCollection<string> FileList { get; } = [];
        public List<string> SelectedFiles { get => _selectedFiles; set { if (SetProperty(ref _selectedFiles, value)) { OnSelectedFilesChanged(); } } }

        public ConversionOptionsViewModel Options { get; }

        public bool IsProcessing { get => _isProcessing; set => SetProperty(ref _isProcessing, value); }
        public int ConversionProgress { get => _conversionProgress; set => SetProperty(ref _conversionProgress, value); }
        public string ProgressFileText { get => _progressFileText; set => SetProperty(ref _progressFileText, value); }

        public bool IsDropZoneVisible => !FileList.Any();
        public bool IsFileListViewHitTestVisible => FileList.Any();

        public DelegateCommand AddFilesCommand { get; }
        public DelegateCommand AddFolderCommand { get; }
        public DelegateCommand DeleteSelectedFilesCommand { get; }
        public DelegateCommand ClearListCommand { get; }
        public DelegateCommand CreateFilesCommand { get; }
        public DelegateCommand AnalyzeIcoCommand { get; }
        public DelegateCommand BrowseOutputCommand { get; }
        public DelegateCommand OpenColorPickerCommand { get; }

        public MainWindowViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
            var settingsService = new SettingsService();
            Options = new ConversionOptionsViewModel(settingsService);

            var faviconPackGenerator = new FaviconPackGenerator(_converterService);
            _orchestrator = new ConversionOrchestrator(_dialogService, _converterService, faviconPackGenerator);

            AddFilesCommand = new DelegateCommand(_ => OnAddFiles());
            AddFolderCommand = new DelegateCommand(_ => OnAddFolder());
            DeleteSelectedFilesCommand = new DelegateCommand(OnDeleteSelectedFiles, CanDeleteSelectedFiles);
            ClearListCommand = new DelegateCommand(_ => OnClearList());
            CreateFilesCommand = new DelegateCommand(async (o) => await OnCreateFiles(), CanCreateFiles);
            AnalyzeIcoCommand = new DelegateCommand(async _ => await OnAnalyzeIco(), CanAnalyzeIco);
            BrowseOutputCommand = new DelegateCommand(_ => OnBrowseOutput());
            OpenColorPickerCommand = new DelegateCommand(_ => OnOpenColorPicker());

            FileList.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(IsDropZoneVisible));
                OnPropertyChanged(nameof(IsFileListViewHitTestVisible));
                CreateFilesCommand.RaiseCanExecuteChanged();
                DeleteSelectedFilesCommand.RaiseCanExecuteChanged();
            };
        }

        private void OnSelectedFilesChanged()
        {
            CreateFilesCommand.RaiseCanExecuteChanged();
            DeleteSelectedFilesCommand.RaiseCanExecuteChanged();
            AnalyzeIcoCommand.RaiseCanExecuteChanged();
        }

        public void AddFilesToList(IEnumerable<string> files)
        {
            var addedFiles = new List<string>();
            foreach (var file in files)
            {
                if (_fileSet.Add(file))
                {
                    FileList.Add(file);
                    addedFiles.Add(file);
                }
            }
            if (addedFiles.Any())
            {
                SelectedFiles = [.. addedFiles];
            }
        }

        private void OnAddFiles()
        {
            var files = _dialogService.ShowOpenFileDialog();
            if (files != null)
            {
                AddFilesToList(files);
            }
        }

        private void OnAddFolder()
        {
            var folder = _dialogService.ShowFolderPickerDialog();
            if (!string.IsNullOrEmpty(folder))
            {
                string[] validImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".svg", ".webp", ".tif", ".tiff"];
                var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                                     .Where(f => validImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
                AddFilesToList(files);
            }
        }

        private void OnBrowseOutput()
        {
            Options.IsOutputToSource = false;
            var path = _dialogService.ShowSaveDialog();
            if (!string.IsNullOrEmpty(path))
            {
                Options.CustomOutputPath = path;
            }
        }

        private void OnOpenColorPicker()
        {
            var newColor = _dialogService.ShowColorPickerDialog(Options.SvgColor);
            if (!string.IsNullOrEmpty(newColor))
            {
                Options.SvgColor = newColor;
            }
        }

        private void OnDeleteSelectedFiles(object? parameter)
        {
            var itemsToRemove = new List<string>(SelectedFiles);
            foreach (var item in itemsToRemove)
            {
                FileList.Remove(item);
                _fileSet.Remove(item);
            }
            SelectedFiles = [];
        }
        private bool CanDeleteSelectedFiles(object? parameter) => SelectedFiles.Any();

        private void OnClearList()
        {
            FileList.Clear();
            _fileSet.Clear();
        }

        private bool CanCreateFiles(object? parameter)
        {
            if (!FileList.Any()) return false;
            if (Options.SelectedProfile?.Type == OutputProfileType.FaviconPack)
            {
                return SelectedFiles.Count == 1;
            }
            return true;
        }

        private bool CanAnalyzeIco(object? parameter)
        {
            return SelectedFiles.Count == 1 &&
                   Path.GetExtension(SelectedFiles.FirstOrDefault() ?? string.Empty)
                       .Equals(".ico", StringComparison.OrdinalIgnoreCase);
        }

        private async Task OnAnalyzeIco()
        {
            if (SelectedFiles.FirstOrDefault() is not string filePath) return;
            string fileName = Path.GetFileName(filePath);

            try
            {
                string? reportText = await Task.Run(() =>
                {
                    IcoAnalysisReport? report = _analyzerService.Analyze(filePath);
                    return report?.FormatReport(fileName);
                });

                if (reportText != null)
                {
                    _dialogService.ShowAnalysisReport(reportText, $"Analysis for {fileName}");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessageBox($"Failed to analyze ICO file: {ex.Message}", "Analysis Error");
            }
        }

        private async Task OnCreateFiles()
        {
            IsProcessing = true;
            try
            {
                if (!AreInputsValid()) return;

                if (Options.SelectedProfile.Type == OutputProfileType.FaviconPack)
                {
                    await HandleFaviconCreation(SelectedFiles.First());
                }
                else
                {
                    await HandleIcoConversion();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessageBox($"An unexpected error occurred during conversion: {ex.Message}", "Fatal Error");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task HandleIcoConversion()
        {
            var filesToProcess = SelectedFiles.Any() ? new List<string>(SelectedFiles) : [.. FileList];
            await _orchestrator.HandleIcoConversionAsync(Options, filesToProcess, new Progress<IconConversionProgress>(UpdateProgress));
        }

        private async Task HandleFaviconCreation(string inputFile)
        {
            await _orchestrator.HandleFaviconCreationAsync(Options, inputFile, new Progress<IconConversionProgress>(UpdateProgress));
        }

        private bool AreInputsValid()
        {
            if (Options.SelectedProfile.Type == OutputProfileType.FaviconPack && SelectedFiles.Count != 1)
            {
                _dialogService.ShowMessageBox("Please select a single file to create a Favicon Pack from.", "No File Selected");
                return false;
            }
            if (!Options.GetSelectedSizes().Any())
            {
                _dialogService.ShowMessageBox("Please select at least one icon size.", "No Sizes Selected");
                return false;
            }
            return true;
        }

        private void UpdateProgress(IconConversionProgress progress)
        {
            ConversionProgress = progress.Percentage;
            ProgressFileText = progress.CurrentFile;
        }
    }
}