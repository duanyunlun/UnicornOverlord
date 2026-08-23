using Avalonia.Data.Converters;
using System.Globalization;

namespace UnicornOverlord
{
	class ClassIDConverter : IValueConverter
	{
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is not uint id) return null;
			var cls = Info.Instance().Search(Info.Instance().Class, id);
			if (cls == null) return id.ToString();
			return cls.Name;
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
