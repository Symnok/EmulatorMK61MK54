@echo off
rem Builds the ORIGINAL pmk-android engine with RefRun.java and regenerates the reference output
rem the C# self test compares with (tests\EmulatorMK61.Tests\fixtures\expected.txt).
rem   tests\java-reference\run.cmd <path to a pmk-android checkout> [path to a JDK]
rem Only needed when tests\scripts.txt changes.
setlocal
set "PMK=%~1"
if "%PMK%"=="" echo usage: run.cmd ^<pmk-android checkout^> [JDK] & exit /b 1
set "JDK=%~2"
if "%JDK%"=="" set "JDK=C:\Program Files\Android\openjdk\jdk-21.0.8"
set "SRC=%PMK%\pmk\app\src\main\java\com\cax\pmk"
set "OUT=%~dp0out"
if exist "%OUT%" rd /s /q "%OUT%"
mkdir "%OUT%"
"%JDK%\bin\javac" -encoding UTF-8 -nowarn -d "%OUT%" "%SRC%\EmulatorInterface.java" "%SRC%\emulator\Emulator.java" "%SRC%\emulator\IndicatorInterface.java" "%SRC%\emulator\MCU.java" "%SRC%\emulator\Memory.java" "%SRC%\emulator\MCommands.java" "%SRC%\emulator\Synchro.java" "%SRC%\emulator\UCommands.java" "%~dp0RefRun.java"
if errorlevel 1 exit /b 1
"%JDK%\bin\java" -cp "%OUT%" com.cax.pmk.emulator.RefRun "%~dp0..\scripts.txt" > "%~dp0..\EmulatorMK61.Tests\fixtures\expected.txt"
if errorlevel 1 exit /b 1
type "%~dp0..\EmulatorMK61.Tests\fixtures\expected.txt"
