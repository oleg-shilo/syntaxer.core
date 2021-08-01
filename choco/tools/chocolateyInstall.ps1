$packageName = 'CS-Syntaxer'
$url = 'https://github.com/oleg-shilo/syntaxer.core/releases/download/v3.0.1.0/syntaxer.7z'

Stop-Process -Name "syntaxer" -ErrorAction SilentlyContinue

$installDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
Install-ChocolateyEnvironmentVariable 'CSSYNTAXER_ROOT' $installDir User

$cheksum = '284C6D48C0A92D72F20B018F8B9CCD33B1905EA8D023F959429398F2CBE34EA0'
$checksumType = "sha256"

# Download and unpack a zip file
Install-ChocolateyZipPackage "$packageName" "$url" "$installDir" -checksum $checksum -checksumType $checksumType
