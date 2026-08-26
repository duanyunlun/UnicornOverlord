using System.Buffers.Binary;
using System.Text;

namespace UnicornOverlord;

internal sealed class FmsDocument
{
	private readonly Byte[] mHeader;
	private readonly UInt32[] mProperties;
	private readonly Byte[] mFooter;
	private readonly String[] mOriginalStrings;
	private readonly String[] mStrings;

	public int Count => mStrings.Length;
	public int ChangedCount => Enumerable.Range(0, Count).Count(index => IsChanged(index));

	private FmsDocument(Byte[] header, UInt32[] properties, Byte[] footer, String[] strings)
	{
		mHeader = header;
		mProperties = properties;
		mFooter = footer;
		mOriginalStrings = [.. strings];
		mStrings = strings;
	}

	public static FmsDocument Load(String filename)
	{
		Byte[] data = File.ReadAllBytes(filename);
		if (data.Length < 48 || !data.AsSpan(0, 4).SequenceEqual("FMSB"u8))
			throw new InvalidDataException("所选文件不是有效的 FMS 文件。");

		UInt32 dataSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4));
		UInt32 headerSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8, 4));
		UInt32 stringCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(20, 4));
		if (headerSize != 32 || dataSize != data.Length - 48 || stringCount > 2_000_000)
			throw new InvalidDataException("FMS 文件头或数据长度无效。");

		int propertyBytes = checked((int)stringCount * 8);
		int position = checked(32 + propertyBytes);
		if (position > data.Length - 16) throw new InvalidDataException("FMS 属性表超出文件范围。");

		var properties = new UInt32[stringCount * 2];
		for (int index = 0; index < properties.Length; index++)
			properties[index] = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(32 + index * 4, 4));

		var strings = new String[stringCount];
		var strictUtf8 = new UTF8Encoding(false, true);
		for (int index = 0; index < strings.Length; index++)
		{
			int end = Array.IndexOf(data, (Byte)0, position);
			if (end < 0 || end >= data.Length - 16) throw new InvalidDataException($"FMS 第 {index} 项缺少结束符。");
			strings[index] = strictUtf8.GetString(data, position, end - position);
			position = end + 1;
		}

		int footerPosition = (position + 15) & ~15;
		if (footerPosition + 16 != data.Length || !data.AsSpan(footerPosition, 4).SequenceEqual("FEOC"u8))
			throw new InvalidDataException("FMS 尾部或对齐无效。");
		if (data.AsSpan(position, footerPosition - position).IndexOfAnyExcept((Byte)0) >= 0)
			throw new InvalidDataException("FMS 对齐区包含非零数据。");

		return new FmsDocument(data[..32], properties, data[footerPosition..], strings);
	}

	public String GetText(int index) => mStrings[index];
	public String GetOriginalText(int index) => mOriginalStrings[index];
	public bool IsChanged(int index) => mStrings[index] != mOriginalStrings[index];

	public void SetText(int index, String value)
	{
		if (value.Contains('\0')) throw new InvalidDataException("文本不能包含 NUL 字符。");
		mStrings[index] = value;
	}

	public void Write(String filename)
	{
		using var stream = File.Create(filename);
		stream.Write(mHeader);
		Span<Byte> buffer = stackalloc Byte[4];
		foreach (UInt32 property in mProperties)
		{
			BinaryPrimitives.WriteUInt32LittleEndian(buffer, property);
			stream.Write(buffer);
		}

		foreach (String text in mStrings)
		{
			stream.Write(Encoding.UTF8.GetBytes(text));
			stream.WriteByte(0);
		}
		while (stream.Position % 16 != 0) stream.WriteByte(0);
		stream.Write(mFooter);

		long dataSize = stream.Length - 48;
		stream.Position = 4;
		Span<Byte> sizeBuffer = stackalloc Byte[4];
		BinaryPrimitives.WriteUInt32LittleEndian(sizeBuffer, checked((UInt32)dataSize));
		stream.Write(sizeBuffer);
	}
}
