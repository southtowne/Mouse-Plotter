// SPDX-License-Identifier: MIT
// Ported from MouseTester's MousePlot.cs
// (https://github.com/valleyofdoom/MouseTester), originally (c) 2013 microe1.
// Statistics and plotting logic are unchanged; series/axis colors are
// switched to the dark palette and a CPI field was added since this viewer
// opens an already-recorded CSV rather than measuring CPI itself.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.WindowsForms;

namespace MousePlotter
{
    public partial class MousePlot : Form
    {
        private MouseLog mlog;
        private int last_start;
        private double last_start_time;
        private int last_end;
        private double last_end_time;
        double x_min;
        double x_max;
        double y_min;
        double y_max;
        private string xlabel = "";
        private string ylabel = "";
        private bool suppress_refresh;

        public MousePlot(MouseLog Mlog)
        {
            InitializeComponent();
            DarkTheme.Apply(this);

            this.mlog = new MouseLog();
            this.mlog.Desc = Mlog.Desc;
            this.mlog.Cpi = Mlog.Cpi;
            int i = 1;
            int x = Mlog.Events[0].lastx;
            int y = Mlog.Events[0].lasty;
            ushort buttonflags = Mlog.Events[0].buttonflags;
            double ts = Mlog.Events[0].ts;
            while (i < Mlog.Events.Count)
            {
                this.mlog.Add(new MouseEvent(buttonflags, x, y, ts));
                x = Mlog.Events[i].lastx;
                y = Mlog.Events[i].lasty;
                buttonflags = Mlog.Events[i].buttonflags;
                ts = Mlog.Events[i].ts;
                i++;
            }
            this.mlog.Add(new MouseEvent(buttonflags, x, y, ts));

            this.Text = $"MousePlotter — {this.mlog.Desc}";

            this.last_end = mlog.Events.Count - 1;
            this.last_end_time = Mlog.Events[this.last_end].ts;

            this.last_start_time = mlog.Events[last_end].ts > 100 ? 100 : 0;
            initialize_plot();

            this.comboBoxPlotType.SelectedIndex = 0;
            this.comboBoxPlotType.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);

            this.numericUpDownStart.Minimum = 0;
            this.numericUpDownStart.Maximum = (decimal)last_end_time;
            this.numericUpDownStart.Value = (decimal)last_start_time;
            this.numericUpDownStart.DecimalPlaces = 3;
            this.numericUpDownStart.Increment = 10;
            this.numericUpDownStart.ValueChanged += new System.EventHandler(this.numericUpDownStart_ValueChanged);

            this.numericUpDownEnd.Minimum = 0;
            this.numericUpDownEnd.Maximum = (decimal)last_end_time;
            this.numericUpDownEnd.Value = (decimal)last_end_time;
            this.numericUpDownEnd.DecimalPlaces = 3;
            this.numericUpDownEnd.Increment = 10;
            this.numericUpDownEnd.ValueChanged += new System.EventHandler(this.numericUpDownEnd_ValueChanged);

            suppress_refresh = true;
            this.numericUpDownCpi.Value = ClampCpi((decimal)this.mlog.Cpi);
            suppress_refresh = false;
            this.numericUpDownCpi.ValueChanged += new System.EventHandler(this.numericUpDownCpi_ValueChanged);

            this.checkBoxStem.Checked = false;
            this.checkBoxStem.CheckedChanged += new System.EventHandler(this.checkBoxStem_CheckedChanged);

            this.checkBoxLines.Checked = false;
            this.checkBoxLines.CheckedChanged += new System.EventHandler(this.checkBoxLines_CheckedChanged);

            // open MousePlot to "Interval vs. Time" selection
            comboBoxPlotType.SelectedItem = "Interval vs. Time";

            refresh_plot();
        }

        private decimal ClampCpi(decimal value)
        {
            if (value < numericUpDownCpi.Minimum) return numericUpDownCpi.Minimum;
            if (value > numericUpDownCpi.Maximum) return numericUpDownCpi.Maximum;
            return value;
        }

