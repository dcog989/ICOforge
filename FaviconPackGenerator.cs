using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using System.Reflection;
using System.Text;

namespace ICOforge
{
    public class FaviconPackGenerator
    {
        private readonly IconConverterService _converterService;

        public FaviconPackGenerator(IconConverterService converterService)
        {
            _converterService = converterService;
        }

        public async Task CreateAsync(Image<Rgba32> sourceImage, string sourceFilePath, string outputDirectory, PngOptimizationOptions optimizationOptions)
        {
            string iconsDir = Path.Combine(outputDirectory, "icons");
            Directory.CreateDirectory(iconsDir);

            // Generate PNGs using the user's chosen optimization settings
            var pngTasks = new List<Task>
            {
                _converterService.SavePngAsync(sourceImage, 48, Path.Combine(iconsDir, "favicon-x48.png"), optimizationOptions),
                _converterService.SavePngAsync(sourceImage, 96, Path.Combine(iconsDir, "favicon-x96.png"), optimizationOptions),
                _converterService.SavePngAsync(sourceImage, 180, Path.Combine(iconsDir, "apple-touch-x180.png"), optimizationOptions),
                _converterService.SavePngAsync(sourceImage, 192, Path.Combine(iconsDir, "favicon-x192.png"), optimizationOptions),
                _converterService.SavePngAsync(sourceImage, 512, Path.Combine(iconsDir, "favicon-x512.png"), optimizationOptions)
            };
            await Task.WhenAll(pngTasks);

            // Generate favicon.ico with specific sizes, respecting user's optimization choices
            var icoSizes = new List<int> { 16, 32 };
            await _converterService.GenerateIcoFileAsync(sourceImage, icoSizes, optimizationOptions, Path.Combine(outputDirectory, "favicon.ico"));

            // Generate SVG if the source is an SVG
            if (Path.GetExtension(sourceFilePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                await _converterService.SaveSvgAsync(sourceFilePath, Path.Combine(iconsDir, "favicon.svg"));
            }

            // Generate HTML
            await SaveHtmlFileAsync(Path.Combine(outputDirectory, "index.html"));
        }

        private async Task SaveHtmlFileAsync(string path)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "ICOforge.favicon_template.html";

            await using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) throw new FileNotFoundException($"Could not find embedded resource: {resourceName}");

            using var reader = new StreamReader(stream, Encoding.UTF8);
            string htmlContent = await reader.ReadToEndAsync();

            await File.WriteAllTextAsync(path, htmlContent, Encoding.UTF8);
        }
    }
}