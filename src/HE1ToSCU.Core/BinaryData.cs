using System.Buffers.Binary;
using System.Text;

namespace HE1ToSCU.Core;

internal sealed class BinaryData(byte[] data, bool bigEndian)
{
    public byte[] Bytes { get; } = data;
    public bool BigEndian { get; } = bigEndian;
    public void Check(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > Bytes.Length - count)
            throw new InvalidDataException($"Truncated or out-of-range binary data at 0x{offset:X} ({count} bytes).");
    }
    public byte U8(int p) { Check(p, 1); return Bytes[p]; }
    public ushort U16(int p) { Check(p, 2); return BigEndian ? BinaryPrimitives.ReadUInt16BigEndian(Bytes.AsSpan(p)) : BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan(p)); }
    public short I16(int p) => unchecked((short)U16(p));
    public uint U32(int p) { Check(p, 4); return BigEndian ? BinaryPrimitives.ReadUInt32BigEndian(Bytes.AsSpan(p)) : BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan(p)); }
    public int I32(int p) => unchecked((int)U32(p));
    public int Count(int p, int maximum = 5_000_000)
    {
        int n = I32(p);
        return n >= 0 && n <= maximum ? n : throw new InvalidDataException($"Invalid count {n} at 0x{p:X}.");
    }
    public float F32(int p)
    {
        float v = BitConverter.UInt32BitsToSingle(U32(p));
        return float.IsFinite(v) ? v : throw new InvalidDataException($"Non-finite float at 0x{p:X}.");
    }
    public string String(int p)
    {
        Check(p, 1);
        int end = Array.IndexOf(Bytes, (byte)0, p, Math.Min(4096, Bytes.Length-p));
        if (end < 0) throw new InvalidDataException("Unterminated binary string.");
        return Encoding.UTF8.GetString(Bytes, p, end-p);
    }
}
