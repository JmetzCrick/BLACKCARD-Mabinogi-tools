@echo off
title Mabinogi FastPing & Registry Reverter
color 0c

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
echo       Mabinogi Registry & FastPing Reverter
echo ========================================================
echo.
echo [1/3] Removing TCP/IP interface optimization values...
for /f %%i in ('reg query "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces" /s 2^>nul ^| findstr "HKEY_LOCAL_MACHINE"') do (
    reg query "%%i" /v "DhcpIPAddress" >nul 2>&1
    if not errorlevel 1 (
        reg delete "%%i" /v "TcpAckFrequency" /f >nul 2>&1
        reg delete "%%i" /v "TCPNoDelay" /f >nul 2>&1
    )
    reg query "%%i" /v "IPAddress" >nul 2>&1
    if not errorlevel 1 (
        reg delete "%%i" /v "TcpAckFrequency" /f >nul 2>&1
        reg delete "%%i" /v "TCPNoDelay" /f >nul 2>&1
    )
)

echo [2/3] Removing global TCP/IP parameters optimization values...
reg delete "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v "TcpAckFrequency" /f >nul 2>&1
reg delete "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v "TCPNoDelay" /f >nul 2>&1

echo [3/3] Disabling MSMQ feature...
dism /online /disable-feature /featurename:MSMQ-Container /norestart >nul 2>&1
dism /online /disable-feature /featurename:MSMQ-Server /norestart >nul 2>&1

echo.
echo ========================================================
echo        All settings have been reverted successfully!
echo ========================================================
echo Please restart your computer to apply the changes.
echo.
pause

:: Made by 블랙카드 리하(Crick)