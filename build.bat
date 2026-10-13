@echo off
rem Builds ItemSheetMaker.exe with the C# compiler that ships with Windows (.NET Framework 4.x)
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist bin mkdir bin
"%CSC%" /nologo /target:winexe /optimize+ /out:bin\ItemSheetMaker.exe /win32manifest:app.manifest /win32icon:app.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.dll /r:System.Core.dll src\*.cs
if %errorlevel%==0 echo Built bin\ItemSheetMaker.exe
pause
