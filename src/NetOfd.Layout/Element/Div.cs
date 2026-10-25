using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.PageDescription.Color;

namespace NetOfd.Layout.Element;

/// <summary>
/// 浮动方式枚举
/// </summary>
public enum AFloat { None, Left, Right, Center }

/// <summary>
/// 定位方式枚举
/// </summary>
public enum Position { Static, Relative, Absolute, Fixed }

/// <summary>
/// CSS 盒模型布局元素基类。
/// 对应 Java: org.ofdrw.layout.element.Div
/// 
/// 使用 builder 风格的 fluent API 配置样式。
/// </summary>
public class Div
{
    // ── 尺寸 ─────────────────────────────────────────────────
    /// <summary>宽度（mm），null 表示自动/撑满</summary>
    public double? Width { get; set; }
    /// <summary>高度（mm），null 表示自动</summary>
    public double? Height { get; set; }

    // ── 内边距（mm）：Top, Right, Bottom, Left ───────────────
    private readonly double[] _padding = new double[4];
    public double PaddingTop { get => _padding[0]; set => _padding[0] = value; }
    public double PaddingRight { get => _padding[1]; set => _padding[1] = value; }
    public double PaddingBottom { get => _padding[2]; set => _padding[2] = value; }
    public double PaddingLeft { get => _padding[3]; set => _padding[3] = value; }

    // ── 外边距（mm）───────────────────────────────────────────
    private readonly double[] _margin = new double[4];
    public double MarginTop { get => _margin[0]; set => _margin[0] = value; }
    public double MarginRight { get => _margin[1]; set => _margin[1] = value; }
    public double MarginBottom { get => _margin[2]; set => _margin[2] = value; }
    public double MarginLeft { get => _margin[3]; set => _margin[3] = value; }

    // ── 边框（mm）────────────────────────────────────────────
    private readonly double[] _border = new double[4];
    public double BorderTop { get => _border[0]; set => _border[0] = value; }
    public double BorderRight { get => _border[1]; set => _border[1] = value; }
    public double BorderBottom { get => _border[2]; set => _border[2] = value; }
    public double BorderLeft { get => _border[3]; set => _border[3] = value; }

    // ── 颜色 ─────────────────────────────────────────────────
    public CT_Color? BackgroundColor { get; set; }
    public CT_Color? BorderColor { get; set; }

    // ── 边框虚线（Java Div.BorderDash 语义）───────────────────
    // 数组元素意义：[虚线偏移量, 虚线长度, 虚线间隔, 虚线长度, 虚线间隔, ...]；null 表示实线
    private double[]? _borderDash;
    public double[]? BorderDash => _borderDash;

    // ── 不透明度（Java Div.opacity）───────────────────────────
    /// <summary>元素自身不透明度，取值 [0,1]，null 表示不透明（opaque）。</summary>
    public double? Opacity { get; set; }

    // ── 定位 ─────────────────────────────────────────────────
    public AFloat Float { get; set; } = AFloat.None;
    public Position Position { get; set; } = Position.Static;

    /// <summary>
    /// 浮动元素清除策略——控制左右两侧是否允许出现浮动元素。
    /// <para>对应 Java <c>Div.clear</c>（默认 <see cref="Clear.Both"/>）。</para>
    /// <para>
    /// 流式布局引擎 v1（<see cref="OFDDoc"/>）已实现块级自动分页与 y 光标推进；浮动（AFloat）
    /// 与 Clear 的「行内共享/浮动元素同行排布」语义仍属大缺口 #1（未消费）。
    /// </para>
    /// </summary>
    public Clear Clear { get; set; } = Clear.Both;

    // ── 完整性 / 占位（Java Div.integrity / placeholder）──────
    private bool _integrity = false;
    private bool _placeholder = false;

