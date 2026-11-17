@echo off
rem Builds DesktopMenu.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /codepage:65001 /optimize ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:Microsoft.CSharp.dll /r:System.Core.dll ^
  /out:DesktopMenu.exe DesktopMenu.cs Strings.cs || exit /b 1
powershell -NoProfile -ExecutionPolicy Bypass -File test-strings.ps1
