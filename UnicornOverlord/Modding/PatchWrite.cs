namespace UnicornOverlord;

internal sealed record PatchWrite(uint Address, byte[] Data, String Comment)
{
	public uint EndAddress => Address + (uint)Data.Length;
}
