// SPDX-License-Identifier: MIT
// Ported from MouseTester's Form1.cs
// (https://github.com/valleyofdoom/MouseTester), originally (c) 2013 microe1.
// This is the "tester" half that pairs with MousePlot's "plotter" half,
// matching MouseTester's original two-window design. Dark-themed, and
// LoadFile is exposed publicly plus wired to drag-and-drop so a CSV can be
// opened the same way whether it came from the Load button, a drop, or
// argv[0].
//
// Timestamps are no longer plain Raw Input: the first sample of each
// capture binds to that Raw Input device and starts an ETW session via
// native/etw.c (XBAB Tech's MousePlotter capture engine), which is more
// accurate -- kernel timestamps from the USB completion/xHCI interrupt that
// serviced the report, not the userspace WM_INPUT dispatch time. Only one
// physical device is trusted per capture, both because the ETW session is
// bound to one and because a second mouse interleaving reports would corrupt
// the pairing regardless. Requires elevation; falls back to Raw Input
// timestamps otherwise, same as windows_gui did.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MousePlotter
{
    public partial class CaptureForm : Form
    {
        private MouseLog mlog = new MouseLog();
        enum state { idle, measure_wait, measure, collect_wait, collect, log };
        private state test_state = state.idle;
        private long pFreq;

        private IntPtr activeDevice = IntPtr.Zero;
        private bool haveActiveDevice = false;
        private bool etwActive = false;
        private string lastTimestampSource = "";

        public CaptureForm()
        {
            InitializeComponent();
            DarkTheme.Apply(this);

            this.Text = "MousePlotter";

            this.RegisterRawInputMouse(Handle);
            this.textBoxDesc.Text = this.mlog.Desc.ToString();
            this.textBoxCPI.Text = this.mlog.Cpi.ToString();
            this.textBox1.Text = "Enter the correct CPI" +
                                 "\r\n        or\r\n" +
                                 "Press the Measure button" +
                                 "\r\n        or\r\n" +
                                 "Press the Load button, or drop a CSV here";
            this.toolStripStatusLabel1.Text = "";
            this.KeyPreview = true;
            this.KeyDown += new KeyEventHandler(CaptureForm_KeyDown);

            this.AllowDrop = true;
            this.DragEnter += CaptureForm_DragEnter;
            this.DragDrop += CaptureForm_DragDrop;

            QueryPerformanceFrequency(out pFreq);
        }

        private void CaptureForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1)
            {
                buttonLog.PerformClick();
                e.Handled = true;
            }
            if (e.KeyCode == Keys.F2)
            {
                buttonLog.PerformClick();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F3)
            {
                buttonPlot.PerformClick();
                e.Handled = false;
            }
        }

        private void CaptureForm_DragEnter(object sender, DragEventArgs e)
        {
            if (test_state == state.idle &&
                e.Data.GetDataPresent(DataFormats.FileDrop) &&
                ((string[])e.Data.GetData(DataFormats.FileDrop)).Length == 1)
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void CaptureForm_DragDrop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            LoadFile(files[0]);
        }

        protected override void WndProc(ref Message m)
        {
            QueryPerformanceCounter(out long pCounter);
            if (m.Msg == WM_INPUT)
            {
                RAWINPUT raw = new RAWINPUT();
                uint size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(RAWINPUT));
                int outsize = GetRawInputData(m.LParam, RID_INPUT, out raw, ref size, (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(RAWINPUTHEADER)));
                if (outsize != -1)
                {
                    if (raw.header.dwType == RIM_TYPEMOUSE)
                    {
                        logMouseEvent(new MouseEvent(raw.data.mouse.buttonsStr.usButtonFlags, raw.data.mouse.lLastX, -(raw.data.mouse.lLastY), pCounter), raw.header.hDevice);
                    }
                }
            }
            base.WndProc(ref m);
        }

        private void logMouseEvent(MouseEvent mevent, IntPtr device)
        {
            if (this.test_state == state.idle)
            {

            }
            else if (this.test_state == state.measure_wait)
            {
                if (mevent.buttonflags == 0x0001)
                {
                    beginActiveCapture(device);
                    this.mlog.Add(mevent);
                    this.toolStripStatusLabel1.Text = "Measuring";
                    this.test_state = state.measure;
                }
            }
            else if (this.test_state == state.measure)
            {
                if (device != activeDevice) return;
                this.mlog.Add(mevent);
                if (mevent.buttonflags == 0x0002)
                {
                    double x = 0.0;
                    double y = 0.0;
                    foreach (MouseEvent e in this.mlog.Events)
                    {
                        x += (double)e.lastx;
                        y += (double)e.lasty;
                    }
                    endActiveCapture();
                    tsCalc();
                    this.mlog.Cpi = Math.Round(Math.Sqrt((x * x) + (y * y)) / (10 / 2.54));
                    this.textBoxCPI.Text = this.mlog.Cpi.ToString();
                    this.textBox1.Text = "Press the Collect or Log Start button\r\n";
                    this.toolStripStatusLabel1.Text = "";
                    this.test_state = state.idle;
                }
            }
            else if (this.test_state == state.collect_wait)
            {
                if (mevent.buttonflags == 0x0001)
                {
                    beginActiveCapture(device);
                    this.mlog.Add(mevent);
                    this.toolStripStatusLabel1.Text = "Collecting";
                    this.test_state = state.collect;
                }
            }
            else if (this.test_state == state.collect)
            {
                if (device != activeDevice) return;
                this.mlog.Add(mevent);
                if (mevent.buttonflags == 0x0002)
                {
                    endActiveCapture();
                    tsCalc();
                    this.textBox1.Text = "Press the plot button to view data\r\n" +
                                         "        or\r\n" +
                                         "Press the save button to save log file\r\n" +
                                         "Timestamps: " + lastTimestampSource + "\r\n" +
                                         "Events: " + this.mlog.Events.Count.ToString() + "\r\n" +
                                         "Sum X: " + this.mlog.deltaX().ToString() + " counts    " + Math.Abs(this.mlog.deltaX() / this.mlog.Cpi * 2.54).ToString("0.0") + " cm\r\n" +
                                         "Sum Y: " + this.mlog.deltaY().ToString() + " counts    " + Math.Abs(this.mlog.deltaY() / this.mlog.Cpi * 2.54).ToString("0.0") + " cm\r\n" +
                                         "Path: " + this.mlog.path().ToString("0") + " counts    " + (this.mlog.path() / this.mlog.Cpi * 2.54).ToString("0.0") + " cm";
                    this.toolStripStatusLabel1.Text = "";
                    this.test_state = state.idle;
                }
            }
            else if (this.test_state == state.log)
            {
                if (!haveActiveDevice)
                    beginActiveCapture(device);
                if (device != activeDevice) return;
                this.mlog.Add(mevent);
            }
        }

        // Binds a capture to the device that started it (an identifying
        // click for Measure/Collect, the first report for Log) and starts an
        // ETW session for it when elevated. Samples from any other device
        // are ignored for the rest of this capture; see endActiveCapture.
        private void beginActiveCapture(IntPtr device)
        {
            activeDevice = device;
            haveActiveDevice = true;
            etwActive = EtwCapture.Start(device);
        }

        // Stops the ETW session (if one was running) and pairs its kernel
        // timestamps onto the recorded samples in place of their Raw Input
        // ones, before tsCalc() turns pcounter into relative milliseconds.
        private void endActiveCapture()
        {
            if (etwActive)
            {
                EtwCapture.Stop();
                long[] rawTicks = mlog.Events.Select(e => e.pcounter).ToArray();
                EtwPairingResult paired = EtwCapture.Pair(rawTicks, pFreq);
                if (paired != null)
                {
                    for (int i = 0; i < mlog.Events.Count; i++)
                        mlog.Events[i].pcounter = paired.Times[i];
                    lastTimestampSource = paired.SourceLabel();
                }
                else
                {
                    lastTimestampSource = "Raw Input (kernel pairing failed)";
                }
            }
            else
            {
                lastTimestampSource = "Raw Input";
            }
            haveActiveDevice = false;
            etwActive = false;
        }

        private void buttonMeasure_Click(object sender, EventArgs e)
        {
            if (this.test_state == state.idle) {
                this.textBox1.Text = "1. Press and hold the left mouse button\r\n" +
                                     "2. Move the mouse 10 cm in a straight line\r\n" +
                                     "3. Release the left mouse button\r\n";
                this.toolStripStatusLabel1.Text = "Press the left mouse button";
                this.mlog.Clear();
                this.test_state = state.measure_wait;
            }
        }

        private void buttonCollect_Click(object sender, EventArgs e)
        {
            if (this.test_state == state.idle)
            {
                this.textBox1.Text = "1. Press and hold the left mouse button\r\n" +
                                     "2. Move the mouse\r\n" +
                                     "3. Release the left mouse button\r\n";
                this.toolStripStatusLabel1.Text = "Press the left mouse button";
                this.mlog.Clear();
                this.test_state = state.collect_wait;
            }
        }

        private void buttonLog_Click(object sender, EventArgs e)
        {
            if (this.test_state == state.idle)
            {
                this.textBox1.Text = "1. Press the Log Stop button\r\n";
                this.toolStripStatusLabel1.Text = "Logging...";
                this.mlog.Clear();
                this.test_state = state.log;
                buttonLog.Text = "Stop (F2)";
            }
            else if (this.test_state == state.log)
            {
                endActiveCapture();
                tsCalc();
                this.textBox1.Text = "Press the plot button to view data\r\n" +
                                     "        or\r\n" +
                                     "Press the save button to save log file\r\n" +
                                     "Timestamps: " + lastTimestampSource + "\r\n" +
                                     "Events: " + this.mlog.Events.Count.ToString() + "\r\n" +
                                     "Sum X: " + this.mlog.deltaX().ToString() + " counts    " + Math.Abs(this.mlog.deltaX() / this.mlog.Cpi * 2.54).ToString("0.0") + " cm\r\n" +
                                     "Sum Y: " + this.mlog.deltaY().ToString() + " counts    " + Math.Abs(this.mlog.deltaY() / this.mlog.Cpi * 2.54).ToString("0.0") + " cm\r\n" +
                                     "Path: " + this.mlog.path().ToString("0") + " counts    " + (this.mlog.path() / this.mlog.Cpi * 2.54).ToString("0.0") + " cm";
                this.toolStripStatusLabel1.Text = "";
                this.test_state = state.idle;
                buttonLog.Text = "Start (F1)";
            }
        }

        private void buttonPlot_Click(object sender, EventArgs e)
        {
            if (this.mlog.Events.Count > 0 && this.test_state != state.log && this.test_state != state.collect_wait)
            {
                this.mlog.Desc = textBoxDesc.Text;
                MousePlot mousePlot = new MousePlot(this.mlog);
                mousePlot.Show();
            }
        }

        private void buttonLoad_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Filter = "CSV Files (*.csv)|*.csv|All Files(*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.Multiselect = false;
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                LoadFile(openFileDialog1.FileName);
            }
        }

        // Loads a CSV -- MouseTester's own format or MousePlotter's -- and, on
        // success, opens the plot immediately, same as the original Load button.
        public void LoadFile(string path)
        {
            if (this.test_state != state.idle || !File.Exists(path))
                return;

            this.mlog.Load(path);
            this.textBox1.Text = "Events: " + this.mlog.Events.Count.ToString() + "\r\n" +
                                 "Sum X: " + this.mlog.deltaX().ToString() + " counts    " + Math.Abs(this.mlog.deltaX() / this.mlog.Cpi * 2.54).ToString("0.0") + " cm\r\n" +
                                 "Sum Y: " + this.mlog.deltaY().ToString() + " counts    " + Math.Abs(this.mlog.deltaY() / this.mlog.Cpi * 2.54).ToString("0.0") + " cm\r\n" +
                                 "Path: " + this.mlog.path().ToString("0") + " counts    " + (this.mlog.path() / this.mlog.Cpi * 2.54).ToString("0.0") + " cm";
            this.textBoxDesc.Text = this.mlog.Desc.ToString();
            this.textBoxCPI.Text = this.mlog.Cpi.ToString();
            if (this.mlog.Events.Count > 0)
            {
                MousePlot mousePlot = new MousePlot(this.mlog);
                mousePlot.Show();
            }
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "CSV Files (*.csv)|*.csv|All Files(*.*)|*.*";
            saveFileDialog1.FilterIndex = 1;
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                this.mlog.Desc = textBoxDesc.Text;
                this.mlog.Save(saveFileDialog1.FileName);
            }
        }

        private void textBoxCPI_Validated(object sender, EventArgs e)
        {
            try
            {
                this.mlog.Cpi = double.Parse(this.textBoxCPI.Text);
            }
            catch
            {
                MessageBox.Show("Invalid CPI, resetting to previous value");
                this.textBoxCPI.Text = this.mlog.Cpi.ToString();
            }
            this.textBox1.Text = "Press the Collect or Log Start button\r\n";
        }

        private void tsCalc()
        {
            long pcounter_min = this.mlog.Events[0].pcounter;
            foreach (MouseEvent me in this.mlog.Events)
            {
                me.ts = (me.pcounter - pcounter_min) * 1000.0 / pFreq;
            }
        }
    }
}
