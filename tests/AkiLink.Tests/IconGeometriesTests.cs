using System.Windows.Media;
using AkiLink;

namespace AkiLink.Tests;

/// <summary>
/// Tests for the static sidebar icon geometries defined in C# (IconGeometries).
/// Guards that every path parses into a non-empty geometry and that the star
/// icon's points stay inside the 24x24 unit box.
/// </summary>
public class IconGeometriesTests
{
    [Fact]
    public void StarIcon_WhenParsed_IsNotNull()
    {
        Assert.NotNull(IconGeometries.StarIcon);
    }

    [Fact]
    public void StarIcon_Bounds_AreNonEmpty()
    {
        var bounds = IconGeometries.StarIcon.Bounds;

        Assert.True(bounds.Width > 0 && bounds.Height > 0);
    }

    [Fact]
    public void StarIcon_Bounds_StayInside24x24UnitBox()
    {
        var bounds = IconGeometries.StarIcon.Bounds;

        Assert.True(bounds.Left >= 0 && bounds.Top >= 0);
        Assert.True(bounds.Right <= 24 && bounds.Bottom <= 24);
    }

    [Fact]
    public void DevicesIcon_WhenParsed_HasNonEmptyBounds()
    {
        Assert.True(IconGeometries.DevicesIcon.Bounds.Width > 0);
        Assert.True(IconGeometries.DevicesIcon.Bounds.Height > 0);
    }

    [Fact]
    public void SettingsIcon_WhenParsed_HasNonEmptyBounds()
    {
        Assert.True(IconGeometries.SettingsIcon.Bounds.Width > 0);
        Assert.True(IconGeometries.SettingsIcon.Bounds.Height > 0);
    }

    [Fact]
    public void HistoryIcon_WhenParsed_HasNonEmptyBounds()
    {
        Assert.True(IconGeometries.HistoryIcon.Bounds.Width > 0);
        Assert.True(IconGeometries.HistoryIcon.Bounds.Height > 0);
    }

    [Fact]
    public void AudioQualityIcon_WhenParsed_HasNonEmptyBounds()
    {
        Assert.True(IconGeometries.AudioQualityIcon.Bounds.Width > 0);
        Assert.True(IconGeometries.AudioQualityIcon.Bounds.Height > 0);
    }
}
