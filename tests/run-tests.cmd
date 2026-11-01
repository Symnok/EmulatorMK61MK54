@echo off
rem Builds and runs the core's self test on the PC (MSBuild 14, .NET Framework 4.5).
setlocal
set "MSB=C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe"
"%MSB%" "%~dp0EmulatorMK61.Tests\EmulatorMK61.Tests.csproj" /p:Configuration=Release /v:minimal /nologo
if errorlevel 1 exit /b 1
"%~dp0EmulatorMK61.Tests\bin\EmulatorMK61.Tests.exe" %*
