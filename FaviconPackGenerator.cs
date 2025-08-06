using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace ICOforge
{
    public class FaviconPackGenerator
    {
        private readonly IconConverterService _converterService;
        private readonly OxiPngOptimizer _optimizer = new();

        public FaviconPackGenerator(IconConverterService converterService)
        {
            _converterService = converterService;
        }

        public async Task CreateAsync(string filePath, List<int> icoSizes, string svgHexColor, PngOptimizationOptions optimizationOptions, string outputDirectory, IProgress<IconConversionProgress> progress)
        {
            progress.Report(new IconConversionProgress { Percentage = 10, CurrentFile = "Creating directories..." });
            string iconsDir = Path.Combine(outputDirectory, "icons");
            Directory.CreateDirectory(iconsDir);

            var sizesToGenerate = new[] { 128, 180, 256, 512 };
            var pngTasks = new List<Task>();

            progress.Report(new IconConversionProgress { Percentage = 20, CurrentFile = "Generating PNGs..." });
            foreach (var size in sizesToGenerate)
            {
                string filename = size == 180 ? "apple-touch-icon.png" : $"favicon-x{size}.png";
                string outputPath = Path.Combine(iconsDir, filename);
                pngTasks.Add(CreateAndSavePngAsync(filePath, size, svgHexColor, outputPath, optimizationOptions));
            }
            await Task.WhenAll(pngTasks);

            progress.Report(new IconConversionProgress { Percentage = 70, CurrentFile = "Generating ICO..." });
            var icoOptions = new PngOptimizationOptions(false, 0);
            await _converterService.ConvertImagesToIcoAsync(new List<string> { filePath }, icoSizes, svgHexColor, icoOptions, outputDirectory, new Progress<IconConversionProgress>());

            progress.Report(new IconConversionProgress { Percentage = 80, CurrentFile = "Copying SVG..." });
            if (Path.GetExtension(filePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(filePath, Path.Combine(iconsDir, "favicon.svg"), true);
            }

            progress.Report(new IconConversionProgress { Percentage = 90, CurrentFile = "Generating HTML..." });
            await SaveHtmlFileAsync(Path.Combine(outputDirectory, "index.html"));
        }

        private async Task CreateAndSavePngAsync(string filePath, int size, string svgHexColor, string outputPath, PngOptimizationOptions optimizationOptions)
        {
            var pngData = await _converterService.CreatePngAsync(filePath, size, svgHexColor, optimizationOptions);
            await File.WriteAllBytesAsync(outputPath, pngData);

            var oxiOptions = new OxiPngOptions
            {
                OptimizationLevel = optimizationOptions.UseLossy ? OxiPngOptimizationLevel.Level4 : OxiPngOptimizationLevel.Level2,
                NoColorTypeReduction = !optimizationOptions.UseLossy,
                NoBitDepthReduction = !optimizationOptions.UseLossy
            };
            try
            {
                await _optimizer.OptimizeAsync(outputPath, oxiOptions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OxiPNG optimization failed for {outputPath}: {ex.Message}");
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