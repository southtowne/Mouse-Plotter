// SPDX-License-Identifier: MIT
//
// P/Invoke wrapper around native/etw.c (XBAB Tech's MousePlotter capture
// engine, https://github.com/XBAB-Tech/MousePlotter), compiled separately
// as MousePlotterEtw.dll. Gives CaptureForm the same kernel-timestamp
// precision windows_gui had, in place of MouseTester's plain Raw Input
// timing, without pulling any of that engine's own GUI/CSV code along.
//
// etw_start binds to one physical device (its PDO/USB interface); a second
// mouse's reports would go unmatched, so CaptureForm filters to a single
// device per capture the same way windows_gui's click-to-pick does.

using System;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace MousePlotter
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct EtwTime
    {
        public long t;
        public int source; // 0 = raw, 1 = completion, 2 = interrupt
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EtwPairingNative
    {
        public IntPtr times;
        public UIntPtr interrupts;
        public UIntPtr completions;
        public UIntPtr raw;
        public UIntPtr unmatched;
        public UIntPtr shared;
        public double lag_us;
        public double resid_us;
    }

    internal sealed class EtwPairingResult
    {
        public long[] Times;
        public long Interrupts;
        public long Completions;
        public long Raw;
        public long Unmatched;
        public long Shared;

        // Mirrors windows_gui's timestamp_source(): names whichever event type
        // supplied most of the kernel times.
        public string SourceLabel()
        {
            if (Interrupts >= Completions && Interrupts >= Raw && Interrupts > 0) return "ETW xHCI interrupt";
            if (Completions >= Raw && Completions > 0) return "ETW USB completion";
            return "Raw Input";
        }
    }

    internal static class EtwCapture
    {
        private const string Lib = "MousePlotterEtw.dll";

        [DllImport(Lib)] private static extern int etw_start(IntPtr mouse);
        [DllImport(Lib)] private static extern void etw_stop();
        [DllImport(Lib)] private static extern int etw_pair(long[] userTimes, UIntPtr n, ref EtwPairingNative out_, long qpf);
        [DllImport(Lib)] private static extern void etw_free_pairing(ref EtwPairingNative p);

        public static bool IsElevated()
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        // Only meaningful when IsElevated(); returns false otherwise without
        // even trying, same as windows_gui only opening a session when admin.
        public static bool Start(IntPtr mouseDevice)
        {
            return IsElevated() && etw_start(mouseDevice) == 0;
        }

        public static void Stop() => etw_stop();

        // Call after Stop(). Returns null if tracing failed or nothing could
        // be matched; the caller keeps its own Raw Input timestamps then.
        public static EtwPairingResult Pair(long[] rawTicks, long qpf)
        {
            var native = new EtwPairingNative();
            int rc = etw_pair(rawTicks, (UIntPtr)rawTicks.Length, ref native, qpf);
            if (rc != 0 || native.times == IntPtr.Zero)
                return null;

            try
            {
                var times = new long[rawTicks.Length];
                int stride = Marshal.SizeOf<EtwTime>();
                for (int i = 0; i < rawTicks.Length; i++)
                {
                    var et = Marshal.PtrToStructure<EtwTime>(native.times + i * stride);
                    times[i] = et.t;
                }
                return new EtwPairingResult
                {
                    Times = times,
                    Interrupts = (long)native.interrupts,
                    Completions = (long)native.completions,
                    Raw = (long)native.raw,
                    Unmatched = (long)native.unmatched,
                    Shared = (long)native.shared,
                };
            }
            finally
            {
                etw_free_pairing(ref native);
            }
        }
    }
}
