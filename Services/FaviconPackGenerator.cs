using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using ICOforge.Models;

namespace ICOforge.Services
{
    public record FaviconPackResult(bool Success, string? OptimizationError);

    public class FaviconPackGenerator(IconConverterService converterService)
    {
        private readonly OxiPngOptimizer _optimizer = new();

        private static readonly int[] PngSizes = [128, 180, 256, 512];
        private const string AppleTouchIconName = "apple-touch-icon.png";
        private const int AppleTouchIconSize = 180;
        private const string FaviconIcoName = "favicon.ico";
        private const string FaviconSvgName = "favicon.svg";

        public async Task<FaviconPackResult> CreateAsync(string filePath, List<int> icoSizes, string svgHexColor, PngOptimizationOptions optimizationOptions, string outputDirectory, IProgress<IconConversionProgress> progress)
        {
            progress.Report(new IconConversionProgress { Percentage = 10, CurrentFile = "Creating directories..." });
            string iconsDir = Path.Combine(outputDirectory, "icons");
            Directory.CreateDirectory(iconsDir);

            var pngTasks = new List<Task<string>>();

            progress.Report(new IconConversionProgress { Percentage = 20, CurrentFile = "Generating PNGs..." });
            foreach (var size in PngSizes)
            {
                string filename = size == AppleTouchIconSize ? AppleTouchIconName : $"favicon-x{size}.png";
                string outputPath = Path.Combine(iconsDir, filename);
                pngTasks.Add(CreateAndSavePngAsync(filePath, size, svgHexColor, outputPath, optimizationOptions));
            }
            var generatedPngPaths = await Task.WhenAll(pngTasks);

            progress.Report(new IconConversionProgress { Percentage = 60, CurrentFile = "Optimizing PNGs..." });
            string? optimizationError = await OptimizePngsAsync(generatedPngPaths, optimizationOptions);

            progress.Report(new IconConversionProgress { Percentage = 70, CurrentFile = "Generating ICO..." });
            string sourceIcoFileName = $"{Path.GetFileNameWithoutExtension(filePath)}.ico";
            string initialIcoPath = Path.Combine(outputDirectory, sourceIcoFileName);
            string finalIcoPath = Path.Combine(outputDirectory, FaviconIcoName);
            var icoConversionResult = await converterService.ConvertImagesToIcoAsync([filePath], icoSizes, svgHexColor, optimizationOptions, outputDirectory, new Progress<IconConversionProgress>());

            if (icoConversionResult.FailedFiles.Any())
            {
                // ICO conversion is a critical part of the pack, so treat its failure as a pack failure.
                var firstError = icoConversionResult.FailedFiles.First();
                return new FaviconPackResult(false, $"ICO conversion failed for {firstError.File}: {firstError.Error}");
            }

            if (File.Exists(initialIcoPath))
            {
                File.Move(initialIcoPath, finalIcoPath, true);
            }

            progress.Report(new IconConversionProgress { Percentage = 80, CurrentFile = "Copying SVG..." });
            if (Path.GetExtension(filePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(filePath, Path.Combine(iconsDir, FaviconSvgName), true);
            }

            progress.Report(new IconConversionProgress { Percentage = 90, CurrentFile = "Generating HTML..." });
            await SaveHtmlFileAsync(Path.Combine(outputDirectory, "index.html"));

            progress.Report(new IconConversionProgress { Percentage = 100, CurrentFile = "Done!" });
            return new FaviconPackResult(true, optimizationError);
        }

        private async Task<string> CreateAndSavePngAsync(string filePath, int size, string svgHexColor, string outputPath, PngOptimizationOptions optimizationOptions)
        {
            var pngData = await converterService.CreatePngAsync(filePath, size, svgHexColor, optimizationOptions);
            await File.WriteAllBytesAsync(outputPath, pngData);
            return outputPath;
        }

        private async Task<string?> OptimizePngsAsync(IEnumerable<string> paths, PngOptimizationOptions optimizationOptions)
        {
            var oxiOptions = new OxiPngOptions
            {
                OptimizationLevel = optimizationOptions.UseLossy ? OxiPngOptimizationLevel.Level4 : OxiPngOptimizationLevel.Level2,
                NoColorTypeReduction = !optimizationOptions.UseLossy,
                NoBitDepthReduction = !optimizationOptions.UseLossy
            };
            try
            {
                await _optimizer.OptimizeAsync(paths, oxiOptions);
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OxiPNG optimization failed: {ex.Message}");
                return $"PNG optimization failed: {ex.Message}";
            }
        }

        private async Task SaveHtmlFileAsync(string path)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "ICOforge.assets.favicon_template.html";

            await using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) throw new FileNotFoundException($"Could not find embedded resource: {resourceName}");

            using var reader = new StreamReader(stream, Encoding.UTF8);
            string htmlContent = await reader.ReadToEndAsync();

            await File.WriteAllTextAsync(path, htmlContent, Encoding.UTF8);
        }
    }
}