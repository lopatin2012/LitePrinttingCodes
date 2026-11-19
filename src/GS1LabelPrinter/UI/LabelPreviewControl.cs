using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Gs1LabelPrinter.Core;

namespace Gs1LabelPrinter.UI;

public sealed class LabelPreviewControl : Control
{
    private const int OuterPadding = 18;
    private const int TopInfoHeight = 24;
    private const int BottomWarningHeight = 34;

    private LabelRenderResult? _data;
    private bool _dragging;
    private bool _mouseOverDm;
    private int _dragStartMouseX;
    private int _dragStartMouseY;
    private int _dragStartDmX;
    private int _dragStartDmY;

    public LabelPreviewControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        TabStop = true;
        BackColor = Color.FromArgb(244, 246, 249);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public LabelRenderResult? Data
    {
        get => _data;
        set
        {
            _data = value;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public LabelSettings Settings { get; set; } = new();

    /// <summary>0 — вписать в окно, иначе масштаб в процентах.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ZoomPercent { get; set; }

    public event EventHandler? DmMoved;
    public event EventHandler? DmMoveCompleted;

    public int GetFitScalePercent()
    {
        var transform = GetTransform();
        return Math.Max(10, (int)Math.Round(transform.Scale * 100));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;

        var s = Settings;
        var transform = GetTransform();
        var scale = transform.Scale;
        var ox = transform.OriginX;
        var oy = transform.OriginY;

        var infoRect = new Rectangle(OuterPadding, 4, Width - OuterPadding * 2, TopInfoHeight - 4);
        var dimsText =
            $"{s.LabelWidthDots} × {s.LabelHeightDots} точек   ·   " +
            $"{s.DotsToMm(s.LabelWidthDots):0.#} × {s.DotsToMm(s.LabelHeightDots):0.#} мм при {s.Dpi} dpi";
        TextRenderer.DrawText(g, dimsText, Font, infoRect, Color.FromArgb(90, 100, 112),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var labelRect = new RectangleF(
            ox, oy,
            s.LabelWidthDots * scale,
            s.LabelHeightDots * scale);

        // тень и сама этикетка
        using (var shadow = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
            g.FillRectangle(shadow, labelRect.X + 4, labelRect.Y + 4, labelRect.Width, labelRect.Height);
        using (var white = new SolidBrush(Color.White))
            g.FillRectangle(white, labelRect);
        using (var border = new Pen(Color.FromArgb(160, 170, 182), 1f))
            g.DrawRectangle(border, labelRect.X, labelRect.Y, labelRect.Width, labelRect.Height);

        if (_data is null)
        {
            DrawEmptyState(g, labelRect);
        }
        else
        {
            var layout = _data.GetLayout(s);
            var oldClip = g.Clip;
            g.SetClip(labelRect);

            // DataMatrix
            var matrix = _data.Matrix;
            var module = s.ModuleSizeDots;
            using (var black = new SolidBrush(Color.Black))
            {
                var dmLeft = layout.DmLeft;
                var dmTop = layout.DmTop;

                for (var my = 0; my < matrix.Height; my++)
                {
                    for (var mx = 0; mx < matrix.Width; mx++)
                    {
                        if (!matrix[mx, my])
                            continue;

                        var x1 = (int)Math.Round(ox + (dmLeft + mx * module) * scale);
                        var y1 = (int)Math.Round(oy + (dmTop + my * module) * scale);
                        var x2 = (int)Math.Round(ox + (dmLeft + (mx + 1) * module) * scale);
                        var y2 = (int)Math.Round(oy + (dmTop + (my + 1) * module) * scale);
                        g.FillRectangle(black, x1, y1, Math.Max(1, x2 - x1), Math.Max(1, y2 - y1));
                    }
                }
            }

            // Текст под кодом — та же картинка, что уйдёт в ^GF
            if (layout.TextHeight > 0)
            {
                var dest = new RectangleF(
                    ox + layout.TextLeft * scale,
                    oy + layout.TextTop * scale,
                    _data.Text.Bitmap.Width * scale,
                    _data.Text.Bitmap.Height * scale);
                g.DrawImage(_data.Text.Bitmap, dest);
            }

            g.Clip = oldClip;

            // рамка вокруг DM (для перетаскивания)
            var dmRect = GetDmClientRect(transform, layout);
            if (_mouseOverDm || _dragging)
            {
                using var pen = new Pen(Color.FromArgb(0, 122, 204), _dragging ? 2f : 1.2f)
                {
                    DashStyle = DashStyle.Dash
                };
                g.DrawRectangle(pen, dmRect.X, dmRect.Y, dmRect.Width, dmRect.Height);
            }
        }

        DrawWarnings(g);
    }

    private void DrawEmptyState(Graphics g, RectangleF labelRect)
    {
        var titleRect = new Rectangle(
            (int)labelRect.X,
            (int)(labelRect.Y + labelRect.Height / 2 - 22),
            (int)labelRect.Width,
            24);
        TextRenderer.DrawText(g, "Откройте файл с кодами", Font,
            titleRect, Color.FromArgb(70, 80, 95),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var hintRect = new Rectangle(
            (int)labelRect.X,
            (int)(labelRect.Y + labelRect.Height / 2 + 4),
            (int)labelRect.Width,
            22);
        using var small = new Font(Font.FontFamily, Math.Max(8f, Font.Size - 1f));
        TextRenderer.DrawText(g, "Один GS1-код в строке · можно перетащить файл в окно", small,
            hintRect, Color.FromArgb(125, 135, 148),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private void DrawWarnings(Graphics g)
    {
        if (_data is null)
            return;

        var layout = _data.GetLayout(Settings);
        if (!layout.HasWarnings)
            return;

        var warning = string.Join("   •   ", layout.Warnings.Take(2));
        var rect = new Rectangle(
            OuterPadding,
            Height - BottomWarningHeight + 4,
            Width - OuterPadding * 2,
            BottomWarningHeight - 6);

        using var warningFont = new Font(Font.FontFamily, Math.Max(8f, Font.Size - 0.5f));
        TextRenderer.DrawText(g, warning, warningFont, rect, Color.FromArgb(190, 40, 35),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }

    private (float Scale, float OriginX, float OriginY) GetTransform()
    {
        var s = Settings;
        var areaWidth = Math.Max(20, Width - OuterPadding * 2);
        var areaHeight = Math.Max(20, Height - TopInfoHeight - BottomWarningHeight - 8);

        var fit = Math.Min(
            areaWidth / Math.Max(1f, s.LabelWidthDots),
            areaHeight / Math.Max(1f, s.LabelHeightDots));

        var scale = ZoomPercent > 0 ? ZoomPercent / 100f : fit;
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0)
            scale = 0.1f;
        scale = Math.Clamp(scale, 0.05f, 5f);

        var ox = (Width - s.LabelWidthDots * scale) / 2f;
        var oy = TopInfoHeight + (areaHeight - s.LabelHeightDots * scale) / 2f;
        return (scale, ox, oy);
    }

    private Rectangle GetDmClientRect(
        (float Scale, float OriginX, float OriginY) transform,
        LabelLayout layout)
    {
        var x = (int)Math.Round(transform.OriginX + layout.DmLeft * transform.Scale);
        var y = (int)Math.Round(transform.OriginY + layout.DmTop * transform.Scale);
        var size = (int)Math.Round(layout.DmSizePx * transform.Scale);
        return new Rectangle(x, y, Math.Max(1, size), Math.Max(1, size));
    }

    private bool TryGetLabelPoint(MouseEventArgs e, out float xDots, out float yDots)
    {
        var transform = GetTransform();
        xDots = (e.X - transform.OriginX) / transform.Scale;
        yDots = (e.Y - transform.OriginY) / transform.Scale;
        return xDots >= 0 && yDots >= 0 &&
               xDots <= Settings.LabelWidthDots && yDots <= Settings.LabelHeightDots;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (_data is null || e.Button != MouseButtons.Left)
            return;

        var layout = _data.GetLayout(Settings);
        var dmRect = GetDmClientRect(GetTransform(), layout);
        if (!dmRect.Contains(e.Location))
            return;

        if (!TryGetLabelPoint(e, out var xDots, out var yDots))
            return;

        _dragging = true;
        _dragStartMouseX = (int)Math.Round(xDots);
        _dragStartMouseY = (int)Math.Round(yDots);
        _dragStartDmX = Settings.DmX;
        _dragStartDmY = Settings.DmY;
        Capture = true;
        Cursor = Cursors.SizeAll;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_data is null)
            return;

        var layout = _data.GetLayout(Settings);
        var dmRect = GetDmClientRect(GetTransform(), layout);

        if (_dragging)
        {
            if (!TryGetLabelPoint(e, out var xDots, out var yDots))
            {
                // всё равно двигаем, даже если курсор чуть ушёл за этикетку
                var transform = GetTransform();
                xDots = (e.X - transform.OriginX) / transform.Scale;
                yDots = (e.Y - transform.OriginY) / transform.Scale;
            }

            var newX = _dragStartDmX + (int)Math.Round(xDots) - _dragStartMouseX;
            var newY = _dragStartDmY + (int)Math.Round(yDots) - _dragStartMouseY;

            var dmSize = layout.DmSizePx;
            newX = Math.Clamp(newX, Math.Min(0, Settings.LabelWidthDots - dmSize),
                Math.Max(0, Settings.LabelWidthDots - dmSize));
            newY = Math.Clamp(newY, Math.Min(0, Settings.LabelHeightDots - dmSize),
                Math.Max(0, Settings.LabelHeightDots - dmSize));

            if (newX != Settings.DmX || newY != Settings.DmY)
            {
                Settings.DmX = newX;
                Settings.DmY = newY;
                DmMoved?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }
        else
        {
            var over = dmRect.Contains(e.Location);
            if (over != _mouseOverDm)
            {
                _mouseOverDm = over;
                Cursor = over ? Cursors.SizeAll : Cursors.Default;
                Invalidate();
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging)
            return;

        _dragging = false;
        Capture = false;
        Cursor = _mouseOverDm ? Cursors.SizeAll : Cursors.Default;
        DmMoveCompleted?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_dragging && _mouseOverDm)
        {
            _mouseOverDm = false;
            Cursor = Cursors.Default;
            Invalidate();
        }
    }

    protected override bool IsInputKey(Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        return key is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_data is null)
            return;

        var step = e.Shift ? 10 : 1;
        var dx = 0;
        var dy = 0;

        switch (e.KeyCode)
        {
            case Keys.Left: dx = -step; break;
            case Keys.Right: dx = step; break;
            case Keys.Up: dy = -step; break;
            case Keys.Down: dy = step; break;
            default: return;
        }

        var layout = _data.GetLayout(Settings);
        var dmSize = layout.DmSizePx;
        Settings.DmX = Math.Clamp(Settings.DmX + dx,
            Math.Min(0, Settings.LabelWidthDots - dmSize),
            Math.Max(0, Settings.LabelWidthDots - dmSize));
        Settings.DmY = Math.Clamp(Settings.DmY + dy,
            Math.Min(0, Settings.LabelHeightDots - dmSize),
            Math.Max(0, Settings.LabelHeightDots - dmSize));

        DmMoved?.Invoke(this, EventArgs.Empty);
        DmMoveCompleted?.Invoke(this, EventArgs.Empty);
        Invalidate();
        e.Handled = true;
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Invalidate();
    }
}
