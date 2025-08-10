using System.Collections.Concurrent;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SkiaSharp;
using Svg.Skia;

namespace ICOforge
{
    public record PngOptimizationOptions(bool UseLossy, int MaxColors);
    public record ConversionResult(List<string> SuccessfulFiles, List<(string File, string Error)> FailedFiles);

    public class IconConverterService
    {
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
                    var pngs = new List<(byte[] Data, int Width, int Height)>();
                    foreach (var size in sizes)
                    {
                        var pngData = await CreatePngAsync(filePath, size, svgHexColor, optimizationOptions);
                        pngs.Add((pngData, size, size));
                    }

                    string finalPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(filePath)}.ico");
                    await WriteIcoFileAsync(finalPath, pngs);
                    successfulFiles.Add(finalPath);
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

        public async Task<byte[]> CreatePngAsync(string filePath, int size, string svgHexColor, PngOptimizationOptions optimizationOptions)
        {
            using var image = await LoadImageAsync(filePath, size, svgHexColor);

            image.Mutate(x => x.Resize(size, size));

            PngEncoder encoder;
            if (optimizationOptions.UseLossy)
            {
                encoder = new PngEncoder
                {
                    Quantizer = new WuQuantizer(new QuantizerOptions { MaxColors = optimizationOptions.MaxColors }),
                    ColorType = PngColorType.Palette,
                    BitDepth = PngBitDepth.Bit8
                };
            }
            else
            {
                encoder = new PngEncoder();
            }

            using var memoryStream = new MemoryStream();
            await image.SaveAsync(memoryStream, encoder);
            return memoryStream.ToArray();
        }

        private async Task<Image<Rgba32>> LoadImageAsync(string filePath, int size, string svgHexColor)
        {
            if (Path.GetExtension(filePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return await LoadSvgAndApplyColorAsync(filePath, size, svgHexColor);
            }
            return await Image.LoadAsync<Rgba32>(filePath);
        }

        private Task<Image<Rgba32>> LoadSvgAndApplyColorAsync(string filePath, int size, string svgHexColor)
        {
            using var svg = new SKSvg();
            if (svg.Load(filePath) == null)
            {
                throw new InvalidDataException("Could not load SVG file.");
            }

            if (svg.Picture == null)
            {
                throw new InvalidDataException("The SVG file is invalid or could not be rendered.");
            }

            var imageInfo = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var bitmap = new SKBitmap(imageInfo);
            using var canvas = new SKCanvas(bitmap);

            canvas.Clear(SKColors.Transparent);
            var scaleMatrix = SKMatrix.CreateScale(size / svg.Picture.CullRect.Width, size / svg.Picture.CullRect.Height);
            canvas.Save();
            canvas.Concat(ref scaleMatrix);

            if (!string.IsNullOrWhiteSpace(svgHexColor) && SKColor.TryParse(svgHexColor, out var skColor))
            {
                using var paint = new SKPaint
                {
                    ColorFilter = SKColorFilter.CreateBlendMode(skColor, SKBlendMode.SrcIn)
                };
                canvas.DrawPicture(svg.Picture, paint);
            }
            else
            {
                canvas.DrawPicture(svg.Picture);
            }
            canvas.Restore();

            var image = Image.LoadPixelData<Rgba32>(bitmap.GetPixelSpan(), size, size);
            return Task.FromResult(image);
        }

        private async Task WriteIcoFileAsync(string outputPath, List<(byte[] Data, int Width, int Height)> pngs)
        {
            var orderedPngs = pngs.OrderBy(p => p.Width).ToList();

            await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await using var writer = new BinaryWriter(stream);

            writer.Write((ushort)0); // Reserved
            writer.Write((ushort)1); // Type: 1 for icon
            writer.Write((ushort)orderedPngs.Count); // Number of images

            long offset = 6 + (16 * orderedPngs.Count);
            foreach (var png in orderedPngs)
            {
                writer.Write((byte)(png.Width >= 256 ? 0 : png.Width));
                writer.Write((byte)(png.Height >= 256 ? 0 : png.Height));
                writer.Write((byte)0); // bColorCount
                writer.Write((byte)0); // bReserved
                                       // This is the critical fix. The working icon uses wPlanes = 0.
                writer.Write((ushort)0); // wPlanes
                writer.Write((ushort)32); // wBitCount
                writer.Write((uint)png.Data.Length); // dwBytesInRes
                writer.Write((uint)offset); // dwImageOffset

                offset += png.Data.Length;
            }

            foreach (var png in orderedPngs)
            {
                await stream.WriteAsync(png.Data);
            }
        }
    }
}