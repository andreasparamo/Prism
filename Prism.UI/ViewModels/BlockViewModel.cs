using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace Prism.UI.ViewModels;

public partial class BlockViewModel : ObservableObject
{
    [ObservableProperty]
    private string _quote = "Stay focused on your goals.";

    [ObservableProperty]
    private bool _canBypass = true;

    private readonly Window _window;

    public BlockViewModel(Window window)
    {
        _window = window;
    }

    [RelayCommand]
    private void Unblock()
    {
        if (CanBypass)
        {
            _window.Close();
        }
    }
}
