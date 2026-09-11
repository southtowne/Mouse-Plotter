# Mouse-Plotter
Records raw mouse input and plots it, for checking polling rate, jitter, and sensor quality. Inspired by [XBAB Mouse Plotter](https://github.com/XBAB-Tech/MousePlotter)

<img width="296" height="448" alt="MousePlotter" src="https://github.com/user-attachments/assets/6d3b2b17-ef84-4e5b-8eb0-305683672a69" />

![GitHub Release Downloads](https://img.shields.io/github/downloads/southtowne/Mouse-Plotter/total)

# Usage
Simply follow the quick and easy steps below ↓

1. Download [Mouse plotter](https://github.com/southtowne/Mouse-Plotter/releases/download/V1.0/MousePlotter.exe).
2. Right-click & run as administrator.

# Build instructions:

1. Prerequisites: Windows 10/11 x64, .NET 8 SDK (https://dotnet.microsoft.com/download/dotnet/8.0), and Zig (https://ziglang.org/download/), any recent version, added to PATH
2. Clone: git clone https://github.com/southtowne/MousePlotter.git
3. Build the capture engine: Open a terminal in MousePlotter\classic_viewer\native and run build.bat
4. Build the app: From the MousePlotter folder, run dotnet publish classic_viewer -c Release -o dist
5. Output: The finished exe lands at dist\MousePlotter.exe
6. Run: Run as administrator
