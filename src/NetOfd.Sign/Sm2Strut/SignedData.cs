using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;

namespace NetOfd.Sign.Sm2Strut;

/// <summary>
/// GM/T 35275-2017 8.1 signedData 结构。
/// <code>
/// SignedData ::= SEQUENCE {
///     version              Version,                          -- 1
///     digestAlgorithms     DigestAlgorithmIdentifiers,       -- SET
///     contentInfo          ContentInfo,
///     certificates         [0] IMPLICIT ExtendedCertificatesAndCertificates OPTIONAL,
///     crls                 [1] IMPLICIT CertificateRevocationLists OPTIONAL,
///     signerInfos          SignerInfos                       -- SET OF SignerInfo
/// }
/// </code>
/// 参考 ofdrw-gm 的 sm2strut.SignedData。
/// </summary>
public sealed class SignedData : Asn1Encodable
{
    public static readonly DerInteger Version1 = new(1);

    public DerInteger Version { get; set; } = Version1;
    public Asn1Set DigestAlgorithms { get; set; } = null!;
    public ContentInfo ContentInfo { get; set; } = null!;
    public Asn1Set? Certificates { get; set; }
    public Asn1Set? Crls { get; set; }
    public Asn1Set SignerInfos { get; set; } = null!;

    public SignedData() { }

    public SignedData(Asn1Set digestAlgorithms, ContentInfo contentInfo, Asn1Set? certificates, Asn1Set signerInfos)
    {
        DigestAlgorithms = digestAlgorithms;
        ContentInfo = contentInfo;
        Certificates = certificates;
        SignerInfos = signerInfos;
    }

    public static SignedData GetInstance(object? obj)
    {
        if (obj is SignedData sd) return sd;
        if (obj != null) return new SignedData(Asn1Sequence.GetInstance(obj));
        return null!;
    }

    private SignedData(Asn1Sequence seq)
    {
        int i = 0;
        Version = DerInteger.GetInstance(seq[i++]);
        DigestAlgorithms = Asn1Set.GetInstance(seq[i++]);
        ContentInfo = ContentInfo.GetInstance(seq[i++]);
        while (i < seq.Count)
        {
            var o = seq[i++];
            if (o is Asn1TaggedObject tagged)
            {
                switch (tagged.TagNo)
                {
                    case 0:
                        Certificates = Asn1Set.GetInstance(tagged, false);
                        break;
                    case 1:
                        Crls = Asn1Set.GetInstance(tagged, false);
                        break;
                    default:
                        throw new ArgumentException($"无法解析 未知标签 {tagged.TagNo}");
                }
            }
            else
            {
                SignerInfos = Asn1Set.GetInstance(o);
            }
        }
    }

    /// <summary>根据 IssuerAndSerialNumber 查找匹配的签名证书。</summary>
    public X509CertificateStructure? GetSignCert(IssuerAndSerialNumber iasn)
    {
        if (iasn == null || Certificates == null) return null;
        foreach (var item in Certificates)
        {
            var c = X509CertificateStructure.GetInstance(item);
            bool snEq = c.SerialNumber.Value.Equals(iasn.CertSerialNumber.Value);
            bool dnEq = c.Issuer.Equals(iasn.Name);
            if (snEq && dnEq) return c;
        }
        return null;
    }

    public override Asn1Object ToAsn1Object()
    {
        var v = new Asn1EncodableVector { Version, DigestAlgorithms, ContentInfo };
        if (Certificates != null)
            v.Add(new DerTaggedObject(false, 0, Certificates));
        if (Crls != null)
            v.Add(new DerTaggedObject(false, 1, Crls));
        v.Add(SignerInfos);
        return new DerSequence(v);
    }
}