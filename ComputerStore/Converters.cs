using System;
using System.Windows.Data;

namespace ElmirClone
{
    public class BooleanToHiddenStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isHidden)
            {
                return isHidden ? "Приховано" : "Видимо";
            }
            return "Невідомо";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack не підтримується для цього конвертера.");
        }
    }

    public class BooleanToBlockButtonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isHidden)
            {
                return isHidden ? "Розблокувати" : "Заблокувати";
            }
            return "Невідомо";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack не підтримується для цього конвертера.");
        }
    }

    public class BooleanToHideButtonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isHidden)
            {
                return isHidden ? "Розблокувати" : "Заблокувати";
            }
            return "Невідомо";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack не підтримується для цього конвертера.");
        }
    }

    public class BooleanToHiddenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isHidden)
            {
                return isHidden ? "Розблокувати" : "Заблокувати";
            }
            return "Невідомо";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack не підтримується для цього конвертера.");
        }
    }
    public class BooleanToBlockedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isHidden)
            {
                return isHidden ? "Розблокувати" : "Заблокувати";
            }
            return "Невідомо";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack не підтримується для цього конвертера.");
        }
    }
    public class BooleanToBlockButtonConverters : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isHidden)
            {
                return isHidden ? "Розблокувати" : "Заблокувати";
            }
            return "Невідомо";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack не підтримується для цього конвертера.");
        }
    }
}