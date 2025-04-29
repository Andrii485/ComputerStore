using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace ElmirClone
{
    public class FieldsNotEmptyConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Проверяем, что values не null и содержит хотя бы один элемент
            if (values == null || values.Length == 0)
            {
                return false; // Если нет значений, кнопка неактивна
            }

            // Проверяем, все ли значения являются строками и не пустые
            return values.All(value =>
                value is string str && !string.IsNullOrWhiteSpace(str));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // Обратное преобразование не требуется для этого сценария
            throw new NotImplementedException();
        }
    }
}