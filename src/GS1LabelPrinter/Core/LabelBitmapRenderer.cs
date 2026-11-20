using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Gs1LabelPrinter.Core;

/// <summary>
/// Собирает готовую этикетку в обычный Bitmap для печати через
/// Windows-драйвер (GDI). Нужен для принтеров без ZPL, например TSC DA220.
/// </summary>
public static class LabelBitmapRenderer
{
    public static Bitmap Render(LabelRenderResult label, LabelSettings settings)
    {
        var width = Math.Max(1, settings.LabelWidthDots);
        var height = Math.Max(1, settings.LabelHeightDots);
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        bitmap.SetResolution(Math.Max(1, settings.Dpi), Math.Max(1, settings.Dpi));

        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;

        var layout = label.GetLayout(settings);
        var module = Math.Max(1, settings.ModuleSizeDots);

        using (var black = new SolidBrush(Color.Black))
        {
            for (var my = 0; my < label.Matrix.Height; my++)
            {
                for (var mx = 0; mx < label.Matrix.Width; mx++)
                {
                    if (!label.Matrix[mx, my])
                        continue;

                    graphics.FillRectangle(
                        black,
                        layout.DmLeft + mx * module,
                        layout.DmTop + my * module,
                        module,
                        module);
                }
            }
        }

        if (label.Text.HeightPx > 0)
        {
            graphics.DrawImage(
                label.Text.Bitmap,
                new Rectangle(
                    layout.TextLeft,
                    layout.TextTop,
                    label.Text.Bitmap.Width,
                    label.Text.Bitmap.Height));
        }

        return bitmap;
    }
}
