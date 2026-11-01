// EmulatorMK61 - the stack and the memory registers, as pmk-android's info screen shows them.
// GPL-3.0, see LICENSE.
using System.Text;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace EmulatorMK61.Views
{
    public sealed partial class RegistersPage : Page
    {
        static readonly string[] Names = {
            "X1", "X", "Y", "Z", "T",
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e"
        };

        public RegistersPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            Status.Text = "чтение…";
            string[] regs = await Machine.Current.ReadRegistersAsync();
            if (regs == null)
            {
                Status.Text = "Регистры недоступны, пока выполняется программа (индикатор погашен) или калькулятор выключен.";
                return;
            }
            Status.Text = "";
            var stack = new StringBuilder();
            for (int i = 0; i < 5; i++) stack.AppendFormat("{0,-3}{1}\n", Names[i], regs[i]);
            Stack.Text = stack.ToString().TrimEnd();
            var mem = new StringBuilder();
            for (int i = 5; i < regs.Length; i++)
                if (!string.IsNullOrEmpty(regs[i])) mem.AppendFormat("{0,-3}{1}\n", Names[i], regs[i]);
            Memory.Text = mem.ToString().TrimEnd();
        }
    }
}
