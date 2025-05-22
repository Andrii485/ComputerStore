using System;
using System.Globalization;
using System.Windows.Data;

namespace ElmirClone
{
    public class ImagePathConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2)
            {
                return null;
            }

            string imagePath = values[0] as string;
            string fallbackUrl = values[1] as string;

            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return new Uri(fallbackUrl, UriKind.Absolute);
            }

            try
            {
                return new Uri(imagePath, UriKind.Absolute);
            }
            catch (UriFormatException)
            {
                return new Uri(fallbackUrl, UriKind.Absolute);
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}