        private void initialize_plot()
        {
            var pm = new PlotModel
            {
                Title = this.mlog.Desc.ToString(),
                PlotType = PlotType.Cartesian,
                Background = DarkTheme.PlotBackground,
                PlotAreaBorderColor = DarkTheme.PlotBorder,
                TextColor = DarkTheme.PlotForeground,
                TitleColor = DarkTheme.PlotForeground,
                SubtitleColor = DarkTheme.PlotForeground,
                Subtitle = this.mlog.Cpi.ToString() + " cpi"
            };
            plot1.Model = pm;
        }

        private void refresh_plot()
        {
            for (int j = 0; j < mlog.Events.Count; j++) {
                double currentTimeStamp = mlog.Events[j].ts;

                if (currentTimeStamp >= last_start_time) {
                    last_start = j;
                    last_start_time = currentTimeStamp;
                    numericUpDownStart.Value = (decimal)currentTimeStamp;
                    break;
                }
            }

            for (int j = mlog.Events.Count - 1; j >= 0; j--) {
                double currentTimeStamp = mlog.Events[j].ts;

                if (currentTimeStamp <= last_end_time) {
                    last_end = j;
                    last_end_time = currentTimeStamp;
                    numericUpDownEnd.Value = (decimal)currentTimeStamp;
                    break;
                }
            }

            PlotModel pm = plot1.Model;
            pm.Series.Clear();
            pm.Axes.Clear();
            pm.Subtitle = this.mlog.Cpi.ToString() + " cpi";
            OxyColor singleLineColor = DarkTheme.SeriesSingle;

            var scatterSeries1 = new ScatterSeries
            {
                BinSize = 8,
                MarkerFill = DarkTheme.SeriesBlue,
                MarkerSize = 1.5,
                MarkerStroke = DarkTheme.SeriesBlue,
                MarkerStrokeThickness = 1.0,
                MarkerType = MarkerType.Circle
            };

            var scatterSeries2 = new ScatterSeries
            {
                BinSize = 8,
                MarkerFill = DarkTheme.SeriesRed,
                MarkerSize = 1.5,
                MarkerStroke = DarkTheme.SeriesRed,
                MarkerStrokeThickness = 1.0,
                MarkerType = MarkerType.Circle
            };

            var lineSeries1 = new LineSeries
            {
                Color = DarkTheme.SeriesBlue,
                LineStyle = LineStyle.Solid,
                StrokeThickness = 2.0,
                InterpolationAlgorithm = InterpolationAlgorithms.CanonicalSpline
            };

            var lineSeries2 = new LineSeries
            {
                Color = DarkTheme.SeriesRed,
                LineStyle = LineStyle.Solid,
                StrokeThickness = 2.0,
                InterpolationAlgorithm = InterpolationAlgorithms.CanonicalSpline
            };

            if (checkBoxLines.Checked)
            {
                lineSeries1.InterpolationAlgorithm = null;
                lineSeries2.InterpolationAlgorithm = null;
            }

            var stemSeries1 = new StemSeries
            {
                Color = DarkTheme.SeriesBlue,
                StrokeThickness = 1.0,
            };

            var stemSeries2 = new StemSeries
            {
                Color = DarkTheme.SeriesRed,
                StrokeThickness = 1.0
            };

            // Only display statistics when "Interval" or "Frequency" is selected
            statisticsGroupBox.Visible = comboBoxPlotType.Text.Contains("Interval") || comboBoxPlotType.Text.Contains("Frequency");

            if (comboBoxPlotType.Text.Contains("xyCount"))
            {
                plot_xycounts_vs_time(scatterSeries1, scatterSeries2, stemSeries1, stemSeries2, lineSeries1, lineSeries2);
                pm.Series.Add(scatterSeries1);
                pm.Series.Add(scatterSeries2);
                if (!checkBoxLines.Checked)
                {
                    plot_fit(scatterSeries1, lineSeries1);
                    plot_fit(scatterSeries2, lineSeries2);
                }
                pm.Series.Add(lineSeries1);
                pm.Series.Add(lineSeries2);
                if (checkBoxStem.Checked)
                {
                    pm.Series.Add(stemSeries1);
                    pm.Series.Add(stemSeries2);
                }
            }
            else if (comboBoxPlotType.Text.Contains("xCount"))
            {
                plot_xcounts_vs_time(scatterSeries1, stemSeries1, lineSeries1);
                pm.Series.Add(scatterSeries1);
                if (!checkBoxLines.Checked)
                {
                    plot_fit(scatterSeries1, lineSeries1);
                    lineSeries1.Color = singleLineColor;
                }
                pm.Series.Add(lineSeries1);
                if (checkBoxStem.Checked)
                {
                    pm.Series.Add(stemSeries1);
                }
            }
            else if (comboBoxPlotType.Text.Contains("yCount"))
            {
                plot_ycounts_vs_time(scatterSeries1, stemSeries1, lineSeries1);
                pm.Series.Add(scatterSeries1);
                if (!checkBoxLines.Checked)
                {
                    plot_fit(scatterSeries1, lineSeries1);
                    lineSeries1.Color = singleLineColor;
                }
                pm.Series.Add(lineSeries1);
                if (checkBoxStem.Checked)
                {
                    pm.Series.Add(stemSeries1);
                }

            }
            else if (comboBoxPlotType.Text.Contains("Interval") || comboBoxPlotType.Text.Contains("Frequency"))
            {
                plot_interval_vs_time(scatterSeries1, stemSeries1, lineSeries1, comboBoxPlotType.Text.Contains("Interval"), (value) => comboBoxPlotType.Text.Contains("Interval") ? value : 1000 / value);
                pm.Series.Add(scatterSeries1);
                if (!checkBoxLines.Checked)
                {
                    plot_fit(scatterSeries1, lineSeries1);
                    lineSeries1.Color = singleLineColor;
                }
                pm.Series.Add(lineSeries1);
                if (checkBoxStem.Checked)
                {
                    pm.Series.Add(stemSeries1);
                }

            }
            else if (comboBoxPlotType.Text.Contains("xyVelocity"))
            {
                plot_xyvelocity_vs_time(scatterSeries1, scatterSeries2, stemSeries1, stemSeries2, lineSeries1, lineSeries2);
                pm.Series.Add(scatterSeries1);
                pm.Series.Add(scatterSeries2);
                if (!checkBoxLines.Checked)
                {
                    plot_fit(scatterSeries1, lineSeries1);
                    plot_fit(scatterSeries2, lineSeries2);
                }
                pm.Series.Add(lineSeries1);
                pm.Series.Add(lineSeries2);
                if (checkBoxStem.Checked)
                {
                    pm.Series.Add(stemSeries1);
                    pm.Series.Add(stemSeries2);
                }

            }
            else if (comboBoxPlotType.Text.Contains("xVelocity"))
            {
                plot_xvelocity_vs_time(scatterSeries1, stemSeries1, lineSeries1);
                pm.Series.Add(scatterSeries1);
                if (!checkBoxLines.Checked)
                {
                    plot_fit(scatterSeries1, lineSeries1);
                    lineSeries1.Color = singleLineColor;
                }
                pm.Series.Add(lineSeries1);
                if (checkBoxStem.Checked)
                {
                    pm.Series.Add(stemSeries1);
                }
            }
            else if (comboBoxPlotType.Text.Contains("yVelocity"))
            {
                plot_yvelocity_vs_time(scatterSeries1, stemSeries1, lineSeries1);
                pm.Series.Add(scatterSeries1);
                if (!checkBoxLines.Checked)
                {
                    plot_fit(scatterSeries1, lineSeries1);
                    lineSeries1.Color = singleLineColor;
                }
                pm.Series.Add(lineSeries1);
                if (checkBoxStem.Checked)
                {
                    pm.Series.Add(stemSeries1);
                }
            }
            else if (comboBoxPlotType.Text.Contains("X vs. Y"))
            {
                plot_x_vs_y(scatterSeries1, lineSeries1);
                pm.Series.Add(scatterSeries1);
                if (checkBoxLines.Checked)
                {
                    pm.Series.Add(lineSeries1);
                    lineSeries1.Color = singleLineColor;
                }
            }

            var linearAxis1 = new LinearAxis();
            linearAxis1.AbsoluteMinimum = x_min - (x_max - x_min) / 20.0;
            linearAxis1.AbsoluteMaximum = x_max + (x_max - x_min) / 20.0;
            linearAxis1.MajorGridlineColor = DarkTheme.PlotMajorGrid;
            linearAxis1.MajorGridlineStyle = LineStyle.Solid;
            linearAxis1.MinorGridlineColor = DarkTheme.PlotMinorGrid;
            linearAxis1.MinorGridlineStyle = LineStyle.Solid;
            linearAxis1.TicklineColor = DarkTheme.PlotForeground;
            linearAxis1.TextColor = DarkTheme.PlotForeground;
            linearAxis1.AxislineColor = DarkTheme.PlotBorder;
            linearAxis1.Position = AxisPosition.Bottom;
            linearAxis1.Title = xlabel;
            pm.Axes.Add(linearAxis1);

            var linearAxis2 = new LinearAxis();
            linearAxis2.AbsoluteMinimum = y_min - (y_max - y_min) / 20.0;
            linearAxis2.AbsoluteMaximum = y_max + (y_max - y_min) / 20.0;
            linearAxis2.MajorGridlineColor = DarkTheme.PlotMajorGrid;
            linearAxis2.MajorGridlineStyle = LineStyle.Solid;
            linearAxis2.MinorGridlineColor = DarkTheme.PlotMinorGrid;
            linearAxis2.MinorGridlineStyle = LineStyle.Solid;
            linearAxis2.TicklineColor = DarkTheme.PlotForeground;
            linearAxis2.TextColor = DarkTheme.PlotForeground;
            linearAxis2.AxislineColor = DarkTheme.PlotBorder;
            linearAxis2.Title = ylabel;
            pm.Axes.Add(linearAxis2);

            plot1.InvalidatePlot(true);
        }

