using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SILocalAI
{
    public static class Theme
    {
        public static readonly Color Ink = Color.FromArgb(26, 32, 44), Muted = Color.FromArgb(103, 118, 136);
        public static readonly Color Background = Color.FromArgb(245, 246, 248), Line = Color.FromArgb(224, 230, 238);
        public static readonly Color Accent = Color.FromArgb(34, 91, 214), Pale = Color.FromArgb(234, 240, 255);
        public static readonly Color Danger = Color.FromArgb(174, 62, 57), Amber = Color.FromArgb(155, 102, 22);
        public static GraphicsPath Round(Rectangle rect, int radius)
        {
            var p = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            if (d <= 0) { p.AddRectangle(rect); return p; }
            p.AddArc(rect.X, rect.Y, d, d, 180, 90); p.AddArc(rect.Right-d, rect.Y, d, d, 270, 90);
            p.AddArc(rect.Right-d, rect.Bottom-d, d, d, 0, 90); p.AddArc(rect.X, rect.Bottom-d, d, d, 90, 90); p.CloseFigure(); return p;
        }
        public static Label Text(string value, float size, bool bold)
        {
            return new WrappingLabel { Text = value, Dock = DockStyle.Top,
                Font = new Font("Malgun Gothic", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = Ink,
                TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) };
        }
    }
    public sealed class Surface : Panel
    {
        public Color Edge = Theme.Line;
        public Surface() { DoubleBuffered = true; BackColor = Color.White; Padding = new Padding(16); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.Round(new Rectangle(0, 0, Width-1, Height-1), 8))
            using (var pen = new Pen(Edge)) e.Graphics.DrawPath(pen, path);
        }
    }
    public sealed class ActionButton : Button
    {
        public bool Primary;
        bool over;
        public ActionButton()
        {
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand;
            BackColor = Color.White; ForeColor = Theme.Ink; Font = new Font("Malgun Gothic", 10F);
            Height = 38; MinimumSize = new Size(60, 36); Padding = new Padding(12, 7, 12, 7); Margin = new Padding(0, 0, 8, 0);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        public override Size GetPreferredSize(Size proposedSize)
        {
            var text = TextRenderer.MeasureText(Text, Font, new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            return new Size(Math.Max(MinimumSize.Width, text.Width + Padding.Horizontal + 8), Math.Max(MinimumSize.Height, text.Height + Padding.Vertical + 4));
        }
        protected override void OnMouseEnter(EventArgs e) { over = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { over = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Control ancestor = Parent;
            while (ancestor != null && ancestor.BackColor.A < 255) ancestor = ancestor.Parent;
            e.Graphics.Clear(ancestor == null ? Theme.Background : ancestor.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = !Enabled ? Color.FromArgb(235,239,243) : Primary ? (over ? Color.FromArgb(16, 81, 147) : Theme.Accent) : over ? Theme.Background : Color.White;
            Color ink = !Enabled ? Theme.Muted : Primary ? Color.White : ForeColor;
            using (var path = Theme.Round(new Rectangle(0,0,Width-1,Height-1),6))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(Primary && Enabled ? fill : Theme.Line))
            { e.Graphics.FillPath(brush,path); e.Graphics.DrawPath(pen,path); }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5));
        }
    }
    public sealed class Badge : Label
    {
        public Badge() { AutoSize = false; TextAlign = ContentAlignment.MiddleCenter; ForeColor = Theme.Accent; BackColor = Theme.Pale; Padding = new Padding(8,0,8,0); }
    }
}
