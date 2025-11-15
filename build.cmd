echo off
dotnet pack -c Release

rd /S /Q .\out\

rem win defender has "all clear" on the freshly built binaries (*.zip) and yet when they are downloaded as part of winget install the very same zip files 
rem are falsely identified as "Trojan:Script/Wacatac.B!ml". Even though winget checks that the file hash to ensure the correct/identical file is downloaded.
rem This is the problem that has been reported top MS numerous times. Just google for "windows defender Trojan:Script/Wacatac.B!ml".

rem for now excluding syntaxer.cli, just to have less moving parts
rem dotnet publish -o .\out\syntaxer.net9 -c Release -f net9.0 syntaxer.cli\syntaxer.cli.csproj
dotnet publish -o .\out\syntaxer.net10 -c Release -f net10.0 syntaxer.csproj

cd .\out\syntaxer.net10
echo cd: %cd%
rem 7z.exe a -r "..\syntaxer.net9.7z" "*.*"
7z.exe a -r "..\syntaxer.net10.zip" "*.*"
cd ..\..

rem dotnet publish -o .\out\syntaxer.net8 -c Release -f net8.0 syntaxer.cli\syntaxer.cli.csproj
dotnet publish -o .\out\syntaxer.net8 -c Release -f net8.0 syntaxer.csproj

cd .\out\syntaxer.net8
echo cd: %cd%
rem 7z.exe a -r "..\syntaxer.net8.7z" "*.*"
7z.exe a -r "..\syntaxer.net8.zip" "*.*"
cd ..\..

copy .\nupkg\* .\out

explorer .\out
rem pause