        private void reset_minmax()
        {
            x_min = double.MaxValue;
            x_max = double.MinValue;
            y_min = double.MaxValue;
            y_max = double.MinValue;
        }

        private void update_minmax(double x, double y)
        {
            if (x < x_min) x_min = x;
            if (x > x_max) x_max = x;
            if (y < y_min) y_min = y;
            if (y > y_max) y_max = y;
        }

        private void plot_xcounts_vs_time(ScatterSeries scatterSeries1, StemSeries stemSeries1, LineSeries lineSeries1)
        {
            xlabel = "Time (ms)";
            ylabel = "xCounts";
            reset_minmax();
            for (int i = last_start; i <= last_end; i++)
            {
                double x = this.mlog.Events[i].ts;
                double y = this.mlog.Events[i].lastx;
                update_minmax(x, y);
                scatterSeries1.Points.Add(new ScatterPoint(x, y));
                lineSeries1.Points.Add(new DataPoint(x, y));
                stemSeries1.Points.Add(new DataPoint(x, y));
            }
        }

        private void plot_ycounts_vs_time(ScatterSeries scatterSeries1, StemSeries stemSeries1, LineSeries lineSeries1)
        {
            xlabel = "Time (ms)";
            ylabel = "yCounts";
            reset_minmax();
            for (int i = last_start; i <= last_end; i++)
            {
                double x = this.mlog.Events[i].ts;
                double y = this.mlog.Events[i].lasty;
                update_minmax(x, y);
                scatterSeries1.Points.Add(new ScatterPoint(x, y));
                lineSeries1.Points.Add(new DataPoint(x, y));
                stemSeries1.Points.Add(new DataPoint(x, y));
            }
        }