    /// <summary>是否不可拆分（整块不拆页），默认 false=可拆。对应 Java <c>isIntegrity()</c>。
    /// <para>流式引擎 v1 已实现块级自动分页（整块超页移入新页），但「「整块不可拆」完整性约束暂未消费，
    /// 引擎会将超高块强制放页首并截断；行级完整性（段落内某段不可断行）同样未消费。</para></summary>
    public bool IsIntegrity() => _integrity;

    /// <summary>是否为空间占位符（共享段空间且不可分割），默认 false。对应 Java <c>isPlaceholder()</c>。</summary>
    public bool IsPlaceholder() => _placeholder;

    /// <summary>绝对定位时的 X 坐标</summary>
    public double? X { get; set; }
    /// <summary>绝对定位时的 Y 坐标</summary>
    public double? Y { get; set; }

    // ── 相对定位偏移（mm）：仅当 Position==Relative 时由流式引擎在自动落位后施加 ──
    // 对齐 Java Div.getLeft/getRight/getTop：X += Left - Right，Y += Top（见 OFDDoc.ApplyRelativeOffset）。
    /// <summary>相对定位水平偏移：自动落位 X 之后再右移（左负右正）。仅 <see cref="Position.Relative"/> 生效。</summary>
    public double? Left { get; set; }
    /// <summary>相对定位水平反向偏移：自动落位 X 之后再左移（右负左正）。仅 <see cref="Position.Relative"/> 生效。</summary>
    public double? Right { get; set; }
    /// <summary>相对定位垂直偏移：自动落位 Y 之后再下移（上负下正）。仅 <see cref="Position.Relative"/> 生效。</summary>
    public double? Top { get; set; }

    // ── 图层 ─────────────────────────────────────────────────
    /// <summary>所属图层（默认 Body）</summary>
    public LayerType Layer { get; set; } = LayerType.Body;

    /// <summary>元素类型（用于渲染器分发）</summary>
    public virtual string ElementType => "Div";

    // ── 子元素 ────────────────────────────────────────────────
    protected readonly List<Div> _children = new();

    public IReadOnlyList<Div> Children => _children;

    // ── Fluent API ────────────────────────────────────────────
    public Div SetWidth(double w) { Width = w; return this; }
    public Div SetHeight(double h) { Height = h; return this; }
    public Div SetPadding(double all) { _padding[0] = _padding[1] = _padding[2] = _padding[3] = all; return this; }
    public Div SetPadding(double topBottom, double leftRight)
    {
        _padding[0] = _padding[2] = topBottom;
        _padding[1] = _padding[3] = leftRight;
        return this;
    }
    public Div SetPadding(double top, double right, double bottom, double left)
    {
        _padding[0] = top; _padding[1] = right; _padding[2] = bottom; _padding[3] = left;
        return this;
    }

    public Div SetMargin(double all) { _margin[0] = _margin[1] = _margin[2] = _margin[3] = all; return this; }

    public Div SetBorder(double all) { _border[0] = _border[1] = _border[2] = _border[3] = all; return this; }

    // ── 单项边距 setter（对齐 Java Div.setPaddingTop / setMarginLeft / setBorderBottom 等）──
    public Div SetPaddingTop(double v) { _padding[0] = v; return this; }
    public Div SetPaddingRight(double v) { _padding[1] = v; return this; }
    public Div SetPaddingBottom(double v) { _padding[2] = v; return this; }
    public Div SetPaddingLeft(double v) { _padding[3] = v; return this; }
    public Div SetMarginTop(double v) { _margin[0] = v; return this; }
    public Div SetMarginRight(double v) { _margin[1] = v; return this; }
    public Div SetMarginBottom(double v) { _margin[2] = v; return this; }
    public Div SetMarginLeft(double v) { _margin[3] = v; return this; }
    public Div SetBorderTop(double v) { _border[0] = v; return this; }
    public Div SetBorderRight(double v) { _border[1] = v; return this; }
    public Div SetBorderBottom(double v) { _border[2] = v; return this; }
    public Div SetBorderLeft(double v) { _border[3] = v; return this; }

