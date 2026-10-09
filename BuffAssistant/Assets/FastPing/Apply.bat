@echo off
title Mabinogi FastPing & Registry Optimizer
color 0b

:: Check and request Administrator privileges
>nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system"
if '%errorlevel%' NEQ '0' (
    echo [!] Administrator privileges required. Restarting as Administrator...
    goto UACPrompt
) else ( goto gotAdmin )

:UACPrompt
    echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
    echo UAC.ShellExecute "%~s0", "", "", "runas", 1 >> "%temp%\getadmin.vbs"
    "%temp%\getadmin.vbs"
    exit /B

:gotAdmin
    if exist "%temp%\getadmin.vbs" ( del "%temp%\getadmin.vbs" )
    pushd "%~dp0"
    cls

echo ========================================================
echo       Mabinogi Registry & FastPing Optimizer
echo ========================================================
echo.
echo [Checking] Inspecting current optimization status...

:: Check if already optimized (checks global TcpAckFrequency value)
for /f "tokens=3" %%a in ('reg query "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v "TcpAckFrequency" 2^>nul') do set "CHECK_VAL=%%a"

if "%CHECK_VAL%"=="0x1" (
    echo.
    echo ========================================================
    echo  [Notice] Optimization is already applied!
    echo ========================================================
    echo No changes needed. Closing the program.
    echo.
    pause
    exit
)

echo.
echo [1/3] Modifying TCP/IP interface registry...
for /f %%i in ('reg query "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces" /s 2^>nul ^| findstr "HKEY_LOCAL_MACHINE"') do (
    reg query "%%i" /v "DhcpIPAddress" >nul 2>&1
    if not errorlevel 1 (
        reg add "%%i" /v "TcpAckFrequency" /t REG_DWORD /d "1" /f >nul
        reg add "%%i" /v "TCPNoDelay" /t REG_DWORD /d "1" /f >nul
    )
    reg query "%%i" /v "IPAddress" >nul 2>&1
    if not errorlevel 1 (
        reg add "%%i" /v "TcpAckFrequency" /t REG_DWORD /d "1" /f >nul
        reg add "%%i" /v "TCPNoDelay" /t REG_DWORD /d "1" /f >nul
    )
)

echo [2/3] Optimizing global TCP/IP parameters...
reg add "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v "TcpAckFrequency" /t REG_DWORD /d "1" /f >nul
reg add "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v "TCPNoDelay" /t REG_DWORD /d "1" /f >nul

echo [3/3] Enabling MSMQ feature (for network stability)...
dism /online /enable-feature /featurename:MSMQ-Container /all /norestart >nul 2>&1
dism /online /enable-feature /featurename:MSMQ-Server /all /norestart >nul 2>&1

echo.
echo ========================================================
echo               All tasks completed successfully!
echo ========================================================
echo Please restart your computer to apply the changes.
echo.
pause

::Made by 블랙카드 리하(Crick)