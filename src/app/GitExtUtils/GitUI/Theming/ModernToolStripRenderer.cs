using System.Drawing.Drawing2D;

namespace GitExtUtils.GitUI.Theming;

public sealed class ModernToolStripRenderer : ToolStripProfessionalRenderer
{
    private static readonly int CornerRadius = 5;

    private static readonly Color HoverBackground = Color.FromArgb(255, 50, 57, 70);
    private static readonly Color PressedBackground = Color.FromArgb(255, 36, 42, 54);
    private static readonly Color SeparatorColor = Color.FromArgb(255, 52, 59, 73);
    private static readonly Color AccentColor = Color.FromArgb(255, 77, 176, 80);
    private static readonly Color CheckedBackground = Color.FromArgb(255, 34, 68, 34);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        e.Graphics.Clear(SystemColors.Control);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        // No border
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        // No image margin stripe
    }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        ToolStripButton? btn = e.Item as ToolStripButton;

        Color? fill = null;
        if (btn?.Checked is true)
        {
            fill = CheckedBackground;
        }
        else if (e.Item.Pressed)
        {
            fill = PressedBackground;
        }
        else if (e.Item.Selected)
        {
            fill = HoverBackground;
        }

        if (fill is Color background)
        {
            Rectangle rect = new(1, 1, e.Item.Width - 2, e.Item.Height - 2);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using GraphicsPath path = RoundedRect(rect, CornerRadius);
            using SolidBrush brush = new(background);
            e.Graphics.FillPath(brush, path);

            if (btn?.Checked is true)
            {
                using Pen accentPen = new(AccentColor, 1.5f);
                e.Graphics.DrawPath(accentPen, path);
            }
        }
    }

    protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e) =>
        OnRenderButtonBackground(e);

    protected override void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs e) =>
        OnRenderButtonBackground(e);

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected)
        {
            return;
        }

        Rectangle rect = new(2, 1, e.Item.Width - 4, e.Item.Height - 2);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath path = RoundedRect(rect, CornerRadius);
        using SolidBrush brush = new(HoverBackground);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.Default;
        using Pen pen = new(SeparatorColor, 1f);
        if (e.Vertical)
        {
            int x = e.Item.Width / 2;
            e.Graphics.DrawLine(pen, x, 4, x, e.Item.Height - 4);
        }
        else
        {
            e.Graphics.DrawLine(pen, 8, e.Item.Height / 2, e.Item.Width - 8, e.Item.Height / 2);
        }
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        Rectangle r = e.ImageRectangle;
        r.Inflate(2, 2);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath path = RoundedRect(r, 3);
        using SolidBrush brush = new(CheckedBackground);
        e.Graphics.FillPath(brush, path);
        using Pen pen = new(AccentColor, 1f);
        e.Graphics.DrawPath(pen, path);
        base.OnRenderItemCheck(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Color.FromArgb(160, SystemColors.ControlText);
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? SystemColors.ControlText : SystemColors.GrayText;
        base.OnRenderItemText(e);
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        GraphicsPath path = new();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
