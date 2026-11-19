using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace Gs1LabelPrinter.Core;

public sealed class TextBlock : IDisposable
{
    public Bitmap Bitmap { get; }
    public byte[] PackedBits { get; }
    public int WidthPx { get; }
    public int HeightPx { get; }
    public int LogicalWidth { get; }
    public int BytesPerRow { get; }

    public float ProductFontUsed { get; }
    public float CodeFontUsed { get; }
    public bool ProductShrunk { get; }
    public bool CodeShrunk { get; }
    public bool ProductOverflow { get; }
    public bool CodeOverflow { get; }

    internal TextBlock(
        Bitmap bitmap,
        byte[] packedBits,
        int logicalWidth,
        int bytesPerRow,
        float productFontUsed,
        float codeFontUsed,
        bool productShrunk,
        bool codeShrunk,
        bool productOverflow,
        bool codeOverflow)
    {
        Bitmap = bitmap;
        PackedBits = packedBits;
        WidthPx = bitmap.Width;
        HeightPx = bitmap.Height;
        LogicalWidth = logicalWidth;
        BytesPerRow = bytesPerRow;
        ProductFontUsed = productFontUsed;
        CodeFontUsed = codeFontUsed;
        ProductShrunk = productShrunk;
        CodeShrunk = codeShrunk;
        ProductOverflow = productOverflow;
        CodeOverflow = codeOverflow;
    }

    public void Dispose() => Bitmap.Dispose();
}

public static class TextBlockRenderer
{
    private const float MinFontPx = 7f;
    private const string ProductFontFamily = "Segoe UI";
    private const string CodeFontFamily = "Consolas";

    public static TextBlock Render(string productText, string codeText, int logicalWidth, LabelSettings settings)
    {
        logicalWidth = Math.Max(20, logicalWidth);
        productText = (productText ?? "")
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .TrimEnd();

        var codeLines = (codeText ?? "")
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var bytesPerRow = (logicalWidth + 7) / 8;
        var bitmapWidth = bytesPerRow * 8;

        using var measureBitmap = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        using var measureGraphics = Graphics.FromImage(measureBitmap);
        measureGraphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

        Font? productFont = null;
        Font? codeFont = null;

        try
        {
            var productShrunk = false;
            var codeShrunk = false;
            var productOverflow = false;
            var codeOverflow = false;

            if (productText.Length > 0)
            {
                productFont = FitFont(
                    measureGraphics, productText, ProductFontFamily, FontStyle.Bold,
                    settings.ProductFontPx, logicalWidth,
                    out productShrunk, out productOverflow);
            }

            if (codeLines.Count > 0)
            {
                codeFont = FitFontForLines(
                    measureGraphics, codeLines, CodeFontFamily, FontStyle.Regular,
                    settings.CodeFontPx, logicalWidth,
                    out codeShrunk, out codeOverflow);
            }

            var productHeight = productFont is null
                ? 0
                : (int)Math.Ceiling(productFont.GetHeight(measureGraphics));
            var codeLineHeight = codeFont is null
                ? 0
                : (int)Math.Ceiling(codeFont.GetHeight(measureGraphics));
            var lineGap = (int)Math.Round(settings.LineGapPx);

            var codeBlockHeight = codeLines.Count == 0
                ? 0
                : codeLines.Count * codeLineHeight + Math.Max(0, codeLines.Count - 1) * lineGap;

            var gapAfterProduct = productFont is not null && codeLines.Count > 0
                ? lineGap
                : 0;

            var height = Math.Max(1, productHeight + gapAfterProduct + codeBlockHeight + 1);

            var bitmap = new Bitmap(bitmapWidth, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                g.SmoothingMode = SmoothingMode.None;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.CompositingMode = CompositingMode.SourceOver;
                g.CompositingQuality = CompositingQuality.HighSpeed;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;

                using var brush = new SolidBrush(Color.Black);
                var y = 0f;

                if (productFont is not null)
                {
                    g.DrawString(productText, productFont, brush, new PointF(0, y),
                        StringFormat.GenericTypographic);
                    y += productHeight;

                    if (codeLines.Count > 0)
                        y += lineGap;
                }

                if (codeFont is not null)
                {
                    foreach (var line in codeLines)
                    {
                        g.DrawString(line, codeFont, brush, new PointF(0, y),
                            StringFormat.GenericTypographic);
                        y += codeLineHeight + lineGap;
                    }
                }
            }

            var packed = ThresholdAndPack(bitmap, bytesPerRow);

            return new TextBlock(
                bitmap,
                packed,
                logicalWidth,
                bytesPerRow,
                productFont?.Size ?? 0,
                codeFont?.Size ?? 0,
                productShrunk,
                codeShrunk,
                productOverflow,
                codeOverflow);
        }
        finally
        {
            productFont?.Dispose();
            codeFont?.Dispose();
        }
    }

