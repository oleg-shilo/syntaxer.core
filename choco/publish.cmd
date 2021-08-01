echo off
echo *******************
echo *  must be admin  *
echo *******************
rem choco apikey --key ???????? --source https://push.chocolatey.org/
choco push cs-syntaxer.3.0.1.0.nupkg --source https://push.chocolatey.org/
