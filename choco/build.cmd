echo off
echo *******************
echo *  must be admin  *
echo *******************

choco pack
REM choco install cs-syntaxer -s '%cd%' --force