    private static Font FitFont(
        Graphics graphics,
        string text,
        string family,
        FontStyle style,
        float startSize,
        float maxWidth,
        out bool shrunk,
        out bool overflow)
    {
        shrunk = false;
        overflow = false;

        var size = Math.Max(MinFontPx, startSize);
        while (true)
        {
            var font = new Font(family, size, style, GraphicsUnit.Pixel);
            var measured = graphics.MeasureString(
                text, font, new PointF(0, 0), StringFormat.GenericTypographic).Width;

            if (measured <= maxWidth)
            {
                shrunk = size < startSize - 0.01f;
                return font;
            }

            if (size <= MinFontPx)
            {
                shrunk = true;
                overflow = true;
                return font;
            }

            font.Dispose();
            size = Math.Max(MinFontPx, size - 0.5f);
        }
    }

    private static Font FitFontForLines(
        Graphics graphics,
        IReadOnlyList<string> lines,
        string family,
        FontStyle style,
        float startSize,
        float maxWidth,
        out bool shrunk,
        out bool overflow)
    {
        shrunk = false;
        overflow = false;

        var size = Math.Max(MinFontPx, startSize);
        var format = StringFormat.GenericTypographic;

        while (true)
        {
            var font = new Font(family, size, style, GraphicsUnit.Pixel);
            var widest = 0f;

            foreach (var line in lines)
            {
                var measured = graphics.MeasureString(
                    line, font, new PointF(0, 0), format).Width;
                if (measured > widest)
                    widest = measured;
            }

            if (widest <= maxWidth)
            {
                shrunk = size < startSize - 0.01f;
                return font;
            }

            if (size <= MinFontPx)
            {
                shrunk = true;
                overflow = true;
                return font;
            }

            font.Dispose();
            size = Math.Max(MinFontPx, size - 0.5f);
        }
    }

    private static byte[] ThresholdAndPack(Bitmap bitmap, int bytesPerRow)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var packed = new byte[bytesPerRow * height];

        var rect = new Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var strideInts = data.Stride / 4;
            var pixels = new int[strideInts * height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            for (var y = 0; y < height; y++)
            {
                var rowOffset = y * strideInts;
                var packedRow = y * bytesPerRow;

                for (var x = 0; x < width; x++)
                {
                    var argb = pixels[rowOffset + x];
                    var alpha = (argb >> 24) & 0xFF;
                    var r = (argb >> 16) & 0xFF;
                    var g = (argb >> 8) & 0xFF;
                    var b = argb & 0xFF;

                    var isBlack = alpha > 16 && (r + g + b) / 3 < 128;
                    if (isBlack)
                    {
                        pixels[rowOffset + x] = unchecked((int)0xFF000000);
                        packed[packedRow + x / 8] |= (byte)(0x80 >> (x % 8));
                    }
                    else
                    {
                        pixels[rowOffset + x] = 0;
                    }
                }
            }

            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return packed;
    }
}
