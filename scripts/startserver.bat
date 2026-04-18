@echo off
setlocal EnableExtensions EnableDelayedExpansion
title LicentaMed - Backend + Python + Cloudflare Quick Tunnel
color 0B

rem =========================================================
rem CONFIG
rem =========================================================
set "APP_DIR=E:\Facultate\Licenta\Licenta\Licenta"
set "PY_DIR=E:\Facultate\Licenta\Licenta\Python"
set "PY_EXE=C:\Users\Administrator\AppData\Local\Programs\Python\Python311\python.exe"
set "UNBLOCK_SCRIPT=E:\Facultate\Licenta\Licenta\scripts\unblock-project.ps1"

set "APP_HOST=127.0.0.1"
set "APP_PORT=5000"
set "APP_URL=http://%APP_HOST%:%APP_PORT%"

set "ML_HOST=127.0.0.1"
set "ML_PORT=8001"
set "ML_URL=http://%ML_HOST%:%ML_PORT%"

set "CONFIGURATION=Debug"
set "FRAMEWORK=net9.0"
set "APP_DLL=%APP_DIR%\bin\%CONFIGURATION%\%FRAMEWORK%\Licenta.dll"

set "START_CLOUDFLARE=1"
set "CLEAN_BEFORE_BUILD=1"
set "UNBLOCK_BEFORE_BUILD=1"

set "LOG_DIR=%APP_DIR%\startup-logs"
set "APP_LOG=%LOG_DIR%\backend.log"
set "ML_LOG=%LOG_DIR%\ml.log"
set "CF_LOG=%LOG_DIR%\cloudflare.log"
set "DIAG_LOG=%LOG_DIR%\diagnostics.log"

set "CF_HOME=%USERPROFILE%\.cloudflared"
set "CF_CONFIG_YML=%CF_HOME%\config.yml"
set "CF_CONFIG_YAML=%CF_HOME%\config.yaml"
set "CF_CONFIG_YML_BAK=%CF_HOME%\config.yml.quicktunnel.bak"
set "CF_CONFIG_YAML_BAK=%CF_HOME%\config.yaml.quicktunnel.bak"

set "PUBLIC_URL="
set "READY="

rem =========================================================
rem VALIDATION
rem =========================================================
echo [1/10] Validating paths...

if not exist "%APP_DIR%\Licenta.csproj" (
    echo ERROR: Cannot find Licenta.csproj in:
    echo   %APP_DIR%
    goto :fail
)

if not exist "%PY_EXE%" (
    echo ERROR: Cannot find Python executable at:
    echo   %PY_EXE%
    goto :fail
)

if not exist "%PY_DIR%\src\run_server.py" (
    echo ERROR: Cannot find run_server.py at:
    echo   %PY_DIR%\src\run_server.py
    goto :fail
)

if "%UNBLOCK_BEFORE_BUILD%"=="1" (
    if not exist "%UNBLOCK_SCRIPT%" (
        echo ERROR: Cannot find unblock script at:
        echo   %UNBLOCK_SCRIPT%
        goto :fail
    )
)

where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: dotnet is not available in PATH.
    goto :fail
)

where powershell >nul 2>&1
if errorlevel 1 (
    echo ERROR: powershell is not available in PATH.
    goto :fail
)

if "%START_CLOUDFLARE%"=="1" (
    where cloudflared >nul 2>&1
    if errorlevel 1 (
        echo ERROR: cloudflared is not available in PATH.
        goto :fail
    )
)

if not exist "%LOG_DIR%" mkdir "%LOG_DIR%" >nul 2>&1

rem Do not truncate logs here to avoid "file is being used by another process"

echo APP_DIR=%APP_DIR%
echo PY_DIR=%PY_DIR%
echo PY_EXE=%PY_EXE%
echo APP_URL=%APP_URL%
echo ML_URL=%ML_URL%
echo.

rem =========================================================
rem CLEAN OLD PROCESSES
rem =========================================================
echo [2/10] Killing old processes...
call :kill_port %APP_PORT%
call :kill_port %ML_PORT%
taskkill /F /IM cloudflared.exe >nul 2>&1

rem Give Windows a moment to release handles
timeout /t 2 /nobreak >nul

rem =========================================================
rem PREPARE CLOUDFLARE QUICK TUNNEL
rem =========================================================
echo [3/10] Preparing Cloudflare Quick Tunnel...
if "%START_CLOUDFLARE%"=="1" (
    if exist "%CF_CONFIG_YML%" (
        if exist "%CF_CONFIG_YML_BAK%" del /f /q "%CF_CONFIG_YML_BAK%" >nul 2>&1
        ren "%CF_CONFIG_YML%" "config.yml.quicktunnel.bak"
    )

    if exist "%CF_CONFIG_YAML%" (
        if exist "%CF_CONFIG_YAML_BAK%" del /f /q "%CF_CONFIG_YAML_BAK%" >nul 2>&1
        ren "%CF_CONFIG_YAML%" "config.yaml.quicktunnel.bak"
    )
)