    public Div SetBorderColor(CT_Color color) { BorderColor = color; return this; }
    public Div SetBackgroundColor(CT_Color color) { BackgroundColor = color; return this; }

    /// <summary>
    /// 设置元素自身不透明度（对齐 Java <c>Div.setOpacity(Double)</c>）。
    /// 取值区间 [0,1]：<c>null</c> 表示不透明（移除）；<c>&lt;0</c> 钳制为 0；<c>&gt;1</c> 钳制为 1。
    /// 作用于该元素渲染产生的图形（背景/边框/图片/文字）。
    /// </summary>
    public Div SetOpacity(double? opacity)
    {
        if (opacity is null) Opacity = null;
        else if (opacity < 0) Opacity = 0;
        else if (opacity > 1) Opacity = 1;
        else Opacity = opacity;
        return this;
    }

    /// <summary>
    /// 设置边框虚线（对齐 Java <c>Div.setBorderDash(Double...)</c>）。
    /// 数组内部规整为 <c>[偏移量, 线长, 空白, 线长, 空白, ...]</c>：
    /// <list type="bullet">
    /// <item>null / 空：取消虚线（实线）</item>
    /// <item>单参数 <c>d</c>：线长 = 空白 = d（偏移 0）</item>
    /// <item>偶数参数 <c>(a, b)</c>：线长 a、空白 b（偏移 0）</item>
    /// <item>奇数参数 <c>(p, a, b)</c>：偏移 p、线长 a、空白 b</item>
    /// </list>
    /// </summary>
    public Div SetBorderDash(params double?[] dash)
    {
        if (dash is null || dash.Length == 0 || (dash.Length == 1 && dash[0] is null))
        {
            _borderDash = null;
            return this;
        }
        if (dash.Length == 1)
        {
            // 单参数：线长与空白相同
            _borderDash = [0d, dash[0]!.Value, dash[0]!.Value];
            return this;
        }

        var values = new double[dash.Length];
        for (int i = 0; i < dash.Length; i++) values[i] = dash[i] ?? 0d;

        if (values.Length % 2 == 1)
        {
            // 奇数参数：第一个为虚线偏移量
            _borderDash = values;
        }
        else
        {
            // 偶数参数：补充 0 作为偏移量
            var arr = new double[values.Length + 1];
            arr[0] = 0d;
            Array.Copy(values, 0, arr, 1, values.Length);
            _borderDash = arr;
        }
        return this;
    }

    /// <summary>取消虚线边框（实线）。等价 <c>SetBorderDash(null)</c>。</summary>
    public Div ClearBorderDash() { _borderDash = null; return this; }

    public Div SetFloat(AFloat f) { Float = f; return this; }
    public Div SetPosition(Position p) { Position = p; return this; }
    public Div SetClear(Clear c) { Clear = c; return this; }

    /// <summary>设置相对定位水平偏移（右移为正）。需配合 <see cref="SetPosition(Position)"/> 设为 Relative。</summary>
    public Div SetLeft(double left) { Left = left; return this; }
    /// <summary>设置相对定位水平反向偏移（左移为正）。需配合 <see cref="SetPosition(Position)"/> 设为 Relative。</summary>
    public Div SetRight(double right) { Right = right; return this; }
    /// <summary>设置相对定位垂直偏移（下移为正）。需配合 <see cref="SetPosition(Position)"/> 设为 Relative。</summary>
    public Div SetTop(double top) { Top = top; return this; }

    // ── 块级 / 显示样式 / 完整性 / 占位（对齐 Java Div 公开 API） ──

    /// <summary>
    /// 是否独占段（块级元素）。对应 Java <c>Div.isBlockElement()</c>：
    /// 绝对定位默认不是块级；clear==Both 或 (float==Right 且 clear==Left) 视为独占。
    /// </summary>
    public bool IsBlockElement()
    {
        if (Position == Position.Absolute) return false;
        return Clear == Clear.Both || (Float == AFloat.Right && Clear == Clear.Left);
    }

