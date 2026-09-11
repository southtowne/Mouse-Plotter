// SPDX-License-Identifier: MIT
//
// Entry point. Opens straight into CaptureForm -- MouseTester's Measure/
// Collect/Log capture window, the "tester" half -- which can also Load or
// have a CSV dropped onto it to reach MousePlot, the "plotter" half. A CSV
// path on the command line (e.g. from a file association) is loaded the
// same way on startup.

using System;
using System.IO;
using System.Windows.Forms;

namespace MousePlotter
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var captureForm = new CaptureForm();

            string csvPath = args.Length > 0 ? args[0] : null;
            if (csvPath != null && File.Exists(csvPath))
                captureForm.LoadFile(csvPath);

            Application.Run(captureForm);
        }
    }
}
