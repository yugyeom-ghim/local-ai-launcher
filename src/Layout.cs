using System;
using System.Drawing;
using System.Windows.Forms;

namespace SILocalAI
{
    // Text rows ask for the height they need at their allocated column width.
    public sealed class WrappingLabel : Label
    {
        public WrappingLabel() { AutoSize = true; AutoEllipsis = false; UseMnemonic = false; }
        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = proposedSize.Width;
            if (width <= 1 || width >= 100000) width = Width > 1 ? Width : 280;
            int inner = Math.Max(1, width - Padding.Horizontal);
            var measured = TextRenderer.MeasureText(String.IsNullOrEmpty(Text) ? " " : Text,
                Font, new Size(inner, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
            return new Size(Math.Min(width, measured.Width + Padding.Horizontal), measured.Height + Padding.Vertical + 2);
        }
    }

    // Switch complete cards to one column without assigning percentage heights.
    public sealed class ResponsiveColumns : TableLayoutPanel
    {
        readonly float[] weights;
        readonly int breakpoint;
        bool changing;
        int activeColumns = -1;
        float scale = 1F;
        public ResponsiveColumns(int collapseBelow, params float[] columnWeights)
        {
            breakpoint = collapseBelow; weights = columnWeights;
            Dock = DockStyle.Top; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Margin = new Padding(0); BackColor = Color.Transparent;
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            int columns = Width < breakpoint * scale ? 1 : weights.Length;
            if (!changing && (columns != activeColumns || RowCount != (Controls.Count + columns - 1) / columns))
            {
                changing = true; SuspendLayout();
                try
                {
                    activeColumns = columns;
                    ColumnCount = columns; RowCount = Math.Max(1, (Controls.Count + columns - 1) / columns);
                    ColumnStyles.Clear(); RowStyles.Clear();
                    for (int i = 0; i < columns; i++) ColumnStyles.Add(new ColumnStyle(SizeType.Percent, columns == 1 ? 100 : weights[i]));
                    for (int i = 0; i < RowCount; i++) RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    for (int i = 0; i < Controls.Count; i++) SetCellPosition(Controls[i], new TableLayoutPanelCellPosition(i % columns, i / columns));
                }
                finally { ResumeLayout(false); changing = false; }
            }
            base.OnLayout(e);
        }
        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            scale *= factor.Width;
            base.ScaleControl(factor, specified);
        }
    }

    // A scrollable viewport owns the width. Content is free to grow vertically.
    public sealed class ContentViewport : Panel
    {
        Control content;
        bool arranging;
        public ContentViewport() { Dock = DockStyle.Fill; AutoScroll = true; Margin = new Padding(0); }
        public void SetContent(Control child)
        {
            content = child; child.Dock = DockStyle.None; child.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            Controls.Add(child); child.SizeChanged += delegate { if (!arranging) PerformLayout(); };
            PerformLayout();
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (content == null || arranging) return;
            arranging = true;
            try
            {
                // Always reserve the scrollbar gutter: adding a scrollbar cannot make text oscillate.
                int width = Math.Max(1, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                content.Width = width;
                int height = content.GetPreferredSize(new Size(width, 0)).Height;
                content.SetBounds(Padding.Left + Math.Min(0, AutoScrollPosition.X), Padding.Top + Math.Min(0, AutoScrollPosition.Y), width, height);
                AutoScrollMinSize = new Size(0, height + Padding.Vertical);
            }
            finally { arranging = false; }
        }
    }
}
