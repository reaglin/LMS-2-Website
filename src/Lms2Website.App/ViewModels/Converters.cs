using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Lms2Website.App.ViewModels;

/// <summary>Shows an element only when its bound text has something in it.</summary>
public sealed class ShowIfTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Bold for a section row of the preview tree, normal for everything under it.</summary>
public sealed class BoldIfSectionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? FontWeights.SemiBold : FontWeights.Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
