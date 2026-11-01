// EmulatorMK61 core self test on the PC: the same source files as the phone app.
//  1. tests\scripts.txt through the C# engine must give exactly fixtures\expected.txt, which the
//     ORIGINAL Java engine produced (tests\java-reference\run.cmd);
//  2. a state saved halfway and loaded again must continue exactly as an uninterrupted run;
//  3. program listings: import -> machine -> export round trip.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using EmulatorMK61.Core;

static class Program
{
    static int _failed;

    static readonly Dictionary<string, int> Keys = new Dictionary<string, int>();

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        for (int d = 0; d <= 9; d++) Keys[d.ToString()] = 10 + d;
        string[] names = { ".", "CHS", "EE", "CX", "UP", "XY", "+", "-", "*", "/", "F", "K",
                           "RCL", "STO", "GTO", "GSB", "SST", "BST", "RTN", "RS" };
        int[] codes = { 25, 26, 27, 28, 29, 24, 20, 21, 22, 23, 39, 38, 36, 34, 31, 33, 35, 37, 32, 30 };
        for (int i = 0; i < names.Length; i++) Keys[names[i]] = codes[i];

        string dir = AppDomain.CurrentDomain.BaseDirectory;
        string root = Path.GetFullPath(Path.Combine(dir, "..", ".."));
        string scripts = File.ReadAllText(Path.Combine(root, "scripts.txt"), Encoding.UTF8);
        string expected = File.ReadAllText(Path.Combine(root, "EmulatorMK61.Tests", "fixtures", "expected.txt"), Encoding.UTF8);

        // 1. Against the Java engine.
        var sb = new StringBuilder();
        foreach (string line in Lines(scripts))
            sb.Append(RunScript(line, -1));
        string got = sb.ToString();
        Check("scripts match the Java engine", Norm(got) == Norm(expected), FirstDifference(Norm(got), Norm(expected)));

        // 2. Save halfway, load, continue.
        foreach (string line in Lines(scripts))
        {
            int keys = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length - 2;
            for (int cut = 1; cut < keys; cut += 3)
            {
                string a = RunScript(line, -1), b = RunScript(line, cut);
                if (a != b) { Check("save/load at key " + cut + " of: " + line, false, FirstDifference(b, a)); goto saved; }
            }
        }
        Check("save/load mid-script continues identically", true, null);
        saved:

        // 3. Program listings.
        var prog = new List<int> { 0x0E, 0x12, 0x50, 0x40 + 3, 0x51, 0x07, 0x60 + 0xE, 0x6F, 0x2A, 0x54, 0x5E, 0x12, 0x10 };
        string text = ProgramText.Export(prog);
        List<int> back = ProgramText.Import(text);
        Check("listing round trip", back.SequenceEqual(prog), text);
        var lunolet = ProgramText.Import("00.Пx4\t01.KНОП\t02.xП0 03.В/О\n# comment\n04.БП 05.A2 06.Fsin");
        Check("listing parse", lunolet.SequenceEqual(new[] { 0x64, 0x54, 0x40, 0x52, 0x51, 0xA2, 0x1C }),
            string.Join(",", lunolet.Select(x => x.ToString("X2"))));
        try { ProgramText.Import("00.ЖЖЖ"); Check("bad listing rejected", false, null); }
        catch (ProgramParseException e) { Check("bad listing rejected", e.Command == "ЖЖЖ" || e.Command.EndsWith("ЖЖЖ"), e.Command); }

        var emu = PowerOn(Emulator.ModelMK61, 0);
        emu.ImportProgram(prog);
        for (int i = 0; i < 3; i++) emu.step();
        List<int> exported = null;
        emu.RequestExport(c => exported = c);
        for (int i = 0; i < 3 && exported == null; i++) emu.step();
        Check("import into the machine and export back",
            exported != null && exported.Take(prog.Count).SequenceEqual(prog) && exported.Count == 105,
            exported == null ? "no export" : string.Join(",", exported.Take(16).Select(x => x.ToString("X2"))));

        // Registers: 5 STO 3, then the dump.
        emu = PowerOn(Emulator.ModelMK61, 0);
        for (int i = 0; i < 3; i++) emu.step(); // a key pressed before the machine is up is lost
        foreach (string k in "1 STO 0 3 STO 3 1 4 STO CX 1 5 STO UP 7 UP 2".Split(' ')) Key(emu, k);
        string[] regs = null;
        emu.RequestRegisters(r => regs = r);
        for (int i = 0; i < 3 && regs == null; i++) emu.step();
        Check("register dump", regs != null && regs[1].Trim() == "2." && regs[2].Trim() == "7." &&
            regs[5].Trim() == "1." && regs[5 + 3].Trim() == "3." && regs[5 + 13].Trim() == "14." && regs[5 + 14].Trim() == "15.",
            regs == null ? "none" : string.Join("|", regs));

        // Speed, for information.
        emu = PowerOn(Emulator.ModelMK61, 0);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 200; i++) emu.step();
        Console.WriteLine("speed: {0:F1} ms per step on this PC (real calculator ~ {1:F0} ms)",
            sw.Elapsed.TotalMilliseconds / 200, 560 * Emulator.RealSpeedMsPerTick42);

        Console.WriteLine(_failed == 0 ? "ALL PASSED" : _failed + " FAILED");
        return _failed == 0 ? 0 : 1;
    }

    static IEnumerable<string> Lines(string scripts)
    {
        return scripts.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#"));
    }

    static Emulator PowerOn(int model, int angle)
    {
        var e = new Emulator();
        e.MkModel = model;
        e.AngleMode = angle;
        return e;
    }

    static void Key(Emulator e, string k)
    {
        e.keypad(Keys[k]);
        e.step();
        e.step();
    }

    /// <summary>The Java harness's output for one line; with cut >= 0 the machine is saved and
    /// reloaded before key number cut.</summary>
    static string RunScript(string line, int cut)
    {
        string[] t = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var e = PowerOn(t[0] == "54" ? Emulator.ModelMK54 : Emulator.ModelMK61, t[1] == "R" ? 0 : t[1] == "G" ? 1 : 2);
        var sb = new StringBuilder(line).Append('\n');
        for (int i = 2; i < t.Length; i++)
        {
            if (i - 2 == cut)
            {
                var ms = new MemoryStream();
                e.Save(ms);
                ms.Position = 0;
                var loaded = Emulator.Load(ms);
                loaded.AngleMode = e.AngleMode;
                e = loaded;
            }
            if (t[i].StartsWith("w"))
            {
                int n = int.Parse(t[i].Substring(1));
                for (int k = 0; k < n; k++) e.step();
            }
            else Key(e, t[i]);
            sb.Append("  ").Append(t[i]).Append(" [").Append(e.IndicatorString()).Append("]\n");
        }
        return sb.ToString();
    }

    static string Norm(string s) { return s.Replace("\r\n", "\n").TrimEnd(); }

    static string FirstDifference(string a, string b)
    {
        string[] x = a.Split('\n'), y = b.Split('\n');
        for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            string p = i < x.Length ? x[i] : "<end>", q = i < y.Length ? y[i] : "<end>";
            if (p != q) return string.Format("line {0}: got '{1}', want '{2}'", i + 1, p, q);
        }
        return null;
    }

    static void Check(string what, bool ok, string detail)
    {
        Console.WriteLine("{0} {1}", ok ? "ok  " : "FAIL", what);
        if (!ok)
        {
            _failed++;
            if (detail != null) Console.WriteLine("     " + detail);
        }
    }
}
