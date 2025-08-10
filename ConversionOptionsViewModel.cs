using System.Collections.ObjectModel;
using ICOforge.ViewModels;

namespace ICOforge
{
    public class ConversionOptionsViewModel : ViewModelBase
    {
        private List<OutputProfile> _profiles = null!;
        private OutputProfile _selectedProfile = null!;
        private bool _isUpdatingFromProfile;

        private bool _enableSvgColorization;
        private string _svgColor = "#FFD193";
        private bool _useLossyCompression;
        private int _selectedColorCount;
        private bool _isOutputToSource = true;
        private string _customLocationText = "[None Selected]";
        private string _customOutputPath = string.Empty;

        public List<OutputProfile> Profiles { get => _profiles; set => SetProperty(ref _profiles, value); }
        public OutputProfile SelectedProfile { get => _selectedProfile; set { if (SetProperty(ref _selectedProfile, value)) { OnProfileChanged(); } } }
        public ObservableCollection<SizeViewModel> IcoSizes { get; } = new();

        public bool IsIcoSizesEnabled => SelectedProfile?.Type == OutputProfileType.CustomIco || SelectedProfile?.Type == OutputProfileType.FaviconPack;

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
            var allSizes = new[] { 16, 20, 24, 32, 48, 64, 72, 96, 128, 180, 192, 256 };
            foreach (var size in allSizes)
            {
                IcoSizes.Add(new SizeViewModel(size, onSelectionChanged: OnIcoSizeChanged));
            }

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
            foreach (var sizeVM in IcoSizes)
            {
                sizeVM.IsSelected = sizes.Contains(sizeVM.Size);
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
            return IcoSizes.Where(s => s.IsSelected).Select(s => s.Size).ToList();
        }

        public PngOptimizationOptions GetPngOptimizationOptions() => new(UseLossyCompression, SelectedColorCount);

        public string GetSvgHexColor() => EnableSvgColorization ? SvgColor : string.Empty;
    }
}