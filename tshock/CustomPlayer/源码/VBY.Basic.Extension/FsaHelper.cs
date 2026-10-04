using GetText;

namespace VBY.Basic.Extension;

/// <summary>反编译产物里 FormattableStringAdapter.op_Implicit(x) 在 TShock 6.2 上不能显式调用，等价改写。</summary>
public static class FsaHelper
{
    public static FormattableStringAdapter Make(string text) => text;
}