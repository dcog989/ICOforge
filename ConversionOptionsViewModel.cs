using ICOforge.ViewModels;
using System.Runtime.CompilerServices;

namespace ICOforge
{
    public class ConversionOptionsViewModel : ViewModelBase
    {
        private List<OutputProfile> _profiles = null!;
        private OutputProfile _selectedProfile = null!;
        private bool _isUpdatingFromProfile;

        private readonly Dictionary<int, bool> _icoSizes = new()
        {
            { 16, false }, { 20, false }, { 24, false }, { 32, false },
            { 48, false }, { 64, false }, { 72, false }, { 96, false },
            { 128, false }, { 180, false }, { 192, false }, { 256, false }
        };

        private bool _enableSvgColorization;
        private string _svgColor = "#FFD193";
        private bool _useLossyCompression;
        private int _selectedColorCount;
        private bool _isOutputToSource = true;
        private string _customLocationText = "[None Selected]";
        private string _customOutputPath = string.Empty;

        public List<OutputProfile> Profiles { get => _profiles; set => SetProperty(ref _profiles, value); }
        public OutputProfile SelectedProfile { get => _selectedProfile; set { if (SetProperty(ref _selectedProfile, value)) { OnProfileChanged(); } } }

        public bool IsIcoSizesEnabled => SelectedProfile?.Type == OutputProfileType.CustomIco || SelectedProfile?.Type == OutputProfileType.FaviconPack;

        private void SetIcoSizeValue(int size, bool value, [CallerMemberName] string? propertyName = null)
        {
            if (_icoSizes.TryGetValue(size, out bool current) && current == value) return;
            _icoSizes[size] = value;
            OnPropertyChanged(propertyName);
        }

        public bool Size16 { get => _icoSizes[16]; set { SetIcoSizeValue(16, value); OnIcoSizeChanged(); } }
        public bool Size20 { get => _icoSizes[20]; set { SetIcoSizeValue(20, value); OnIcoSizeChanged(); } }
        public bool Size24 { get => _icoSizes[24]; set { SetIcoSizeValue(24, value); OnIcoSizeChanged(); } }
        public bool Size32 { get => _icoSizes[32]; set { SetIcoSizeValue(32, value); OnIcoSizeChanged(); } }
        public bool Size48 { get => _icoSizes[48]; set { SetIcoSizeValue(48, value); OnIcoSizeChanged(); } }
        public bool Size64 { get => _icoSizes[64]; set { SetIcoSizeValue(64, value); OnIcoSizeChanged(); } }
        public bool Size72 { get => _icoSizes[72]; set { SetIcoSizeValue(72, value); OnIcoSizeChanged(); } }
        public bool Size96 { get => _icoSizes[96]; set { SetIcoSizeValue(96, value); OnIcoSizeChanged(); } }
        public bool Size128 { get => _icoSizes[128]; set { SetIcoSizeValue(128, value); OnIcoSizeChanged(); } }
        public bool Size180 { get => _icoSizes[180]; set { SetIcoSizeValue(180, value); OnIcoSizeChanged(); } }
        public bool Size192 { get => _icoSizes[192]; set { SetIcoSizeValue(192, value); OnIcoSizeChanged(); } }
        public bool Size256 { get => _icoSizes[256]; set { SetIcoSizeValue(256, value); OnIcoSizeChanged(); } }

        public bool EnableSvgColorization { get => _enableSvgColorization; set => SetProperty(ref _enableSvgColorization, value); }
        public string SvgColor { get => _svgColor; set => SetProperty(ref _svgColor, value); }
        public bool UseLossyCompression { get => _useLossyCompression; set => SetProperty(ref _useLossyCompression, value); }
        public List<int> ColorOptions { get; } = new() { 4, 8, 16, 32, 64, 128, 256 };
        public int SelectedColorCount { get => _selectedColorCount; set => SetProperty(ref _selectedColorCount, value); }
        public bool IsOutputToSource { get => _isOutputToSource; set => SetProperty(ref _isOutputToSource, value); }
        public string CustomLocationText { get => _customLocationText; set => SetProperty(ref _customLocationText, value); }
        public string CustomOutputPath { get => _customOutputPath; set { if (SetProperty(ref _customOutputPath, value)) { CustomLocationText = System.IO.Path.GetFileName(_customOutputPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)); } } }

        public ConversionOptionsViewModel()
        {
            Profiles = OutputProfile.GetAvailableProfiles();
            SelectedProfile = Profiles.First(p => p.Type == OutputProfileType.StandardIco);
            SelectedColorCount = ColorOptions.Last();
            CustomOutputPath = NativeMethods.GetDownloadsPath();
        }

        private void OnProfileChanged()
        {
            if (SelectedProfile == null) return;

            _isUpdatingFromProfile = true;

            OnPropertyChanged(nameof(IsIcoSizesEnabled));

            var sizes = new HashSet<int>(SelectedProfile.DefaultSizes);
            var allSizes = _icoSizes.Keys.ToList();
            foreach (var sizeKey in allSizes)
            {
                SetIcoSizeValue(sizeKey, sizes.Contains(sizeKey), $"Size{sizeKey}");
            }

            _isUpdatingFromProfile = false;
        }

        private void OnIcoSizeChanged()
        {
            if (_isUpdatingFromProfile) return;

            if (SelectedProfile.Type != OutputProfileType.CustomIco && SelectedProfile.Type != OutputProfileType.FaviconPack)
            {
                SelectedProfile = Profiles.First(p => p.Type == OutputProfileType.CustomIco);
            }
        }

        public List<int> GetSelectedSizes()
        {
            return _icoSizes.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
        }

        public PngOptimizationOptions GetPngOptimizationOptions() => new(UseLossyCompression, SelectedColorCount);

        public string GetSvgHexColor() => EnableSvgColorization ? SvgColor : string.Empty;
    }
}