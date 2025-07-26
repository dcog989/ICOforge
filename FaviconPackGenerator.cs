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

        public async Task CreateFromRasterAsync(Image<Rgba32> sourceImage, string outputDirectory, PngOptimizationOptions optimizationOptions)
        {
            string iconsDir = Path.Combine(outputDirectory, "icons");
            Directory.CreateDirectory(iconsDir);

            var pngTasks = new List<Task>
            {
                SavePngFromRasterAsync(sourceImage, 48, Path.Combine(iconsDir, "favicon-x48.png"), optimizationOptions),
                SavePngFromRasterAsync(sourceImage, 96, Path.Combine(iconsDir, "favicon-x96.png"), optimizationOptions),
                SavePngFromRasterAsync(sourceImage, 180, Path.Combine(iconsDir, "apple-touch-icon.png"), optimizationOptions),
                SavePngFromRasterAsync(sourceImage, 192, Path.Combine(iconsDir, "favicon-x192.png"), optimizationOptions),
                SavePngFromRasterAsync(sourceImage, 512, Path.Combine(iconsDir, "favicon-x512.png"), optimizationOptions)
            };
            await Task.WhenAll(pngTasks);

            await GenerateIcoFromRasterAsync(sourceImage, new List<int> { 16, 32 }, optimizationOptions, Path.Combine(outputDirectory, "favicon.ico"));
            await SaveHtmlFileAsync(Path.Combine(outputDirectory, "index.html"));
        }

        public async Task CreateFromSvgAsync(string sourceFilePath, string svgHexColor, string outputDirectory, PngOptimizationOptions optimizationOptions)
        {
            string iconsDir = Path.Combine(outputDirectory, "icons");
            Directory.CreateDirectory(iconsDir);

            var pngTasks = new List<Task>
            {
                SavePngFromSvgAsync(sourceFilePath, svgHexColor, 48, Path.Combine(iconsDir, "favicon-x48.png"), optimizationOptions),
                SavePngFromSvgAsync(sourceFilePath, svgHexColor, 96, Path.Combine(iconsDir, "favicon-x96.png"), optimizationOptions),
                SavePngFromSvgAsync(sourceFilePath, svgHexColor, 180, Path.Combine(iconsDir, "apple-touch-icon.png"), optimizationOptions),
                SavePngFromSvgAsync(sourceFilePath, svgHexColor, 192, Path.Combine(iconsDir, "favicon-x192.png"), optimizationOptions),
                SavePngFromSvgAsync(sourceFilePath, svgHexColor, 512, Path.Combine(iconsDir, "favicon-x512.png"), optimizationOptions)
            };
            await Task.WhenAll(pngTasks);

            await GenerateIcoFromSvgAsync(sourceFilePath, svgHexColor, new List<int> { 16, 32 }, optimizationOptions, Path.Combine(outputDirectory, "favicon.ico"));
            await _converterService.SaveSvgAsync(sourceFilePath, Path.Combine(iconsDir, "favicon.svg"));
            await SaveHtmlFileAsync(Path.Combine(outputDirectory, "index.html"));
        }

        private async Task SavePngFromRasterAsync(Image<Rgba32> sourceImage, int size, string outputPath, PngOptimizationOptions options)
        {
            var pngBytes = await _converterService.CreatePngBytesFromResizedImageAsync(sourceImage, size, options);
            await File.WriteAllBytesAsync(outputPath, pngBytes);
        }

        private async Task SavePngFromSvgAsync(string svgPath, string hexColor, int size, string outputPath, PngOptimizationOptions options)
        {
            using var image = await _converterService.LoadSvgAtSizeAsync(svgPath, hexColor, size);
            if (image == null) throw new Exception($"Failed to render SVG at size {size}x{size}.");
            var pngBytes = await _converterService.CreatePngBytesFromImageAsync(image, options);
            await File.WriteAllBytesAsync(outputPath, pngBytes);
        }

        private async Task GenerateIcoFromRasterAsync(Image<Rgba32> sourceImage, List<int> sizes, PngOptimizationOptions options, string outputPath)
        {
            var imageEntries = new List<(byte[] Data, byte Width, byte Height)>();
            foreach (var size in sizes)
            {
                var pngBytes = await _converterService.CreatePngBytesFromResizedImageAsync(sourceImage, size, options);
                byte icoWidth = size == 256 ? (byte)0 : (byte)size;
                byte icoHeight = size == 256 ? (byte)0 : (byte)size;
                imageEntries.Add((pngBytes, icoWidth, icoHeight));
            }
            await _converterService.CreateIconFile(imageEntries, outputPath);
        }

        private async Task GenerateIcoFromSvgAsync(string svgPath, string hexColor, List<int> sizes, PngOptimizationOptions options, string outputPath)
        {
            var imageEntries = new List<(byte[] Data, byte Width, byte Height)>();
            foreach (var size in sizes)
            {
                using var image = await _converterService.LoadSvgAtSizeAsync(svgPath, hexColor, size);
                if (image == null) throw new Exception($"Failed to render SVG for ICO at size {size}x{size}.");
                var pngBytes = await _converterService.CreatePngBytesFromImageAsync(image, options);
                byte icoWidth = size == 256 ? (byte)0 : (byte)size;
                byte icoHeight = size == 256 ? (byte)0 : (byte)size;
                imageEntries.Add((pngBytes, icoWidth, icoHeight));
            }
            await _converterService.CreateIconFile(imageEntries, outputPath);
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