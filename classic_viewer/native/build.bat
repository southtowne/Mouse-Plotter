@echo off
setlocal enabledelayedexpansion
REM Build the ETW capture engine that CaptureForm P/Invokes into. Prefers
REM `zig` on PATH, otherwise a vendored ..\..\zig-*\ copy. Flags must match
REM the Makefile.

cd /d "%~dp0"

set "ZIG="
where zig >nul 2>nul && set "ZIG=zig"
if not defined ZIG (
  for /d %%D in ("%~dp0..\..\zig-*") do set "ZIG=%%D\zig.exe"
)
if not defined ZIG (
  echo [error] Could not find zig. Install Zig from https://ziglang.org/download/
  echo         or place an extracted zig-*\ folder next to this repository.
  exit /b 1
)

REM Pinned so an arm64 host still builds the x86_64 binary.
set "TARGET=x86_64-windows-gnu"
set "CFLAGS=-O3 -fno-stack-protector -shared"
set "LIBS=-ladvapi32 -lcfgmgr32 -luser32"

if /i "%~1"=="test" (
  if not exist build mkdir build
  "!ZIG!" cc -target %TARGET% -O2 -Wall -Wextra -Wconversion -Wshadow tests\etw_test.c -o build\etw_test.exe %LIBS%
  if errorlevel 1 exit /b 1
  build\etw_test.exe
  exit /b !errorlevel!
)

echo Using: !ZIG!
"!ZIG!" cc -target %TARGET% etw.c etw_exports.def -o MousePlotterEtw.dll %CFLAGS% %LIBS%
if errorlevel 1 ( echo [error] Build failed. & exit /b 1 )
echo Built %~dp0MousePlotterEtw.dll