        private void plot_xycounts_vs_time(ScatterSeries scatterSeries1, ScatterSeries scatterSeries2, StemSeries stemSeries1, StemSeries stemSeries2, LineSeries lineSeries1, LineSeries lineSeries2)
        {
            xlabel = "Time (ms)";
            ylabel = "Counts [x = Blue, y = Red]";
            reset_minmax();
            for (int i = last_start; i <= last_end; i++)
            {
                double x = this.mlog.Events[i].ts;
                double y = this.mlog.Events[i].lastx;
                update_minmax(x, y);
                scatterSeries1.Points.Add(new ScatterPoint(x, y));
                lineSeries1.Points.Add(new DataPoint(x, y));
                stemSeries1.Points.Add(new DataPoint(x, y));
            }

            for (int i = last_start; i <= last_end; i++)
            {
                double x = this.mlog.Events[i].ts;
                double y = this.mlog.Events[i].lasty;
                update_minmax(x, y);
                scatterSeries2.Points.Add(new ScatterPoint(x, y));
                lineSeries2.Points.Add(new DataPoint(x, y));
                stemSeries2.Points.Add(new DataPoint(x, y));
            }
        }

        private void plot_interval_vs_time(ScatterSeries scatterSeries1, StemSeries stemSeries1, LineSeries lineSeries1, bool isInterval, Func<double, double> transformFunction)
        {
            xlabel = "Time (ms)";

            double firstPercentileMetric;
            double secondPercentileMetric;

            if (isInterval) {
                ylabel = "Update Time (ms)";
                firstPercentileMetric = 99;
                firstPercentileMetricLabel.Text = "99 Percentile:";
                secondPercentileMetric = 99.9;
                secondPercentileMetricLabel.Text = "99.9 Percentile:";
            } else {
                ylabel = "Frequency (Hz)";
                firstPercentileMetric = 1;
                firstPercentileMetricLabel.Text = "1 Percentile:";
                secondPercentileMetric = 0.1;
                secondPercentileMetricLabel.Text = "0.1 Percentile:";
            }

            List<double> intervals = new List<double>();
            reset_minmax();
            for (int i = last_start; i <= last_end; i++)
            {
                double x = this.mlog.Events[i].ts;
                double y;
                if (i == 0)
                {
                    y = 0.0;
                }
                else
                {
                    y = this.mlog.Events[i].ts - this.mlog.Events[i - 1].ts;
                }
                intervals.Add(y);
                update_minmax(x, transformFunction(y));
                scatterSeries1.Points.Add(new ScatterPoint(x, transformFunction(y)));
                lineSeries1.Points.Add(new DataPoint(x, transformFunction(y)));
                stemSeries1.Points.Add(new DataPoint(x, transformFunction(y)));
            }

            // Calculate statistics

            List<double> intervals_descending = intervals.OrderByDescending(x => x).ToList();
            List<double> intervals_ascending = intervals.OrderBy(x => x).ToList();

            double sum = intervals_descending.Sum();
            int count = intervals_descending.Count();
            double average = transformFunction(sum / count);
            double squared_deviations = 0.0;
            int middle_index = count / 2;
            int last_index = count - 1;
            double range = intervals_descending[0] - intervals_descending[last_index];

            foreach (double interval in intervals)
            {
                squared_deviations += Math.Pow(transformFunction(interval) - average, 2);
            }
            double maximumValue = transformFunction(isInterval ? intervals_descending[0] : intervals_descending[last_index]);
            double minimumValue = transformFunction(isInterval ? intervals_descending[last_index] : intervals_descending[0]);

            maxInterval.Text = $"{maximumValue:0.0000####}";
            minInterval.Text = $"{minimumValue:0.0000####}";
            avgInterval.Text = $"{average:0.0000####}";
            stdevInterval.Text = $"{Math.Sqrt(squared_deviations / (last_index)):0.0000####}";
            rangeInterval.Text = $"{maximumValue - minimumValue:0.0000####}";
            medianInterval.Text = $"{(transformFunction(count % 2 == 1 ? intervals_descending[middle_index] : (intervals_descending[middle_index - 1] + intervals_descending[middle_index]) / 2)):0.0000####}";

            List<double> percentiles_list = isInterval ? intervals_ascending : intervals_descending;

            firstPercentileInterval.Text = $"{transformFunction(percentiles_list[(int) Math.Ceiling(firstPercentileMetric / 100 * count) - 1]):0.0000####}";
            secondPercentileInterval.Text = $"{transformFunction(percentiles_list[(int)Math.Ceiling(secondPercentileMetric / 100 * count) - 1]):0.0000####}";
        }

