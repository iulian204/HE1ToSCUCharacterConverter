namespace HE1ToSCU.Core.ThirdParty;

// Minimal adapter for the attributed spline decoder; no SoulsFormats runtime dependency.
internal sealed class BinaryReaderEx(bool bigEndian, byte[] bytes)
{
    private readonly BinaryData data = new(bytes,bigEndian);
    private int position;
    public long Position { get=>position; set {data.Check(checked((int)value),0);position=checked((int)value);} }
    public byte ReadByte()=>data.U8(position++);
    public short ReadInt16() {var v=data.I16(position);position+=2;return v;}
    public ushort ReadUInt16() {var v=data.U16(position);position+=2;return v;}
    public uint ReadUInt32() {var v=data.U32(position);position+=4;return v;}
    public float ReadSingle() {var v=data.F32(position);position+=4;return v;}
    public byte[] ReadBytes(int count) {data.Check(position,count);var v=bytes.AsSpan(position,count).ToArray();position+=count;return v;}
    public byte[] GetBytes(long offset,int count) {int p=checked((int)offset);data.Check(p,count);return bytes.AsSpan(p,count).ToArray();}
    public void Pad(int alignment)=>Position=(Position+alignment-1)/alignment*alignment;
}
