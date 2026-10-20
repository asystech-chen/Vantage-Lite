@echo off
setlocal
cd /d "%~dp0"

echo === Vantage Lite build ===

where dotnet >nul 2>nul
if %errorlevel%==0 (
    echo [1/2] Using dotnet SDK
    dotnet build "VantageLite.csproj" -c Release
    if errorlevel 1 goto :failed
    goto :done
)

echo [1/2] dotnet not found, trying Visual Studio MSBuild...

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo [ERROR] Neither dotnet SDK nor vswhere.exe was found.
    echo         Install .NET SDK: https://dotnet.microsoft.com/download
    goto :failed
)

for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD=%%i"

if not defined MSBUILD (
    echo [ERROR] MSBuild was not found. Install the .NET desktop development workload.
    goto :failed
)

echo Using MSBuild: %MSBUILD%
"%MSBUILD%" "VantageLite.csproj" /restore /p:Configuration=Release /p:PlatformTarget=x64 /m
if errorlevel 1 goto :failed

:done
echo.
echo === Build OK ===
echo Output: %~dp0bin\Release\net48\
exit /b 0

:failed
echo.
echo === Build FAILED ===
pause
exit
