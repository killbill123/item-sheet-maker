using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ItemSheetMaker
{
    static class Gfx
    {
        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath(); float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
        public static void Hq(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
        /// <summary>The app mark: dark tile, white ISM, blue underline.</summary>
        public static void Badge(Graphics g, RectangleF r)
        {
            using (var p = Round(r, r.Width * 0.2f)) using (var b = new SolidBrush(Color.FromArgb(0x1B, 0x1B, 0x1B))) g.FillPath(b, p);
            using (var p = Round(r, r.Width * 0.2f)) using (var pen = new Pen(Color.FromArgb(0x55, 0x55, 0x55), 1f)) g.DrawPath(pen, p);
            using (var f = new Font("Segoe UI", r.Width * 0.32f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString("ISM", f, Brushes.White, new RectangleF(r.X, r.Y - r.Height * 0.05f, r.Width, r.Height), sf);
            using (var b = new SolidBrush(MainForm.Blue))
                g.FillRectangle(b, r.X + r.Width * 0.24f, r.Bottom - r.Height * 0.25f, r.Width * 0.52f, Math.Max(2f, r.Height * 0.06f));
        }
    }

    /// <summary>Top application bar.</summary>
    class AppBar : Control
    {
        public AppBar() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Gfx.Hq(g);
            g.Clear(MainForm.Chrome);
            Gfx.Badge(g, new RectangleF(8, 6, Height - 12, Height - 12));
            float x = Height + 4;
            using (var f = new Font("Segoe UI Semibold", 10f)) using (var b = new SolidBrush(MainForm.Text1))
                g.DrawString("Item Sheet Maker", f, b, x, 10);
            using (var f = new Font("Segoe UI", 9f)) using (var b = new SolidBrush(MainForm.Text2))
            {
                g.DrawString("Product photo sheets  ·  PDF  ·  JPEG  ·  Print", f, b, x + 134, 11);
                var v = "Version " + Application.ProductVersion;
                var sz = g.MeasureString(v, f);
                g.DrawString(v, f, b, Width - sz.Width - 14, (Height - sz.Height) / 2);
            }
            using (var pen = new Pen(MainForm.Line)) g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }

    /// <summary>Document tab above the canvas.</summary>
    class DocTab : Control
    {
        public DocTab() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Gfx.Hq(g);
            g.Clear(MainForm.Chrome);
            using (var f = new Font("Segoe UI", 8.75f))
            {
                var sz = g.MeasureString(Text, f);
                var tab = new RectangleF(0, 3, sz.Width + 46, Height - 3);
                using (var b = new SolidBrush(MainForm.Panel)) g.FillRectangle(b, tab);
                using (var b = new SolidBrush(MainForm.Blue)) g.FillRectangle(b, tab.X, tab.Y, tab.Width, 2);
                using (var b = new SolidBrush(MainForm.Text1)) g.DrawString(Text, f, b, 12, 3 + (Height - 3 - sz.Height) / 2);
                using (var pen = new Pen(MainForm.Text2, 1.2f))
                {
                    float cx = tab.Right - 18, cy = 3 + (Height - 3) / 2f;
                    g.DrawLine(pen, cx - 3, cy - 3, cx + 3, cy + 3); g.DrawLine(pen, cx - 3, cy + 3, cx + 3, cy - 3);
                }
            }
        }
    }

    /// <summary>Panel section title with a chevron and divider, like a properties panel group.</summary>
    class PanelHeader : Control
    {
        public PanelHeader() { DoubleBuffered = true; }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Gfx.Hq(g);
            g.Clear(MainForm.Panel);
            using (var pen = new Pen(MainForm.Line)) g.DrawLine(pen, 0, 0, Width, 0);
            using (var b = new SolidBrush(MainForm.Text2))
                g.FillPolygon(b, new[] { new PointF(14, 13), new PointF(22, 13), new PointF(18, 18) });
            using (var f = new Font("Segoe UI", 9f, FontStyle.Bold)) using (var b = new SolidBrush(MainForm.Text1))
            {
                var sz = g.MeasureString(Text, f);
                g.DrawString(Text, f, b, 28, (Height - sz.Height) / 2 + 1);
            }
        }
    }

    /// <summary>Vertical tool strip with drawn icons.</summary>
    class ToolRail : Control
    {
        public enum Icon { Folder, Plus, Grid, Pdf, Image, Printer }
        class Tool { public string Tip; public Icon Ic; public Action Run; public bool Sep; }
        readonly List<Tool> tools = new List<Tool>();
        readonly ToolTip tip = new ToolTip { InitialDelay = 300 };
        int hover = -1;
        const int Top0 = 10, Size = 36, SepH = 14;

        public ToolRail() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); Cursor = Cursors.Hand; }
        public void Add(string t, Icon i, Action a) { tools.Add(new Tool { Tip = t, Ic = i, Run = a }); Invalidate(); }
        public void AddSeparator() { tools.Add(new Tool { Sep = true }); }

        RectangleF Rect(int i)
        {
            float y = Top0;
            for (int k = 0; k < i; k++) y += tools[k].Sep ? SepH : Size + 4;
            return new RectangleF((Width - Size) / 2f, y, Size, Size);
        }
        int HitTest(Point p)
        {
            for (int i = 0; i < tools.Count; i++) if (!tools[i].Sep && Rect(i).Contains(p)) return i;
            return -1;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h != hover) { hover = h; Invalidate(); if (h >= 0) tip.SetToolTip(this, tools[h].Tip); else tip.SetToolTip(this, ""); }
        }
        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }
        protected override void OnMouseClick(MouseEventArgs e) { int h = HitTest(e.Location); if (h >= 0 && tools[h].Run != null) tools[h].Run(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Gfx.Hq(g);
            g.Clear(MainForm.Panel);
            using (var pen = new Pen(MainForm.Line)) g.DrawLine(pen, Width - 1, 0, Width - 1, Height);
            using (var grip = new SolidBrush(MainForm.Hover)) g.FillRectangle(grip, 12, 4, Width - 24, 2);
            for (int i = 0; i < tools.Count; i++)
            {
                var r = Rect(i);
                if (tools[i].Sep)
                {
                    using (var pen = new Pen(MainForm.Hover)) g.DrawLine(pen, 10, r.Y + SepH / 2f - 2, Width - 10, r.Y + SepH / 2f - 2);
                    continue;
                }
                if (i == hover) using (var p = Gfx.Round(r, 4)) using (var b = new SolidBrush(MainForm.Hover)) g.FillPath(b, p);
                DrawIcon(g, tools[i].Ic, r, i == hover ? Color.White : Color.FromArgb(0xC8, 0xC8, 0xC8));
            }
        }

        static void DrawIcon(Graphics g, Icon ic, RectangleF r, Color c)
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            using (var pen = new Pen(c, 1.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                switch (ic)
                {
                    case Icon.Folder:
                        g.DrawLines(pen, new[] { new PointF(cx - 9, cy + 7), new PointF(cx - 9, cy - 6), new PointF(cx - 3, cy - 6),
                            new PointF(cx - 1, cy - 3), new PointF(cx + 9, cy - 3), new PointF(cx + 9, cy + 7), new PointF(cx - 9, cy + 7) });
                        break;
                    case Icon.Plus:
                        g.DrawRectangle(pen, cx - 8, cy - 8, 16, 16);
                        g.DrawLine(pen, cx, cy - 4, cx, cy + 4); g.DrawLine(pen, cx - 4, cy, cx + 4, cy);
                        break;
                    case Icon.Grid:
                        for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++) g.DrawRectangle(pen, cx - 8 + i * 9, cy - 8 + j * 9, 7, 7);
                        break;
                    case Icon.Pdf:
                        g.DrawLines(pen, new[] { new PointF(cx - 7, cy - 9), new PointF(cx + 3, cy - 9), new PointF(cx + 7, cy - 5),
                            new PointF(cx + 7, cy + 9), new PointF(cx - 7, cy + 9), new PointF(cx - 7, cy - 9) });
                        using (var f = new Font("Arial", 5.5f, FontStyle.Bold)) using (var b = new SolidBrush(c))
                        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            g.DrawString("PDF", f, b, new RectangleF(cx - 10, cy - 3, 20, 10), sf);
                        break;
                    case Icon.Image:
                        g.DrawRectangle(pen, cx - 9, cy - 7, 18, 14);
                        g.DrawLines(pen, new[] { new PointF(cx - 7, cy + 5), new PointF(cx - 2, cy - 1), new PointF(cx + 2, cy + 3), new PointF(cx + 4, cy + 1), new PointF(cx + 7, cy + 5) });
                        g.DrawEllipse(pen, cx + 2, cy - 5, 3, 3);
                        break;
                    case Icon.Printer:
                        g.DrawRectangle(pen, cx - 5, cy - 9, 10, 5);
                        g.DrawRectangle(pen, cx - 9, cy - 4, 18, 8);
                        g.DrawRectangle(pen, cx - 5, cy + 1, 10, 7);
                        break;
                }
            }
        }
    }

    /// <summary>Artboard on the pasteboard: page centered with a label and drop shadow.</summary>
    class PagePreview : Control
    {
        Image img;
        string label = "01 - Artboard 1  (Letter)";
        public string Label { get { return label; } set { if (value != label) { label = value; Invalidate(); } } }
        public PagePreview() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
        public void SetPage(Image i) { var old = img; img = i; Invalidate(); if (old != null) old.Dispose(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Gfx.Hq(g);
            g.Clear(BackColor);
            if (img == null) return;
            float pad = 40, aw = Width - 2 * pad, ah = Height - 2 * pad - 10;
            if (aw < 10 || ah < 10) return;
            float sc = Math.Min(aw / img.Width, ah / img.Height);
            float w = img.Width * sc, h = img.Height * sc;
            var r = new RectangleF((Width - w) / 2, (Height - h) / 2 + 8, w, h);
            using (var f = new Font("Segoe UI", 8.25f)) using (var b = new SolidBrush(Color.FromArgb(0xD0, 0xD0, 0xD0)))
                g.DrawString(label, f, b, r.X, r.Y - 18);
            using (var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillRectangle(b, r.X + 4, r.Y + 4, r.Width, r.Height);
            g.DrawImage(img, r);
            using (var pen = new Pen(Color.FromArgb(0x2A, 0x2A, 0x2A))) g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
        }
    }
}
