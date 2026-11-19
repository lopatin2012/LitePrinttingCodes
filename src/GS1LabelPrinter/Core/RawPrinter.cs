using System.ComponentModel;
using System.Drawing.Printing;
using System.Runtime.InteropServices;

namespace Gs1LabelPrinter.Core;

public static class RawPrinter
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo1
    {
        public string pDocName;
        public string? pOutputFile;
        public string pDatatype;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string printerName, out IntPtr hPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr hPrinter, int level, ref DocInfo1 docInfo);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int count, out int written);

    public static List<string> GetInstalledPrinters()
    {
        var result = new List<string>();
        try
        {
            foreach (string name in PrinterSettings.InstalledPrinters)
            {
                if (!string.IsNullOrWhiteSpace(name))
                    result.Add(name);
            }
        }
        catch
        {
            // Если список получить не удалось, вернём пустой — UI покажет сообщение.
        }

        result.Sort(StringComparer.CurrentCultureIgnoreCase);
        return result;
    }

    public static string? GetDefaultPrinterName()
    {
        try
        {
            var name = new PrinterSettings().PrinterName;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Отправляет готовый ZPL в очередь принтера как RAW-задание.
    /// </summary>
    public static void SendRaw(string printerName, byte[] data, string jobName = "GS1 DataMatrix")
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new ArgumentException(L.T("Принтер не выбран."), nameof(printerName));
        if (data is null || data.Length == 0)
            throw new ArgumentException(L.T("Нет данных для печати."), nameof(data));

        IntPtr printer = IntPtr.Zero;
        var docStarted = false;
        var pageStarted = false;
        IntPtr unmanaged = IntPtr.Zero;

        try
        {
            if (!OpenPrinter(printerName, out printer, IntPtr.Zero))
                throw Win32(L.T("Не удалось открыть принтер"));

            var docInfo = new DocInfo1
            {
                pDocName = jobName,
                pOutputFile = null,
                pDatatype = "RAW"
            };

            if (StartDocPrinter(printer, 1, ref docInfo) == 0)
                throw Win32(L.T("Не удалось создать задание печати"));
            docStarted = true;

            if (!StartPagePrinter(printer))
                throw Win32(L.T("Не удалось начать страницу"));
            pageStarted = true;

            unmanaged = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, unmanaged, data.Length);

            var offset = 0;
            while (offset < data.Length)
            {
                if (!WritePrinter(
                        printer,
                        IntPtr.Add(unmanaged, offset),
                        data.Length - offset,
                        out var written))
                {
                    throw Win32(L.T("Ошибка отправки данных на принтер"));
                }

                if (written <= 0)
                    throw new IOException(L.T("Принтер не принял данные (WritePrinter вернул 0 байт)."));

                offset += written;
            }
        }
        finally
        {
            if (unmanaged != IntPtr.Zero)
                Marshal.FreeHGlobal(unmanaged);

            if (pageStarted)
                EndPagePrinter(printer);
            if (docStarted)
                EndDocPrinter(printer);
            if (printer != IntPtr.Zero)
                ClosePrinter(printer);
        }
    }

    private static Win32Exception Win32(string message)
    {
        var code = Marshal.GetLastWin32Error();
        return new Win32Exception(code, L.F(
            "{0}. Код Windows: {1}. {2}",
            message, code, new Win32Exception(code).Message));
    }
}