    /// <summary>水平方向盒模型总宽（contentWidth + marginLR + paddingLR + borderLR）。对应 Java <c>Div.widthPlus()</c>。</summary>
    public double WidthPlus()
        => MarginLeft + MarginRight + PaddingLeft + PaddingRight + BorderLeft + BorderRight;

    /// <summary>完整盒宽（contentWidth + 水平装饰）。对应 Java <c>Div.width + Div.widthPlus()</c>。</summary>
    public double FullWidth() => (Width ?? 0) + WidthPlus();

    /// <summary>是否存在边框（false 表示四边全 0）。对应 Java <c>Div.isNoBorder()</c>。</summary>
    public bool IsNoBorder()
        => BorderTop == 0d && BorderRight == 0d && BorderBottom == 0d && BorderLeft == 0d;

    /// <summary>
    /// 设置显示样式（对应 Java <c>Div.setDisplay</c>）——<b>与 Clear 联动不改自身存储</b>：
    /// <see cref="Display.InlineBlock"/> → <see cref="Clear.None"/>（行内共享）；
    /// <see cref="Display.Block"/> → <see cref="Clear.Both"/>（独占段）。
    /// </summary>
    public Div SetDisplay(Display display)
        => display == Display.InlineBlock ? SetClear(Clear.None) : SetClear(Clear.Both);

    /// <summary>
    /// 获取显示样式（对应 Java <c>Div.getDisplay</c> 推导逻辑）：
    /// 块级元素 → Block；clear==None → InlineBlock；否则 Block。
    /// </summary>
    public Display GetDisplay()
    {
        if (IsBlockElement()) return Display.Block;
        return Clear == Clear.None ? Display.InlineBlock : Display.Block;
    }

    /// <summary>设置元素是否不可拆分（对应 Java <c>setIntegrity</c>）。</summary>
    public Div SetIntegrity(bool integrity) { _integrity = integrity; return this; }

    /// <summary>设置是否为空间占位符（对应 Java <c>setPlaceholder</c>）。</summary>
    public Div SetPlaceholder(bool placeholder) { _placeholder = placeholder; return this; }

    // ── 克隆 / 拷贝 / 竖直切分（对齐 Java Div.clone / copyTo / split） ──

    /// <summary>
    /// 克隆一个同类型的盒模型 Div —— 仅复制盒属性（宽高/边距/边框/背景/定位/完整性），
    /// <b>不复制渲染内容</b>（Children、Paragraph 文本等）。对应 Java <c>Div.clone()</c>。
    /// <para>Java 语义中 clone 用于 <see cref="Split"/>；由于内容不随克隆，切分仅适用于
    /// 纯盒 Div（背景/边框等装饰盒），文本与画布内容元素由引擎按行/整块处理。</para>
    /// </summary>
    public virtual Div Clone()
    {
        var d = new Div();
        CopyTo(d);
        return d;
    }

    /// <summary>将本元素的盒模型属性复制到目标元素（对齐 Java <c>Div.copyTo(T)</c>）。</summary>
    public virtual void CopyTo(Div target)
    {
        target.BackgroundColor = BackgroundColor;
        target.BorderColor = BorderColor;
        target.Width = Width;
        target.Height = Height;
        Array.Copy(_padding, target._padding, 4);
        Array.Copy(_border, target._border, 4);
        Array.Copy(_margin, target._margin, 4);
        // 修正 Java 缺陷：Java copyTo 漏拷 borderDash，clone 后虚线信息丢失
        target._borderDash = _borderDash is null ? null : (double[])_borderDash.Clone();
        target.X = X;
        target.Y = Y;
        target.Clear = Clear;
        target.Float = Float;
        target.Position = Position;
        target.Opacity = Opacity;
        target._integrity = _integrity;
        target._placeholder = _placeholder;
        target.Layer = Layer;
    }

