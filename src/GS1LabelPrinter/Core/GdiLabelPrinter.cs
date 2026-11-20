using System.Drawing.Printing;
using System.Runtime.ExceptionServices;

namespace Gs1LabelPrinter.Core;

/// <summary>
/// Печать готовой этикетки через Windows-драйвер (GDI).
/// Работает с USB-принтерами, которые не понимают ZPL (например, TSC DA220).
/// </summary>
public static class GdiLabelPrinter
{
    public static void Print(
        string printerName,
        IReadOnlyList<LabelRenderResult> labels,
        LabelSettings settings,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new ArgumentException(L.T("Принтер не выбран."), nameof(printerName));
        if (labels.Count == 0)
            throw new ArgumentException(L.T("Нет данных для печати."), nameof(labels));

        Exception? failure = null;

        // Драйверы печати Windows лучше работают в STA-потоке.
        var thread = new Thread(() =>
        {
            try
            {
                PrintCore(printerName, labels, settings, token);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void PrintCore(
        string printerName,
        IReadOnlyList<LabelRenderResult> labels,
        LabelSettings settings,
        CancellationToken token)
    {
        using var document = new PrintDocument();
        document.PrinterSettings.PrinterName = printerName;

        if (!document.PrinterSettings.IsValid)
            throw new InvalidOperationException(L.F("Принтер недоступен: {0}", printerName));

        document.DocumentName = "GS1 DataMatrix";
        document.PrintController = new StandardPrintController();
        document.OriginAtMargins = false;
        document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        document.DefaultPageSettings.Landscape = false;

        var dpi = Math.Max(1, settings.Dpi);
        var widthHundredInch = Math.Max(1, (int)Math.Round(settings.LabelWidthDots * 100.0 / dpi));
        var heightHundredInch = Math.Max(1, (int)Math.Round(settings.LabelHeightDots * 100.0 / dpi));
        document.DefaultPageSettings.PaperSize =
            new PaperSize("GS1 Label", widthHundredInch, heightHundredInch);

        var index = 0;
        document.PrintPage += (_, e) =>
        {
            token.ThrowIfCancellationRequested();

            if (index >= labels.Count)
            {
                e.HasMorePages = false;
                return;
            }

            using var bitmap = LabelBitmapRenderer.Render(labels[index], settings);

            var graphics = e.Graphics;
            if (graphics is null)
            {
                e.HasMorePages = false;
                return;
            }

            graphics.PageUnit = GraphicsUnit.Pixel;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

            var dpiX = graphics.DpiX > 0 ? graphics.DpiX : dpi;
            var dpiY = graphics.DpiY > 0 ? graphics.DpiY : dpi;
            var scaleX = dpiX / (float)dpi;
            var scaleY = dpiY / (float)dpi;

            var width = Math.Max(1, (int)Math.Round(bitmap.Width * scaleX));
            var height = Math.Max(1, (int)Math.Round(bitmap.Height * scaleY));

            graphics.DrawImage(bitmap, new Rectangle(0, 0, width, height));

            index++;
            e.HasMorePages = index < labels.Count;
        };

        document.Print();

        if (index != labels.Count)
            throw new InvalidOperationException(L.T("Драйвер не напечатал все этикетки."));
    }
}
