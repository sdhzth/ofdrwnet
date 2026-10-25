namespace NetOfd.Layout;

/// <summary>
/// 表格实体。通过 <see cref="OFDDoc.AddTable"/> 加入文档后，
/// 在 <see cref="OFDDoc"/> 落盘时会被渲染为 OFD 页面图元
/// （单元格边框为 PathObject，文字为 TextObject，可选背景填充）。
/// 对应 Java 中以 canvas/Cell 定位后产出 Path/Text 的做法。
/// </summary>
public class Table
{
    /// <summary>表格标题（可选，渲染为表格上方一行文字）</summary>
    public string? Title { get; set; }

    /// <summary>表格列数</summary>
    public int ColumnCount { get; set; }

    /// <summary>表格行数</summary>
    public int RowCount { get; set; }

    /// <summary>单元格集合</summary>
    public List<TableCell> Cells { get; } = new();

    /// <summary>表格左上角 X（mm，页坐标，默认 0）。</summary>
    public double X { get; set; } = 0;

    /// <summary>
    /// 表格左上角 Y（mm，页坐标）。
    /// 默认 -1 表示自动向下堆叠（多个表格连续 AddTable 时不会重叠）。
    /// </summary>
    public double Y { get; set; } = -1;

    /// <summary>表格总宽度（mm）。null 表示使用页面宽度减去左边距。</summary>
    public double? Width { get; set; }

    /// <summary>
    /// 各列宽度（mm）。null 表示等宽。
    /// 若提供，长度应等于 <see cref="ColumnCount"/>，总和将归一化为 <see cref="Width"/>。
    /// </summary>
    public double[]? ColumnWidths { get; set; }

    /// <summary>行高（mm，默认 8）</summary>
    public double RowHeight { get; set; } = 8;

    /// <summary>单元格字号（mm，默认 3.5）</summary>
    public double FontSize { get; set; } = 3.5;

    /// <summary>字族（默认 SimSun）</summary>
    public string FontFamily { get; set; } = "SimSun";

    /// <summary>边框颜色（十六进制 #RRGGBB，默认黑色）</summary>
    public string BorderColor { get; set; } = "#000000";

    /// <summary>文字颜色（十六进制 #RRGGBB，默认黑色）</summary>
    public string TextColor { get; set; } = "#000000";

    /// <summary>边框线宽（mm，默认 0.35）</summary>
    public double BorderWidth { get; set; } = 0.35;

    /// <summary>单元格内边距（mm，默认 1）</summary>
    public double CellPadding { get; set; } = 1;

    /// <summary>
    /// 表头行数（从 0 行起算的前 N 行视为表头）。
    /// 仅当 <see cref="RepeatHeader"/> 为 true 且本值 &gt; 0 时生效，
    /// 用于跨页时在新页顶部重复绘制表头。
    /// </summary>
    public int HeaderRows { get; set; } = 0;

    /// <summary>
    /// 是否在新页顶部重复表头（跨页分页时）。
    /// 默认 false。开启后须配合 <see cref="HeaderRows"/> 指定表头行数。
    /// 注意：跨页断点处的跨行(RowSpan)单元格不会拆分，可能导致该单元格在断页处视觉不完整。
    /// </summary>
    public bool RepeatHeader { get; set; } = false;

    /// <summary>添加单元格</summary>
    /// <param name="cell">要添加的单元格</param>
    /// <param name="rowIndex">行索引（从0开始）</param>
    /// <param name="columnIndex">列索引（从0开始）</param>
    public void AddCell(TableCell cell, int rowIndex, int columnIndex)
    {
        if (rowIndex >= RowCount || columnIndex >= ColumnCount)
            throw new ArgumentOutOfRangeException();

        cell.RowIndex = rowIndex;
        cell.ColumnIndex = columnIndex;
        Cells.Add(cell);
    }
}

/// <summary>
/// 单元格实体
/// </summary>
public class TableCell
{
    /// <summary>单元格内容</summary>
    public string? Content { get; set; }

    /// <summary>行索引</summary>
    public int RowIndex { get; set; }

    /// <summary>列索引</summary>
    public int ColumnIndex { get; set; }

    /// <summary>是否跨行（默认 1）</summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>是否跨列（默认 1）</summary>
    public int ColumnSpan { get; set; } = 1;

    /// <summary>单元格填充色（十六进制 #RRGGBB，null 表示不填充）</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>边框样式（预留，当前渲染未使用）</summary>
    public string? BorderStyle { get; set; }

    /// <summary>边框宽度（预留，当前渲染使用 Table.BorderWidth）</summary>
    public double BorderWidth { get; set; } = 0.5;
}
