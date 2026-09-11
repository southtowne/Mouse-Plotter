// SPDX-License-Identifier: MIT
// Ported from MouseTester (https://github.com/valleyofdoom/MouseTester),
// originally (c) 2013 microe1.
//
// Load() is extended to also accept the 5-column CSV that MousePlotter's
// Windows GUI logger writes once it has paired ETW kernel timestamps onto
// the Raw Input samples: xCount,yCount,eventTime (ms),userTime (ms),
// timestampSource. The plain 3-column capture MousePlotter writes without
// kernel pairing, and MouseTester's own 3/4-column format, are unchanged.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MousePlotter
{
    public class MouseLog
    {
        private string desc = "MousePlotter";
        private double cpi = 400.0;
        private List<MouseEvent> events = new List<MouseEvent>();

        public double Cpi
        {
            get => this.cpi;
            set => this.cpi = value;
        }

        public string Desc
        {
            get => this.desc;
            set => this.desc = value;
        }

        public List<MouseEvent> Events => this.events;

        public void Add(MouseEvent e) => this.events.Add(e);

        public void Clear() => this.events.Clear();

        public void Load(string fname)
        {
            this.Clear();

            try
            {
                using (StreamReader sr = File.OpenText(fname))
                {
                    this.desc = sr.ReadLine();
                    this.cpi = double.Parse(sr.ReadLine(), CultureInfo.InvariantCulture);
                    sr.ReadLine(); // header line; column names vary by source, values don't
                    while (sr.Peek() > -1)
                    {
                        string line = sr.ReadLine();
                        if (string.IsNullOrEmpty(line))
                            continue;
                        string[] values = line.Split(',');
                        if (values.Length == 4)
                        {
                            // MouseTester's own save format: xCount,yCount,Time (ms),buttonflags
                            this.Add(new MouseEvent(
                                ushort.Parse(values[3], CultureInfo.InvariantCulture),
                                int.Parse(values[0], CultureInfo.InvariantCulture),
                                int.Parse(values[1], CultureInfo.InvariantCulture),
                                double.Parse(values[2], CultureInfo.InvariantCulture)));
                        }
                        else if (values.Length == 5)
                        {
                            // MousePlotter kernel-paired format:
                            // xCount,yCount,eventTime (ms),userTime (ms),timestampSource
                            this.Add(new MouseEvent(
                                0,
                                int.Parse(values[0], CultureInfo.InvariantCulture),
                                int.Parse(values[1], CultureInfo.InvariantCulture),
                                double.Parse(values[2], CultureInfo.InvariantCulture)));
                        }
                        else if (values.Length == 3)
                        {
                            // MousePlotter Raw-Input-only format: xCount,yCount,Time (ms)
                            this.Add(new MouseEvent(
                                0,
                                int.Parse(values[0], CultureInfo.InvariantCulture),
                                int.Parse(values[1], CultureInfo.InvariantCulture),
                                double.Parse(values[2], CultureInfo.InvariantCulture)));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        public void Save(string fname)
        {
            try
            {
                using (StreamWriter sw = File.CreateText(fname))
                {
                    sw.WriteLine(this.desc);
                    sw.WriteLine(this.cpi.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("xCount,yCount,Time (ms),buttonflags");
                    foreach (MouseEvent e in this.events)
                    {
                        sw.WriteLine(e.lastx.ToString(CultureInfo.InvariantCulture) + "," +
                                     e.lasty.ToString(CultureInfo.InvariantCulture) + "," +
                                     e.ts.ToString(CultureInfo.InvariantCulture) + "," +
                                     e.buttonflags.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        public int deltaX() => this.events.Sum(e => e.lastx);

        public int deltaY() => this.events.Sum(e => e.lasty);

        public double path() => this.events.Sum(e => Math.Sqrt((e.lastx * e.lastx) + (e.lasty * e.lasty)));
    }
}
