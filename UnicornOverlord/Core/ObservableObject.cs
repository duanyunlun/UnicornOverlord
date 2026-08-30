using System.ComponentModel;

namespace UnicornOverlord;

internal abstract class ObservableObject : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	protected bool SetField<T>(ref T field, T value, String propertyName, params String[] dependentProperties)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return false;
		field = value;
		Notify(propertyName);
		Notify(dependentProperties);
		return true;
	}

	protected void OnPropertyChanged(String propertyName) => Notify(propertyName);

	protected void Notify(params String[] propertyNames)
	{
		foreach (String propertyName in propertyNames)
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
