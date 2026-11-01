// EmulatorMK61 - emulator of the Soviet programmable calculators "Elektronika MK-61" and "MK-54"
// for Windows Phone 8.1 / Windows 10 Mobile. GPL-3.0, see LICENSE.
//
// The application: one frame, the hardware Back button, the machine's state saved on suspend and
// the file pickers' return (WP8.1 pickers leave the app and come back through OnActivated).
using System;
using EmulatorMK61.Views;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace EmulatorMK61
{
    public sealed partial class App : Application
    {
        Frame _frame;

        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
            Resuming += (s, e) => Machine.Current.Resume();
            HardwareButtons.BackPressed += OnBackPressed;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            EnsureWindow();
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            EnsureWindow();
            var open = args as FileOpenPickerContinuationEventArgs;
            if (open != null && open.Files.Count > 0)
                await ProgramFiles.ImportAsync(open.Files[0]);
            var save = args as FileSavePickerContinuationEventArgs;
            if (save != null && save.File != null)
                await ProgramFiles.ExportAsync(save.File);
        }

        void EnsureWindow()
        {
            if (_frame == null)
            {
                _frame = new Frame();
                _frame.NavigationFailed += (s, e) =>
                {
                    e.Handled = true;
                    MainPage.Say(string.Format("Cannot open {0}: {1}", e.SourcePageType.Name, e.Exception.Message));
                };
                Window.Current.Content = _frame;
                _frame.Navigate(typeof(MainPage));
            }
            Window.Current.Activate();
        }

        async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            SuspendingDeferral deferral = e.SuspendingOperation.GetDeferral();
            try { await Machine.Current.SuspendAsync(); }
            catch (Exception) { }
            deferral.Complete();
        }

        void OnBackPressed(object sender, BackPressedEventArgs e)
        {
            // Back goes back a page; on the calculator it is not handled, so the app closes
            // (and is suspended first, which saves the machine).
            if (_frame != null && _frame.CanGoBack)
            {
                _frame.GoBack();
                e.Handled = true;
            }
        }
    }
}
