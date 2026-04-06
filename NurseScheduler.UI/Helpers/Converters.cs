using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NurseScheduler.UI.Helpers
{
    /// <summary>True → Collapsed, False → Visible</summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    /// <summary>Flips a bool — used with InverseBoolConverter={x:Static local:...}</summary>
    [ValueConversion(typeof(bool), typeof(bool))]
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is bool b && !b;
        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => v is bool b && !b;
    }

    /// <summary>Converts a non-empty string to Visible</summary>
    [ValueConversion(typeof(string), typeof(Visibility))]
    public class StringNotEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => !string.IsNullOrEmpty(value?.ToString()) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    /// <summary>Checks equality with a parameter — used for radio button binding</summary>
    public class EqualityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value?.ToString() == p?.ToString();
        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => value is bool b && b ? p : Binding.DoNothing;
    }
}
