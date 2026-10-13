#!/bin/sh
# Build on Linux/macOS with Mono (apt install mono-devel). Output runs on Windows.
mkdir -p bin
mcs -sdk:4.5 -target:winexe -optimize+ -out:bin/ItemSheetMaker.exe -win32manifest:app.manifest -win32icon:app.ico \
  -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.dll -r:System.Core.dll src/*.cs
