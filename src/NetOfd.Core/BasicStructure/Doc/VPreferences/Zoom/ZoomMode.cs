using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.BasicStructure.Doc.VPreferences.Zoom;

/// <summary>
/// 自动缩放模式类型（7.5 表 9 视图首选项）。
/// 对应 Java: org.ofdrw.core.basicStructure.doc.vpreferences.zoom.ZoomMode.Type
/// <para>
/// C# 中不命名为 <c>Type</c>，避免与 <see cref="System.Type"/> 及 <c>object.GetType()</c> 冲突。
/// </para>
/// </summary>
public enum ZoomModeType
{
    /// <summary>默认缩放（默认值）</summary>
    Default,
    /// <summary>合适高度</summary>
    FitHeight,
    /// <summary>合适宽度</summary>
    FitWidth,
    /// <summary>合适区域</summary>
    FitRect
}

/// <summary>
/// 自动缩放模式节点（<c>&lt;ZoomMode&gt;Default&lt;/ZoomMode&gt;</c>）。
/// 对应 Java: org.ofdrw.core.basicStructure.doc.vpreferences.zoom.ZoomMode
/// </summary>
public class ZoomMode : ZoomScale
{
    public ZoomMode(XElement element) : base(element) { }

    private ZoomMode() : base("ZoomMode") { }

    private ZoomMode(ZoomModeType type) : this() => SetText(type.ToString());

    /// <summary>获取自动缩放模式类型；未知值抛 <see cref="ArgumentException"/>（对齐 Java）。</summary>
    public ZoomModeType ModeType
    {
        get
        {
            var s = GetText()?.Trim();
            if (Enum.TryParse<ZoomModeType>(s, ignoreCase: false, out var t))
                return t;
            throw new ArgumentException($"未知的自动缩放模式： {s}");
        }
    }

    /// <summary>按枚举获取自动缩放模式实例。</summary>
    public static ZoomMode GetInstance(ZoomModeType type) => new(type);

    /// <summary>按名称获取自动缩放模式实例（大小写敏感，对齐 Java 的 switch）。</summary>
    public static ZoomMode GetInstance(string type) =>
        GetInstance(Enum.Parse<ZoomModeType>(type, ignoreCase: false));
}
