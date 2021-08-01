dotnet publish -o .\out\syntaxer -c RELEASE syntaxer.csproj
cd .\out\syntaxer
echo cd: %cd%
7z.exe a -r "..\syntaxer.7z" "*.*"
cd ..\..
explorer .\out
pause