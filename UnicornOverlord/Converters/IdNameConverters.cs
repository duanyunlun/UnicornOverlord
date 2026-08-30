using Avalonia.Data.Converters;
using System.Globalization;

namespace UnicornOverlord;

internal abstract class IdNameConverter : IValueConverter
{
	protected abstract List<NameValueInfo> Entries(Info info);

	public virtual object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not uint id) return null;
		Info info = Info.Instance();
		return info.Search(Entries(info), id)?.Name ?? id.ToString();
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotImplementedException();
}

internal sealed class NameIDConverter : IdNameConverter
{
	protected override List<NameValueInfo> Entries(Info info) => info.Name;

	public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is null && parameter is Bond bond)
			return LocaleManager.Instance.Format("未知角色（实例 ID {0}）", bond.ID);
		return base.Convert(value, targetType, parameter, culture);
	}
}

internal sealed class ItemIDConverter : IdNameConverter
{
	protected override List<NameValueInfo> Entries(Info info) => info.Item;
}

internal sealed class ClassIDConverter : IdNameConverter
{
	protected override List<NameValueInfo> Entries(Info info) => info.Class;
}
