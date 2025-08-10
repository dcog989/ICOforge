using ICOforge.ViewModels;

namespace ICOforge
{
    public class SizeViewModel(int size, bool isSelected = false, Action? onSelectionChanged = null) : ViewModelBase
    {
        private bool _isSelected = isSelected;

        public int Size { get; } = size;
        public string Label => $"{Size}x{Size}";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    onSelectionChanged?.Invoke();
                }
            }
        }
    }
}