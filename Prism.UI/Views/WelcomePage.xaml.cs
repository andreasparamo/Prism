using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Prism.UI.Views
{
    public partial class WelcomePage : Page
    {
        public WelcomePage()
        {
            InitializeComponent();
        }

        private void BtnGetStarted_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new DashboardPage());
        }
    }
}
