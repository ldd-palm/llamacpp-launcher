@echo off
setlocal enabledelayedexpansion
title llama.cpp Service Manager
:: ============================================================
::  llama.cpp Server Manager
::  Stable configuration (tuned 2024-08):
::   - N_GPU_LAYERS=0  : CPU inference. Vulkan GPU memory
::                       allocation has failed on this machine
::                       before (-ngl 99 triggers
::                       ErrorOutOfDeviceMemory or garbled output),
::                       so CPU is the most reliable choice.
::   - CTX_SIZE=8192   : Context window. The model default is
::                       only 4096; 8192 gives more headroom.
::   - -ctk/-ctv q8_0  : KV cache quantization, greatly reduces
::                       memory usage with minimal quality loss.
::   - Launch method   : PowerShell Start-Process hidden-window
::                       detached launch, so closing this console
::                       window does NOT stop the background service.
:: ============================================================

:: ================= Basic Configuration =================
set "HOST=127.0.0.1"
set "PORT=8080"
set "CTX_SIZE=8192"
set "N_GPU_LAYERS=0"
set "MODELS_DIR=models"
:: =======================================================

:MENU
cls
echo ======================================================
echo              llama.cpp Server Manager
echo ======================================================
echo.

:: Check port status and retrieve active model name
netstat -ano | findstr /R /C:":%PORT% .*LISTENING" >nul
if %errorlevel% equ 0 (
    echo Status: [ RUNNING on port %PORT% ]
    echo API:    http://%HOST%:%PORT%/v1
    for /f "usebackq delims=" %%A in (`powershell -NoProfile -Command "try { (Invoke-RestMethod -Uri 'http://%HOST%:%PORT%/v1/models' -TimeoutSec 1 -ErrorAction Stop).data[0].id } catch { 'Loading / Unknown' }"`) do (
        echo Model:  %%A
    )
) else (
    echo Status: [ STOPPED ]
)
echo.
echo ------------------------------------------------------
echo [1] Start / Load Model (runs in background)
echo [2] Check Status and Query Endpoint
echo [3] Stop Server
echo [4] Switch Model
echo [0] Exit
echo ------------------------------------------------------
set /p "CHOICE=Enter your choice [0-4]: "

if "%CHOICE%"=="1" goto SELECT_AND_START
if "%CHOICE%"=="2" goto CHECK_STATUS
if "%CHOICE%"=="3" goto STOP_SERVER
if "%CHOICE%"=="4" goto SWITCH_MODEL
if "%CHOICE%"=="0" exit /b
goto MENU

:SELECT_AND_START
netstat -ano | findstr /R /C:":%PORT% .*LISTENING" >nul
if %errorlevel% equ 0 (
    echo.
    echo [WARNING] A service is already active on port %PORT%.
    echo Please stop the running service or choose option [4] to switch models.
    pause
    goto MENU
)

:LIST_MODELS
cls
echo ======================================================
echo                Available GGUF Models
echo ======================================================
echo.

set count=0
for %%F in ("%MODELS_DIR%\*.gguf") do (
    set /a count+=1
    set "model[!count!]=%%~nxF"
    set "model_path[!count!]=%%F"
    echo  [!count!] %%~nxF
)

if %count% equ 0 (
    echo [ERROR] No .gguf files found in "%MODELS_DIR%".
    echo Please ensure your model files are located in the models directory.
    pause
    goto MENU
)

echo.
echo  [0] Return to Main Menu
echo ------------------------------------------------------
set /p "M_CHOICE=Select a model [1-%count%]: "

if "%M_CHOICE%"=="0" goto MENU
if not defined model[%M_CHOICE%] (
    echo Invalid choice. Please try again.
    pause
    goto LIST_MODELS
)

set "SELECTED_MODEL=!model_path[%M_CHOICE%]!"
set "SELECTED_NAME=!model[%M_CHOICE%]!"

echo.
echo Launching model in background: !SELECTED_NAME! ...

