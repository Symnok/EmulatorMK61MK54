// EmulatorMK61 - emulator of the Soviet programmable calculators "Elektronika MK-61" and "MK-54"
// for Windows Phone 8.1 / Windows 10 Mobile. GPL-3.0, see LICENSE.
//
// The calculator face: indicator, switches and the 30 keys laid out as pmk-android's
// activity_main.xml (key codes from its android:tag values). Symbols the phone fonts lack are
// drawn with pmk-android's missing-symbols font, as there.
using System;
using System.Collections.Generic;
using EmulatorMK61.Core;
using Windows.Phone.Devices.Notification;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace EmulatorMK61.Views
{
    public sealed partial class MainPage : Page
    {
        const int Black = 0, Yellow = 1, Blue = 2, Gray = 3, Red = 4;
        const int KeyF = 39, KeyK = 38;

        sealed class KeyDef
        {
            public int Code, Color;
            public string Text, YellowLabel, BlueLabel, Middle;
            public bool TextSym, YellowSym;
        }

        static KeyDef K(int code, string text, int color, string yellow = null, string blue = null,
                        bool textSym = false, bool yellowSym = false, string middle = null)
        {
            return new KeyDef { Code = code, Text = text, Color = color, YellowLabel = yellow, BlueLabel = blue,
                                TextSym = textSym, YellowSym = yellowSym, Middle = middle };
        }

        // In missing-symbols.ttf: > ШГ→, < ШГ←, : П→X, ; X→П, = ↔, BA В↑, ) √, %$ eˣ, (& xʸ, !#$ 10ˣ, 0 ○.
        static readonly KeyDef[][] KeyRows = {
            new[] {
                K(KeyF, "F", Yellow),
                K(35, ">", Black, "x<0", textSym: true),
                K(37, "<", Black, "x=0", textSym: true),
                K(32, "В/О", Black, "x≥0"),
                K(30, "С/П", Black, "x≠0"),
            },
            new[] {
                K(KeyK, "K", Blue),
                K(36, ":", Black, "L0", textSym: true),
                K(34, ";", Black, "L1", textSym: true),
                K(31, "БП", Black, "L2"),
                K(33, "ПП", Black, "L3"),
            },
            new[] {
                K(17, "7", Gray, "sin", "[x]"),
                K(18, "8", Gray, "cos", "{x}"),
                K(19, "9", Gray, "tg", "max"),
                K(21, "–", Gray, ")", yellowSym: true),
                K(23, "÷", Gray, "1/x"),
            },
            new[] {
                K(14, "4", Gray, "sin⁻¹", "|x|"),
                K(15, "5", Gray, "cos⁻¹", "ЗН"),
                K(16, "6", Gray, "tg⁻¹", "°′←"),
                K(20, "+", Gray, "π", "°′→"),
                K(22, "×", Gray, "x²"),
            },
            new[] {
                K(11, "1", Gray, "%$", yellowSym: true),
                K(12, "2", Gray, "lg"),
                K(13, "3", Gray, "ln", "°′″←"),
                K(24, "=", Gray, "(&", "°′″→", textSym: true, yellowSym: true),
                K(29, "BA", Gray, "Вx", "СЧ", textSym: true, middle: "e"),
            },
            new[] {
                K(10, "0", Gray, "!#$", "НОП", yellowSym: true),
                K(25, "•", Gray, "0", "∧", yellowSym: true),
                K(26, "/–/", Gray, "АВТ", "∨"),
                K(27, "ВП", Gray, "ПРГ", "⊕"),
                K(28, "CX", Red, "CF", "ИНВ"),
            },
        };

        // White letters under the bottom row (register names a..d; "НОП" on the MK-54).
        static readonly string[] Letters = { null, "a", "b", "c", "d" };

        static readonly Color IndicatorOff = Color.FromArgb(255, 0, 0, 0);
        static readonly Color IndicatorFast = Color.FromArgb(255, 0x10, 0x38, 0x10);
        static readonly Color IndicatorSlow = Color.FromArgb(255, 0x10, 0x30, 0x10);

        readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        readonly List<TextBlock> _blueLabels = new List<TextBlock>();
        readonly List<TextBlock> _pairedYellow = new List<TextBlock>();
        TextBlock _middleE, _nop54;
        Button _clearKey, _exchangeKey;
        FontFamily _symbols;
        string _shown;
        bool _fPressed, _kPressed, _faultShown;

        public MainPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            _symbols = (FontFamily)Application.Current.Resources["SymbolsFont"];
            BuildKeyboard();
            _timer.Tick += (s, e) => Refresh();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            try { await Windows.UI.ViewManagement.StatusBar.GetForCurrentView().HideAsync(); } catch (Exception) { }
            await Machine.Current.EnsureLoadedAsync();
            UpdateAll();
            _timer.Start();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _timer.Stop();
        }

        // ------------------------------------------------------------------ the face

        void BuildKeyboard()
        {
            for (int c = 0; c < 5; c++) Keyboard.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;
            foreach (KeyDef[] keys in KeyRows)
            {
                Keyboard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Keyboard.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                for (int c = 0; c < keys.Length; c++)
                {
                    KeyDef k = keys[c];
                    var labels = new Grid { Height = 21, Margin = new Thickness(5, 2, 5, 1) };
                    if (k.YellowLabel != null)
                    {
                        var y = Label(k.YellowLabel, "LabelYellow", k.YellowSym);
                        y.HorizontalAlignment = k.BlueLabel != null ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                        labels.Children.Add(y);
                        if (k.BlueLabel != null) _pairedYellow.Add(y);
                    }
                    if (k.BlueLabel != null)
                    {
                        var b = Label(k.BlueLabel, "LabelBlue", false);
                        b.HorizontalAlignment = HorizontalAlignment.Right;
                        labels.Children.Add(b);
                        _blueLabels.Add(b);
                    }
                    if (k.Middle != null)
                    {
                        _middleE = Label(k.Middle, null, false);
                        _middleE.HorizontalAlignment = HorizontalAlignment.Center;
                        labels.Children.Add(_middleE);
                    }
                    Grid.SetRow(labels, row);
                    Grid.SetColumn(labels, c);
                    Keyboard.Children.Add(labels);

                    var key = MakeKey(k);
                    Grid.SetRow(key, row + 1);
                    Grid.SetColumn(key, c);
                    Keyboard.Children.Add(key);
                    if (k.Code == 28) _clearKey = key;
                    if (k.Code == 24) _exchangeKey = key;
                }
                row += 2;
            }

            Keyboard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < Letters.Length; c++)
            {
                var t = Label(Letters[c] ?? "", null, false);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                t.Margin = new Thickness(0, 1, 0, 0);
                Grid.SetRow(t, row);
                Grid.SetColumn(t, c);
                Keyboard.Children.Add(t);
                if (c == 0) _nop54 = t;
            }
        }

        TextBlock Label(string text, string brushKey, bool sym)
        {
            var t = new TextBlock
            {
                Text = text,
                FontSize = sym ? 17 : 15,
                Foreground = brushKey == null ? new SolidColorBrush(Colors.White) : (Brush)Application.Current.Resources[brushKey],
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (sym) t.FontFamily = _symbols;
            return t;
        }

        Button MakeKey(KeyDef k)
        {
            var b = new Button { Style = (Style)Application.Current.Resources["KeyButton"], Tag = k.Code };
            Color bg, fg = Colors.White;
            switch (k.Color)
            {
                case Yellow: bg = Color.FromArgb(255, 0xF5, 0xE3, 0x45); fg = Colors.Black; break;
                case Blue: bg = Color.FromArgb(255, 0x71, 0xE3, 0xFF); fg = Colors.Black; break;
                case Gray: bg = Color.FromArgb(255, 0xCC, 0xCC, 0xCC); fg = Colors.Black; break;
                case Red: bg = Color.FromArgb(255, 0xF0, 0x05, 0x05); break;
                default: bg = Colors.Black; b.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 0x80, 0x80, 0x80)); break;
            }
            b.Background = new SolidColorBrush(bg);
            b.Foreground = new SolidColorBrush(fg);
            b.Content = KeyText(k.Text, k.TextSym, fg);
            b.Click += OnKey;
            return b;
        }

        TextBlock KeyText(string text, bool sym, Color fg)
        {
            var t = new TextBlock { Text = text, Foreground = new SolidColorBrush(fg), FontSize = sym ? 26 : 21, FontWeight = FontWeights.SemiBold };
            if (sym) t.FontFamily = _symbols;
            return t;
        }

        /// <summary>The MK-54 has no blue functions, no register e and says "Cx" (SkinHelper.setMkModelSkin).</summary>
        void ApplyModelSkin(int model)
        {
            bool mk54 = model == Emulator.ModelMK54;
            foreach (var b in _blueLabels) b.Visibility = mk54 ? Visibility.Collapsed : Visibility.Visible;
            foreach (var y in _pairedYellow) y.HorizontalAlignment = mk54 ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            _middleE.Visibility = mk54 ? Visibility.Collapsed : Visibility.Visible;
            _nop54.Text = mk54 ? "НОП" : "";
            _clearKey.Content = KeyText(mk54 ? "Cx" : "CX", false, Colors.White);
            _exchangeKey.Content = mk54 ? KeyText("XY", false, Colors.Black) : KeyText("=", true, Colors.Black);
            ModelName.Text = "Электроника  МК" + (mk54 ? "-54" : " 61");
        }

        void UpdateAll()
        {
            Machine m = Machine.Current;
            ApplyModelSkin(m.Model);
            PowerSwitch.IsChecked = m.IsOn;
            AngleR.IsChecked = m.AngleMode == 0;
            AngleG.IsChecked = m.AngleMode == 1;
            AngleD.IsChecked = m.AngleMode == 2;
            SetFK(false, false);
            Refresh();
        }

        void Refresh()
        {
            Machine m = Machine.Current;
            Emulator e = m.Emulator;
            string text = e != null ? e.DisplayText : Emulator.EmptyIndicator;
            if (text != _shown)
            {
                _shown = text;
                Display.Text = text;
            }
            var bg = e == null ? IndicatorOff : m.SpeedMode == 0 ? IndicatorFast : IndicatorSlow;
            var brush = IndicatorBox.Background as SolidColorBrush;
            if (brush == null || brush.Color != bg) IndicatorBox.Background = new SolidColorBrush(bg);

            if (e != null && e.Fault != null && !_faultShown)
            {
                _faultShown = true;
                Say("Эмулятор остановился с ошибкой: " + e.Fault.Message);
            }
        }

        void SetFK(bool f, bool k)
        {
            _fPressed = f;
            _kPressed = k;
            IndicatorF.Opacity = f ? 1 : 0;
            IndicatorK.Opacity = k ? 1 : 0;
        }

        // ------------------------------------------------------------------ input

        void OnKey(object sender, RoutedEventArgs e)
        {
            Emulator emu = Machine.Current.Emulator;
            if (emu == null) return;
            int code = (int)((Button)sender).Tag;
            Buzz(15);
            SetFK(code == KeyF, code == KeyK);
            emu.keypad(code);
        }

        void OnIndicatorTapped(object sender, TappedRoutedEventArgs e)
        {
            if (!Machine.Current.IsOn) return;
            Machine.Current.SetSpeedMode(1 - Machine.Current.SpeedMode);
            Refresh();
        }

        void OnPowerClick(object sender, RoutedEventArgs e)
        {
            Machine m = Machine.Current;
            if (PowerSwitch.IsChecked == true) m.PowerOn();
            else m.PowerOff();
            Buzz(50);
            _faultShown = false;
            UpdateAll();
        }

        void OnAngleClick(object sender, RoutedEventArgs e)
        {
            int mode = int.Parse((string)((FrameworkElement)sender).Tag);
            if (mode != Machine.Current.AngleMode && Machine.Current.IsOn) Buzz(20);
            Machine.Current.SetAngleMode(mode);
            UpdateAll();
        }

        void OnModelNameHolding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started) SwapModel();
        }

        void SwapModel()
        {
            Machine m = Machine.Current;
            if (m.IsOn)
            {
                Say("Модель меняется только на выключенном калькуляторе.");
                return;
            }
            m.SetModel(1 - m.Model);
            UpdateAll();
        }

        void Buzz(int ms)
        {
            if (!Machine.Current.Vibrate) return;
            try { VibrationDevice.GetDefault().Vibrate(TimeSpan.FromMilliseconds(ms)); } catch (Exception) { }
        }

        // ------------------------------------------------------------------ menu

        void OnMenuOpened(object sender, object e)
        {
            Machine m = Machine.Current;
            MenuSpeed.Label = m.SpeedMode == 0 ? "скорость: быстро (включить реальную)" : "скорость: реальная (включить быструю)";
            MenuModel.Label = m.Model == Emulator.ModelMK61 ? "сменить модель на МК-54" : "сменить модель на МК-61";
            MenuModel.Visibility = m.IsOn ? Visibility.Collapsed : Visibility.Visible;
            MenuRegisters.Visibility = m.IsOn ? Visibility.Visible : Visibility.Collapsed;
            bool txt = m.IsOn && m.Model == Emulator.ModelMK61;
            MenuImport.Visibility = txt ? Visibility.Visible : Visibility.Collapsed;
            MenuExport.Visibility = txt ? Visibility.Visible : Visibility.Collapsed;
            MenuVibrate.Label = m.Vibrate ? "вибрация: вкл" : "вибрация: выкл";
        }

        void OnMenuSpeed(object sender, RoutedEventArgs e)
        {
            Machine.Current.SetSpeedMode(1 - Machine.Current.SpeedMode);
            Refresh();
        }

        void OnMenuModel(object sender, RoutedEventArgs e) { SwapModel(); }

        void OnMenuRegisters(object sender, RoutedEventArgs e) { Frame.Navigate(typeof(RegistersPage)); }

        void OnMenuImport(object sender, RoutedEventArgs e) { ProgramFiles.PickImport(); }

        async void OnMenuExport(object sender, RoutedEventArgs e)
        {
            string error = await ProgramFiles.PickExportAsync();
            if (error != null) Say(error);
        }

        void OnMenuVibrate(object sender, RoutedEventArgs e) { Machine.Current.SetVibrate(!Machine.Current.Vibrate); }

        void OnMenuInstruction(object sender, RoutedEventArgs e) { Frame.Navigate(typeof(HtmlPage), "instruction.html"); }

        void OnMenuUndocumented(object sender, RoutedEventArgs e) { Frame.Navigate(typeof(HtmlPage), "undocument_features.html"); }

        void OnMenuAbout(object sender, RoutedEventArgs e)
        {
            var v = Windows.ApplicationModel.Package.Current.Id.Version;
            Say(string.Format("МК-61/54 {0}.{1}.{2}.{3}\n\n" +
                "Эмулятор программируемых калькуляторов «Электроника МК-61» и «МК-54» на уровне микрокода.\n\n" +
                "Перенос на Windows Phone Android-приложения «Extended emulator of МК 61/54» (Вадим Хохлов), " +
                "движок которого основан на emu145 Феликса Лазарева.\n" +
                "Google Play: play.google.com/store/apps/details?id=com.cax.pmk.ext\n" +
                "Исходный код: github.com/xvadim/pmk-android\n\n" +
                "Перенос выполнен с помощью Claude (ИИ-ассистент Anthropic, Claude Code).\n\n" +
                "Нажатие на индикатор: реальная / быстрая скорость. Долгое нажатие на название " +
                "(калькулятор выключен): смена модели.\n\nЛицензия GNU GPL v3.",
                v.Major, v.Minor, v.Build, v.Revision), "О программе");
        }

        public static async void Say(string text, string title = null)
        {
            try { await new MessageDialog(text, title ?? "").ShowAsync(); } catch (Exception) { }
        }
    }
}
