// EmulatorMK61 - emulator of the Soviet programmable calculators "Elektronika MK-61" and "MK-54"
// for Windows Phone 8.1 / Windows 10 Mobile. A port of pmk-android (github.com/xvadim/pmk-android),
// whose engine follows Felix Lazarev's emu145. GPL-3.0, see LICENSE.
//
// One K745IK13 microcontroller (IK1302, IK1303 or IK1306): a rule-for-rule copy of MCU.java.
// Every value here is non-negative, so Java's >>> is plain >> in C#.
using System.IO;

namespace EmulatorMK61.Core
{
    public sealed class Mcu
    {
        public Mcu()
        {
            R = new int[ARRAY_SIZE];
            M = new int[ARRAY_SIZE];
            ST = new int[ARRAY_SIZE];
            ind_comma = new bool[IND_COMMA_SIZE];
        }

        public void tick()
        {
            int tick_0123 = microtick & 3;
            int chetv_1248 = 1 << tick_0123;
            int signal_I = microtick >> 2;
            int signal_D = microtick / 12;
            bool keyb_processed = false;

            if (microtick == 0)
            {
                AK = R[36] + 16 * R[39];
                if ((cmd_rom[AK] & 0xfc0000) == 0) T = 0;
            }

            if (chetv_1248 == 1)
            {
                int k = microtick / 36;
                if (k < 3) ASP = 0xff & cmd_rom[AK];
                else if (k == 3) ASP = 0xff & cmd_rom[AK] >> 8;
                else if (k == 4)
                {
                    ASP = 0xff & cmd_rom[AK] >> 16;
                    if (ASP > 0x1f)
                    {
                        if (microtick == 144)
                        {
                            R[37] = ASP & 0xf;
                            R[40] = ASP >> 4;
                        }
                        ASP = 0x5f;
                    }
                }
                MOD = 0xff & cmd_rom[AK] >> 24;
                AMK = synchro_rom[ASP * 9 + J[microtick >> 2]];
                AMK = AMK & 0x3f;
                if (AMK > 59)
                {
                    AMK = (AMK - 60) * 2;
                    if (L == 0) AMK++;
                    AMK += 60;
                }
                mcmd = ucmd_rom[AMK];
            }

            int alpha = 0, beta = 0, gamma = 0;

            switch (mcmd >> 24 & 3)
            {
                case 2:
                case 3:
                    if (microtick / 12 != keyb_x - 1)
                        if (keyb_y > 0)
                        {
                            if (chetv_1248 == 1) S1 |= keyb_y;
                            keyb_processed = true;
                        }
                    break;
            }

            if ((mcmd & 1) > 0) alpha |= R[signal_I];
            if ((mcmd & 2) > 0) alpha |= M[signal_I];
            if ((mcmd & 4) > 0) alpha |= ST[signal_I];
            if ((mcmd & 8) > 0) alpha |= ~R[signal_I] & 0xf;
            if ((mcmd & 16) > 0) if (L == 0) alpha |= 0xa;
            if ((mcmd & 32) > 0) alpha |= S;
            if ((mcmd & 64) > 0) alpha |= 4;
            if ((mcmd >> 7 & 16) > 0) beta |= 1;
            if ((mcmd >> 7 & 8) > 0) beta |= 6;
            if ((mcmd >> 7 & 4) > 0) beta |= S1;
            if ((mcmd >> 7 & 2) > 0) beta |= ~S & 0xf;
            if ((mcmd >> 7 & 1) > 0) beta |= S;
            if ((cmd_rom[AK] & 0xfc0000) > 0)
            {
                if (keyb_y == 0) T = 0;
            }
            else
            {
                redraw_indic = true;
                if (microtick / 12 == keyb_x - 1)
                    if (keyb_y > 0)
                    {
                        S1 = keyb_y;
                        T = 1;
                        keyb_processed = true;
                    }
                if (tick_0123 == 0)
                    if (signal_D >= 0 && signal_D < 12)
                        if (L > 0) comma = signal_D;
                ind_comma[signal_D] = L > 0;
            }
            if ((mcmd >> 12 & 4) > 0) gamma = ~T & 1;
            if ((mcmd >> 12 & 2) > 0) gamma |= ~L & 1;
            if ((mcmd >> 12 & 1) > 0) gamma |= L & 1;

            int sum = alpha + beta + gamma;
            int sigma = sum & 0xf;
            P = sum >> 4;

            if (MOD == 0 || (microtick >> 2) >= 36)
            {
                switch (mcmd >> 15 & 7)
                {
                    case 1: R[signal_I] = R[(signal_I + 3) % ARRAY_SIZE]; break;
                    case 2: R[signal_I] = sigma; break;
                    case 3: R[signal_I] = S; break;
                    case 4: R[signal_I] = R[signal_I] | S | sigma; break;
                    case 5: R[signal_I] = S | sigma; break;
                    case 6: R[signal_I] = R[signal_I] | S; break;
                    case 7: R[signal_I] = R[signal_I] | sigma; break;
                }
                if ((mcmd >> 18 & 1) > 0) R[(signal_I + 41) % ARRAY_SIZE] = sigma;
                if ((mcmd >> 19 & 1) > 0) R[(signal_I + 40) % ARRAY_SIZE] = sigma;
            }
            if ((mcmd >> 21 & 1) > 0) L = 1 & P;
            if ((mcmd >> 20 & 1) > 0) M[signal_I] = S;

            switch (mcmd >> 22 & 3)
            {
                case 1: S = S1; break;
                case 2: S = sigma; break;
                case 3: S = S1 | sigma; break;
            }

            switch (mcmd >> 24 & 3)
            {
                case 1: S1 = sigma; break;
                case 3: S1 = S1 | sigma; break;
            }

            int x, y, z;
            switch (mcmd >> 26 & 3)
            {
                case 1:
                    ST[(signal_I + 2) % ARRAY_SIZE] = ST[(signal_I + 1) % ARRAY_SIZE];
                    ST[(signal_I + 1) % ARRAY_SIZE] = ST[signal_I];
                    ST[signal_I] = sigma;
                    break;
                case 2:
                    x = ST[signal_I];
                    ST[signal_I] = ST[(signal_I + 1) % ARRAY_SIZE];
                    ST[(signal_I + 1) % ARRAY_SIZE] = ST[(signal_I + 2) % ARRAY_SIZE];
                    ST[(signal_I + 2) % ARRAY_SIZE] = x;
                    break;
                case 3:
                    x = ST[signal_I];
                    y = ST[(signal_I + 1) % ARRAY_SIZE];
                    z = ST[(signal_I + 2) % ARRAY_SIZE];
                    ST[(signal_I + 0) % ARRAY_SIZE] = sigma | y;
                    ST[(signal_I + 1) % ARRAY_SIZE] = x | z;
                    ST[(signal_I + 2) % ARRAY_SIZE] = y | x;
                    break;
            }

            output = 0xf & M[signal_I];
            M[signal_I] = input;
            microtick += 4;
            if (microtick > 167) microtick = 0;

            if (keyb_processed && ik130x != 3) keyb_x = keyb_y = 0;
        }

