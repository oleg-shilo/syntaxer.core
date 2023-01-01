$packageName = 'CS-Syntaxer'
$url = 'https://github.com/oleg-shilo/syntaxer.core/releases/download/v3.1.2.0/syntaxer.7z'

Stop-Process -Name "syntaxer" -ErrorAction SilentlyContinue

$installDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
Install-ChocolateyEnvironmentVariable 'CSSYNTAXER_ROOT' $installDir User

$checksum = 'AEACF9FFFBF717A906BDADD1B6846F55101E1BE4CB1BF3F8B0793AFEE70F2E5A'
$checksumType = "sha256"

# Download and unpack a zip file
Install-ChocolateyZipPackage "$packageName" "$url" "$installDir" -checksum $checksum -checksumType $checksumType
