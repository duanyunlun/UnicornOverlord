using Avalonia.Data.Converters;
using System.Globalization;

namespace UnicornOverlord
{
	internal class NameIDConverter : IValueConverter
	{
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is null && parameter is Bond bond) return LocaleManager.Instance.Format("未知角色（实例 ID {0}）", bond.ID);
			if (value is not uint id) return null;
			var nm = Info.Instance().Search(Info.Instance().Name, id);
			if (nm == null) return id.ToString();
			return nm.Name;
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
