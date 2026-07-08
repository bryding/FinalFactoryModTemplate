@echo off
REM Copies the required Final Factory DLLs into Assets\FinalFactoryDlls.
REM Reads the game install path from finalfactory.properties.
REM Set that path via the "Modding > Set Final Factory Path..." menu in Unity,
REM or by editing finalfactory.properties by hand.
setlocal enabledelayedexpansion

set "ROOT=%~dp0"
set "PROPS=%ROOT%finalfactory.properties"

if not exist "%PROPS%" (
  echo Could not find "%PROPS%". Set FinalFactoryDir in finalfactory.properties, or run 'Modding ^> Set Final Factory Path...' in Unity first.
  exit /b 1
)

REM Read the FinalFactoryDir value. eol=# skips comment lines; delims== splits on the first '='.
set "DIR="
for /f "usebackq eol=# tokens=1,* delims==" %%A in ("%PROPS%") do (
  if /i "%%A"=="FinalFactoryDir" set "DIR=%%B"
)

if not defined DIR (
  echo FinalFactoryDir is not set in "%PROPS%"
  exit /b 1
)

REM Normalize forward slashes to backslashes for Windows paths.
set "DIR=%DIR:/=\%"

REM Accept either the install root (contains finalfactory_Data\Managed) or the Managed folder itself.
set "MANAGED="
if exist "%DIR%\finalfactory_Data\Managed\FFCore.dll" set "MANAGED=%DIR%\finalfactory_Data\Managed"
if not defined MANAGED (
  if exist "%DIR%\FFCore.dll" set "MANAGED=%DIR%"
)

if not defined MANAGED (
  echo Could not find FFCore.dll under "%DIR%". Point FinalFactoryDir at your Final Factory install folder - the one containing finalfactory_Data.
  exit /b 1
)

set "DEST=%ROOT%Assets\FinalFactoryDlls"
if not exist "%DEST%" mkdir "%DEST%"

set "COUNT=0"
for %%D in (FFCore.dll FFSystems.dll FFComponents.dll FFTechnology.dll FFNetcode.dll) do (
  if not exist "%MANAGED%\%%D" (
    echo Missing "%%D" in "%MANAGED%"
    exit /b 1
  )
  copy /y "%MANAGED%\%%D" "%DEST%\%%D" >nul
  if errorlevel 1 (
    echo Failed to copy "%%D"
    exit /b 1
  )
  echo Copied %%D
  set /a COUNT+=1
)

echo Done. Copied %COUNT% DLLs to "%DEST%"
endlocal
