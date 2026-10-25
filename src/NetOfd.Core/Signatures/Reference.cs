using NetOfd.Core.BasicType;
using System.Xml.Linq;

namespace NetOfd.Core.Signatures;

/// <summary>
/// 针对一个文件的摘要节点（GB/T 33190-2016 §18.2.2 签名的范围 图 87 表 68）。
/// 对应 Java: org.ofdrw.core.signatures.range.Reference
/// </summary>
public class Reference : OFDElement
{
    public Reference() : base("Reference") { }

    public Reference(XElement element) : base(element) { }

    /// <summary>以包内文件绝对路径与摘要值创建。</summary>
    /// <param name="fileRef">指向包内文件的绝对路径</param>
    /// <param name="checkValue">对包内文件计算所得的杂凑值（写出时做 Base64 编码）</param>
    public Reference(STLoc fileRef, byte[] checkValue) : this()
    {
        FileRef = fileRef;
        CheckValue = checkValue;
    }

    /// <summary>【必选 属性】指向包内文件的绝对路径。</summary>
    public STLoc? FileRef
    {
        get
        {
            string? v = GetAttr("FileRef");
            return v is null ? null : new STLoc(v);
        }
        set => SetAttr("FileRef", value?.Value);
    }

    /// <summary>
    /// 【必选】对包内文件计算所得的杂凑值（<c>CheckValue</c> 子元素，Base64 编码）。
    /// <para>节点不存在时返回 null —— Java 版此处抛 <c>IllegalArgumentException</c>，
    /// .NET 版改为返回 null 以便调用方用 <c>is null</c> 判断。</para>
    /// </summary>
    public byte[]? CheckValue
    {
        get
        {
            string? t = GetChildText("CheckValue");
            return string.IsNullOrWhiteSpace(t) ? null : Convert.FromBase64String(t.Trim());
        }
        set
        {
            if (value is null || value.Length == 0)
            {
                GetChildElement("CheckValue")?.Remove();
                return;
            }
            SetChildText("CheckValue", Convert.ToBase64String(value));
        }
    }
}