set "EXE_NAME=llama-server.exe"
if not exist "!EXE_NAME!" (
    if exist "server.exe" (
        set "EXE_NAME=server.exe"
    ) else (
        echo [ERROR] llama-server.exe or server.exe not found in current directory!
        pause
        goto MENU
    )
)

REM Hidden window, fully detached launch (closing this window does not affect the service); plus KV cache quantization
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Start-Process -FilePath '%~dp0!EXE_NAME!' -WorkingDirectory '%~dp0' -ArgumentList @('-m','!SELECTED_MODEL!','--host','!HOST!','--port','!PORT!','-c','!CTX_SIZE!','-ngl','!N_GPU_LAYERS!','-ctk','q8_0','-ctv','q8_0','--alias','!SELECTED_NAME!') -WindowStyle Hidden -RedirectStandardOutput '%~dp0llama_server.log' -RedirectStandardError '%~dp0llama_server.err.log'"

timeout /t 2 /nobreak >nul
netstat -ano | findstr /R /C:":%PORT% .*LISTENING" >nul
if %errorlevel% equ 0 (
    echo.
    echo ======================================================
    echo [SUCCESS] Service is running in background.
    echo - Base API URL: http://!HOST!:!PORT!/v1
    echo - Model Name:   !SELECTED_NAME!
    echo - Log Files:    llama_server.log / llama_server.err.log
    echo.
    echo You can safely close this terminal window now.
    echo ======================================================
) else (
    echo.
    echo [WARNING] Server failed to bind to port %PORT%. Check llama_server.log for details.
)
pause
goto MENU

:CHECK_STATUS
cls
echo ======================================================
echo                   Server Status
echo ======================================================
echo.

netstat -ano | findstr /R /C:":%PORT% .*LISTENING" >nul
if %errorlevel% neq 0 (
    echo [STATUS] No active llama-server instance detected.
    goto END_CHECK
)

echo [STATUS] llama-server is ACTIVE (Port: %PORT%)
echo [API]    http://%HOST%:%PORT%/v1
echo.
echo ------------------------------------------------------
echo                Active Model Details
echo ------------------------------------------------------

powershell -NoProfile -Command ^
    "try { "^
    "  $resp = Invoke-RestMethod -Uri 'http://%HOST%:%PORT%/v1/models' -TimeoutSec 3 -ErrorAction Stop; "^
    "  $m = $resp.data[0]; "^
    "  Write-Host (' Model Name:    ' + $m.id); "^
    "  if ($m.meta) { "^
    "    Write-Host (' Context Size:  ' + $m.meta.n_ctx); "^
    "    Write-Host (' Quantization:  ' + $m.meta.ftype); "^
    "    Write-Host (' Total Params:  ' + [math]::Round($m.meta.n_params / 1e9, 2) + ' B'); "^
    "  } "^
    "} catch { "^
    "  Write-Host ' [Warning] Server is listening on port %PORT% but /v1/models did not respond.'; "^
    "}"

echo ------------------------------------------------------

:END_CHECK
echo.
pause
goto MENU

:STOP_SERVER
cls
echo ======================================================
echo                  Stopping Server
echo ======================================================
echo.
echo Terminating llama-server processes...

taskkill /F /IM llama-server.exe >nul 2>&1
taskkill /F /IM server.exe >nul 2>&1

timeout /t 1 /nobreak >nul
netstat -ano | findstr /R /C:":%PORT% .*LISTENING" >nul
if %errorlevel% neq 0 (
    echo [SUCCESS] Service stopped. Port %PORT% is now free.
) else (
    echo [INFO] No llama.cpp process was holding the port.
)
echo.
pause
goto MENU

:SWITCH_MODEL
cls
echo ======================================================
echo                  Switching Model
echo ======================================================
echo.
echo Stopping active server...
taskkill /F /IM llama-server.exe >nul 2>&1
taskkill /F /IM server.exe >nul 2>&1
timeout /t 1 /nobreak >nul
goto LIST_MODELS
