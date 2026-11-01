# EmulatorMK61 - create the package signing certificate.
#
# The .pfx is a private key and is gitignored, so a fresh clone has none and the appx step
# would fail. build.bat calls this automatically when the file is missing; run it by hand only
# if you want to replace the certificate.
#
# The subject MUST equal the manifest's Publisher exactly (CN=EmulatorMK61) or signing is
# rejected. The .cer beside it is what gets installed on the phone to trust the package.

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dir  = Join-Path $root "src\EmulatorMK61.WP81"
$pfx  = Join-Path $dir "EmulatorMK61.WP81_TemporaryKey.pfx"
$cer  = Join-Path $dir "EmulatorMK61.WP81_TemporaryKey.cer"

if (Test-Path $pfx) {
    Write-Output "signing certificate already present: $pfx"
    exit 0
}

Write-Output "creating a package signing certificate (CN=EmulatorMK61)..."

$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=EmulatorMK61" `
    -KeyUsage DigitalSignature -FriendlyName "EmulatorMK61 dev signing" `
    -CertStoreLocation "Cert:\CurrentUser\My" -KeyExportPolicy Exportable `
    -NotAfter (Get-Date).AddYears(10) `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}Subject Type:End Entity")

# Empty-password PFX, matching how Visual Studio emits a _TemporaryKey.pfx.
[System.IO.File]::WriteAllBytes(
    $pfx, $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, ""))
[System.IO.File]::WriteAllBytes(
    $cer, $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert))

Write-Output ("thumbprint: " + $cert.Thumbprint)
Write-Output "wrote $pfx"
Write-Output "wrote $cer"
Write-Output ""
Write-Output "NOTE: this is a NEW certificate. The phone must trust it before the app will"
Write-Output "install - deploy the .cer from AppPackages once, before the .appx."
