using Avalonia.Data.Converters;
using System.Globalization;

namespace UnicornOverlord
{
	internal class ItemIDConverter : IValueConverter
	{
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is not uint id) return null;
			var item = Info.Instance().Search(Info.Instance().Item, id);
			if (item == null) return id.ToString();
			return item.Name;
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
