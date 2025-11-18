using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using ICOforge.Models;
using ICOforge.Services;
using ICOforge.Utilities;

namespace ICOforge.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IconConverterService _converterService = new();
        private readonly FaviconPackGenerator _faviconPackGenerator;
        private readonly IcoAnalyzerService _analyzerService = new();
        private readonly IDialogService _dialogService;
        private readonly HashSet<string> _fileSet = new(StringComparer.OrdinalIgnoreCase);

        private List<string> _selectedFiles = [];
        private bool _isProcessing;
        private int _conversionProgress;
        private string _progressFileText = "Initializing...";

        public ObservableCollection<string> FileList { get; } = [];
        public List<string> SelectedFiles { get => _selectedFiles; set { if (SetProperty(ref _selectedFiles, value)) { OnSelectedFilesChanged(); } } }

        public ConversionOptionsViewModel Options { get; } = new();

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
            _faviconPackGenerator = new FaviconPackGenerator(_converterService);

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
            if (!TryGetOutputDirectory("ICO", filesToProcess, out string outputDir)) return;

            var result = await _converterService.ConvertImagesToIcoAsync(filesToProcess, Options.GetSelectedSizes(), Options.GetSvgHexColor(), Options.GetPngOptimizationOptions(), outputDir, new Progress<IconConversionProgress>(UpdateProgress));
            HandleIcoConversionResult(result, outputDir);
        }

        private async Task HandleFaviconCreation(string inputFile)
        {
            if (!TryGetOutputDirectory("FaviconPack", [inputFile], out string outputDir)) return;

            var result = await _faviconPackGenerator.CreateAsync(inputFile, Options.GetSelectedSizes(), Options.GetSvgHexColor(), Options.GetPngOptimizationOptions(), outputDir, new Progress<IconConversionProgress>(UpdateProgress));

            var messageBuilder = new StringBuilder();

            if (!result.Success)
            {
                messageBuilder.AppendLine("Favicon pack creation failed.");
                messageBuilder.AppendLine($"Error: {result.OptimizationError ?? "An unknown error occurred."}");
            }
            else
            {
                messageBuilder.AppendLine("Favicon pack created successfully!");
                messageBuilder.AppendLine($"Location:\n{outputDir}");

                if (result.OptimizationError != null)
                {
                    messageBuilder.AppendLine("\n--- Warning ---");
                    messageBuilder.AppendLine(result.OptimizationError);
                    messageBuilder.AppendLine("The pack was created, but PNG optimization failed.");
                }
            }

            _dialogService.ShowMessageBox(messageBuilder.ToString(), "Favicon Pack Creation");

            if (result.Success && !string.IsNullOrEmpty(outputDir))
            {
                _dialogService.OpenInExplorer(outputDir);
            }
        }

        private bool TryGetOutputDirectory(string type, List<string> filesToProcess, out string outputDirectory)
        {
            if (Options.IsOutputToSource && !filesToProcess.Any())
            {
                _dialogService.ShowMessageBox("Cannot determine source directory. File list is empty.", "Output Error");
                outputDirectory = string.Empty;
                return false;
            }

            string? baseOutputPath = Options.IsOutputToSource ? Path.GetDirectoryName(filesToProcess.First()) : Options.CustomOutputPath;

            if (string.IsNullOrEmpty(baseOutputPath))
            {
                _dialogService.ShowMessageBox("Could not determine the output directory.", "Output Error");
                outputDirectory = string.Empty;
                return false;
            }

            // Path Validation: Ensure the base path is a fully qualified absolute path.
            // This defends against unexpected relative paths (though file dialogs usually return absolute).
            if (!Path.IsPathFullyQualified(baseOutputPath))
            {
                _dialogService.ShowMessageBox($"The selected output path is invalid or not fully qualified: {baseOutputPath}", "Output Error");
                outputDirectory = string.Empty;
                return false;
            }

            string timestamp = DateTime.Now.ToString("yyMMdd-HHmmss");
            outputDirectory = Path.Combine(baseOutputPath, $"ICOforge-{type}-{timestamp}");
            try
            {
                Directory.CreateDirectory(outputDirectory);
                return true;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessageBox($"Could not create output directory:\n{outputDirectory}\n\nError: {ex.Message}\n\nPlease check your permissions or select a custom output location.", "Output Error");
                outputDirectory = string.Empty;
                return false;
            }
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
            _dialogService.ShowMessageBox(messageBuilder.ToString(), "Conversion Finished");

            if (result.SuccessfulFiles.Any())
            {
                _dialogService.OpenInExplorer(outputDirectory);
            }
        }
    }
}