rem =========================================================
rem ENVIRONMENT
rem =========================================================
echo [4/10] Setting environment...
set "ASPNETCORE_ENVIRONMENT=Development"
set "DOTNET_ENVIRONMENT=Development"
set "ASPNETCORE_URLS=%APP_URL%"
set "ASPNETCORE_FORWARDEDHEADERS_ENABLED=true"
set "PYTHONUNBUFFERED=1"
set "ML_LOG_LEVEL=info"

set "MlServiceOptions__BaseUrl=%ML_URL%"
set "MlServiceOptions__AutoStartPythonServer=false"
set "MlServiceOptions__PythonExecutablePath=%PY_EXE%"
set "MlServiceOptions__PythonProjectDirectory=%PY_DIR%"
set "MlServiceOptions__PythonScriptPath=src\run_server.py"
set "MlServiceOptions__TimeoutSeconds=120"

rem =========================================================
rem UNBLOCK SOURCE TREE
rem =========================================================
if "%UNBLOCK_BEFORE_BUILD%"=="1" (
    echo [5/10] Unblocking trusted project files...
    powershell -NoProfile -ExecutionPolicy Bypass -File "%UNBLOCK_SCRIPT%" -AppDir "%APP_DIR%" -PyDir "%PY_DIR%" >> "%DIAG_LOG%" 2>&1
)

rem =========================================================
rem CLEAN BIN/OBJ
rem =========================================================
echo [6/10] Cleaning old build artifacts...
if "%CLEAN_BEFORE_BUILD%"=="1" (
    if exist "%APP_DIR%\bin" rmdir /s /q "%APP_DIR%\bin" >> "%DIAG_LOG%" 2>&1
    if exist "%APP_DIR%\obj" rmdir /s /q "%APP_DIR%\obj" >> "%DIAG_LOG%" 2>&1
)

rem =========================================================
rem BUILD BACKEND
rem =========================================================
echo [7/10] Building backend...
cd /d "%APP_DIR%"
dotnet build "%APP_DIR%\Licenta.csproj" -c %CONFIGURATION% -f %FRAMEWORK% -p:UseAppHost=false >> "%APP_LOG%" 2>&1
if errorlevel 1 (
    echo ERROR: Backend build failed.
    call :tail "%APP_LOG%" 120
    goto :fail
)

