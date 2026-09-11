// SPDX-License-Identifier: MIT
//
// A small, hand-rolled dark theme for the classic WinForms UI. .NET 8
// WinForms has no built-in dark mode for these control types, so every
// color is set explicitly rather than relying on OS/app theming.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using OxyPlot;

namespace MousePlotter
{
    internal static class DarkTheme
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // Introduced in the Windows 10 20H1 SDK; 19 is the pre-release value
        // some older 1809-1903 builds still expect.
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        public static readonly Color WindowBg = Color.FromArgb(30, 30, 30);
        public static readonly Color PanelBg = Color.FromArgb(37, 37, 38);
        public static readonly Color ControlBg = Color.FromArgb(45, 45, 48);
        public static readonly Color ControlBorder = Color.FromArgb(63, 63, 70);
        public static readonly Color Text = Color.FromArgb(241, 241, 241);
        public static readonly Color MutedText = Color.FromArgb(160, 160, 160);
        public static readonly Color Accent = Color.FromArgb(0, 153, 214);

        public static readonly OxyColor PlotBackground = OxyColor.FromArgb(255, 24, 24, 26);
        public static readonly OxyColor PlotForeground = OxyColor.FromArgb(255, 220, 220, 220);
        public static readonly OxyColor PlotBorder = OxyColor.FromArgb(255, 80, 80, 84);
        public static readonly OxyColor PlotMajorGrid = OxyColor.FromArgb(60, 200, 200, 210);
        public static readonly OxyColor PlotMinorGrid = OxyColor.FromArgb(28, 200, 200, 210);

        public static readonly OxyColor SeriesBlue = OxyColor.FromRgb(86, 182, 255);
        public static readonly OxyColor SeriesRed = OxyColor.FromRgb(255, 120, 120);
        public static readonly OxyColor SeriesSingle = OxyColor.FromRgb(120, 220, 140);

        // ToolStrip/StatusStrip use their own renderer rather than plain
        // Control colors, so build the dark palette into one and reuse it.
        private sealed class DarkColorTable : ProfessionalColorTable
        {
            public override Color StatusStripGradientBegin => ControlBg;
            public override Color StatusStripGradientEnd => ControlBg;
            public override Color ToolStripGradientBegin => ControlBg;
            public override Color ToolStripGradientMiddle => ControlBg;
            public override Color ToolStripGradientEnd => ControlBg;
        }

        internal static readonly ToolStripRenderer StripRenderer = new ToolStripProfessionalRenderer(new DarkColorTable());

        // Applies the palette to a form and every descendant control, dispatching
        // on control type since plain WinForms controls don't share a themable base.
        // Also darkens the OS-drawn title bar and sets the running window's icon
        // -- <ApplicationIcon> in the csproj only sets the compiled exe's file
        // icon, not Form.Icon, so without this the title bar shows the default
        // WinForms icon instead of the one baked into the exe.
        public static void Apply(Form root)
        {
            root.BackColor = WindowBg;
            root.ForeColor = Text;
            ApplyRecursive(root);
            ApplyDarkTitleBar(root);
            ApplyIcon(root);
        }

        private static void ApplyDarkTitleBar(Form form)
        {
            int value = 1;
            IntPtr handle = form.Handle; // forces handle creation
            if (DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref value, sizeof(int));
        }

        private static void ApplyIcon(Form form)
        {
            form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }

        private static void ApplyRecursive(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                switch (c)
                {
                    case GroupBox gb:
                        gb.ForeColor = MutedText;
                        gb.BackColor = parent.BackColor;
                        break;
                    case Label lbl:
                        lbl.BackColor = Color.Transparent;
                        if (lbl.ForeColor == SystemColors.ControlText || lbl.ForeColor == Color.Black)
                            lbl.ForeColor = Text;
                        break;
                    case Button btn:
                        btn.FlatStyle = FlatStyle.Flat;
                        btn.BackColor = ControlBg;
                        btn.ForeColor = Text;
                        btn.FlatAppearance.BorderColor = ControlBorder;
                        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 55, 59);
                        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(35, 35, 38);
                        break;
                    case CheckBox chk:
                        chk.FlatStyle = FlatStyle.Flat;
                        chk.BackColor = Color.Transparent;
                        chk.ForeColor = Text;
                        chk.FlatAppearance.BorderSize = 0;
                        chk.FlatAppearance.CheckedBackColor = ControlBg;
                        break;
                    case ComboBox cmb:
                        cmb.FlatStyle = FlatStyle.Flat;
                        cmb.BackColor = ControlBg;
                        cmb.ForeColor = Text;
                        break;
                    case NumericUpDown nud:
                        nud.BackColor = ControlBg;
                        nud.ForeColor = Text;
                        nud.BorderStyle = BorderStyle.FixedSingle;
                        break;
                    case TextBox txt:
                        txt.BackColor = ControlBg;
                        txt.ForeColor = Text;
                        txt.BorderStyle = BorderStyle.FixedSingle;
                        break;
                    case SplitContainer split:
                        split.BackColor = ControlBorder;
                        split.Panel1.BackColor = WindowBg;
                        split.Panel2.BackColor = WindowBg;
                        break;
                    case TableLayoutPanel or Panel:
                        c.BackColor = Color.Transparent;
                        break;
                    case StatusStrip ss:
                        ss.BackColor = ControlBg;
                        ss.Renderer = StripRenderer;
                        foreach (ToolStripItem item in ss.Items)
                            item.ForeColor = Text;
                        break;
                }

                if (c.HasChildren)
                    ApplyRecursive(c);
            }
        }
    }
}
