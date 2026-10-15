@echo off
setlocal
set "CHROMATIC_PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if exist "%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe" set "CHROMATIC_PS=%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
"%CHROMATIC_PS%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0eng\Run-App.ps1" %*
set "CHROMATIC_EXIT=%errorlevel%"
if not "%CHROMATIC_EXIT%"=="0" (
  echo.
  echo Chromatic Desktop could not start. See the error above.
  if not defined CHROMATIC_LAUNCHER_NO_PAUSE pause
)
exit /b %CHROMATIC_EXIT%
