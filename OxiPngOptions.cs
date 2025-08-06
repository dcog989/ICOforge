namespace ICOforge
{
    public enum OxiPngOptimizationLevel
    {
        Level0,
        Level1,
        Level2,
        Level3,
        Level4,
        Level5,
        Level6,
        Max
    }

    public enum OxiPngStripMode
    {
        None,
        Safe,
        All
    }

    public record OxiPngOptions
    {
        public OxiPngOptimizationLevel OptimizationLevel { get; init; } = OxiPngOptimizationLevel.Level2;
        public OxiPngStripMode StripMode { get; init; } = OxiPngStripMode.Safe;
        public TimeSpan? Timeout { get; init; }
        public bool NoColorTypeReduction { get; init; } = false;
        public bool NoBitDepthReduction { get; init; } = false;
    }
}