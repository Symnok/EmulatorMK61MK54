// EmulatorMK61 - pmk-android's manual and its "undocumented features" page, from the package.
// GPL-3.0, see LICENSE.
using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace EmulatorMK61.Views
{
    public sealed partial class HtmlPage : Page
    {
        public HtmlPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Web.Navigate(new Uri("ms-appx-web:///Html/" + (string)e.Parameter));
        }
    }
}
