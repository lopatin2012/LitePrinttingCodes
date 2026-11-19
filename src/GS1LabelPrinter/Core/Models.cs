namespace Gs1LabelPrinter.Core;

/// <summary>
/// Один файл с кодами: одна строка = один GS1 DataMatrix.
/// </summary>
public sealed class LabelFile
{
    public string FilePath { get; init; } = "";
    public string FileName { get; init; } = "";
    public string FullProductName { get; init; } = "";
    public string? Gtin { get; init; }
    public int DeclaredQuantity { get; init; }
    public List<string> Codes { get; init; } = new();

    public string SettingsKey =>
        !string.IsNullOrWhiteSpace(Gtin)
            ? "GTIN:" + Gtin
            : "FILE:" + FileName.ToLowerInvariant();
}

/// <summary>
/// Геометрия и оформление этикетки. Все размеры — в точках принтера (dots).
/// </summary>
public sealed class LabelSettings
{
    public int LabelWidthDots { get; set; } = 800;
    public int LabelHeightDots { get; set; } = 240;
    public int Dpi { get; set; } = 203;

    public int DmX { get; set; } = 19;
    public int DmY { get; set; } = 40;
    public int ModuleSizeDots { get; set; } = 3;
    public bool FixSymbolSize { get; set; } = true;

    public int TextLeftDots { get; set; } = 19;
    public int RightMarginDots { get; set; } = 19;
    public int BottomMarginDots { get; set; } = 8;
    public int TextGapDots { get; set; } = 4;
    public float ProductFontPx { get; set; } = 15f;
    public float CodeFontPx { get; set; } = 13f;
    public float LineGapPx { get; set; } = 2f;
    public int CodeBreaks { get; set; } = 0;
    public bool ShowCodeText { get; set; } = true;

    public int Copies { get; set; } = 1;

    public double DotsToMm(int dots) => dots * 25.4 / Math.Max(1, Dpi);

    public LabelSettings Clone() => (LabelSettings)MemberwiseClone();
}

public sealed class AppSettings
{
    public string? PrinterName { get; set; }
    public string? LastFilePath { get; set; }
    public bool AutoLoadLastFile { get; set; } = true;
    public int PreviewZoomPercent { get; set; } = 0;

    public bool SaveJobFiles { get; set; } = true;
    public string? JobFolder { get; set; }

    public LabelSettings Label { get; set; } = new();
    public Dictionary<string, string> ProductLabels { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public static class AppData
{
    public static string Directory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GS1LabelPrinter");

    public static string SettingsPath => Path.Combine(Directory, "settings.json");

    public static string DefaultJobFolder
    {
        get
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return string.IsNullOrWhiteSpace(documents)
                ? Path.Combine(Directory, "jobs")
                : Path.Combine(documents, "GS1 Label Jobs");
        }
    }
}
