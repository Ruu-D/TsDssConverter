@echo off
rem ---------------------------------------------------------------------------------------------------------
rem  Builds the delivery file: ONE self-contained TsDssConverter.exe (no .NET installation needed at the customer).
rem
rem  Run it from the project folder (double-click, or type:  publish.cmd ).
rem  It runs all the tests first (a build that fails a test is never delivered; skip them with:  publish.cmd -SkipTests ),
rem  then publishes to  dist\v^<version^>\TsDssConverter.exe  and shows the size, the version and a checksum.
rem  The version is the ^<Version^> line in src\TsDssConverter.Tray\TsDssConverter.Tray.csproj (the only place to change it).
rem ---------------------------------------------------------------------------------------------------------
setlocal
cd /d "%~dp0"

set PROJECT=src\TsDssConverter.Tray\TsDssConverter.Tray.csproj

rem The version, read from the project file
set VERSION=
for /f "delims=" %%v in ('dotnet msbuild %PROJECT% -getProperty:Version') do set VERSION=%%v
if "%VERSION%"=="" (
    echo No Version found in %PROJECT%
    exit /b 1
)

set TARGET=dist\v%VERSION%
echo Building TsDssConverter %VERSION% into %TARGET%

if /i not "%~1"=="-SkipTests" (
    echo Running the tests first ...
    dotnet test TsDssConverter.slnx
    if errorlevel 1 (
        echo The tests failed: nothing was built.
        exit /b 1
    )
)

rem Start from an empty folder, so old files can never end up in the delivery
if exist "%TARGET%" rmdir /s /q "%TARGET%"

rem win-x64 + self-contained: the .NET runtime is inside the exe. Compression makes the exe about half the size.
rem No debug files. (Trimming is not used: it is not supported for Windows Forms.)
dotnet publish %PROJECT% -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o "%TARGET%"
if errorlevel 1 (
    echo dotnet publish failed.
    exit /b 1
)

rem The delivery is exactly one file
set COUNT=0
for %%f in ("%TARGET%\*") do set /a COUNT+=1
if not "%COUNT%"=="1" (
    echo Expected exactly one file in %TARGET% but found %COUNT%:
    dir /b "%TARGET%"
    exit /b 1
)
if not exist "%TARGET%\TsDssConverter.exe" (
    echo TsDssConverter.exe is not in %TARGET%
    exit /b 1
)

echo.
echo Done.
for %%f in ("%TARGET%\TsDssConverter.exe") do (
    echo   File:     %%~ff
    echo   Size:     %%~zf bytes
)
powershell -NoProfile -Command "'  Version:  ' + (Get-Item '%TARGET%\TsDssConverter.exe').VersionInfo.FileVersion"
echo   SHA-256:
certutil -hashfile "%TARGET%\TsDssConverter.exe" SHA256 | findstr /v /c:"hash of" /c:"CertUtil"
