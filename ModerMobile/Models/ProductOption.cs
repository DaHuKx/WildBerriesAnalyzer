using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ModerMobile.Models;

public sealed class ProductOption : INotifyPropertyChanged
{
    private bool _isSelected;

    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string DisplayText => $"#{Id}  {Name}";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
