@echo off
setlocal
rem Zaloha pre pripad, ze Smart App Control zablokuje FriWorldLauncher.exe.
rem
rem Je to ta ista aplikacia, len spustena cez podpisany dotnet host — ten Smart App
rem Control pusti tam, kde apphost .exe zastavi. Vyzaduje nainstalovany .NET 10.
rem
rem Pozor: aj toto Smart App Control obcas zablokuje. Rozhoduje aj podla mena suboru
rem a verdikt sa casom meni. Ak to prestane ist, jedina spolahliva cesta je Smart App
rem Control vypnut (Windows Security -> App & browser control), co je od jari 2026 vratne.

cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo Nenasiel sa dotnet. Nainstaluj .NET 10 Desktop Runtime a skus znova.
    pause
    exit /b 1
)

dotnet exec "%~dp0zaloha\FriWorldLauncherHost.dll"

if errorlevel 1 (
    echo.
    echo Nepodarilo sa spustit. Ak je v chybe "Application Control policy",
    echo zablokoval to Smart App Control — pozri poznamku v CITAJ-MA.txt.
    pause
)
