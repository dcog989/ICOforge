namespace ICOforge
{
    public enum OutputProfileType
    {
        ApplicationIco,
        CustomIco,
        FaviconPack,
        StandardIco
    }

    public class OutputProfile
    {
        public string Name { get; set; } = string.Empty;
        public string ToolTip { get; set; } = string.Empty;
        public OutputProfileType Type { get; set; }
        public List<int> DefaultSizes { get; set; } = new List<int>();

        public static List<OutputProfile> GetAvailableProfiles()
        {
            return new List<OutputProfile>
            {
                new OutputProfile
                {
                    Name = "Application ICO",
                    ToolTip = "A profile for application icons, including a wide range of standard sizes.",
                    Type = OutputProfileType.ApplicationIco,
                    DefaultSizes = new List<int> { 16, 20, 24, 32, 48, 64, 72, 96, 128, 256 }
                },
                new OutputProfile
                {
                    Name = "Custom ICO",
                    ToolTip = "Allows for manual selection of any available icon size. All sizes are selected by default.",
                    Type = OutputProfileType.CustomIco,
                    DefaultSizes = new List<int> { 16, 20, 24, 32, 48, 64, 72, 96, 128, 180, 192, 256 }
                },
                new OutputProfile
                {
                    Name = "Favicon Pack",
                    ToolTip = "Generates a full set of web favicons, including PNGs, an SVG, and a multi-size ICO file. Only one source image can be processed.",
                    Type = OutputProfileType.FaviconPack,
                    DefaultSizes = new List<int> { 16, 24, 32, 48, 64 }
                },
                new OutputProfile
                {
                    Name = "Standard ICO",
                    ToolTip = "A standard set of sizes suitable for most general-purpose icons.",
                    Type = OutputProfileType.StandardIco,
                    DefaultSizes = new List<int> { 16, 24, 32, 48, 64, 128, 256 }
                }
            }.OrderBy(p => p.Name).ToList();
        }
    }
}