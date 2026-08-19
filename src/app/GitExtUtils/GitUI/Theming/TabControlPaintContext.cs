using System.Drawing.Drawing2D;

namespace GitExtUtils.GitUI.Theming;

internal sealed class TabControlPaintContext
{
    private static readonly Color AccentColor = Color.FromArgb(255, 77, 176, 80);
    private static readonly Color HoverBackground = Color.FromArgb(255, 50, 57, 70);
    private static readonly int IndicatorHeight = DpiUtil.Scale(3);
    private static readonly int IndicatorRadius = 2;
    private static readonly int ImagePadding = DpiUtil.Scale(6);

    private readonly Point _mouseCursor;
    private readonly Graphics _graphics;
    private readonly Rectangle _clipRectangle;
    private readonly Color _parentBackColor;
    private readonly int _selectedIndex;
    private readonly int _tabCount;
    private readonly Size _imageSize;
    private readonly Font _font;
    private readonly bool _enabled;
    private readonly Image?[] _tabImages;
    private readonly Rectangle[] _tabRects;
    private readonly string[] _tabTexts;
    private readonly Size _size;
    private readonly bool _failed;

    public TabControlPaintContext(TabControl tabs, PaintEventArgs e)
    {
        _mouseCursor = tabs.PointToClient(Cursor.Position);
        _graphics = e.Graphics;
        _clipRectangle = e.ClipRectangle;
        _size = tabs.Size;
        _parentBackColor = GetParentBackColor(tabs);
        _selectedIndex = tabs.SelectedIndex;
        _tabCount = tabs.TabCount;
        _font = tabs.Font;
        _imageSize = tabs.ImageList?.ImageSize ?? Size.Empty;
        _enabled = tabs.Enabled;

        try
        {
            _tabTexts = [.. Enumerable.Range(0, _tabCount).Select(i => tabs.TabPages[i].Text)];
            _tabImages = [.. Enumerable.Range(0, _tabCount).Select(i => GetTabImage(tabs, i))];
            _tabRects = [.. Enumerable.Range(0, _tabCount).Select(tabs.GetTabRect)];
        }
        catch (ArgumentOutOfRangeException)
        {
            _failed = true;
            _tabTexts = null!;
            _tabImages = null!;
            _tabRects = null!;
        }
    }

    public void Paint()
    {
        if (_failed)
        {
            return;
        }

        using SolidBrush canvasBrush = new(_parentBackColor);
        _graphics.FillRectangle(canvasBrush, _clipRectangle);

        RenderPageBackground();

        IEnumerable<int> order = _selectedIndex.IsWithin(0, _tabCount)
            ? Enumerable.Range(0, _selectedIndex)
                .Concat(Enumerable.Range(_selectedIndex, _tabCount - _selectedIndex).Reverse())
            : Enumerable.Range(0, _tabCount);

        foreach (int i in order)
        {
            RenderTab(i);
        }
    }

    private void RenderPageBackground()
    {
        if (!_selectedIndex.IsWithin(0, _tabCount))
        {
            return;
        }

        Rectangle tabRow = _tabRects[_selectedIndex];
        Rectangle pageRect = Rectangle.FromLTRB(0, tabRow.Bottom, _size.Width - 1, _size.Height - 1);

        if (!_clipRectangle.IntersectsWith(pageRect))
        {
            return;
        }

        using SolidBrush bg = new(SystemColors.Window);
        _graphics.FillRectangle(bg, pageRect);

        using Pen sep = new(Color.FromArgb(255, 52, 59, 73), 1);
        _graphics.DrawLine(sep, 0, tabRow.Bottom, _size.Width, tabRow.Bottom);
    }

    private void RenderTab(int index)
    {
        Rectangle r = _tabRects[index];
        bool isSelected = index == _selectedIndex;
        bool isHovered = r.Contains(_mouseCursor) && !isSelected;

        _graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (isHovered)
        {
            using SolidBrush hoverBrush = new(HoverBackground);
            _graphics.FillRectangle(hoverBrush, r);
        }

        RenderTabImage(index, r);
        RenderTabText(index, r, isSelected, isHovered);

        if (isSelected)
        {
            int y = r.Bottom - IndicatorHeight;
            Rectangle bar = new(r.Left + 4, y, r.Width - 8, IndicatorHeight);
            using GraphicsPath path = RoundedRect(bar, IndicatorRadius);
            using SolidBrush accent = new(AccentColor);
            _graphics.FillPath(accent, path);
        }
    }

    private void RenderTabImage(int index, Rectangle tabRect)
    {
        Image? image = _tabImages[index];
        if (image is null)
        {
            return;
        }

        int imgH = _imageSize.Height;
        Rectangle imgRect = new(
            tabRect.X + ImagePadding,
            tabRect.Y + ((tabRect.Height - imgH) / 2),
            _imageSize.Width,
            imgH);

        _graphics.DrawImage(image, imgRect);
    }

    private void RenderTabText(int index, Rectangle tabRect, bool isSelected, bool isHovered)
    {
        if (string.IsNullOrEmpty(_tabTexts[index]))
        {
            return;
        }

        bool hasImage = _tabImages[index] is not null;
        Rectangle textRect = hasImage
            ? new Rectangle(tabRect.X + _imageSize.Width + ImagePadding, tabRect.Y, tabRect.Width - _imageSize.Width - ImagePadding, tabRect.Height)
            : tabRect;

        Color textColor = !_enabled
            ? SystemColors.GrayText
            : isSelected
                ? SystemColors.WindowText
                : isHovered
                    ? SystemColors.ControlText
                    : Color.FromArgb(160, SystemColors.ControlText);

        using Font font = isSelected
            ? new Font(_font, FontStyle.Bold)
            : _font;

        const TextFormatFlags flags =
            TextFormatFlags.NoClipping |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.HorizontalCenter;

        TextRenderer.DrawText(_graphics, _tabTexts[index], font, textRect, textColor, flags);
    }

    private static Image? GetTabImage(TabControl tabs, int index)
    {
        ImageList.ImageCollection? images = tabs.ImageList?.Images;
        if (images is null)
        {
            return null;
        }

        TabPage page = tabs.TabPages[index];
        if (!string.IsNullOrEmpty(page.ImageKey))
        {
            return images[page.ImageKey];
        }

        if (page.ImageIndex.IsWithin(0, images.Count))
        {
            return images[page.ImageIndex];
        }

        return null;
    }

    private static Color GetParentBackColor(TabControl tabs)
    {
        Control? parent = tabs.Parent;
        while (parent is not null)
        {
            if (parent.BackColor != Color.Transparent)
            {
                return parent.BackColor;
            }

            parent = parent.Parent;
        }

        return SystemColors.Window;
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
