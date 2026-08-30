using System.IO.Compression;
using System.Text;

namespace UnicornOverlord;

internal static class ZipArchiveExtensions
{
	public static void WriteUtf8Text(this ZipArchive archive, String path, String content,
		CompressionLevel compressionLevel = CompressionLevel.Optimal)
	{
		using Stream stream = archive.CreateEntry(path, compressionLevel).Open();
		using var writer = new StreamWriter(stream, new UTF8Encoding(false));
		writer.Write(content);
	}
}
