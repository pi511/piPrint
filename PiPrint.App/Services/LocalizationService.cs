using System.Globalization;
using System.Windows;

namespace PiPrint.App.Services;

public record LanguageOption(string Code, string DisplayName);

public class LocalizationService
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    public static readonly IReadOnlyList<LanguageOption> SupportedLanguages = new List<LanguageOption>
    {
        new("en", "🇬🇧 English"),
        new("tr", "🇹🇷 Türkçe"),
        new("de", "🇩🇪 Deutsch"),
        new("es", "🇪🇸 Español")
    };

    public string CurrentLanguageCode { get; private set; } = "en";

    public event Action? LanguageChanged;

    public LocalizationService()
    {
        // Detect system culture
        string sysLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        if (SupportedLanguages.Any(l => l.Code == sysLang))
        {
            CurrentLanguageCode = sysLang;
        }
        else
        {
            CurrentLanguageCode = "en";
        }
    }

    public void Initialize()
    {
        ApplyLanguage(CurrentLanguageCode);
    }

    public void SetLanguage(string langCode)
    {
        if (CurrentLanguageCode == langCode && Application.Current.Resources.MergedDictionaries.Any(d => d.Source?.OriginalString.Contains($"Strings.{langCode}.xaml") == true))
        {
            return;
        }

        CurrentLanguageCode = langCode;
        ApplyLanguage(langCode);
        LanguageChanged?.Invoke();
    }

    private ResourceDictionary? _fallbackDict;

    private void ApplyLanguage(string langCode)
    {
        try
        {
            var newDict = new ResourceDictionary
            {
                Source = new Uri($"/PiPrint;component/Resources/Strings.{langCode}.xaml", UriKind.RelativeOrAbsolute)
            };
            _fallbackDict = newDict;

            if (Application.Current != null)
            {
                var appResources = Application.Current.Resources;
                var existingDict = appResources.MergedDictionaries
                    .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Strings."));

                if (existingDict != null)
                {
                    int index = appResources.MergedDictionaries.IndexOf(existingDict);
                    appResources.MergedDictionaries[index] = newDict;
                }
                else
                {
                    appResources.MergedDictionaries.Add(newDict);
                }
            }
        }
        catch
        {
        }
    }

    public string GetString(string key, params object[] args)
    {
        object? valObj = null;
        if (Application.Current != null && Application.Current.Resources.Contains(key))
        {
            valObj = Application.Current.Resources[key];
        }
        else if (_fallbackDict != null && _fallbackDict.Contains(key))
        {
            valObj = _fallbackDict[key];
        }

        if (valObj != null)
        {
            string val = valObj.ToString()!;
            return args.Length > 0 ? string.Format(val, args) : val;
        }
        return key;
    }
}
