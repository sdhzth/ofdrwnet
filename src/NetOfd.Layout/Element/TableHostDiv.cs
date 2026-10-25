namespace NetOfd.Layout.Element;

/// <summary>
/// 流式队列内部载体：把 <see cref="Table"/>（NetOfd.Layout 自研扩展，Java ofdrw 无对应表格类）
/// 包装为 <see cref="Div"/> 流元素，使 <see cref="OFDDoc.AddTable"/> 与 <see cref="OFDDoc.Add"/>
/// 共享同一书写顺序队列——表格插在段落之间时按 Add 先后就地排布，而非统一追加到文末。
/// 引擎在 <c>FlushStreamQueue</c> 中优先识别本类型并转入表格渲染分支，不参与普通盒渲染。
/// </summary>
internal sealed class TableHostDiv : Div
{
    /// <summary>被包装的表格实体。</summary>
    public Table Table { get; }

    public TableHostDiv(Table table)
    {
        Table = table ?? throw new ArgumentNullException(nameof(table));
    }
}
