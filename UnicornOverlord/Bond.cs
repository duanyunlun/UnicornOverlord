using System.ComponentModel;

namespace UnicornOverlord
{
	internal class Bond : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		private readonly uint mAddress;
		public Bond(uint address, uint? nameID)
		{
			mAddress = address;
			NameID = nameID;
		}

		public uint? NameID { get; }

		public uint ID
		{
			get => SaveData.Instance().ReadNumber(mAddress, 4);
		}

		public uint Value
		{
			get => SaveData.Instance().ReadNumber(mAddress + 4, 4);
			set
			{
				Util.WriteNumber(mAddress + 4, 4, value, 0, 1000);
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
			}
		}
	}
}
