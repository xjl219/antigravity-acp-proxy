@echo off
setlocal enabledelayedexpansion

set "CSC_EXE=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC_EXE%" (
    echo [ERROR] csc.exe not found at %CSC_EXE%
    exit /b 1
)

set "SCRIPT_DIR=%~dp0"
set "PROJECT_ROOT=%SCRIPT_DIR%.."
set "SRC_FILE=%PROJECT_ROOT%\src\wrapper.cs"
set "OUT_FILE=%PROJECT_ROOT%\bin\wrapper.exe"

if not exist "%PROJECT_ROOT%\bin" mkdir "%PROJECT_ROOT%\bin"

echo [*] Compiling wrapper.cs ...
"%CSC_EXE%" /target:exe /optimize+ /out:"%OUT_FILE%" "%SRC_FILE%"

if %errorlevel% neq 0 (
    echo [ERROR] Compilation failed!
    exit /b %errorlevel%
)

echo [OK] Compilation successful!
echo Output: %OUT_FILE%
