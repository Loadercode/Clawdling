@echo off
rem Renders README images into docs\ (needs Python 3 with Pillow for compositing).
setlocal
cd /d "%~dp0\.."
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REFS=/r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll
if not exist bin mkdir bin
"%CSC%" /nologo /target:exe /main:Clawdling.AssetGen /out:bin\AssetGen.exe %REFS% src\*.cs tools\AssetGen.cs || exit /b 1
bin\AssetGen.exe bin\frames || exit /b 1
python tools\compose.py bin\frames docs || exit /b 1
