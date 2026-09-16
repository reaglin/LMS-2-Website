using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Lms2Website.Core.Publish;

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

/// <summary>Green for done, amber for "done but something changed since", grey for not yet.</summary>
public sealed class StateColourConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            StepState.Yes   => new SolidColorBrush(Color.FromRgb(0x1A, 0x7F, 0x37)),   // green
            StepState.Stale => new SolidColorBrush(Color.FromRgb(0x9A, 0x67, 0x00)),        // amber
            _               => new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E))     // grey
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