        // Not saved: set up by the emulator.
        internal int[] ucmd_rom;
        internal int[] cmd_rom;
        internal int[] synchro_rom;
        internal int ik130x;

        public int[] R;
        public int[] M;
        public int[] ST;
        public int S, S1, L, T, P, microtick, mcmd, keyb_x, keyb_y, comma, input, output, AMK, ASP, AK, MOD;
        public bool[] ind_comma;
        public bool redraw_indic;

        static readonly int[] J = {
            0, 1, 2, 3, 4, 5,
            3, 4, 5, 3, 4, 5,
            3, 4, 5, 3, 4, 5,
            3, 4, 5, 3, 4, 5,
            6, 7, 8, 0, 1, 2,
            3, 4, 5, 6, 7, 8,
            0, 1, 2, 3, 4, 5
        };

        const int ARRAY_SIZE = 42;
        const int IND_COMMA_SIZE = 14;

        // The same fields in the same order as MCU.writeExternal.
        internal void Write(BinaryWriter w)
        {
            for (int i = 0; i < ARRAY_SIZE; i++) w.Write(R[i]);
            for (int i = 0; i < ARRAY_SIZE; i++) w.Write(M[i]);
            for (int i = 0; i < ARRAY_SIZE; i++) w.Write(ST[i]);
            for (int i = 0; i < IND_COMMA_SIZE; i++) w.Write(ind_comma[i]);
            w.Write(redraw_indic);
            foreach (int v in new[] { S, S1, L, T, P, microtick, mcmd, keyb_x, keyb_y, comma, input, output, AMK, ASP, AK, MOD })
                w.Write(v);
        }

        internal void Read(BinaryReader r)
        {
            for (int i = 0; i < ARRAY_SIZE; i++) R[i] = r.ReadInt32();
            for (int i = 0; i < ARRAY_SIZE; i++) M[i] = r.ReadInt32();
            for (int i = 0; i < ARRAY_SIZE; i++) ST[i] = r.ReadInt32();
            for (int i = 0; i < IND_COMMA_SIZE; i++) ind_comma[i] = r.ReadBoolean();
            redraw_indic = r.ReadBoolean();
            S = r.ReadInt32(); S1 = r.ReadInt32(); L = r.ReadInt32(); T = r.ReadInt32(); P = r.ReadInt32();
            microtick = r.ReadInt32(); mcmd = r.ReadInt32(); keyb_x = r.ReadInt32(); keyb_y = r.ReadInt32();
            comma = r.ReadInt32(); input = r.ReadInt32(); output = r.ReadInt32();
            AMK = r.ReadInt32(); ASP = r.ReadInt32(); AK = r.ReadInt32(); MOD = r.ReadInt32();
        }
    }

    /// <summary>A K145IR2 shift-register memory chip (Memory.java).</summary>
    public sealed class Memory
    {
        public void tick()
        {
            if (microtick == MEM_SIZE) microtick = 0;
            output = M[microtick];
            M[microtick] = input;
            microtick++;
        }

        public int input;
        public int output;
        public int microtick;
        public int[] M = new int[MEM_SIZE];

        const int MEM_SIZE = 252;

        internal void Write(BinaryWriter w)
        {
            w.Write(input); w.Write(output); w.Write(microtick);
            for (int i = 0; i < MEM_SIZE; i++) w.Write(M[i]);
        }

        internal void Read(BinaryReader r)
        {
            input = r.ReadInt32(); output = r.ReadInt32(); microtick = r.ReadInt32();
            for (int i = 0; i < MEM_SIZE; i++) M[i] = r.ReadInt32();
        }
    }
}
