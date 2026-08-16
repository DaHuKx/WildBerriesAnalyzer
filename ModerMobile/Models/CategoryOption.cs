using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ModerMobile.Models;

public sealed class CategoryOption : INotifyPropertyChanged
{
    private bool _isSelected;

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsNew { get; set; }

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
