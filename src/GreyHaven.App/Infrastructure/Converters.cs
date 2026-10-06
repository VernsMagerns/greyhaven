using System.Globalization;
using System.Windows;
using System.Windows.Data;
using GreyHaven.Presentation.ViewModels;

namespace GreyHaven.App.Infrastructure;

/// <summary>Visible when the value equals the parameter (e.g. Screen == Results).</summary>
public sealed class EqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Equals(value?.ToString(), parameter?.ToString()) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Collapsed for null, empty strings, false and empty lists; visible otherwise.</summary>
public sealed class HasValueToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var has = value switch
        {
            null => false,
            string s => s.Length > 0,
            bool b => b,
            System.Collections.ICollection c => c.Count > 0,
            _ => true,
        };
        return has ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Looks up a palette brush by name, e.g. "Works" → GH.Works.</summary>
public sealed class PaletteBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key ? Application.Current.TryFindResource($"GH.{key}") : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Notice background: warning or info tint.</summary>
public sealed class NoticeBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Application.Current.TryFindResource(value is NoticeKind.Warning ? "GH.NoticeWarning" : "GH.NoticeInfo");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>"1." "2." numbering for fix steps (uses the item's index in its ItemsControl).</summary>
public sealed class ItemNumberConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i ? $"{i + 1}." : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
