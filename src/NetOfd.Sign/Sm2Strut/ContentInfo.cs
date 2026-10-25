using Org.BouncyCastle.Asn1;

namespace NetOfd.Sign.Sm2Strut;

/// <summary>
/// GM/T 35275-2017 6.10 ContentInfo。
/// <code>
/// ContentInfo ::= SEQUENCE {
///     contentType ContentType,
///     content     [0] EXPLICIT ANY DEFINED BY contentType OPTIONAL
/// }
/// </code>
/// 参考 ofdrw-gm 的 sm2strut.ContentInfo。
/// </summary>
public sealed class ContentInfo : Asn1Encodable
{
    public DerObjectIdentifier ContentType { get; set; } = null!;
    public Asn1Encodable? Content { get; set; }

    public ContentInfo() { }

    public ContentInfo(DerObjectIdentifier contentType, Asn1Encodable content)
    {
        ContentType = contentType;
        Content = content;
    }

    public static ContentInfo GetInstance(object? obj)
    {
        if (obj is ContentInfo info) return info;
        if (obj != null) return new ContentInfo(Asn1Sequence.GetInstance(obj));
        return null!;
    }

    private ContentInfo(Asn1Sequence seq)
    {
        ContentType = DerObjectIdentifier.GetInstance(seq[0]);
        if (seq.Count > 1)
        {
            var tagged = Asn1TaggedObject.GetInstance(seq[1]);
            if (!tagged.IsExplicit() || tagged.TagNo != 0)
                throw new ArgumentException("content 无法解析");
            Content = tagged.GetExplicitBaseObject();
        }
    }

    public override Asn1Object ToAsn1Object()
    {
        var v = new Asn1EncodableVector { ContentType };
        if (Content != null)
            v.Add(new DerTaggedObject(0, Content));
        return new DerSequence(v);
    }
}