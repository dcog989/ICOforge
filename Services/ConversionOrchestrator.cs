using System.IO;
using ICOforge.Models;
using ICOforge.ViewModels;

namespace ICOforge.Services
{
    public class ConversionOrchestrator(
        IDialogService dialogService,
        IconConverterService converterService,
        FaviconPackGenerator faviconPackGenerator)
    {
        public async Task<ConversionResult?> HandleIcoConversionAsync(
            ConversionOptionsViewModel options,
            List<string> filesToProcess,
            IProgress<IconConversionProgress> progress)
        {
            if (!TryGetOutputDirectory("ICO", filesToProcess, options, out string outputDir))
            {
                return null;
            }

            var result = await converterService.ConvertImagesToIcoAsync(
                filesToProcess,
                options.GetSelectedSizes(),
                options.GetSvgHexColor(),
                options.GetPngOptimizationOptions(),
                outputDir,
                progress);

            HandleIcoConversionResult(result, outputDir);
            return result;
        }

        public async Task<FaviconPackResult?> HandleFaviconCreationAsync(
            ConversionOptionsViewModel options,
            string inputFile,
            IProgress<IconConversionProgress> progress)
        {
            if (!TryGetOutputDirectory("FaviconPack", [inputFile], options, out string outputDir))
            {
                return null;
            }

            var result = await faviconPackGenerator.CreateAsync(
                inputFile,
                options.GetSelectedSizes(),
                options.GetSvgHexColor(),
                options.GetPngOptimizationOptions(),
                outputDir,
                progress);

            HandleFaviconCreationResult(result, outputDir);
            return result;
        }

        private bool TryGetOutputDirectory(string type, List<string> filesToProcess, ConversionOptionsViewModel options, out string outputDirectory)
        {
            if (options.IsOutputToSource && !filesToProcess.Any())
            {
                dialogService.ShowMessageBox("Cannot determine source directory. File list is empty.", "Output Error");
                outputDirectory = string.Empty;
                return false;
            }

            string? baseOutputPath = options.IsOutputToSource ? Path.GetDirectoryName(filesToProcess.First()) : options.CustomOutputPath;

            if (string.IsNullOrEmpty(baseOutputPath))
            {
                dialogService.ShowMessageBox("Could not determine the output directory.", "Output Error");
                outputDirectory = string.Empty;
                return false;
            }

            if (!Path.IsPathFullyQualified(baseOutputPath))
            {
                dialogService.ShowMessageBox($"The selected output path is invalid or not fully qualified: {baseOutputPath}", "Output Error");
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
                dialogService.ShowMessageBox($"Could not create output directory:\n{outputDirectory}\n\nError: {ex.Message}\n\nPlease check your permissions or select a custom output location.", "Output Error");
                outputDirectory = string.Empty;
                return false;
            }
        }

        private void HandleIcoConversionResult(ConversionResult result, string outputDirectory)
        {
            var messageBuilder = new System.Text.StringBuilder();
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
            dialogService.ShowMessageBox(messageBuilder.ToString(), "Conversion Finished");

            if (result.SuccessfulFiles.Any())
            {
                dialogService.OpenInExplorer(outputDirectory);
            }
        }

        private void HandleFaviconCreationResult(FaviconPackResult result, string outputDirectory)
        {
            var messageBuilder = new System.Text.StringBuilder();

            if (!result.Success)
            {
                messageBuilder.AppendLine("Favicon pack creation failed.");
                messageBuilder.AppendLine($"Error: {result.OptimizationError ?? "An unknown error occurred."}");
            }
            else
            {
                messageBuilder.AppendLine("Favicon pack created successfully!");
                messageBuilder.AppendLine($"Location:\n{outputDirectory}");

                if (result.OptimizationError != null)
                {
                    messageBuilder.AppendLine("\n--- Warning ---");
                    messageBuilder.AppendLine(result.OptimizationError);
                    messageBuilder.AppendLine("The pack was created, but PNG optimization failed.");
                }
            }

            dialogService.ShowMessageBox(messageBuilder.ToString(), "Favicon Pack Creation");

            if (result.Success && !string.IsNullOrEmpty(outputDirectory))
            {
                dialogService.OpenInExplorer(outputDirectory);
            }
        }
    }
}