if not exist "%APP_DLL%" (
    echo ERROR: Cannot find backend DLL:
    echo   %APP_DLL%
    call :tail "%APP_LOG%" 120
    goto :fail
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "try { Unblock-File -Path '%APP_DLL%'; Remove-Item -LiteralPath '%APP_DLL%:Zone.Identifier' -Force -ErrorAction SilentlyContinue } catch {}" >> "%DIAG_LOG%" 2>&1

call :check_motw "%APP_DLL%"
if errorlevel 1 (
    echo ERROR: Backend DLL is still blocked by Windows.
    echo Check diagnostics log:
    echo   %DIAG_LOG%
    goto :fail
)

rem =========================================================
rem START PYTHON ML SERVER
rem =========================================================
echo [8/10] Starting Python ML server...
start "Licenta-ML" /B cmd /C "cd /d ""%PY_DIR%"" && ""%PY_EXE%"" ""src\run_server.py"" >> ""%ML_LOG%"" 2>&1"

call :wait_http "%ML_URL%/api/status" 60 "ML server"
if errorlevel 1 (
    echo ERROR: ML server did not start.
    call :tail "%ML_LOG%" 120
    goto :fail
)

rem =========================================================
rem START BACKEND
rem =========================================================
echo [9/10] Starting Licenta backend...
start "Licenta-Backend" /B cmd /C "cd /d ""%APP_DIR%"" && dotnet exec ""%APP_DLL%"" >> ""%APP_LOG%"" 2>&1"

call :wait_http "%APP_URL%/" 60 "Backend"
if errorlevel 1 (
    echo ERROR: Backend did not start.
    call :tail "%APP_LOG%" 120
    goto :fail
)

rem =========================================================
rem START CLOUDFLARE TUNNEL
rem =========================================================
if "%START_CLOUDFLARE%"=="1" (
    echo [10/10] Starting Cloudflare Quick Tunnel...

    del /f /q "%CF_LOG%" >nul 2>&1

    start "Licenta-Cloudflare" /MIN cmd /C "cloudflared tunnel --url ""%APP_URL%"" --no-autoupdate > ""%CF_LOG%"" 2>&1"

    call :wait_trycloudflare "%CF_LOG%" 120
    if errorlevel 1 (
        echo WARNING: Tunnel started, but the public URL could not be extracted automatically.
        echo Check the Cloudflare log:
        echo   %CF_LOG%
        echo.
        call :tail "%CF_LOG%" 120
    )
) else (
    echo [10/10] Cloudflare skipped.
)

echo.
echo [DONE] Everything is running.
echo.
echo Local app:      %APP_URL%
echo ML API:         %ML_URL%
echo Backend log:    %APP_LOG%
echo Python ML log:  %ML_LOG%
echo Diagnostics:    %DIAG_LOG%
if "%START_CLOUDFLARE%"=="1" echo Cloudflare log: %CF_LOG%
if defined PUBLIC_URL (
    echo Public URL:     !PUBLIC_URL!
) else (
    echo Public URL:     ^<not extracted automatically, check cloudflare.log^>
)
echo.
echo You can share the same Public URL with multiple people.
echo Each user should open it from their own browser/device
echo if you want separate accounts/sessions. The same browser shares cookies.
echo.
echo To stop everything, type Q and press Enter.
echo.

:stop_prompt
set "STOP_CMD="
set /p "STOP_CMD=> "
if /I "%STOP_CMD%"=="Q" goto :shutdown
goto :stop_prompt

:kill_port
for /f "tokens=5" %%P in ('netstat -aon ^| findstr /R /C:":%~1 .*LISTENING"') do (
    taskkill /F /PID %%P >nul 2>&1
)
exit /b 0

:wait_http
set "WAIT_URL=%~1"
set "WAIT_TRIES=%~2"
set "WAIT_NAME=%~3"
set "READY="

for /L %%I in (1,1,%WAIT_TRIES%) do (
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
        "try { $r = Invoke-WebRequest -UseBasicParsing -Uri '%WAIT_URL%' -TimeoutSec 3; exit 0 } catch { if ($_.Exception.Response -ne $null) { exit 0 } else { exit 1 } }" >nul 2>&1

    if !errorlevel! equ 0 (
        set "READY=1"
        goto :wait_http_done
    )

    timeout /t 1 /nobreak >nul
)

:wait_http_done
if defined READY exit /b 0

echo ERROR: %WAIT_NAME% is not responding at %WAIT_URL%
exit /b 1

:wait_trycloudflare
set "TRY_FILE=%~1"
set "TRY_SECONDS=%~2"
set "PUBLIC_URL="

for /L %%I in (1,1,%TRY_SECONDS%) do (
    if exist "%TRY_FILE%" (
        for /f "delims=" %%U in ('findstr /R /C:"https://.*trycloudflare\.com" "%TRY_FILE%"') do (
            set "PUBLIC_URL=%%U"
        )

        if defined PUBLIC_URL (
            for /f "tokens=1,* delims= " %%A in ("!PUBLIC_URL!") do (
                set "PUBLIC_URL=%%A"
            )
            exit /b 0
        )
    )

    timeout /t 1 /nobreak >nul
)

exit /b 1

:check_motw
set "CHECK_FILE=%~1"
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$p='%CHECK_FILE%'; if (Test-Path -LiteralPath ($p + ':Zone.Identifier')) { exit 1 } else { exit 0 }" >nul 2>&1

if errorlevel 1 exit /b 1
exit /b 0

:tail
set "TAIL_FILE=%~1"
set "TAIL_LINES=%~2"
echo.
echo ===== Last %TAIL_LINES% lines of %TAIL_FILE% =====
powershell -NoProfile -ExecutionPolicy Bypass -Command "if (Test-Path '%TAIL_FILE%') { Get-Content '%TAIL_FILE%' -Tail %TAIL_LINES% }"
echo ===============================================
echo.
exit /b 0

:restore_cloudflare_config
if exist "%CF_CONFIG_YML_BAK%" (
    if exist "%CF_CONFIG_YML%" del /f /q "%CF_CONFIG_YML%" >nul 2>&1
    ren "%CF_CONFIG_YML_BAK%" "config.yml"
)

if exist "%CF_CONFIG_YAML_BAK%" (
    if exist "%CF_CONFIG_YAML%" del /f /q "%CF_CONFIG_YAML%" >nul 2>&1
    ren "%CF_CONFIG_YAML_BAK%" "config.yaml"
)
exit /b 0

:shutdown
echo.
echo Stopping processes...
call :kill_port %APP_PORT%
call :kill_port %ML_PORT%
taskkill /F /IM cloudflared.exe >nul 2>&1
call :restore_cloudflare_config
echo Done.
endlocal
exit /b 0

:fail
echo.
echo Startup failed.
echo.
echo Logs:
echo   %APP_LOG%
echo   %ML_LOG%
echo   %CF_LOG%
echo   %DIAG_LOG%
echo.
call :restore_cloudflare_config
pause
endlocal
exit /b 1