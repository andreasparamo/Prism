using Prism.UI.ViewModels;
using System.Windows;

namespace Prism.UI.Views;

public partial class BlockWindow : Window
{
    public BlockWindow()
    {
        InitializeComponent();
        DataContext = new BlockViewModel(this);
    }
}
