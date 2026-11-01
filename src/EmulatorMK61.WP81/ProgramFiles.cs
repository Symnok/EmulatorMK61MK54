// EmulatorMK61 - emulator of the Soviet programmable calculators "Elektronika MK-61" and "MK-54".
// GPL-3.0, see LICENSE.
//
// Programs as text files, in pmk-android's listing format (ProgramText). WP8.1 pickers leave the
// app and come back through App.OnActivated, maybe into a fresh process, so the listing to save
// is kept in LocalSettings until the file is chosen.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EmulatorMK61.Core;
using EmulatorMK61.Views;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace EmulatorMK61
{
    public static class ProgramFiles
    {
        const string PendingKey = "pending_export";

        public static void PickImport()
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add(".txt");
            picker.PickSingleFileAndContinue();
        }

        /// <summary>Reads the program out of the machine, then opens the save picker. Returns an
        /// error to show, or null.</summary>
        public static async Task<string> PickExportAsync()
        {
            List<int> cmds = await Machine.Current.ReadProgramAsync();
            if (cmds == null)
                return "Не удалось прочитать программу. Остановите выполнение программы (С/П) и повторите.";
            ApplicationData.Current.LocalSettings.Values[PendingKey] = ProgramText.Export(cmds);

            var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary, SuggestedFileName = "program" };
            picker.FileTypeChoices.Add("Текст программы", new List<string> { ".txt" });
            picker.PickSaveFileAndContinue();
            return null;
        }

        /// <summary>The open picker's file: load it into program memory.</summary>
        public static async Task ImportAsync(StorageFile file)
        {
            await Machine.Current.EnsureLoadedAsync();
            Emulator e = Machine.Current.Emulator;
            if (e == null || e.MkModel != Emulator.ModelMK61)
            {
                MainPage.Say("Загрузка программы возможна только на включённом МК-61.");
                return;
            }
            try
            {
                string text = await FileIO.ReadTextAsync(file);
                List<int> codes = ProgramText.Import(text);
                e.ImportProgram(codes);
                MainPage.Say(string.Format("Программа загружена: {0} шагов.", codes.Count));
            }
            catch (ProgramParseException ex)
            {
                MainPage.Say("Ошибка в программе, команда: " + ex.Command);
            }
            catch (Exception ex)
            {
                MainPage.Say("Не удалось прочитать файл: " + ex.Message);
            }
        }

        /// <summary>The save picker's file: write the listing kept before the picker opened.</summary>
        public static async Task ExportAsync(StorageFile file)
        {
            object text;
            if (!ApplicationData.Current.LocalSettings.Values.TryGetValue(PendingKey, out text) || !(text is string))
                return;
            try
            {
                await FileIO.WriteTextAsync(file, (string)text);
                ApplicationData.Current.LocalSettings.Values.Remove(PendingKey);
                MainPage.Say("Программа сохранена в " + file.Name + ".");
            }
            catch (Exception ex)
            {
                MainPage.Say("Не удалось записать файл: " + ex.Message);
            }
        }
    }
}
