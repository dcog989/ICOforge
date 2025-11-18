using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ICOforge.ViewModels
{
    public class SizeViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Size { get; }

        public string Label => $"{Size}x{Size}";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    // Selection change logic moved to the parent view model (ConversionOptionsViewModel)
                }
            }
        }

        public SizeViewModel(int size, bool isSelected = false)
        {
            Size = size;
            _isSelected = isSelected;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}