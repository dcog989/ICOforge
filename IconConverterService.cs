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

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            await Parallel.ForEachAsync(filePaths, parallelOptions, async (filePath, ct) =>
            {
                try
                {
                    var outputIcoPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(filePath)}.ico");
                    var imageEntries = new List<(byte[] Data, byte Width, byte Height)>();

                    if (Path.GetExtension(filePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var size in sizes)
                        {
                            using var sizedImage = await LoadSvgAtSizeAsync(filePath, svgHexColor, size);
                            if (sizedImage == null) throw new Exception("Failed to render SVG.");
                            var pngBytes = await CreatePngBytesFromImageAsync(sizedImage, optimizationOptions);
                            byte icoWidth = size >= 256 ? (byte)0 : (byte)size;
                            byte icoHeight = size >= 256 ? (byte)0 : (byte)size;
                            imageEntries.Add((pngBytes, icoWidth, icoHeight));
                        }
                    }
                    else
                    {
                        using var sourceImage = await Image.LoadAsync<Rgba32>(filePath);
                        foreach (var size in sizes)
                        {
                            var pngBytes = await CreatePngBytesFromResizedImageAsync(sourceImage, size, optimizationOptions);
                            byte icoWidth = size >= 256 ? (byte)0 : (byte)size;
                            byte icoHeight = size >= 256 ? (byte)0 : (byte)size;
                            imageEntries.Add((pngBytes, icoWidth, icoHeight));
                        }
                    }
                    await CreateIconFile(imageEntries, outputIcoPath);
                    successfulFiles.Add(outputIcoPath);
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

            if (Path.GetExtension(filePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                await generator.CreateFromSvgAsync(filePath, svgHexColor, outputDirectory, optimizationOptions);
            }
            else
            {
                using var sourceImage = await Image.LoadAsync<Rgba32>(filePath);
                await generator.CreateFromRasterAsync(sourceImage, outputDirectory, optimizationOptions);
            }

            progress.Report(new IconConversionProgress { Percentage = 100, CurrentFile = "Done!" });
            return warningMessage;
        }

        internal async Task<byte[]> CreatePngBytesFromResizedImageAsync(Image<Rgba32> sourceImage, int size, PngOptimizationOptions optimizationOptions)
        {
            using var resizedImage = sourceImage.Clone();
            resizedImage.Mutate(x => x.Resize(size, size, KnownResamplers.Bicubic));
            return await CreatePngBytesFromImageAsync(resizedImage, optimizationOptions);
        }

        internal async Task<byte[]> CreatePngBytesFromImageAsync(Image<Rgba32> image, PngOptimizationOptions optimizationOptions)
        {
            using var ms = new MemoryStream();

            if (optimizationOptions.UseLossy)
            {
                var quantizer = new WuQuantizer(new QuantizerOptions { MaxColors = optimizationOptions.MaxColors, Dither = null });
                var encoder = new PngEncoder { Quantizer = quantizer, ColorType = PngColorType.Palette, CompressionLevel = PngCompressionLevel.BestCompression };
                await image.SaveAsync(ms, encoder);

                ms.Position = 0;
                var imageInfo = await Image.IdentifyAsync(ms);
                if (imageInfo.PixelType.BitsPerPixel > 8)
                {
                    ms.Position = 0;
                    ms.SetLength(0);
                    var fallbackEncoder = new PngEncoder { ColorType = PngColorType.RgbWithAlpha, BitDepth = PngBitDepth.Bit8, CompressionLevel = PngCompressionLevel.BestCompression };
                    await image.SaveAsync(ms, fallbackEncoder);
                }
            }
            else
            {
                var encoder = new PngEncoder { ColorType = PngColorType.RgbWithAlpha, BitDepth = PngBitDepth.Bit8, CompressionLevel = PngCompressionLevel.BestCompression };
                await image.SaveAsync(ms, encoder);
            }

            var pngBytes = ms.ToArray();

            if (optimizationOptions.UseLossless && _optimizer.IsAvailable())
            {
                string tempPngPath = Path.ChangeExtension(Path.GetTempFileName(), ".png");
                try
                {
                    await File.WriteAllBytesAsync(tempPngPath, pngBytes);
                    var oxiLevel = optimizationOptions.UseLossy ? OxiPngOptimizationLevel.Level4 : OxiPngOptimizationLevel.Level2;
                    var options = new OxiPngOptions { OptimizationLevel = oxiLevel, StripMode = OxiPngStripMode.Safe };
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

        internal async Task SaveSvgAsync(string sourcePath, string outputPath)
        {
            await Task.Run(() => File.Copy(sourcePath, outputPath, true));
        }

        internal async Task<Image<Rgba32>?> LoadSvgAtSizeAsync(string path, string hexColor, int targetSize)
        {
            return await Task.Run(() =>
            {
                using var svg = new SKSvg();
                if (svg.Load(path) is null) return null;

                using var picture = svg.Picture;
                if (picture is null) return null;

                var imageInfo = new SKImageInfo(targetSize, targetSize, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var bitmap = new SKBitmap(imageInfo);
                using var canvas = new SKCanvas(bitmap);

                var sourceSize = picture.CullRect;
                float scaleX = targetSize / sourceSize.Width;
                float scaleY = targetSize / sourceSize.Height;
                var matrix = SKMatrix.CreateScale(scaleX, scaleY);

                if (!string.IsNullOrWhiteSpace(hexColor) && SKColor.TryParse(hexColor, out var color))
                {
                    using var paint = new SKPaint { IsAntialias = true, ColorFilter = SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn) };
                    canvas.DrawPicture(picture, matrix, paint);
                }
                else
                {
                    using var paint = new SKPaint { IsAntialias = true };
                    canvas.DrawPicture(picture, matrix, paint);
                }

                var pixelBytes = bitmap.GetPixelSpan();
                using var bgraImage = Image.LoadPixelData<Bgra32>(pixelBytes, targetSize, targetSize);
                return bgraImage.CloneAs<Rgba32>();
            });
        }

        public async Task CreateIconFile(List<(byte[] Data, byte Width, byte Height)> images, string outputPath)
        {
            var sortedImages = images.OrderBy(i => i.Width == 0 ? 256 : i.Width).ToList();
            await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
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
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((uint)image.Data.Length);
                writer.Write((uint)offset);
                offset += image.Data.Length;
            }
            foreach (var image in sortedImages)
            {
                await stream.WriteAsync(image.Data);
            }
        }
    }
}