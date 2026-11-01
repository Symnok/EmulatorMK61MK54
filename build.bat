@echo off
setlocal
rem ==========================================================================
rem  EmulatorMK61 (MK-61/54) for Windows Phone 8.1 / Windows 10 Mobile - build
rem
rem  Usage:  build.bat [ARM|x86|both] [Debug|Release] [nobump]
rem          build.bat clean
rem          default: ARM Release, version bumped
rem
rem  The package version (1.0.<build>.0) is bumped on every build: WP8.1 will NOT
rem  replace an installed app with a package of the SAME version - the deploy
rem  reports success and the phone keeps running the old binary. Pass "nobump"
rem  only for a build you will not deploy (or a release number set by hand).
rem
rem  Before the app, the core's self test runs (tests\run-tests.cmd): a build
rem  whose engine disagrees with the original Java engine stops.
rem ==========================================================================

set "PLAT=%~1"
set "APP=%~dp0src\EmulatorMK61.WP81"

rem  clean removes the intermediate output only. AppPackages - every package built so far - is
rem  never touched: earlier builds are kept for going back to.
if /i "%PLAT%"=="clean" (
  for %%D in ("%APP%\obj" "%APP%\bin" "%~dp0tests\EmulatorMK61.Tests\obj" "%~dp0tests\EmulatorMK61.Tests\bin") do if exist "%%~D" rd /s /q "%%~D"
  echo Clean ^(packages in AppPackages kept^).
  exit /b 0
)

if "%PLAT%"=="" set "PLAT=ARM"
set "CFG=%~2"
if "%CFG%"=="" set "CFG=Release"
set "BUMP=%~3"

set "MSB=C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe"
if not exist "%MSB%" echo ERROR: MSBuild 14 not found & exit /b 1

echo === core self test ===
call "%~dp0tests\run-tests.cmd" > "%~dp0tests\last-run.txt" 2>&1
if errorlevel 1 (
  type "%~dp0tests\last-run.txt"
  echo ================ SELF TEST FAILED ================
  exit /b 1
)
findstr /c:"PASSED" "%~dp0tests\last-run.txt"

rem The signing key is private and gitignored: a fresh clone makes its own.
if not exist "%APP%\EmulatorMK61.WP81_TemporaryKey.pfx" (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0make-cert.ps1"
  if errorlevel 1 exit /b 1
)

if /i not "%BUMP%"=="nobump" (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0bump-version.ps1"
  if errorlevel 1 exit /b 1
)
set "VER="
for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "([xml](Get-Content -LiteralPath '%APP%\Package.appxmanifest')).Package.Identity.Version"`) do set "VER=%%V"
echo === EmulatorMK61 %VER% ===

if /i "%PLAT%"=="both" (
  call :one ARM %CFG% || exit /b 1
  call :one x86 %CFG% || exit /b 1
  goto :done
)
call :one %PLAT% %CFG% || exit /b 1

:done
echo ================ BUILD OK ================
echo Packages: src\EmulatorMK61.WP81\AppPackages\
exit /b 0

:one
echo === %~1 %~2 ===
"%MSB%" "%APP%\EmulatorMK61.WP81.csproj" /p:Configuration=%~2 /p:Platform=%~1 /p:VisualStudioVersion=14.0 /v:minimal /nologo
if errorlevel 1 (
  echo ================ BUILD FAILED ^(%~1 %~2^) ================
  exit /b 1
)
exit /b 0
