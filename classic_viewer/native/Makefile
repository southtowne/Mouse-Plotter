ZIG ?= zig
TARGET ?= x86_64-windows-gnu

# Keep in sync with build.bat.
CFLAGS = -O3 -fno-stack-protector -shared
LIBS = -ladvapi32 -lcfgmgr32 -luser32

.PHONY: all test clean

all:
	$(ZIG) cc -target $(TARGET) etw.c etw_exports.def -o MousePlotterEtw.dll $(CFLAGS) $(LIBS)

# Run this target on Windows; needs Zig only, no elevation or mouse.
test:
	mkdir -p build
	$(ZIG) cc -target $(TARGET) -O2 -Wall -Wextra -Wconversion -Wshadow tests/etw_test.c -o build/etw_test.exe $(LIBS)
	build/etw_test.exe

clean:
	rm -f MousePlotterEtw.dll MousePlotterEtw.pdb
	rm -rf build