    /// <summary>额外（竖直）高度 = 上下 margin + padding + border 之和。对应 Java <c>Div.heightPlus()</c>。</summary>
    public double HeightPlus()
        => MarginTop + MarginBottom + PaddingTop + PaddingBottom + BorderTop + BorderBottom;

    /// <summary>
    /// 根据给定高度竖直切分元素（对应 Java <c>Div.split(double)</c>）。
    /// <list type="bullet">
    /// <item>仅作用于竖直方向，每次只截断一次；要求 Width 与 Height 均已确定（Java 语义，否则抛异常）。</item>
    /// <item>切割点按落入 margin / border / padding / content / padding / border / margin 七个区间
    /// 归属两侧盒装饰，保证 div[0]（上半段）的布局占位高度恰等于 sHeight，div[1]（下半段）
    /// 从切缝处继续（切缝两侧无 margin/border/padding 重叠）。</item>
    /// <item>整块总高（含装饰）≤ sHeight 时返回 <c>{ this }</c>（不切分）。</item>
    /// </list>
    /// </summary>
    /// <param name="sHeight">切分高度（可用剩余空间高度）</param>
    /// <returns>div[0] 上半段 / div[1] 下半段</returns>
    public virtual Div[] Split(double sHeight)
    {
        if (Width == null || Height == null)
            throw new InvalidOperationException("切分元素必须要有固定的宽度（Width）和高度（Height）");

        double totalH = Height.Value + HeightPlus();
        if (totalH <= sHeight) return new[] { this };

        var div1 = Clone();
        var div2 = Clone();

        if (MarginTop >= sHeight)
        {
            // ── ① Margin 耗尽整段空间：div1 退化为纯 margin 空间占位符 ──
            double deltaM = MarginTop - sHeight;
            div1.SetMarginTop(sHeight).SetBorderTop(0).SetPaddingTop(0).SetHeight(0)
                .SetPaddingBottom(0).SetBorderBottom(0).SetMarginBottom(0)
                .SetPlaceholder(true);
            // 剩余 margin 残留在下一段顶部
            div2.SetMarginTop(deltaM);
        }
        else if (MarginTop + BorderTop >= sHeight)
        {
            // ── ② Margin + Border 耗尽空间 ──
            double deltaB = BorderTop - (sHeight - MarginTop);
            div1.SetBorderTop(sHeight - MarginTop).SetPaddingTop(0).SetHeight(0)
                .SetPaddingBottom(0).SetBorderBottom(0).SetMarginBottom(0);
            div2.SetMarginTop(0).SetBorderTop(deltaB);
        }
        else if (MarginTop + BorderTop + PaddingTop >= sHeight)
        {
            // ── ③ Margin + Border + Padding 耗尽空间 ──
            double deltaP = PaddingTop - (sHeight - MarginTop - BorderTop);
            div1.SetPaddingTop(sHeight - MarginTop - BorderTop).SetHeight(0)
                .SetPaddingBottom(0).SetBorderBottom(0).SetMarginBottom(0);
            div2.SetMarginTop(0).SetBorderTop(0).SetPaddingTop(deltaP);
        }
        else if (MarginTop + BorderTop + PaddingTop + Height.Value >= sHeight)
        {
            // ── ④ 切点落在内容区：两段分配内容高度，切缝处两侧无盒装饰 ──
            double h1 = sHeight - (div1.MarginTop + div1.BorderTop + div1.PaddingTop);
            div1.SetHeight(h1).SetMarginBottom(0).SetBorderBottom(0).SetPaddingBottom(0);
            double h2 = div2.Height!.Value - h1;
            div2.SetHeight(h2).SetMarginTop(0).SetBorderTop(0).SetPaddingTop(0);
        }
        else if (MarginTop + BorderTop + PaddingTop + Height.Value + PaddingBottom >= sHeight)
        {
            // ── ⑤ 切点落在内容下 padding 区 ──
            double deltaP = sHeight - (MarginTop + BorderTop + PaddingTop + Height.Value);
            div1.SetPaddingBottom(deltaP).SetBorderBottom(0).SetMarginBottom(0);
            div2.SetMarginTop(0).SetBorderTop(0).SetPaddingTop(0).SetHeight(0)
                .SetPaddingBottom(div2.PaddingBottom - deltaP);
        }
        else if (MarginTop + BorderTop + PaddingTop + Height.Value + PaddingBottom + BorderBottom >= sHeight)
        {
            // ── ⑥ 切点落在内容下 border 区 ──
            double deltaB = sHeight - (MarginTop + BorderTop + PaddingTop + Height.Value + PaddingBottom);
            div1.SetBorderBottom(deltaB).SetMarginBottom(0);
            div2.SetMarginTop(0).SetBorderTop(0).SetPaddingTop(0).SetHeight(0)
                .SetPaddingBottom(0).SetBorderBottom(div2.BorderBottom - deltaB);
        }
        else
        {
            // ── ⑦ 切点落在内容下 margin 区：div2 退化为纯 margin 空间占位符 ──
            double deltaM = sHeight - (MarginTop + BorderTop + PaddingTop + Height.Value
                                       + PaddingBottom + BorderBottom);
            div1.SetMarginBottom(deltaM);
            div2.SetMarginTop(0).SetBorderTop(0).SetPaddingTop(0).SetHeight(0)
                .SetPaddingBottom(0).SetBorderBottom(0)
                .SetMarginBottom(div2.MarginBottom - deltaM)
                .SetPlaceholder(true);
        }
        return new[] { div1, div2 };
    }

