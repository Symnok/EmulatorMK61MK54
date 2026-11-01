# МК-61/54 for Windows Phone 8.1 / Windows 10 Mobile

An emulator of the Soviet programmable calculators "Электроника МК-61" and "МК-54" that works
at the microcode level. It is a WinRT (Windows Phone 8.1 XAML) port of the Android app
"Extended emulator of МК 61/54" by Vadim Khohlov. That emulation engine comes from Felix
Lazarev's emu145. Licence: GNU GPL v3 (see LICENSE).

## Credits

- **Original Android app:** "Extended emulator of МК 61/54"
  - [Google Play](https://play.google.com/store/apps/details?id=com.cax.pmk.ext)
  - source code: [GitHub](https://github.com/xvadim/pmk-android)
- **This port:** written with [Claude](https://claude.ai) (Anthropic's AI assistant, using Claude Code).

## What is ported

- **The engine.** `src/EmulatorMK61.Core` is a rule-for-rule C# copy of `emulator/*.java`. The
  ROM tables are generated from the Java files by `tools/rom2cs.py`. The self test checks the
  port against the original Java engine (see below).
- **The calculator face.** The keys, codes, labels and colours are those of pmk-android, and so
  are its fonts: Digital-7 for the indicator and missing-symbols for ШГ, П→X, X→П, ↔, В↑, √, eˣ,
  xʸ, 10ˣ and ○. The MK-54 look has no blue labels, "Cx", and "НОП" under the 0 key.
- **Switches and controls.**
  - Power, and the Р / ГРД / Г angle switch.
  - Tapping the indicator switches between real and fast speed. Real speed is the default.
  - Holding the model name (while the calculator is off) switches between MK-61 and MK-54.
- **State.** The whole machine is saved when the app is suspended or closed and restored at the
  next start, as pmk-android does with persist.pmk.
- **Menu ("…").**
  - Speed, model, and vibration.
  - The register dump (X1, X, Y, Z, T, R0..Re).
  - Program import/export as a text listing, in pmk-android's format, MK-61 only, as on Android.
  - The manual and the undocumented-features page, from pmk-android's assets.

Not ported:
- Android `.pmk` state files. These are Java serialisation; the app's own state format is in
  `Emulator.Save`.
- Key sounds. WP8.1 cannot play OGG.
- The Y indicator, "delete last digit", and the skin options.

## Build

```
build.bat [ARM|x86|both] [Debug|Release] [nobump]
```

This needs MSBuild 14 (VS2015) with the Windows Phone 8.1 tools. It runs the core self test
first. It bumps the version to 1.0.<n>.0 on every build, because the phone does not replace an
installed app with a package of the same version. It also makes the CN=EmulatorMK61 signing
certificate with `make-cert.ps1` if it is missing. The packages are written to
`src\EmulatorMK61.WP81\AppPackages\`. Install the `.cer` on the phone once, then the `.appx`.

## Tests

`tests\run-tests.cmd` runs the self test on the PC (.NET 4.5, the same core sources). It checks:
- `tests\scripts.txt` (key sequences: arithmetic, functions, errors, programs, MK-54) must give,
  key for key, the indicator in `tests\EmulatorMK61.Tests\fixtures\expected.txt`. That file was
  produced by the **original Java engine** with `tests\java-reference\run.cmd <pmk-android checkout>`.
- Saving the state halfway through a script and loading it must continue identically.
- The program listing round-trips, and so does import into and export out of the machine.
- The register dump.

If you change `tests\scripts.txt`, regenerate `expected.txt` with the Java reference.
