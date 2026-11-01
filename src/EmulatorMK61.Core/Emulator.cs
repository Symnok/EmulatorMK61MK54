// EmulatorMK61 - emulator of the Soviet programmable calculators "Elektronika MK-61" and "MK-54"
// for Windows Phone 8.1 / Windows 10 Mobile. A port of pmk-android (github.com/xvadim/pmk-android),
// whose engine follows Felix Lazarev's emu145. GPL-3.0, see LICENSE.
//
// The calculator: the chips wired in a ring (Emulator.java), run on a thread of its own. The
// indicator is published as a string the page polls: each of the 12 places is a symbol followed
// by "." (decimal point lit) or "/" (an unlit point - an empty glyph in the Digital-7 font).
//
// Differences from the Java original:
// - the Y indicator is not drawn, and the register dump is made only when someone asks for it;
// - "real speed" is paced against a stopwatch (the phone's sleep is too coarse for sleep(1));
// - a normal stop also leaves the 560-tick loop early, so suspending never waits half a second;
// - the saved state is our own binary layout (Java serialization is not readable here).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EmulatorMK61.Core
{
    public sealed class Emulator
    {
        public const int ModelMK61 = 0;
        public const int ModelMK54 = 1;

        /// <summary>Real time per 42 ticks in "real speed" mode. pmk-android sleeps 1 ms there,
        /// which on a phone comes to about 1.1 ms.</summary>
        public static double RealSpeedMsPerTick42 = 1.1;

        const int StateStopped = 0, StateRunning = 1, StateStoppingNormal = 2, StateStoppingForced = 3;

        Mcu IK1302 = new Mcu();
        Mcu IK1303 = new Mcu();
        Mcu IK1306 = new Mcu();
        Memory IR2_1 = new Memory();
        Memory IR2_2 = new Memory();

        int angle_mode = 10; // R=10, GRD=11, G=12
        volatile int speed_mode;  // 0=fast, 1=real speed
        int mk_model = ModelMK61;

        int syncCounter;
        readonly int[] indicator = new int[12];
        readonly int[] indicator_old = new int[12];
        readonly bool[] ind_comma = new bool[12];
        readonly bool[] ind_comma_old = new bool[12];
        readonly StringBuilder displayString = new StringBuilder(24);

        volatile int runningState = StateStopped;
        Task _task;

        readonly object _sync = new object();
        readonly int[] mImportCmds = new int[105];
        int mImportCmdCount = -1;
        Action<List<int>> mExportListener;
        Action<string[]> mRegsListener;

        // Real-speed pacing.
        readonly Stopwatch _pace = new Stopwatch();
        long _pacedTicks;
        volatile bool _paceReset = true;
        readonly ManualResetEventSlim _never = new ManualResetEventSlim(false);

        static readonly char[] show_symbols = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-', 'L', 'C', 'D', 'E', ' ' };

        /// <summary>An unlit indicator in the Digital-7 font: blanks with empty point places.</summary>
        public const string EmptyIndicator = "/ / / / / / / / / / / / //";

        public Emulator()
        {
            IK1302.ik130x = 2;
            IK1303.ik130x = 3;
            IK1306.ik130x = 6;

            IK1302.ucmd_rom = UCommands.ik1302_urom;
            IK1303.ucmd_rom = UCommands.ik1303_urom;
            IK1306.ucmd_rom = UCommands.ik1306_urom;

            IK1302.synchro_rom = Synchro.ik1302_srom;
            IK1303.synchro_rom = Synchro.ik1303_srom;
            IK1306.synchro_rom = Synchro.ik1306_srom;

            IK1302.cmd_rom = MCommands.ik1302_mrom;
            IK1303.cmd_rom = MCommands.ik1303_mrom;
            IK1306.cmd_rom = MCommands.ik1306_mrom;

            DisplayText = EmptyIndicator;
        }

        /// <summary>The indicator as the Digital-7 font draws it; changes from the emulator thread.</summary>
        public string DisplayText { get; private set; }

        /// <summary>Set when the emulator thread died of an exception.</summary>
        public Exception Fault { get; private set; }

        public bool IsRunning { get { return runningState != StateStopped; } }

        // ----------------------------------------------------------------- thread

        public void Start()
        {
            if (_task != null) return;
            runningState = StateRunning;
            _paceReset = true;
            _task = Task.Factory.StartNew(Run, TaskCreationOptions.LongRunning);
        }

        void Run()
        {
            try
            {
                while (runningState == StateRunning)
                    step();

                if (runningState != StateStoppingForced)
                {
                    // Stop where the Java original stops, so a saved state resumes cleanly.
                    while (!(IR2_1.microtick == 84 && syncCounter == 0))
                        tick42();
                }
            }
            catch (Exception ex)
            {
                Fault = ex;
            }
            runningState = StateStopped;
        }

        /// <summary>Stops the thread and waits for it. A normal stop ends at the point the state
        /// is saved from; a forced one (power off) just ends.</summary>
        public void Stop(bool force)
        {
            Task t = _task;
            if (t == null) return;
            runningState = force ? StateStoppingForced : StateStoppingNormal;
            try { t.Wait(); } catch (AggregateException) { }
            _task = null;
        }

        // ----------------------------------------------------------------- settings

        /// <summary>0 = radians, 1 = grads, 2 = degrees (the switch order on the calculator).</summary>
        public int AngleMode
        {
            get { int mode = angle_mode - 10; return mode == 0 ? 0 : 3 - mode; }
            set { int mode = value == 0 ? 0 : 3 - value; angle_mode = mode + 10; }
        }

        /// <summary>0 = as fast as possible, 1 = the speed of the real calculator.</summary>
        public int SpeedMode
        {
            get { return speed_mode; }
            set { speed_mode = value; _paceReset = true; }
        }

        public int MkModel
        {
            get { return mk_model; }
            set { mk_model = value; }
        }

        // ----------------------------------------------------------------- keyboard

        /// <summary>A key, by the pmk-android key code (tens = column group, units = row).</summary>
        public void keypad(int keycode)
        {
            IK1302.keyb_x = (keycode % 10) + 2;
            keycode /= 10;
            IK1302.keyb_y = keycode == 2 ? 8 : (keycode == 3 ? 9 : 1);
        }

        // ----------------------------------------------------------------- program import/export

        /// <summary>Writes a program into program memory at the next synchronisation point
        /// (the emulator must be running). codes[i] goes to address i.</summary>
        public void ImportProgram(IList<int> codes)
        {
            lock (_sync)
            {
                int n = Math.Min(codes.Count, mImportCmds.Length);
                for (int i = 0; i < n; i++) mImportCmds[i] = codes[i];
                mImportCmdCount = n;
            }
        }

        /// <summary>Reads the 105 program steps at the next synchronisation point; the callback
        /// runs on the emulator thread.</summary>
        public void RequestExport(Action<List<int>> listener)
        {
            lock (_sync) mExportListener = listener;
        }

        /// <summary>Reads the stack (X1, X, Y, Z, T) and registers 0..E at the next
        /// synchronisation point; the callback runs on the emulator thread.</summary>
        public void RequestRegisters(Action<string[]> listener)
        {
            lock (_sync) mRegsListener = listener;
        }

        void importIfNeeded()
        {
            int prgSize;
            lock (_sync)
            {
                prgSize = mImportCmdCount;
                mImportCmdCount = -1;
            }
            for (int a = 0; a < prgSize; a++)
                saveCmd(a, mImportCmds[a]);
        }

        void exportIfNeeded()
        {
            Action<List<int>> listener;
            lock (_sync) { listener = mExportListener; mExportListener = null; }
            if (listener == null) return;

            const int prgSize = 105;
            var cmds = new List<int>(prgSize);
            for (int address = 0; address < prgSize; address++)
            {
                int[] addr = cmdAddress(address, IR2_1.microtick / 84);
                int[] m = ChipMemory(addr[0]);
                cmds.Add(m[addr[1]] * 16 + m[addr[1] - 3]);
            }
            listener(cmds);
        }

        void regsIfNeeded()
        {
            Action<string[]> listener;
            lock (_sync) { listener = mRegsListener; mRegsListener = null; }
            if (listener != null) listener(regsDump());
        }

        void saveCmd(int address, int cmdCode)
        {
            int[] addr = cmdAddress(address, IR2_1.microtick / 84);
            int[] m = ChipMemory(addr[0]);
            m[addr[1]] = cmdCode / 16;
            m[addr[1] - 3] = cmdCode % 16;
        }

        int[] ChipMemory(int chipNum)
        {
            switch (chipNum)
            {
                case 1: return IR2_1.M;
                case 2: return IR2_2.M;
                case 3: return IK1302.M;
                case 4: return IK1303.M;
                default: return IK1306.M;
            }
        }

        int[] cmdAddress(int address, int page)
        {
            int addr1 = address / 7;
            int addr2 = address % 7;
            int[] p = memAddrsPages[memAddrsSwaps[page][addr1]];
            if (addr2 == 0) return p;
            return new[] { p[0], p[1] - 42 + addr2 * 6 };
        }

        // ----------------------------------------------------------------- the indicator

        /// <summary>The indicator as plain text: 12 symbols with a "." after each lit point.</summary>
        public string IndicatorString()
        {
            var b = new StringBuilder(24);
            for (int ix = 0; ix < 12; ix++)
            {
                b.Append(show_symbols[indicator[ix]]);
                if (ind_comma[ix]) b.Append('.');
            }
            return b.ToString();
        }

        void show_indicator()
        {
            displayString.Length = 0;
            for (int ix = 0; ix < 12; ix++)
            {
                displayString.Append(show_symbols[indicator[ix]]);
                displayString.Append(ind_comma[ix] ? '.' : '/');
            }
            DisplayText = displayString.ToString();
        }

        // ----------------------------------------------------------------- the clock

        void tick()
        {
            IK1302.input = IR2_2.output; IK1302.tick();
            IK1303.input = IK1302.output; IK1303.tick();

            if (mk_model == ModelMK54)
            {
                IR2_1.input = IK1303.output; IR2_1.tick();
            }
            else
            {
                IK1306.input = IK1303.output; IK1306.tick();
                IR2_1.input = IK1306.output; IR2_1.tick();
            }

            IR2_2.input = IR2_1.output; IR2_2.tick();
            IK1302.M[((IK1302.microtick >> 2) + 41) % 42] = IR2_2.output;
        }

        bool tick42()
        {
            for (int j = 0; j < 42; j++)
                tick();

            if (IR2_1.microtick == 84)
            {
                syncCounter = (syncCounter + 1) % (mk_model == ModelMK61 ? 5 : 7);
                if (IK1302.redraw_indic && syncCounter == (mk_model == ModelMK61 ? 4 : 6))
                {
                    regsIfNeeded();
                    importIfNeeded();
                    exportIfNeeded();
                    return true;
                }
            }
            return false;
        }

        /// <summary>560 x 42 ticks with the indicator refreshed after each 42 (public for the tests).</summary>
        public void step()
        {
            int i, idx;
            bool renew;
            IK1303.keyb_y = 1;
            IK1303.keyb_x = angle_mode;
            for (int ix = 0; ix < 560; ix++)
            {
                if (runningState == StateStoppingForced || runningState == StateStoppingNormal) break;
                if (speed_mode > 0) Pace();
                tick42();

                if (IK1302.redraw_indic)
                {
                    for (i = 0; i <= 8; i++) indicator[i] = IK1302.R[(8 - i) * 3];
                    for (i = 0; i <= 2; i++) indicator[i + 9] = IK1302.R[(11 - i) * 3];
                    for (i = 0; i <= 8; i++) ind_comma[i] = IK1302.ind_comma[9 - i];
                    for (i = 0; i <= 2; i++) ind_comma[i + 9] = IK1302.ind_comma[12 - i];
                    IK1302.redraw_indic = false;
                }
                else
                {
                    for (i = 0; i < 12; i++)
                    {
                        indicator[i] = 15;
                        ind_comma[i] = false;
                    }
                }
                renew = false;
                for (idx = 0; idx < 12; idx++)
                {
                    if (indicator_old[idx] != indicator[idx]) renew = true;
                    indicator_old[idx] = indicator[idx];
                    if (ind_comma_old[idx] != ind_comma[idx]) renew = true;
                    ind_comma_old[idx] = ind_comma[idx];
                }
                if (renew) show_indicator();
            }
        }

        /// <summary>Holds the thread back to RealSpeedMsPerTick42 per tick42, on average.</summary>
        void Pace()
        {
            if (_paceReset)
            {
                _paceReset = false;
                _pace.Reset();
                _pace.Start();
                _pacedTicks = 0;
            }
            _pacedTicks++;
            double dueMs = _pacedTicks * RealSpeedMsPerTick42;
            double nowMs = _pace.ElapsedTicks * 1000.0 / Stopwatch.Frequency;
            if (nowMs - dueMs > 200)
            {
                // Far behind (the app was in the background, say): do not race to catch up.
                _paceReset = true;
            }
            else if (dueMs - nowMs >= 2)
            {
                _never.Wait((int)(dueMs - nowMs));
            }
        }

        // ----------------------------------------------------------------- saved state

        static readonly byte[] Magic = Encoding.UTF8.GetBytes("MK61EMU1");

        /// <summary>Writes the whole machine. Call only while stopped.</summary>
        public void Save(Stream s)
        {
            using (var w = new BinaryWriter(s, Encoding.UTF8, true))
            {
                w.Write(Magic);
                w.Write(angle_mode);
                w.Write(speed_mode | (mk_model << 8));
                IK1302.Write(w);
                IK1303.Write(w);
                IK1306.Write(w);
                IR2_1.Write(w);
                IR2_2.Write(w);
                w.Write(syncCounter);
            }
        }

        /// <summary>Reads a machine written by Save; throws on anything else.</summary>
        public static Emulator Load(Stream s)
        {
            var e = new Emulator();
            using (var r = new BinaryReader(s, Encoding.UTF8, true))
            {
                byte[] magic = r.ReadBytes(Magic.Length);
                for (int i = 0; i < Magic.Length; i++)
                    if (magic.Length != Magic.Length || magic[i] != Magic[i])
                        throw new IOException("not an MK-61 state");
                e.angle_mode = r.ReadInt32();
                int both = r.ReadInt32();
                e.speed_mode = both & 255;
                e.mk_model = both >> 8;
                e.IK1302.Read(r);
                e.IK1303.Read(r);
                e.IK1306.Read(r);
                e.IR2_1.Read(r);
                e.IR2_2.Read(r);
                e.syncCounter = r.ReadInt32();
            }
            return e;
        }

        // ----------------------------------------------------------------- register dump

        static readonly int[] memAddrsSwaps61 = { 10, 11, 6, 7, 2, 3, 4, 5, 0, 1, 14, 13, 12, 8, 9 };
        static readonly int[] memAddrsSwaps54 = { 11, 6, 7, 2, 3, 4, 5, 0, 1, 13, 12, 8, 9, 10 };

        static readonly int[][] memAddrsSwaps = {
            new[] { 1, 2, 3, 4, 5, 14, 13, 12, 6, 7, 8, 9, 10, 11, 0 },
            new[] { 10, 11, 6, 7, 2, 3, 4, 5, 0, 1, 14, 13, 12, 8, 9 },
            new[] { 14, 13, 12, 10, 11, 6, 7, 8, 9, 4, 5, 0, 1, 2, 3 }
        };
        static readonly int[][] memAddrsPages = {
            new[] { 1, 41 }, new[] { 1, 83 }, new[] { 1, 125 }, new[] { 1, 167 }, new[] { 1, 209 }, new[] { 1, 251 },
            new[] { 2, 41 }, new[] { 2, 83 }, new[] { 2, 125 }, new[] { 2, 167 }, new[] { 2, 209 }, new[] { 2, 251 },
            new[] { 3, 41 }, new[] { 4, 41 }, new[] { 5, 41 }
        };

        static readonly int[] stackAddrsSwaps61 = { 14, 13, 12, 8, 9 };
        static readonly int[] stackAddrsSwaps54 = { 13, 12, 8, 9, 10 };
        static readonly int[][] stackAddrs = {
            new[] { 1, 34 }, new[] { 1, 76 }, new[] { 1, 118 }, new[] { 1, 160 }, new[] { 1, 202 }, new[] { 1, 244 },
            new[] { 2, 34 }, new[] { 2, 76 }, new[] { 2, 118 }, new[] { 2, 160 }, new[] { 2, 202 }, new[] { 2, 244 },
            new[] { 3, 34 }, new[] { 4, 34 }, new[] { 5, 34 }
        };

        /// <summary>X1, X, Y, Z, T, then registers 0..E ("" for E on the MK-54).</summary>
        string[] regsDump()
        {
            var regs = new string[5 + 15];
            int regsCount;
            int[] memSwaps, stackSwaps;
            if (mk_model == ModelMK61)
            {
                regsCount = 15;
                memSwaps = memAddrsSwaps61;
                stackSwaps = stackAddrsSwaps61;
            }
            else
            {
                regsCount = 14;
                memSwaps = memAddrsSwaps54;
                stackSwaps = stackAddrsSwaps54;
                regs[regs.Length - 1] = "";
            }

            int i;
            for (i = 0; i < 5; i++)
                regs[i] = readValue(stackAddrs[stackSwaps[i]][0], stackAddrs[stackSwaps[i]][1]);
            for (int j = 0; j < regsCount; j++, i++)
                regs[i] = readValue(memAddrsPages[memSwaps[j]][0], memAddrsPages[memSwaps[j]][1] - 8);
            return regs;
        }

        string readValue(int chipNum, int address)
        {
            int[] memory = ChipMemory(chipNum);

            int exp = memory[address - 3] * 10 + memory[address - 6];
            if (memory[address] == 9) exp = -(100 - exp);
            int idx = 0;
            while (memory[address - 33 + idx * 3] == 0)
            {
                if (exp == 7 - idx || idx == 7) break;
                idx++;
            }

            var digits = new List<int>();
            while (idx < 8)
            {
                digits.Add(memory[address - 33 + idx * 3]);
                idx++;
            }
            digits.Reverse();

            var mantissa = new StringBuilder(memory[address - 9] == 9 ? "-" : " ");
            bool comma = false;
            for (int i = 0; i < digits.Count; i++)
            {
                mantissa.Append(show_symbols[digits[i]]);
                if ((i == 0 && (exp < 0 || exp > 7)) || i == exp)
                {
                    mantissa.Append('.');
                    comma = true;
                }
            }
            if (!comma) mantissa.Append('.');

            if (exp < 0 || exp > 7)
            {
                int size = (exp < 0 ? 10 : 11) - mantissa.Length;
                for (int i = 0; i < size; i++) mantissa.Append(' ');
                mantissa.Append(exp);
            }
            return mantissa.ToString();
        }
    }
}
