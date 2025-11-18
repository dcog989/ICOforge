using System.IO;

namespace ICOforge.Services
{
    public class IcoAnalysisReport
    {
        public IconDir? Directory { get; set; }
        public List<IconDirEntry> Entries { get; set; } = new List<IconDirEntry>();
        public long FileSize { get; set; }
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