        private void plot_xvelocity_vs_time(ScatterSeries scatterSeries1, StemSeries stemSeries1, LineSeries lineSeries1)
        {
            xlabel = "Time (ms)";
            ylabel = "xVelocity (m/s)";
            reset_minmax();
            if (this.mlog.Cpi > 0)
            {
                for (int i = last_start; i <= last_end; i++)
                {
                    double x = this.mlog.Events[i].ts;
                    double y;
                    if (i == 0)
                    {
                        y = 0.0;
                    }
                    else
                    {
                        y = (this.mlog.Events[i].lastx) / (this.mlog.Events[i].ts - this.mlog.Events[i - 1].ts) / this.mlog.Cpi * 25.4;
                    }
                    update_minmax(x, y);
                    scatterSeries1.Points.Add(new ScatterPoint(x, y));
                    lineSeries1.Points.Add(new DataPoint(x, y));
                    stemSeries1.Points.Add(new DataPoint(x, y));
                }
            }
            else
            {
                MessageBox.Show("CPI value is invalid, please run Measure");
            }
        }

        private void plot_yvelocity_vs_time(ScatterSeries scatterSeries1, StemSeries stemSeries1, LineSeries lineSeries1)
        {
            xlabel = "Time (ms)";
            ylabel = "yVelocity (m/s)";
            reset_minmax();
            if (this.mlog.Cpi > 0)
            {
                for (int i = last_start; i <= last_end; i++)
                {
                    double x = this.mlog.Events[i].ts;
                    double y;
                    if (i == 0)
                    {
                        y = 0.0;
                    }
                    else
                    {
                        y = (this.mlog.Events[i].lasty) / (this.mlog.Events[i].ts - this.mlog.Events[i - 1].ts) / this.mlog.Cpi * 25.4;
                    }
                    update_minmax(x, y);
                    scatterSeries1.Points.Add(new ScatterPoint(x, y));
                    lineSeries1.Points.Add(new DataPoint(x, y));
                    stemSeries1.Points.Add(new DataPoint(x, y));
                }
            }
            else
            {
                MessageBox.Show("CPI value is invalid, please run Measure");
            }
        }

