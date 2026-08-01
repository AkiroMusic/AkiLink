using System.Windows.Media;

namespace AkiLink;

/// <summary>
/// Sidebar icon geometries defined in C# to avoid BAML binary deserialization issues
/// with literal Path Data strings in XAML resource dictionaries.
/// </summary>
public static class IconGeometries
{
    /// <summary>Bluetooth / speaker icon</summary>
    public static Geometry DevicesIcon { get; } = Geometry.Parse(
        "M12,3L10,7H14L12,3M10,9L12,12L14,9H10M5,11L4,12L7,15L10,12L7,9L5,11M19,11L17,9L14,12L17,15L19,13L18,12L19,11M12,17L10,21H14L12,17");

    /// <summary>Gear / settings icon</summary>
    public static Geometry SettingsIcon { get; } = Geometry.Parse(
        "M19.14,12.94C19.18,12.64 19.2,12.33 19.2,12C19.2,11.68 19.18,11.36 19.14,11.06L21.16,9.5C21.34,9.36 21.39,9.11 21.28,8.89L19.36,5.55C19.25,5.33 19,5.26 18.78,5.34L16.5,6.32C16,5.97 15.5,5.68 14.94,5.5L14.64,3.08C14.61,2.84 14.41,2.66 14.17,2.66H10.33C10.09,2.66 9.89,2.84 9.86,3.08L9.56,5.5C9,5.68 8.5,5.97 8,6.32L5.72,5.34C5.5,5.26 5.25,5.33 5.14,5.55L3.22,8.89C3.11,9.11 3.16,9.36 3.34,9.5L5.36,11.06C5.32,11.36 5.3,11.68 5.3,12C5.3,12.33 5.32,12.64 5.36,12.94L3.34,14.5C3.16,14.64 3.11,14.89 3.22,15.11L5.14,18.45C5.25,18.67 5.5,18.74 5.72,18.66L8,17.68C8.5,18.03 9,18.32 9.56,18.5L9.86,20.92C9.89,21.16 10.09,21.34 10.33,21.34H14.17C14.41,21.34 14.61,21.16 14.64,20.92L14.94,18.5C15.5,18.32 16,18.03 16.5,17.68L18.78,18.66C19,18.74 19.25,18.67 19.36,18.45L21.28,15.11C21.39,14.89 21.34,14.64 21.16,14.5L19.14,12.94M12,16C9.79,16 8,14.21 8,12C8,9.79 9.79,8 12,8C14.21,8 16,9.79 16,12C16,14.21 14.21,16 12,16Z");

/// <summary>Clock / history icon</summary>
public static Geometry HistoryIcon { get; } = Geometry.Parse(
    "M11.99,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 11.99,2M12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20M12.5,7H11V13L16.25,16.15L17,14.92L12.5,12.25V7Z");

/// <summary>Equalizer / audio quality icon</summary>
public static Geometry AudioQualityIcon { get; } = Geometry.Parse(
    "M3,21 L3,9 L7,9 L7,21 L3,21 Z M9,21 L9,3 L13,3 L13,21 L9,21 Z M15,21 L15,13 L19,13 L19,21 L15,21 Z");

/// <summary>Five-point star icon for background decoration</summary>
public static Geometry StarIcon { get; } = Geometry.Parse(
    "M12 2l2.6 5.3L20 8l-4 3.9.9 5.5L12 14.5 7.1 17.4 8 11.9 4 8l5.4-.7z");
}
