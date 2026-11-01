// EmulatorMK61 - emulator of the Soviet programmable calculators "Elektronika MK-61" and "MK-54"
// for Windows Phone 8.1 / Windows 10 Mobile. GPL-3.0, see LICENSE.
//
// The calculator as the app keeps it: the switches (in LocalSettings) and, while it is on, the
// running emulator, whose whole state is written to LocalFolder\state.mk61 when the app is
// suspended, so it comes back exactly as it was - the way pmk-android keeps persist.pmk.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EmulatorMK61.Core;
using Windows.Storage;

namespace EmulatorMK61
{
    public sealed class Machine
    {
        public static readonly Machine Current = new Machine();

        const string StateFile = "state.mk61";

        readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;
        Task _loading;

        Machine()
        {
            AngleMode = Get("angle", 0);
            SpeedMode = Get("speed", 1); // real speed, as pmk-android (and the CPU mostly idle)
            Model = Get("model", Emulator.ModelMK61);
            Vibrate = Get("vibrate", 1) != 0;
        }

        /// <summary>The running emulator, or null while the calculator is off.</summary>
        public Emulator Emulator { get; private set; }

        public bool IsOn { get { return Emulator != null; } }

        public int AngleMode { get; private set; }
        public int SpeedMode { get; private set; }
        public int Model { get; private set; }
        public bool Vibrate { get; private set; }

        /// <summary>Brings back the calculator as it was left: once, at start.</summary>
        public Task EnsureLoadedAsync()
        {
            if (_loading == null) _loading = LoadAsync();
            return _loading;
        }

        async Task LoadAsync()
        {
            if (Get("power", 0) == 0) return;
            Emulator e = null;
            try
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync(StateFile);
                using (Stream s = await file.OpenStreamForReadAsync())
                    e = Emulator.Load(s);
            }
            catch (Exception)
            {
                // No state, or a damaged one: switch on afresh.
            }
            if (e == null)
            {
                e = new Emulator();
                e.MkModel = Model;
            }
            e.AngleMode = AngleMode;
            e.SpeedMode = SpeedMode;
            Model = e.MkModel;
            Emulator = e;
            e.Start();
        }

        public void PowerOn()
        {
            if (IsOn) return;
            var e = new Emulator();
            e.MkModel = Model;
            e.AngleMode = AngleMode;
            e.SpeedMode = SpeedMode;
            Emulator = e;
            Set("power", 1);
            e.Start();
        }

        public async void PowerOff()
        {
            Emulator e = Emulator;
            if (e == null) return;
            Emulator = null;
            Set("power", 0);
            e.Stop(true);
            try
            {
                // (TryGetItemAsync is not in Windows Phone 8.1.)
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync(StateFile);
                await file.DeleteAsync();
            }
            catch (Exception) { }
        }

        public void SetAngleMode(int mode)
        {
            AngleMode = mode;
            Set("angle", mode);
            if (Emulator != null) Emulator.AngleMode = mode;
        }

        public void SetSpeedMode(int mode)
        {
            SpeedMode = mode;
            Set("speed", mode);
            if (Emulator != null) Emulator.SpeedMode = mode;
        }

        /// <summary>Only while off, as on pmk-android: the machine is built for one model.</summary>
        public void SetModel(int model)
        {
            Model = model;
            Set("model", model);
        }

        public void SetVibrate(bool on)
        {
            Vibrate = on;
            Set("vibrate", on ? 1 : 0);
        }

        /// <summary>App suspended: stop at a clean point and write the state.</summary>
        public async Task SuspendAsync()
        {
            Emulator e = Emulator;
            if (e == null) return;
            e.Stop(false);
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(StateFile, CreationCollisionOption.ReplaceExisting);
            using (Stream s = await file.OpenStreamForWriteAsync())
            {
                e.Save(s);
                s.SetLength(s.Position);
            }
        }

        public void Resume()
        {
            Emulator e = Emulator;
            if (e != null) e.Start();
        }

        // ------------------------------------------------------------------ program text

        /// <summary>The 105 program steps, read at the machine's next synchronisation point.</summary>
        public Task<List<int>> ReadProgramAsync()
        {
            var done = new TaskCompletionSource<List<int>>();
            Emulator e = Emulator;
            if (e == null) done.SetResult(null);
            else
            {
                e.RequestExport(c => done.TrySetResult(c));
                TimeOut(done);
            }
            return done.Task;
        }

        /// <summary>X1, X, Y, Z, T and the registers, read at the next synchronisation point.</summary>
        public Task<string[]> ReadRegistersAsync()
        {
            var done = new TaskCompletionSource<string[]>();
            Emulator e = Emulator;
            if (e == null) done.SetResult(null);
            else
            {
                e.RequestRegisters(r => done.TrySetResult(r));
                TimeOut(done);
            }
            return done.Task;
        }

        static async void TimeOut<T>(TaskCompletionSource<T> done) where T : class
        {
            // The machine answers within a few milliseconds while running; it does not answer
            // while the indicator is dark (a program running), so do not wait for ever.
            await Task.Delay(3000);
            done.TrySetResult(null);
        }

        int Get(string key, int fallback)
        {
            object v;
            return _settings.Values.TryGetValue(key, out v) && v is int ? (int)v : fallback;
        }

        void Set(string key, int value)
        {
            _settings.Values[key] = value;
        }
    }
}
