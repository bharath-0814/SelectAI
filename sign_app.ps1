# SelectAI Code Signing & Authenticode Certificate Script
$certSubject = "CN=SelectAI Open Source, O=SelectAI, C=US"
$cert = Get-ChildItem -Path "Cert:\CurrentUser\My" | Where-Object { $_.Subject -eq $certSubject } | Select-Object -First 1

if (-not $cert) {
    Write-Host "Generating self-signed Code Signing Certificate..."
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $certSubject -KeyUsage DigitalSignature -FriendlyName "SelectAI Code Signing Certificate" -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter (Get-Date).AddYears(5)
}

Write-Host "Using Certificate Thumbprint: $($cert.Thumbprint)"

# Target executables to sign
$exePaths = @(
    "d:\AI  select\dist\SelectAI-SelfContained\SelectAI.exe"
)

foreach ($exe in $exePaths) {
    if (Test-Path $exe) {
        Write-Host "Signing $exe..."
        $sig = Set-AuthenticodeSignature -FilePath $exe -Certificate $cert -TimestampServer "http://timestamp.digicert.com" -HashAlgorithm SHA256
        Write-Host "Status: $($sig.Status) ($($sig.StatusMessage))"
    }
}

# Export public certificate for users
$exportPath = "d:\AI  select\dist\SelectAI-SelfContained\SelectAI_Certificate.cer"
Export-Certificate -Cert $cert -FilePath $exportPath -Force | Out-Null
Write-Host "Exported public certificate to $exportPath"

# Trust locally on CurrentUser Root & TrustedPublisher
Write-Host "Importing certificate into local CurrentUser Root & TrustedPublisher..."
Import-Certificate -FilePath $exportPath -CertStoreLocation "Cert:\CurrentUser\Root" | Out-Null
Import-Certificate -FilePath $exportPath -CertStoreLocation "Cert:\CurrentUser\TrustedPublisher" | Out-Null

Write-Host "Verifying signature on target executable:"
Get-AuthenticodeSignature "d:\AI  select\dist\SelectAI-SelfContained\SelectAI.exe" | Format-List
