using System.ComponentModel;
using System.Globalization;
using System.Windows;

namespace AkiLink.Services;

/// <summary>
/// Provides runtime language switching by swapping locale ResourceDictionaries.
/// The merged dictionaries in Application.Current.Resources are updated with
/// the chosen locale's resources. XAML binds to strings via DynamicResource.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    private const string DefaultCulture = "en-US";
    private string _currentCulture = DefaultCulture;

    /// <summary>
    /// Current culture code, e.g. "en-US" or "zh-CN".
    /// </summary>
    public string CurrentCulture
    {
        get => _currentCulture;
        private set
        {
            if (_currentCulture == value) return;
            _currentCulture = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
        }
    }

    /// <summary>
    /// Human-readable language name for the current locale.
    /// </summary>
    public string LanguageName => CurrentCulture switch
    {
        "zh-CN" => "中文",
        _ => "English"
    };

    /// <summary>
    /// Human-readable language name in the current UI language.
    /// </summary>
    public string LanguageNameLocal => CurrentCulture switch
    {
        "zh-CN" => "中文",
        _ => "English"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationService() { }

    /// <summary>
    /// Change the application UI language. Loads the matching ResourceDictionary
    /// and replaces the existing locale dictionary in app resources.
    /// </summary>
    public void ChangeLanguage(string culture)
    {
        if (culture != "en-US" && culture != "zh-CN")
            culture = DefaultCulture;

        var app = Application.Current;
        if (app == null) return;

        var localeUri = new Uri($"Resources/Locale.{culture}.xaml", UriKind.Relative);
        var newDict = new ResourceDictionary { Source = localeUri };

        // Find and replace the existing locale dictionary (keyed by "LocaleDictionary")
        var merged = app.Resources.MergedDictionaries;
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            var dict = merged[i];
            if (dict.Source != null && dict.Source.OriginalString.Contains("/Locale."))
            {
                merged.RemoveAt(i);
                break;
            }
        }

        merged.Add(newDict);

        CurrentCulture = culture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        CultureInfo.CurrentCulture = new CultureInfo(culture);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageNameLocal)));
    }
}
