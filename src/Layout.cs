using System;
using System.Drawing;
using System.Windows.Forms;

namespace SILocalAI
{
    // Text rows ask for the height they need at their allocated column width.
    public sealed class WrappingLabel : Label
    {
        readonly System.Collections.Generic.Dictionary<int, Size> measuredSizes = new System.Collections.Generic.Dictionary<int, Size>();
        string measuredText;
        Font measuredFont;
        Padding measuredPadding;
        public WrappingLabel() { AutoSize = true; AutoEllipsis = false; UseMnemonic = false; }
        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = proposedSize.Width;
            if (width <= 1 || width >= 100000) width = Width > 1 ? Width : 280;
            if (measuredText != Text || measuredFont != Font || measuredPadding != Padding)
            { measuredSizes.Clear(); measuredText = Text; measuredFont = Font; measuredPadding = Padding; }
            Size cached;
            if (measuredSizes.TryGetValue(width, out cached)) return cached;
            int inner = Math.Max(1, width - Padding.Horizontal);
            var measured = TextRenderer.MeasureText(String.IsNullOrEmpty(Text) ? " " : Text,
                Font, new Size(inner, Int32.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
            var result = new Size(Math.Min(width, measured.Width + Padding.Horizontal), measured.Height + Padding.Vertical + 2);
            if (measuredSizes.Count >= 16) measuredSizes.Clear(); measuredSizes[width] = result; return result;
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
            content = child; child.AutoSize = false; child.Dock = DockStyle.None; child.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            Controls.Add(child); child.Layout += delegate { if (!arranging) PerformLayout(); };
            PerformLayout();
        }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible) PerformLayout(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); PerformLayout(); }
        protected override void OnLayout(LayoutEventArgs e)
        {
            if (arranging) return;
            arranging = true;
            try
            {
                base.OnLayout(e);
                if (content == null || !IsHandleCreated || !Visible) return;
                // Always reserve the scrollbar gutter: adding a scrollbar cannot make text oscillate.
                int width = Math.Max(1, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
                if (content.Width != width) content.Width = width;
                int height = content.GetPreferredSize(new Size(width, 0)).Height;
                content.SetBounds(Padding.Left + Math.Min(0, AutoScrollPosition.X), Padding.Top + Math.Min(0, AutoScrollPosition.Y), width, height);
                var scrollSize = new Size(0, height + Padding.Vertical);
                if (AutoScrollMinSize != scrollSize) AutoScrollMinSize = scrollSize;
                // Reconcile native scrollbars after changing child bounds (including window shrink).
                base.OnLayout(e);
            }
            finally { arranging = false; }
        }
    }
    // Navigation owns fixed viewport bounds. Hidden pages never influence the window height.
    public sealed class WorkspaceTabs : Panel
    {
        readonly FlowLayoutPanel navigation;
        readonly Panel host;
        readonly System.Collections.Generic.List<Control> pages = new System.Collections.Generic.List<Control>();
        readonly System.Collections.Generic.List<ActionButton> buttons = new System.Collections.Generic.List<ActionButton>();
        int selected = -1;
        public WorkspaceTabs()
        {
            Dock = DockStyle.Fill; Margin = new Padding(0); BackColor = Color.White;
            host = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            navigation = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(24, 4, 0, 4), WrapContents = false, BackColor = Color.White };
            Controls.Add(host); Controls.Add(navigation);
        }
        public void AddPage(string title, Control page)
        {
            int index = pages.Count;
            page.Dock = DockStyle.Fill; page.Visible = false; pages.Add(page); host.Controls.Add(page);
            var button = new ActionButton { Text = title, AutoSize = true, MinimumSize = new Size(84, 40), Margin = new Padding(0, 0, 8, 0) };
            button.Click += delegate { SelectedIndex = index; }; buttons.Add(button); navigation.Controls.Add(button);
            if (selected < 0) SelectedIndex = 0;
        }
        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (value < 0 || value >= pages.Count || value == selected) return;
                host.SuspendLayout();
                if (selected >= 0) { pages[selected].Visible = false; buttons[selected].Primary = false; buttons[selected].Invalidate(); }
                selected = value; pages[selected].Visible = true; pages[selected].BringToFront(); buttons[selected].Primary = true; buttons[selected].Invalidate();
                host.ResumeLayout(true);
            }
        }
        public Control SelectedTab { get { return selected < 0 ? null : pages[selected]; } set { SelectedIndex = pages.IndexOf(value); } }
    }

}
