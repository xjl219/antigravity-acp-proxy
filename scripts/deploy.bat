@echo off
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
set "PROJECT_ROOT=%SCRIPT_DIR%.."
set "BIN_EXE=%PROJECT_ROOT%\bin\wrapper.exe"
set "CONF_FILE=%PROJECT_ROOT%\config\proxy.conf"

if not exist "%BIN_EXE%" (
    echo [*] Binary not found. Building now...
    call "%SCRIPT_DIR%build.bat"
    if !errorlevel! neq 0 exit /b !errorlevel!
)

rem 目标路径列表（支持同时部署到系统 AppData 缓存目录 与 IDEA 本地解压目录）
set "TARGET_DIR_1=%LOCALAPPDATA%\JetBrains\IntelliJIdea2026.2\acp-agents\antigravity-acp\1.3.0"
set "TARGET_DIR_2=D:\soft\idea-2026.2.3.win\appdata\.IntelliJIdea\system\acp-agents\antigravity-acp\1.3.0"

for %%D in ("%TARGET_DIR_1%" "%TARGET_DIR_2%") do (
    if exist "%%~fD" (
        echo [*] Deploying to: %%~fD
        
        rem 备份原程序（如尚未备份）
        if exist "%%~fD\agy_acp_server.exe" if not exist "%%~fD\agy_acp_server_real.exe" (
            echo     [-] Backing up agy_acp_server.exe -> agy_acp_server_real.exe
            move /y "%%~fD\agy_acp_server.exe" "%%~fD\agy_acp_server_real.exe" >nul
        )
        if exist "%%~fD\localharness_external.exe" if not exist "%%~fD\localharness_external_real.exe" (
            echo     [-] Backing up localharness_external.exe -> localharness_external_real.exe
            move /y "%%~fD\localharness_external.exe" "%%~fD\localharness_external_real.exe" >nul
        )
        
        rem 部署包装垫片
        echo     [+] Copying wrapper as agy_acp_server.exe
        copy /y "%BIN_EXE%" "%%~fD\agy_acp_server.exe" >nul
        echo     [+] Copying wrapper as localharness_external.exe
        copy /y "%BIN_EXE%" "%%~fD\localharness_external.exe" >nul
        
        rem 部署配置文件（如果不存在才拷贝，避免覆盖用户已修改的配置）
        if not exist "%%~fD\proxy.conf" (
            echo     [+] Initializing proxy.conf
            copy /y "%CONF_FILE%" "%%~fD\proxy.conf" >nul
        )
    )
)

echo.
echo [OK] Deployment completed!
