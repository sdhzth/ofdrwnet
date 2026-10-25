namespace NetOfd.Layout.Element;

/// <summary>
/// 图片对象（Div 的一种）。
/// <para>
/// 为了防止与 System.Drawing.Image 冲突采用 Img 缩写。
/// 图片文件字节将在渲染时经资源管理器写入 Res 目录，并注册到 DocumentRes.xml。
/// </para>
/// 对应 Java: org.ofdrw.layout.element.Img
/// </summary>
public class Img : Div
{
    /// <summary>图片文件路径</summary>
    public string? Src { get; set; }

    public override string ElementType => "Img";

    private Img()
    {
        // 对齐 Java：图片对象不可拆分（integrity=true）；默认定位 Static 参与流式排版，
        // 需要固定摆放时由调用方 SetPosition(Position.Absolute) + SetXY 指定。
        SetIntegrity(true);
    }

    /// <summary>
    /// 构造图片对象（不解析图片大小，尺寸由调用者指定，单位 mm）。
    /// </summary>
    public Img(double width, double height, string src)
    {
        if (string.IsNullOrEmpty(src) || !File.Exists(src))
            throw new ArgumentException("图片文件为空或不存在");
        Src = src;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// 构造图片对象（图片宽度和高度以 5pix/mm 的比例转换）。
    /// </summary>
    public Img(string src)
    {
        if (string.IsNullOrEmpty(src) || !File.Exists(src))
            throw new ArgumentException("图片文件为空或不存在");
        Src = src;
        ParseImg();
    }

    /// <summary>从图片文件解析像素尺寸并换算为 mm（5 px/mm）。</summary>
    private void ParseImg()
    {
        try
        {
            using var bitmap = SkiaSharp.SKBitmap.Decode(Src!);
            if (bitmap is not null)
            {
                Width = bitmap.Width / 5.0;
                Height = bitmap.Height / 5.0;
            }
        }
        catch
        {
            // 图片解析失败时保持调用者指定的尺寸（若已指定）
        }
    }

    public override Rectangle DoPrepare(double widthLimit)
    {
        double w = (Width ?? 0) + MarginLeft + MarginRight;
        double h = (Height ?? 0) + MarginTop + MarginBottom;
        return new Rectangle(w, h);
    }
}