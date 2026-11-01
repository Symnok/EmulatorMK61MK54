// Runs tests\scripts.txt through the ORIGINAL pmk-android engine and prints the indicator after
// each key, the reference the C# port is compared with. See run.cmd.
package com.cax.pmk.emulator;

import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.*;

public class RefRun {
    static final Map<String, Integer> KEYS = new HashMap<>();
    static {
        for (int d = 0; d <= 9; d++) KEYS.put(Integer.toString(d), 10 + d);
        String[] names = { ".", "CHS", "EE", "CX", "UP", "XY", "+", "-", "*", "/", "F", "K",
                "RCL", "STO", "GTO", "GSB", "SST", "BST", "RTN", "RS" };
        int[] codes = { 25, 26, 27, 28, 29, 24, 20, 21, 22, 23, 39, 38, 36, 34, 31, 33, 35, 37, 32, 30 };
        for (int i = 0; i < names.length; i++) KEYS.put(names[i], codes[i]);
    }

    public static void main(String[] args) throws Exception {
        BufferedReader in = new BufferedReader(new InputStreamReader(new FileInputStream(args[0]), StandardCharsets.UTF_8));
        PrintStream out = new PrintStream(System.out, true, "UTF-8");
        String line;
        while ((line = in.readLine()) != null) {
            line = line.trim();
            if (line.isEmpty() || line.startsWith("#")) continue;
            String[] t = line.split("\\s+");
            Emulator e = new Emulator();
            e.initTransient(new IndicatorInterface() {
                public int registerXIndex() { return 0; }
                public int registerYIndex() { return 1; }
                public void displayIndicator(int r, String s) { }
                public boolean isYIndicatorVisible() { return false; }
            });
            e.setMkModel(t[0].equals("54") ? Emulator.modelMK54 : Emulator.modelMK61);
            e.setAngleMode(t[1].equals("R") ? 0 : t[1].equals("G") ? 1 : 2);
            StringBuilder sb = new StringBuilder(line).append('\n');
            for (int i = 2; i < t.length; i++) {
                if (t[i].startsWith("w")) {
                    int n = Integer.parseInt(t[i].substring(1));
                    for (int k = 0; k < n; k++) e.step();
                } else {
                    e.keypad(KEYS.get(t[i]));
                    e.step();
                    e.step();
                }
                sb.append("  ").append(t[i]).append(" [").append(e.indicatorString()).append("]\n");
            }
            out.print(sb);
        }
    }
}
