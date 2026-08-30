namespace UnicornOverlord
{
	internal class Info
	{
		private static Info mThis = new Info();
		public List<NameValueInfo> Item { get; private set; } = new List<NameValueInfo>();
		public List<NameValueInfo> Kind { get; private set; } = new List<NameValueInfo>();
		public List<NameValueInfo> Class { get; private set; } = new List<NameValueInfo>();
		public List<NameValueInfo> Name { get; private set; } = new List<NameValueInfo>();

		private Info() { }

		static Info()
		{
			mThis.Initialize();
		}

		public static Info Instance()
		{
			return mThis;
		}

		private void Initialize()
		{
			String infoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "info");
			AppendList(System.IO.Path.Combine(infoPath, "item.txt"), Item);
			AppendList(System.IO.Path.Combine(infoPath, "kind.txt"), Kind);
			AppendList(System.IO.Path.Combine(infoPath, "class.txt"), Class);
			AppendList(System.IO.Path.Combine(infoPath, "name.txt"), Name);
		}

		public NameValueInfo? Search<Type>(List<Type> list, uint id)
			where Type : NameValueInfo, new()
		{
			int min = 0;
			int max = list.Count;
			for (; min < max;)
			{
				int mid = (min + max) / 2;
				if (list[mid].Value == id) return list[mid];
				else if (list[mid].Value > id) max = mid;
				else min = mid + 1;
			}
			return null;
		}

		private void AppendList<Type>(String filename, List<Type> items)
			where Type : NameValueInfo, new()
		{
			if (!System.IO.File.Exists(filename)) return;
			String[] lines = System.IO.File.ReadAllLines(filename);
			foreach (String line in lines)
			{
				if (line.Length < 3) continue;
				if (line[0] == '#') continue;
				String[] values = line.Split('\t');
				if (values.Length < 2) continue;
				if (String.IsNullOrEmpty(values[0])) continue;
				if (values.Skip(1).All(String.IsNullOrEmpty)) continue;

				Type type = new Type();
				if (type.Line(values))
				{
					items.Add(type);
				}
			}

			items.Sort();
		}
	}
}
