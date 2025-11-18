using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using ICOforge.Models;

namespace ICOforge.Services
{
    public class FaviconPackGenerator(IconConverterService converterService)
    {
        private readonly OxiPngOptimizer _optimizer = new();

        public async Task CreateAsync(string filePath, List<int> icoSizes, string svgHexColor, PngOptimizationOptions optimizationOptions, string outputDirectory, IProgress<IconConversionProgress> progress)
        {
            progress.Report(new IconConversionProgress { Percentage = 10, CurrentFile = "Creating directories..." });
            string iconsDir = Path.Combine(outputDirectory, "icons");
            Directory.CreateDirectory(iconsDir);

            int[] sizesToGenerate = [128, 180, 256, 512];
            var pngTasks = new List<Task<string>>();

            progress.Report(new IconConversionProgress { Percentage = 20, CurrentFile = "Generating PNGs..." });
            foreach (var size in sizesToGenerate)
            {
                string filename = size == 180 ? "apple-touch-icon.png" : $"favicon-x{size}.png";
                string outputPath = Path.Combine(iconsDir, filename);
                pngTasks.Add(CreateAndSavePngAsync(filePath, size, svgHexColor, outputPath, optimizationOptions));
            }
            var generatedPngPaths = await Task.WhenAll(pngTasks);

            progress.Report(new IconConversionProgress { Percentage = 60, CurrentFile = "Optimizing PNGs..." });
            await OptimizePngsAsync(generatedPngPaths, optimizationOptions);

            progress.Report(new IconConversionProgress { Percentage = 70, CurrentFile = "Generating ICO..." });
            string sourceIcoFileName = $"{Path.GetFileNameWithoutExtension(filePath)}.ico";
            string initialIcoPath = Path.Combine(outputDirectory, sourceIcoFileName);
            string finalIcoPath = Path.Combine(outputDirectory, "favicon.ico");
            await converterService.ConvertImagesToIcoAsync([filePath], icoSizes, svgHexColor, optimizationOptions, outputDirectory, new Progress<IconConversionProgress>());

            if (File.Exists(initialIcoPath))
            {
                File.Move(initialIcoPath, finalIcoPath, true);
            }

            progress.Report(new IconConversionProgress { Percentage = 80, CurrentFile = "Copying SVG..." });
            if (Path.GetExtension(filePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(filePath, Path.Combine(iconsDir, "favicon.svg"), true);
            }

            progress.Report(new IconConversionProgress { Percentage = 90, CurrentFile = "Generating HTML..." });
            await SaveHtmlFileAsync(Path.Combine(outputDirectory, "index.html"));
        }

        private async Task<string> CreateAndSavePngAsync(string filePath, int size, string svgHexColor, string outputPath, PngOptimizationOptions optimizationOptions)
        {
            var pngData = await converterService.CreatePngAsync(filePath, size, svgHexColor, optimizationOptions);
            await File.WriteAllBytesAsync(outputPath, pngData);
            return outputPath;
        }

        private async Task OptimizePngsAsync(IEnumerable<string> paths, PngOptimizationOptions optimizationOptions)
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OxiPNG optimization failed: {ex.Message}");
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