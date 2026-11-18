@echo off
rem Builds Clawdling.exe with the C# compiler that ships with Windows (.NET Framework 4.x). No SDK needed.
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REFS=/r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll
if not exist bin mkdir bin
"%CSC%" /nologo /target:winexe /optimize+ /out:bin\Clawdling.exe %REFS% src\*.cs || exit /b 1
bin\Clawdling.exe makeicon bin\Clawdling.ico
"%CSC%" /nologo /target:winexe /optimize+ /win32icon:bin\Clawdling.ico /out:bin\Clawdling.exe %REFS% src\*.cs || exit /b 1
echo Built bin\Clawdling.exe