        private void plot_xyvelocity_vs_time(ScatterSeries scatterSeries1, ScatterSeries scatterSeries2, StemSeries stemSeries1, StemSeries stemSeries2, LineSeries lineSeries1, LineSeries lineSeries2)
        {
            xlabel = "Time (ms)";
            ylabel = "Velocity (m/s) [x = Blue, y = Red]";
            reset_minmax();
            if (this.mlog.Cpi > 0)
            {
                for (int i = last_start; i <= last_end; i++)
                {
                    double x = this.mlog.Events[i].ts;
                    double y;
                    if (i == 0)
                    {
                        y = 0.0;
                    }
                    else
                    {
                        y = (this.mlog.Events[i].lastx) / (this.mlog.Events[i].ts - this.mlog.Events[i - 1].ts) / this.mlog.Cpi * 25.4;
                    }
                    update_minmax(x, y);
                    scatterSeries1.Points.Add(new ScatterPoint(x, y));
                    lineSeries1.Points.Add(new DataPoint(x, y));
                    stemSeries1.Points.Add(new DataPoint(x, y));
                }

                for (int i = last_start; i <= last_end; i++)
                {
                    double x = this.mlog.Events[i].ts;
                    double y;
                    if (i == 0)
                    {
                        y = 0.0;
                    }
                    else
                    {
                        y = (this.mlog.Events[i].lasty) / (this.mlog.Events[i].ts - this.mlog.Events[i - 1].ts) / this.mlog.Cpi * 25.4;
                    }
                    update_minmax(x, y);
                    scatterSeries2.Points.Add(new ScatterPoint(x, y));
                    lineSeries2.Points.Add(new DataPoint(x, y));
                    stemSeries2.Points.Add(new DataPoint(x, y));
                }
            }
            else
            {
                MessageBox.Show("CPI value is invalid, please run Measure");
            }
        }

