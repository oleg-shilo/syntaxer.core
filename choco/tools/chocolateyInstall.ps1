$packageName = 'CS-Syntaxer'
$url = 'https://github.com/oleg-shilo/syntaxer.core/releases/download/v3.1.4.0/syntaxer.7z'

Stop-Process -Name "syntaxer" -ErrorAction SilentlyContinue

$installDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
Install-ChocolateyEnvironmentVariable 'CSSYNTAXER_ROOT' $installDir User

$checksum = '5118D27B449C943A33A491922B2750D9552C529D31EA55C3D3792B1518FA6B1B'
$checksumType = "sha256"

# Download and unpack a zip file
Install-ChocolateyZipPackage "$packageName" "$url" "$installDir" -checksum $checksum -checksumType $checksumType
