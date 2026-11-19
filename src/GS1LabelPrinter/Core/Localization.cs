using System.Globalization;

namespace Gs1LabelPrinter.Core;

public enum UiLanguage
{
    Russian,
    English,
    Chinese
}

/// <summary>
/// Небольшая встроенная локализация. Ключ — русская строка, чтобы проще
/// было добавлять новые подписи; для английского и китайского есть словари.
/// </summary>
public static partial class L
{
    public static UiLanguage Current { get; set; } = UiLanguage.Russian;

    public static string T(string ruText)
    {
        if (string.IsNullOrEmpty(ruText))
            return ruText;

        return Current switch
        {
            UiLanguage.English => En.TryGetValue(ruText, out var en) ? en : ruText,
            UiLanguage.Chinese => Zh.TryGetValue(ruText, out var zh) ? zh : ruText,
            _ => ruText
        };
    }

    public static string F(string ruFormat, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(ruFormat), args);

    public static string ToCode(UiLanguage language) => language switch
    {
        UiLanguage.English => "en",
        UiLanguage.Chinese => "zh",
        _ => "ru"
    };

    public static UiLanguage Parse(string? code) =>
        code?.Trim().ToLowerInvariant() switch
        {
            "en" => UiLanguage.English,
            "zh" => UiLanguage.Chinese,
            _ => UiLanguage.Russian
        };

    public static string DisplayName(UiLanguage language) => language switch
    {
        UiLanguage.English => "English",
        UiLanguage.Chinese => "中文",
        _ => "Русский"
    };

    public static string DetectDefaultCode()
    {
        var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        return code is "en" or "zh" ? code : "ru";
    }

    public static readonly UiLanguage[] AllLanguages =
    {
        UiLanguage.Russian,
        UiLanguage.English,
        UiLanguage.Chinese
    };
}
