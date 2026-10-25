using NetOfd.Layout.Element.Canvas;

namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// 单元格画布元素。
/// <para>
/// 提供便捷的单元格（区域）绘制：文字自动换行 + 水平/垂直对齐 + 字间距 + 下划线/删除线 + 加大加粗
/// 斜体等样式，或直接放置图片。内部由 <see cref="CellContentDrawer"/> 驱动。
/// </para>
/// <para>
/// 对应 Java: org.ofdrw.layout.element.canvas.Cell（extends Canvas，内部持有 CellContentDrawer）。
/// 与 Java 差异：Java 经 Canvas 元素由引擎定位；NetOfd 的 <see cref="CellContentDrawer"/> 自带
/// (X,Y)（页面绝对 mm），因此本类的 (X,Y,Width,Height) 同步到内部绘制器，绘制在页面绝对坐标。
/// </para>
/// </summary>
public class Cell : Canvas
{
    private readonly CellContentDrawer _cellDrawer;

    public Cell(double width, double height)
        : this(0, 0, width, height)
    {
    }

    public Cell(double x, double y, double width, double height)
        : base(x, y, width, height)
    {
        _cellDrawer = new CellContentDrawer(x, y, width, height);
        Drawer = _cellDrawer;
    }

    private void SyncPosition()
    {
        if (X is double sx) _cellDrawer.X = sx;
        if (Y is double sy) _cellDrawer.Y = sy;
        if (Width is double sw) _cellDrawer.Width = sw;
        if (Height is double sh) _cellDrawer.Height = sh;
    }

    /// <summary>设置单元格内容盒左上角（页面绝对 mm），同步到内部绘制器。</summary>
    public Cell SetPosition(double x, double y) { X = x; Y = y; SyncPosition(); return this; }

    /// <summary>获取内部单元格内容绘制器。</summary>
    public CellContentDrawer GetDrawer() => _cellDrawer;

    /// <summary>获取单元格文字内容。</summary>
    public string GetValue() => _cellDrawer.GetValue() ?? "";
    /// <summary>设置单元格文字内容。</summary>
    public Cell SetValue(string value) { _cellDrawer.SetValue(value); return this; }
    /// <summary>设置图片内容（指定宽高 mm）。</summary>
    public Cell SetValue(string imgPath, double w, double h) { _cellDrawer.SetImage(imgPath, w, h); return this; }
    /// <summary>设置图片内容（宽高由图片像素尺寸换算 mm）。对应 Java <c>Cell.setValue(Path)</c>（Path 与 String 区分）。</summary>
    public Cell SetImage(string imgPath) { _cellDrawer.SetImage(imgPath); return this; }

    /// <summary>设置文字颜色。</summary>
    public Cell SetColor(string color) { _cellDrawer.SetColor(color); return this; }
    /// <summary>设置字体名称。</summary>
    public Cell SetFontName(string fontName) { _cellDrawer.SetFontName(fontName); return this; }
    /// <summary>设置字号（mm）。</summary>
    public Cell SetFontSize(double fontSize) { _cellDrawer.SetFontSize(fontSize); return this; }
    /// <summary>设置水平对齐方式。</summary>
    public Cell SetTextAlign(NetOfd.Layout.Element.TextAlign align) { _cellDrawer.SetTextAlign(align); return this; }
    /// <summary>设置垂直对齐方式。</summary>
    public Cell SetVerticalAlign(VerticalAlign align) { _cellDrawer.SetVerticalAlign(align); return this; }
    /// <summary>设置行间距（mm）。</summary>
    public Cell SetLineSpace(double lineSpace) { _cellDrawer.SetLineSpace(lineSpace); return this; }
    /// <summary>设置是否加粗。</summary>
    public Cell SetBold(bool bold) { _cellDrawer.SetBold(bold); return this; }
    /// <summary>设置是否斜体。</summary>
    public Cell SetItalic(bool italic) { _cellDrawer.SetItalic(italic); return this; }
    /// <summary>设置字间距（mm）。</summary>
    public Cell SetLetterSpacing(double letterSpacing) { _cellDrawer.SetLetterSpacing(letterSpacing); return this; }
    /// <summary>设置是否启用下划线。</summary>
    public Cell SetUnderline(bool underline) { _cellDrawer.SetUnderline(underline); return this; }
    /// <summary>设置是否启用删除线。</summary>
    public Cell SetDeleteLine(bool deleteLine) { _cellDrawer.SetDeleteLine(deleteLine); return this; }
}
