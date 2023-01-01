$packageName = 'CS-Syntaxer'
$url = 'https://github.com/oleg-shilo/syntaxer.core/releases/download/v3.1.2.0/syntaxer.7z'

Stop-Process -Name "syntaxer" -ErrorAction SilentlyContinue

$installDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
Install-ChocolateyEnvironmentVariable 'CSSYNTAXER_ROOT' $installDir User

$checksum = '1D4C6ED6FC00CA86844DBE068620D489E719E2E7A19BBD9CF35C6ACB302E9C63'
$checksumType = "sha256"

# Download and unpack a zip file
Install-ChocolateyZipPackage "$packageName" "$url" "$installDir" -checksum $checksum -checksumType $checksumType
