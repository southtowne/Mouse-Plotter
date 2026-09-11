// SPDX-License-Identifier: MIT
// Ported from MouseTester (https://github.com/valleyofdoom/MouseTester),
// originally (c) 2013 microe1.

namespace MousePlotter
{
    public class MouseEvent
    {
        public ushort buttonflags;
        public int lastx;
        public int lasty;
        public long pcounter;
        public double ts;

        public MouseEvent(ushort buttonflags, int lastx, int lasty, long pcounter)
        {
            this.buttonflags = buttonflags;
            this.lastx = lastx;
            this.lasty = lasty;
            this.pcounter = pcounter;
            this.ts = 0.0;
        }

        public MouseEvent(ushort buttonflags, int lastx, int lasty, double ts)
        {
            this.buttonflags = buttonflags;
            this.lastx = lastx;
            this.lasty = lasty;
            this.pcounter = 0;
            this.ts = ts;
        }
    }
}