    /// <summary>
    /// 创建空间占位符（对应 Java <c>Div.placeholder(w, h, aFloat)</c>）——
    /// 共享段空间且不可分割：Placeholder=true + Integrity=true + Clear.None + 指定尺寸与浮动。
    /// </summary>
    public static Div Placeholder(double width, double height, AFloat aFloat)
        => new Div()
            .SetPlaceholder(true)
            .SetWidth(width)
            .SetHeight(height)
            .SetFloat(aFloat)
            .SetClear(Clear.None)
            .SetIntegrity(true);

    /// <summary>创建空间占位符（对应 Java <c>Div.placeholder(Rectangle, aFloat)</c>）。</summary>
    public static Div Placeholder(Rectangle rec, AFloat aFloat)
        => Placeholder(rec.Width, rec.Height, aFloat);
    public Div SetXY(double x, double y) { X = x; Y = y; return this; }
    public Div SetLayer(LayerType layer) { Layer = layer; return this; }

    public Div Add(Div child) { _children.Add(child); return this; }

    /// <summary>
    /// 计算该元素在指定宽度限制下实际占用的尺寸（含 margin）。
    /// 对应 Java <c>Div.doPrepare</c>。
    /// </summary>
    public virtual Rectangle DoPrepare(double widthLimit)
    {
        double w = (Width ?? widthLimit) + MarginLeft + MarginRight;
        double h = (Height ?? 0) + MarginTop + MarginBottom;
        return new Rectangle(w, h);
    }

    // ── 布局计算 ──────────────────────────────────────────────
    /// <summary>获取内容区域宽度（减去 padding 和 border）</summary>
    public double GetContentWidth(double containerWidth)
    {
        double w = Width ?? containerWidth;
        return w - _padding[1] - _padding[3] - _border[1] - _border[3];
    }

    /// <summary>获取实际占用宽度（包含 margin）</summary>
    public double GetOccupiedWidth(double containerWidth)
    {
        double w = Width ?? containerWidth;
        return w + _margin[1] + _margin[3];
    }
}
