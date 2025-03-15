using System.Runtime.InteropServices;

namespace XDLCompiler;

[StructLayout(LayoutKind.Sequential)]
public struct RawGuid
{
    public int A;
    public short B;
    public short C;
    public byte D;
    public byte E;
    public byte F;
    public byte G;
    public byte H;
    public byte I;
    public byte J;
    public byte K;
}
