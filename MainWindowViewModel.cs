using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using ICOforge.ViewModels;

namespace ICOforge
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IconConverterService _converterService = new();
        private readonly FaviconPackGenerator _faviconPackGenerator;

        public Action<string, string>? ShowMessageBoxAction { get; set; }
        public Action<string>? OpenInExplorerAction { get; set; }

        private List<string> _selectedFiles = new();

        private bool _isProcessing;
        private int _conversionProgress;
        private string _progressFileText = "Initializing...";

        public ObservableCollection<string> FileList { get; } = new();
        public List<string> SelectedFiles { get => _selectedFiles; set { if (SetProperty(ref _selectedFiles, value)) { CreateFilesCommand.RaiseCanExecuteChanged(); } } }

        public ConversionOptionsViewModel Options { get; }

        public bool IsProcessing { get => _isProcessing; set => SetProperty(ref _isProcessing, value); }
        public int ConversionProgress { get => _conversionProgress; set => SetProperty(ref _conversionProgress, value); }
        public string ProgressFileText { get => _progressFileText; set => SetProperty(ref _progressFileText, value); }

        public bool IsDropZoneVisible => !FileList.Any();
        public bool IsFileListViewHitTestVisible => FileList.Any();

        public DelegateCommand DeleteSelectedFilesCommand { get; }
        public DelegateCommand ClearListCommand { get; }
        public DelegateCommand CreateFilesCommand { get; }

        public MainWindowViewModel()
        {
            Options = new ConversionOptionsViewModel();
            _faviconPackGenerator = new FaviconPackGenerator(_converterService);

            DeleteSelectedFilesCommand = new DelegateCommand(OnDeleteSelectedFiles, CanDeleteSelectedFiles);
            ClearListCommand = new DelegateCommand(OnClearList);
            CreateFilesCommand = new DelegateCommand(async (o) => await OnCreateFiles(), CanCreateFiles);

            FileList.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(IsDropZoneVisible));
                OnPropertyChanged(nameof(IsFileListViewHitTestVisible));
                CreateFilesCommand.RaiseCanExecuteChanged();
            };

            Options.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Options.SelectedProfile))
                {
                    CreateFilesCommand.RaiseCanExecuteChanged();
                }
            };
        }

        public void AddFilesToList(IEnumerable<string> files)
        {
            var addedFiles = new List<string>();
            foreach (var file in files)
            {
                if (!FileList.Contains(file))
                {
                    FileList.Add(file);
                    addedFiles.Add(file);
                }
            }
            if (addedFiles.Any())
            {
                SelectedFiles = new List<string>(addedFiles);
            }
        }

        private void OnDeleteSelectedFiles(object? parameter)
        {
            var itemsToRemove = new List<string>(SelectedFiles);
            foreach (var item in itemsToRemove)
            {
                FileList.Remove(item);
            }
            SelectedFiles = new List<string>();
        }
        private bool CanDeleteSelectedFiles(object? parameter) => SelectedFiles.Any();

        private void OnClearList(object? parameter) => FileList.Clear();

        private bool CanCreateFiles(object? parameter)
        {
            if (!FileList.Any()) return false;
            if (Options.SelectedProfile?.Type == OutputProfileType.FaviconPack)
            {
                return SelectedFiles.Count == 1;
            }
            return true;
        }

        private async Task OnCreateFiles()
        {
            if (!AreInputsValid()) return;

            IsProcessing = true;
            try
            {
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
                ShowMessageBoxAction?.Invoke($"An unexpected error occurred during conversion: {ex.Message}", "Fatal Error");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task HandleIcoConversion()
        {
            if (!TryGetOutputDirectory("ICO", out string outputDir)) return;

            var filesToProcess = SelectedFiles.Any() ? SelectedFiles : new List<string>(FileList);
            var result = await _converterService.ConvertImagesToIcoAsync(filesToProcess, Options.GetSelectedSizes(), Options.GetSvgHexColor(), Options.GetPngOptimizationOptions(), outputDir, new Progress<IconConversionProgress>(UpdateProgress));
            HandleIcoConversionResult(result, outputDir);
        }

        private async Task HandleFaviconCreation(string inputFile)
        {
            if (!TryGetOutputDirectory("FaviconPack", out string outputDir)) return;

            await _faviconPackGenerator.CreateAsync(inputFile, Options.GetSelectedSizes(), Options.GetSvgHexColor(), Options.GetPngOptimizationOptions(), outputDir, new Progress<IconConversionProgress>(UpdateProgress));
            ShowMessageBoxAction?.Invoke($"Favicon pack created successfully in:\n{outputDir}", "Favicon Pack Created");
            if (!string.IsNullOrEmpty(outputDir))
            {
                OpenInExplorerAction?.Invoke(outputDir);
            }
        }

        private bool TryGetOutputDirectory(string type, out string outputDirectory)
        {
            string? baseOutputPath = Options.IsOutputToSource ? Path.GetDirectoryName(FileList.First()) : Options.CustomOutputPath;

            if (string.IsNullOrEmpty(baseOutputPath))
            {
                ShowMessageBoxAction?.Invoke("Could not determine the output directory.", "Output Error");
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
                ShowMessageBoxAction?.Invoke($"Could not create output directory:\n{outputDirectory}\n\nError: {ex.Message}\n\nPlease check your permissions or select a custom output location.", "Output Error");
                outputDirectory = string.Empty;
                return false;
            }
        }

        private bool AreInputsValid()
        {
            if (Options.SelectedProfile.Type == OutputProfileType.FaviconPack && SelectedFiles.Count != 1)
            {
                ShowMessageBoxAction?.Invoke("Please select a single file to create a Favicon Pack from.", "No File Selected");
                return false;
            }
            if (!Options.GetSelectedSizes().Any())
            {
                ShowMessageBoxAction?.Invoke("Please select at least one icon size.", "No Sizes Selected");
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
            ShowMessageBoxAction?.Invoke(messageBuilder.ToString(), "Conversion Finished");

            if (result.SuccessfulFiles.Any())
            {
                OpenInExplorerAction?.Invoke(outputDirectory);
            }
        }
    }
}