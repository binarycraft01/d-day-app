@echo off
chcp 65001 > nul
echo Compiling DDayWidget.exe ...

set CSC=
for %%v in (4.0.30319) do (
    if exist "C:\Windows\Microsoft.NET\Framework64\v%%v\csc.exe" (
        set "CSC=C:\Windows\Microsoft.NET\Framework64\v%%v\csc.exe"
    )
    if exist "C:\Windows\Microsoft.NET\Framework\v%%v\csc.exe" (
        if not defined CSC set "CSC=C:\Windows\Microsoft.NET\Framework\v%%v\csc.exe"
    )
)

if not defined CSC (
    echo ERROR: csc.exe not found.
    pause & exit /b 1
)

"%CSC%" /target:winexe /out:"%~dp0DDayWidget.exe" ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Drawing.dll ^
    /win32icon:"%~dp0icon.ico" ^
    "%~dp0DDayWidget.cs"

if not exist "%~dp0events.txt" (
    if exist "%~dp0events.sample.txt" copy "%~dp0events.sample.txt" "%~dp0events.txt" > nul
)

if exist "%~dp0DDayWidget.exe" (
    echo.
    echo [SUCCESS] DDayWidget.exe created!
    echo Now run DDayWidget.exe directly.
) else (
    echo [FAILED] Compilation failed.
)
pause
