using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SkiaSharp;
using Svg.Skia;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace ICOforge
{
    public record PngOptimizationOptions(bool UseLossy, bool UseLossless, int MaxColors);

    public record ConversionResult(List<string> SuccessfulFiles, List<(string File, string Error)> FailedFiles);

    public class IconConverterService
    {
        private readonly OxiPngOptimizer _optimizer = new();

        public async Task<ConversionResult> ConvertImagesToIcoAsync(List<string> filePaths, List<int> sizes, string svgHexColor, PngOptimizationOptions optimizationOptions, string outputDirectory, IProgress<IconConversionProgress> progress)
        {
            var successfulFiles = new ConcurrentBag<string>();
            var failedFiles = new ConcurrentBag<(string, string)>();
            int totalFiles = filePaths.Count;
            int processedCount = 0;

            if (optimizationOptions.UseLossless && !_optimizer.IsAvailable())
            {
                failedFiles.Add(("", "OxiPNG.exe not found. Lossless compression was skipped."));
            }

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            await Parallel.ForEachAsync(filePaths, parallelOptions, async (filePath, ct) =>
            {
                try
                {
                    var sourceImage = await LoadImageAsync(filePath, svgHexColor);
                    if (sourceImage == null) throw new Exception("Could not load or process image.");

                    using (sourceImage)
                    {
                        var outputIcoPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(filePath)}.ico");
                        await GenerateIcoFileAsync(sourceImage, sizes, optimizationOptions, outputIcoPath);
                        successfulFiles.Add(outputIcoPath);
                    }
                }
                catch (Exception ex)
                {
                    failedFiles.Add((Path.GetFileName(filePath), ex.Message));
                }
                finally
                {
                    int currentProgress = Interlocked.Increment(ref processedCount);
                    progress.Report(new IconConversionProgress { Percentage = (int)((currentProgress / (double)totalFiles) * 100), CurrentFile = Path.GetFileName(filePath) });
                }
            });

            progress.Report(new IconConversionProgress { Percentage = 100, CurrentFile = "Done!" });
            return new ConversionResult(successfulFiles.ToList(), failedFiles.ToList());
        }

        public async Task<string?> CreateFaviconPackAsync(string filePath, string svgHexColor, PngOptimizationOptions optimizationOptions, string outputDirectory, IProgress<IconConversionProgress> progress)
        {
            string? warningMessage = null;
            if (optimizationOptions.UseLossless && !_optimizer.IsAvailable())
            {
                warningMessage = "Warning: OxiPNG.exe not found. Lossless compression was skipped.";
            }

            var generator = new FaviconPackGenerator(this);
            progress.Report(new IconConversionProgress { Percentage = 10, CurrentFile = "Loading source image..." });

            var sourceImage = await LoadImageAsync(filePath, svgHexColor);
            if (sourceImage == null) throw new Exception("Could not load or process image.");

            using (sourceImage)
            {
                progress.Report(new IconConversionProgress { Percentage = 30, CurrentFile = "Generating icons..." });
                await generator.CreateAsync(sourceImage, filePath, outputDirectory, optimizationOptions);
                progress.Report(new IconConversionProgress { Percentage = 100, CurrentFile = "Done!" });
            }
            return warningMessage;
        }

        internal async Task SavePngAsync(Image<Rgba32> sourceImage, int size, string outputPath, PngOptimizationOptions optimizationOptions)
        {
            var pngBytes = await CreatePngBytesAsync(sourceImage, size, optimizationOptions);
            await File.WriteAllBytesAsync(outputPath, pngBytes);
        }

        internal async Task SaveSvgAsync(string sourcePath, string outputPath)
        {
            await Task.Run(() => File.Copy(sourcePath, outputPath, true));
        }

        internal async Task GenerateIcoFileAsync(Image<Rgba32> sourceImage, List<int> sizes, PngOptimizationOptions optimizationOptions, string outputPath)
        {
            var imageEntries = new List<(byte[] Data, byte Width, byte Height)>();
            foreach (var size in sizes)
            {
                var pngBytes = await CreatePngBytesAsync(sourceImage, size, optimizationOptions);
                byte icoWidth = size == 256 ? (byte)0 : (byte)size;
                byte icoHeight = size == 256 ? (byte)0 : (byte)size;
                imageEntries.Add((pngBytes, icoWidth, icoHeight));
            }
            await CreateIconFile(imageEntries, outputPath);
        }

        private async Task<byte[]> CreatePngBytesAsync(Image<Rgba32> sourceImage, int size, PngOptimizationOptions optimizationOptions)
        {
            using var resizedImage = sourceImage.Clone();
            resizedImage.Mutate(x => x.Resize(size, size, KnownResamplers.Bicubic));

            using var ms = new MemoryStream();

            if (optimizationOptions.UseLossy)
            {
                var quantizer = new OctreeQuantizer(new QuantizerOptions { Dither = KnownDitherings.FloydSteinberg, MaxColors = optimizationOptions.MaxColors });
                var encoder = new PngEncoder { Quantizer = quantizer, ColorType = PngColorType.Palette, CompressionLevel = PngCompressionLevel.BestCompression };
                await resizedImage.SaveAsync(ms, encoder);

                // Verification and fallback logic
                ms.Position = 0;
                var imageInfo = await Image.IdentifyAsync(ms);
                if (imageInfo.PixelType.BitsPerPixel > 8)
                {
                    // Quantization failed, re-encode as 24/32-bit to prevent corruption
                    ms.Position = 0;
                    ms.SetLength(0);
                    var fallbackEncoder = new PngEncoder { ColorType = PngColorType.RgbWithAlpha, BitDepth = PngBitDepth.Bit8, CompressionLevel = PngCompressionLevel.BestCompression };
                    await resizedImage.SaveAsync(ms, fallbackEncoder);
                }
            }
            else
            {
                var encoder = new PngEncoder
                {
                    ColorType = PngColorType.RgbWithAlpha,
                    BitDepth = PngBitDepth.Bit8,
                    CompressionLevel = PngCompressionLevel.BestCompression
                };
                await resizedImage.SaveAsync(ms, encoder);
            }

            var pngBytes = ms.ToArray();

            if (optimizationOptions.UseLossless && _optimizer.IsAvailable())
            {
                string tempPngPath = Path.ChangeExtension(Path.GetTempFileName(), ".png");
                try
                {
                    await File.WriteAllBytesAsync(tempPngPath, pngBytes);
                    var options = new OxiPngOptions { OptimizationLevel = OxiPngOptimizationLevel.Level4, StripMode = OxiPngStripMode.Safe };
                    await _optimizer.OptimizeAsync(tempPngPath, options);
                    pngBytes = await File.ReadAllBytesAsync(tempPngPath);
                }
                finally
                {
                    if (File.Exists(tempPngPath)) File.Delete(tempPngPath);
                }
            }

            return pngBytes;
        }

        private async Task CreateIconFile(List<(byte[] Data, byte Width, byte Height)> images, string outputPath)
        {
            var sortedImages = images.OrderByDescending(i => i.Width == 0 ? 256 : i.Width).ToList();

            await using var stream = new FileStream(outputPath, FileMode.Create);
            await using var writer = new BinaryWriter(stream);

            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)sortedImages.Count);

            int offset = 6 + (16 * sortedImages.Count);

            foreach (var image in sortedImages)
            {
                writer.Write(image.Width);
                writer.Write(image.Height);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)image.Data.Length);
                writer.Write((uint)offset);
                offset += image.Data.Length;
            }

            foreach (var image in sortedImages)
            {
                await stream.WriteAsync(image.Data, 0, image.Data.Length);
            }
        }

        public async Task<Image<Rgba32>?> LoadImageAsync(string path, string hexColor)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".svg") return await Task.Run(() => LoadSvgWithColor(path, hexColor));
            return await Image.LoadAsync<Rgba32>(path);
        }

        private Image<Rgba32>? LoadSvgWithColor(string path, string hexColor)
        {
            using var svg = new SKSvg();
            if (svg.Load(path) is null) return null;

            using var picture = svg.Picture;
            if (picture is null) return null;

            const int targetSize = 512;
            var imageInfo = new SKImageInfo(targetSize, targetSize, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var bitmap = new SKBitmap(imageInfo);
            using var canvas = new SKCanvas(bitmap);

            var sourceSize = picture.CullRect;
            float scaleX = targetSize / sourceSize.Width;
            float scaleY = targetSize / sourceSize.Height;
            var matrix = SKMatrix.CreateScale(scaleX, scaleY);

            if (!string.IsNullOrWhiteSpace(hexColor) && SKColor.TryParse(hexColor, out var color))
            {
                using var paint = new SKPaint { ColorFilter = SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn) };
                canvas.DrawPicture(picture, matrix, paint);
            }
            else
            {
                canvas.DrawPicture(picture, matrix, null);
            }

            var pixelBytes = bitmap.GetPixelSpan();
            using var bgraImage = Image.LoadPixelData<Bgra32>(pixelBytes, targetSize, targetSize);
            return bgraImage.CloneAs<Rgba32>();
        }
    }
}