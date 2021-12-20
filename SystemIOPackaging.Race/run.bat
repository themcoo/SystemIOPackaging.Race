@echo off
set /A Counter=0

:start
set /A Counter+=1
echo Attempt %Counter%
SystemIOPackaging.Race.exe
if ERRORLEVEL 1 goto start

pause>nul