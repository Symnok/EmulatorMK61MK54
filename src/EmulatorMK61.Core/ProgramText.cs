// EmulatorMK61 - emulator of the Soviet programmable calculators "Elektronika MK-61" and "MK-54".
// GPL-3.0, see LICENSE.
//
// Program listings as text, the format pmk-android imports and exports (CommandParser.java and
// SaveStateManager.importTxtProgram/exportCmdsTxt): "00.В↑", "01.Fsin", ... or bare mnemonics.
// Addresses written in the text are skipped, not used, as in the original.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EmulatorMK61.Core
{
    public sealed class ProgramParseException : Exception
    {
        public ProgramParseException(string command) : base("cannot read the command " + command) { Command = command; }
        public string Command { get; private set; }
    }

    public static class ProgramText
    {
        // codes of commands
        static readonly int[] mCodes = {
            0x15, 0x54, 0x1F, 0x2F, 0x56, 0x16, 0x17, 0x18, 0x30, 0x19, 0x31, 0x1A, 0x32, 0x1B, 0x33,
            0x1C, 0x34, 0x1D, 0x35, 0x1E, 0x36, 0x10, 0x11, 0x12, 0x13, 0x20, 0x26, 0x21, 0x22, 0x23,
            0x14, 0x0E, 0x3D, 0x3C, 0x3E, 0x3F, 0x24, 0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E,
            0x2F, 0x0F, 0x3B, 0x0A, 0x0B, 0x0C, 0x0D, 0x25, 0x37, 0x38, 0x39, 0x3A, 0x52, 0x50, 0x59,
            0x57, 0x51, 0x53, 0x58, 0x5A, 0x5C, 0x5E, 0x5D, 0x5B, 0x5F, 0x40, 0x4F, 0x60, 0x6F, 0x70,
            0x7F, 0x80, 0x8F, 0x90, 0x9F, 0xA0, 0xAF, 0xB0, 0xBF, 0xC0, 0xCF, 0xD0, 0xDF, 0xE0, 0xEF,
            0xF0, 0xFF,
        };

        // mnemonics of codes; the first is the one written on export
        static readonly string[][] mMnemonics = {
            new[] {"F10ˣ", "10ˣ", "F10^X", "10X", "F10X", "10**X"},
            new[] {"KНОП", "KНOП", "НOП", "K0"},
            new[] {"1F"},
            new[] {"2F"},
            new[] {"K2"},
            new[] {"Feˣ", "FEˣ", "Eˣ", "FE^X", "E^X", "EX", "FEX", "FE**X", "E**X"},
            new[] {"Flg", "FLG", "LG"},
            new[] {"Fln", "FLN", "LN"},
            new[] {"KЧ→М", "KЧ→M", "KЧM", "ЧM", "K3"},
            new[] {"Fsinᐨ¹", "FSINᐨ¹", "SINᐨ¹", "FSIN-1", "FARCSIN", "ARCSIN", "SIN-1"},
            new[] {"K|x|", "K|X|", "|X|", "K4"},
            new[] {"Fcosᐨ¹", "FCOSᐨ¹", "COSᐨ¹", "FCOS-1", "FARCCOS", "ARCCOS", "COS-1"},
            new[] {"Kзн", "KЗН", "ЗН", "K5"},
            new[] {"Ftgᐨ¹", "FTGᐨ¹", "TGᐨ¹", "FTG-1", "FARCTG", "ARCTG", "TG-1"},
            new[] {"KГ→М", "KГ→M", "Г→M", "KГM", "ГM", "K6"},
            new[] {"Fsin", "FSIN", "SIN"},
            new[] {"K[x]", "K[X]", "[X]", "K7"},
            new[] {"Fcos", "FCOS", "COS"},
            new[] {"K{x}", "K{X}", "{X}", "(X)", "K(X)", "K8"},
            new[] {"Ftg", "FTG", "TG"},
            new[] {"Kmax", "KMAX", "MAX", "K9"},
            new[] {"+"},
            new[] {"-", "–", "–"},
            new[] {"×", "⋅", "X", "*"},
            new[] {"÷", "/", ":"},
            new[] {"Fπ", "FΠ", "ПИ", "Π", "FПИ"},
            new[] {"KМ→Г", "KM→Г", "M→Г", "KMГ", "MГ", "K+"},
            new[] {"F√", "√", "KBKOР", "KOРEНЬ", "KBKOРEНЬ", "FKBKOР", "FKOРEНЬ", "FKBKOРEНЬ"},
            new[] {"Fx²", "FX²", "X^2", "X2", "X²", "FX^2", "FX2"},
            new[] {"F1/x", "F1/X", "1/X"},
            new[] {"<->", "↔", "XY", "X↔Y", "⟷"},
            new[] {"В↑", "B↑", "^", "B^", "↑"},
            new[] {"3D"},
            new[] {"3C"},
            new[] {"x←y", "X←Y"},
            new[] {"3F"},
            new[] {"Fxʸ", "FXʸ", "Xʸ", "FX^Y", "X^Y", "XY", "FXY"},
            new[] {"K-", "K–", "K–"},
            new[] {"K×", "KX", "K*"},
            new[] {"K÷", "K/", "K:"},
            new[] {"KМ→Ч", "KM→Ч", "M→Ч", "КM→Ч"},
            new[] {"2B"},
            new[] {"2C"},
            new[] {"2D"},
            new[] {"2E"},
            new[] {"2F"},
            new[] {"FВх", "FBX", "ВX"},
            new[] {"KCЧ", "KCЧ", "CЧ", "KE"},
            new[] {".", ",", "•"},
            new[] {"/-/", "+/-", "/–/", "/–/"},
            new[] {"ВП", "BП"},
            new[] {"Сx", "CX"},
            new[] {"Fѻ", "Fo", "FO", "FѺ", "Ѻ", "F↻", "->", "↻", "→", "F->", "F→"},
            new[] {"KɅ", "КɅ", "K^", "K⋀", "K∧", "∧", "K/\\", "/\\", "⋀", "K.", "KA", "KΛ"},
            new[] {"KV", "Kv", "K⋁", "K∨", "∨", "K\\/", "\\/", "⋁", "K/-/", "KB"},
            new[] {"K(+)", "K⊕", "(+)", "⊕", "KBП", "KC"},
            new[] {"Kинв", "KИНB", "ИНB", "KCX", "KD"},
            new[] {"В/О", "B/0", "B/O"},
            new[] {"С/П", "C/П"},
            new[] {"Fx≥0", "FX≥0", "X>=0", "X≥0", "X⩾0", "Fx≥O", "FX≥O", "X>=O", "X≥O", "X⩾O"},
            new[] {"Fx≠0", "FX≠0", "X#0", "X!=0", "X<>0", "FX#0", "FX!=0", "FX<>0",
                   "FX≠O", "X#O", "X!=O", "X<>O", "FX#O", "FX!=O", "FX<>O"},
            new[] {"БП"},
            new[] {"ПП"},
            new[] {"FL2", "L2"},
            new[] {"FL3", "L3"},
            new[] {"Fx<0", "FX<0", "X<0", "FX<O", "X<O"},
            new[] {"Fx=0", "FX=0", "X=0", "FX=O", "X=O"},
            new[] {"FL0", "L0", "FLO", "LO"},
            new[] {"FL1", "L1"},
            new[] {"5F"},
            new[] {"xП", "XП", "X→П", "XП", "П"},
            new[] {"4F"},
            new[] {"Пx", "ПX", "П→X", "ИП"},
            new[] {"6F"},
            new[] {"Kx≠0", "KX≠0", "KX#0", "KX!=0", "KX<>0", "KX≠O", "KX#O", "KX!=O", "KX<>O"},
            new[] {"7F"},
            new[] {"KБП"},
            new[] {"8F"},
            new[] {"Kx≥0", "KX≥0", "KX⩾0", "KX>=0", "Kx≥O", "KX≥O", "KX⩾O", "KX>=O"},
            new[] {"9F"},
            new[] {"KПП"},
            new[] {"AF"},
            new[] {"KxП", "KXП", "KX→П", "KП", "КП"},
            new[] {"BF"},
            new[] {"Kx<0", "KX<0", "KX<O"},
            new[] {"CF"},
            new[] {"KПx", "KПX", "KП→x", "KП→X", "KИП"},
            new[] {"DF"},
            new[] {"Kx=0", "KX=0", "KX=O"},
            new[] {"EF"},
            new[] {"F"},
            new[] {"FF"},
        };

        // 0 - ordinary command, 1 - with a register, 2 - followed by an address
        static readonly int[] mTypes = {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 1, 0, 1, 0, 1,
            0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0,
            1, 0,
        };

        /// <summary>The listing pmk-android writes: "00.В↑\t01.Fsin\t..." ten to a line.</summary>
        public static string Export(IList<int> cmds)
        {
            var sb = new StringBuilder();
            bool isAddress = false;
            for (int cmdNum = 0; cmdNum < cmds.Count; cmdNum++)
            {
                string mnemonic = Mnemonic(cmds[cmdNum], ref isAddress);
                if (cmdNum < 10) sb.Append('0');
                if (cmdNum < 100) sb.Append(cmdNum.ToString(CultureInfo.InvariantCulture));
                else sb.Append("A0");
                sb.Append('.');
                sb.Append(mnemonic);
                sb.Append('\t');
                if ((cmdNum + 1) % 10 == 0) sb.Append('\n');
            }
            return sb.ToString();
        }

        static string Mnemonic(int cmd, ref bool isAddress)
        {
            if (isAddress)
            {
                isAddress = false;
                return (cmd < 10 ? "0" : "") + cmd.ToString("X");
            }
            for (int i = 0; i < mCodes.Length; i++)
            {
                if (mTypes[i] == 1)
                {
                    int regNum = cmd % 0x10;
                    if (regNum < 0xF && cmd / 0x10 == mCodes[i] / 0x10)
                        return mMnemonics[i][0] + regNum.ToString("X");
                }
                else if (mCodes[i] == cmd)
                {
                    isAddress = mTypes[i] == 2;
                    return mMnemonics[i][0];
                }
            }
            return cmd.ToString("X");
        }

        /// <summary>Reads a listing into program codes, address 0 first.</summary>
        public static List<int> Import(string text)
        {
            var sb = new StringBuilder();
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                // skip lines like '#	|	00	01	02	03	04	05	06	07	08	09'
                if (!line.StartsWith("#", StringComparison.Ordinal))
                {
                    sb.Append(line);
                    sb.Append(' ');
                }
            }

            // '00	|	В/О' -> '00.В/О', and Russian letters to the look-alike English ones
            string prg = Regex.Replace(sb.ToString().ToUpperInvariant(), @"\s+\|\s+", ".")
                .Replace("А", "A").Replace("В", "B").Replace("С", "C").Replace("Д", "D")
                .Replace("Е", "E").Replace("К", "K").Replace("М", "M").Replace("О", "O")
                .Replace("Х", "X");

            var codes = new List<int>();
            int addr = 0;
            foreach (string cmd in Regex.Split(prg, @"\s+"))
            {
                if (cmd.Length == 0) continue;
                codes.Add(ParseCommand(addr, cmd));
                addr++;
            }
            return codes;
        }

        static int ParseCommand(int address, string cmd)
        {
            string body = cmd.Length > 2 && cmd[2] == '.' ? cmd.Substring(3) : cmd;
            try
            {
                if (body.Length > 0)
                {
                    // a command without its last char: that char can be a register
                    string body2 = body.Substring(0, body.Length - 1);
                    for (int i = 0; i < mMnemonics.Length; i++)
                    {
                        bool withReg = mTypes[i] == 1;
                        if (Array.IndexOf(mMnemonics[i], withReg ? body2 : body) >= 0)
                        {
                            int code = mCodes[i];
                            if (withReg) code += int.Parse(body.Substring(body.Length - 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                            return code;
                        }
                    }
                }
                // the last attempt: just a code 0-9 or an address like 45 or A2
                return int.Parse(body.Replace('-', 'A').Replace('.', 'A'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                throw new ProgramParseException(body.Contains(".") ? body : string.Format(CultureInfo.InvariantCulture, "{0}.{1}", address, body));
            }
            catch (OverflowException)
            {
                throw new ProgramParseException(string.Format(CultureInfo.InvariantCulture, "{0}.{1}", address, body));
            }
        }
    }
}
