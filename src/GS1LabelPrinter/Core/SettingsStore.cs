using System.Text.Encodings.Web;
using System.Text.Json;

namespace Gs1LabelPrinter.Core;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(AppData.SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(AppData.SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            Normalize(settings);
            return settings;
        }
        catch
        {
            TryBackupBrokenSettings();
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Normalize(settings);
            Directory.CreateDirectory(AppData.Directory);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var tmp = AppData.SettingsPath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, AppData.SettingsPath, overwrite: true);
        }
        catch
        {
            // Настройки не являются критичными для печати — не мешаем пользователю.
        }
    }

    private static void Normalize(AppSettings settings)
    {
        settings.Label ??= new LabelSettings();
        settings.ProductLabels ??= new(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(settings.Language))
            settings.Language = L.DetectDefaultCode();
        else
            settings.Language = L.ToCode(L.Parse(settings.Language));

        if (string.IsNullOrWhiteSpace(settings.JobFolder))
            settings.JobFolder = AppData.DefaultJobFolder;

        if (!ReferenceEquals(settings.ProductLabels.Comparer, StringComparer.OrdinalIgnoreCase))
        {
            settings.ProductLabels = new Dictionary<string, string>(
                settings.ProductLabels, StringComparer.OrdinalIgnoreCase);
        }

        var l = settings.Label;
        l.LabelWidthDots = Math.Clamp(l.LabelWidthDots, 200, 4000);
        l.LabelHeightDots = Math.Clamp(l.LabelHeightDots, 100, 4000);
        l.Dpi = l.Dpi is 203 or 300 ? l.Dpi : 203;
        l.ModuleSizeDots = Math.Clamp(l.ModuleSizeDots, 1, 20);
        l.Copies = Math.Clamp(l.Copies, 1, 100);
        l.ProductFontPx = Math.Clamp(l.ProductFontPx, 7f, 60f);
        l.CodeFontPx = Math.Clamp(l.CodeFontPx, 7f, 60f);
        l.LineGapPx = Math.Clamp(l.LineGapPx, 0f, 20f);
        l.CodeBreaks = Math.Clamp(l.CodeBreaks, 0, 10);
        l.TextGapDots = Math.Clamp(l.TextGapDots, 0, 50);
        l.TextLeftDots = Math.Clamp(l.TextLeftDots, 0, 1000);
        l.RightMarginDots = Math.Clamp(l.RightMarginDots, 0, 1000);
        l.BottomMarginDots = Math.Clamp(l.BottomMarginDots, 0, 500);
        settings.PreviewZoomPercent = Math.Clamp(settings.PreviewZoomPercent, 0, 400);
    }

    private static void TryBackupBrokenSettings()
    {
        try
        {
            if (File.Exists(AppData.SettingsPath))
            {
                File.Copy(
                    AppData.SettingsPath,
                    AppData.SettingsPath + ".broken",
                    overwrite: true);
            }
        }
        catch
        {
            // ignore
        }
    }
}