        private void plot_x_vs_y(ScatterSeries scatterSeries1, LineSeries lineSeries1)
        {
            xlabel = "xCounts";
            ylabel = "yCounts";
            reset_minmax();
            double x = 0.0;
            double y = 0.0;
            for (int i = last_start; i <= last_end; i++)
            {
                x += this.mlog.Events[i].lastx;
                y += this.mlog.Events[i].lasty;
                update_minmax(x, x);
                update_minmax(y, y);
                scatterSeries1.Points.Add(new ScatterPoint(x, y));
                lineSeries1.Points.Add(new DataPoint(x, y));
            }
        }

        // Time based smoothing
        private void plot_fit(ScatterSeries scatterSeries1, LineSeries lineSeries1)
        {
            if (scatterSeries1.Points.Count == 0) return;

            double hz = 125;
            double ms = 1000.0 / hz;
            lineSeries1.Points.Clear();

            int ind = 0;
            for (double x = ms; x <= scatterSeries1.Points[scatterSeries1.Points.Count - 1].X; x += ms)
            {
                double sum = 0.0;
                int count = 0;
                while (scatterSeries1.Points[ind].X <= x)
                {
                    sum += scatterSeries1.Points[ind++].Y;
                    count++;
                    if (ind >= scatterSeries1.Points.Count) break;
                }
                if (count > 0)
                    lineSeries1.Points.Add(new DataPoint(x - (ms / 2.0), sum / count));
                if (ind >= scatterSeries1.Points.Count) break;
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            refresh_plot();
        }

        private void numericUpDownStart_ValueChanged(object sender, EventArgs e)
        {
            if (numericUpDownStart.Value >= numericUpDownEnd.Value)
            {
                numericUpDownStart.Value = (decimal)last_start_time;
            }
            else
            {
                last_start_time = (double)numericUpDownStart.Value;
                refresh_plot();
            }
        }

        private void numericUpDownEnd_ValueChanged(object sender, EventArgs e)
        {
            if (numericUpDownEnd.Value <= numericUpDownStart.Value)
            {
                numericUpDownEnd.Value = (decimal)last_end_time;
            }
            else
            {
                last_end_time = (double)numericUpDownEnd.Value;
                refresh_plot();
            }
        }

        private void numericUpDownCpi_ValueChanged(object sender, EventArgs e)
        {
            if (suppress_refresh) return;
            this.mlog.Cpi = (double)numericUpDownCpi.Value;
            refresh_plot();
        }

        private void checkBoxStem_CheckedChanged(object sender, EventArgs e)
        {
            refresh_plot();
        }

        private void checkBoxLines_CheckedChanged(object sender, EventArgs e)
        {
            refresh_plot();
        }

        private void buttonSavePNG_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "PNG Files (*.png)|*.png";
            saveFileDialog1.FilterIndex = 1;
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                Bitmap bitmap = new Bitmap(splitContainer1.Width, splitContainer1.Height);

                using (Graphics graphics = Graphics.FromImage(bitmap)) {
                    graphics.Clear(DarkTheme.WindowBg);
                    splitContainer1.DrawToBitmap(bitmap, new Rectangle(0, 0, splitContainer1.Width, splitContainer1.Height));
                }

                bitmap.Save(saveFileDialog1.FileName, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
