using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Prism.UI.ViewModels;

public partial class WelcomeViewModel : ObservableObject
{
    private readonly Action _onGetStarted;

    public WelcomeViewModel(Action onGetStarted)
    {
        _onGetStarted = onGetStarted;
    }

    [RelayCommand]
    private void GetStarted()
    {
        _onGetStarted?.Invoke();
    }
}
