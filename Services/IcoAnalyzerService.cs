using System.IO;
using System.Text;

namespace ICOforge.Services
{
    public class IcoAnalysisReport
    {
        public IconDir? Directory { get; set; }
        public List<IconDirEntry> Entries { get; set; } = new List<IconDirEntry>();
        public long FileSize { get; set; }

        public string FormatReport(string fileName)
        {
            var sb = new StringBuilder();
            var allFormats = Entries.Select(e => e.Format).Distinct().ToList();
            var hasTransparency = Entries.Any(e => e.HasTransparency);

            sb.AppendLine("--------- SUMMARY ---------");
            sb.AppendLine($"File: {fileName}");
            sb.AppendLine($"File Size: {FormatBytes(FileSize)}");
            sb.AppendLine($"Layers: {Directory?.Count ?? 0}");
            sb.AppendLine($"Formats: {string.Join(", ", allFormats)}");
            sb.AppendLine($"Transparency: {(hasTransparency ? "Yes" : "No")}");
            sb.AppendLine();

            sb.AppendLine("--------- ICONDIR (Header) ---------");
            if (Directory != null)
            {
                sb.AppendLine($"idReserved: {Directory.Reserved} (Must be 0)");
                sb.AppendLine($"idType:     {Directory.Type} (1=ICO, 2=CUR)");
                sb.AppendLine($"idCount:    {Directory.Count} (Number of images)");
            }
            sb.AppendLine();

            sb.AppendLine("--------- ICONDIRENTRY (Image Directory) ---------");
            int index = 0;
            foreach (var entry in Entries)
            {
                sb.AppendLine($"\n[ ENTRY {index} ]");
                sb.AppendLine($"  Dimensions:    {entry.Width}x{entry.Height} pixels");
                sb.AppendLine($"  Bit Depth:     {entry.BitCount} bpp");
                sb.AppendLine($"  Format:        {entry.Format}");
                sb.AppendLine($"  --- Raw Values ---");
                sb.AppendLine($"  bWidth:        {entry.RawWidth} (0 means 256)");
                sb.AppendLine($"  bHeight:       {entry.RawHeight} (0 means 256)");
                sb.AppendLine($"  bColorCount:   {entry.ColorCountPalette} (0 if no palette or >256 colors)");
                sb.AppendLine($"  bReserved:     {entry.Reserved} (Should be 0)");
                sb.AppendLine($"  wPlanes:       {entry.Planes} (Color planes, should be 0 or 1)");
                sb.AppendLine($"  wBitCount:     {entry.BitCount} (Bits per pixel)");
                sb.AppendLine($"  dwBytesInRes:  {entry.BytesInRes} bytes (Size of image data)");
                sb.AppendLine($"  dwImageOffset: {entry.ImageOffset} (Offset to image data)");
                index++;
            }
            return sb.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            string[] suf = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
            if (bytes == 0) return "0 " + suf[0];
            long absBytes = Math.Abs(bytes);
            int place = Convert.ToInt32(Math.Floor(Math.Log(absBytes, 1024)));
            double num = Math.Round(absBytes / Math.Pow(1024, place), 2);
            return (Math.Sign(bytes) * num) + " " + suf[place];
        }
    }

    public class IconDir
    {
        public ushort Reserved { get; set; }
        public ushort Type { get; set; }
        public ushort Count { get; set; }
    }

    public class IconDirEntry
    {
        public byte RawWidth { get; set; }
        public byte RawHeight { get; set; }
        public byte ColorCountPalette { get; set; }
        public byte Reserved { get; set; }
        public ushort Planes { get; set; }
        public ushort BitCount { get; set; }
        public uint BytesInRes { get; set; }
        public uint ImageOffset { get; set; }
        public bool IsPng { get; set; }

        public int Width => RawWidth == 0 ? 256 : RawWidth;
        public int Height => RawHeight == 0 ? 256 : RawHeight;
        public string Format => IsPng ? "PNG" : "BMP";
        public bool HasTransparency => IsPng || BitCount == 32;
    }

    public class IcoAnalyzerService
    {
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public IcoAnalysisReport Analyze(string filePath)
        {
            var report = new IcoAnalysisReport();
            var fileInfo = new FileInfo(filePath);
            report.FileSize = fileInfo.Length;

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            report.Directory = new IconDir
            {
                Reserved = reader.ReadUInt16(),
                Type = reader.ReadUInt16(),
                Count = reader.ReadUInt16()
            };

            if (report.Directory.Reserved != 0 || report.Directory.Type != 1)
            {
                throw new InvalidDataException("The file is not a valid ICO file.");
            }

            for (int i = 0; i < report.Directory.Count; i++)
            {
                report.Entries.Add(new IconDirEntry
                {
                    RawWidth = reader.ReadByte(),
                    RawHeight = reader.ReadByte(),
                    ColorCountPalette = reader.ReadByte(),
                    Reserved = reader.ReadByte(),
                    Planes = reader.ReadUInt16(),
                    BitCount = reader.ReadUInt16(),
                    BytesInRes = reader.ReadUInt32(),
                    ImageOffset = reader.ReadUInt32()
                });
            }

            foreach (var entry in report.Entries)
            {
                stream.Seek(entry.ImageOffset, SeekOrigin.Begin);
                var headerBytes = new byte[8];
                stream.ReadExactly(headerBytes);
                entry.IsPng = headerBytes.SequenceEqual(PngSignature);
            }

            return report;
        }
    }
}