using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;

namespace NetOfd.Sign.Sm2Strut;

/// <summary>
/// GM/T 35275-2017 6.7 IssuerAndSerialNumber。
/// <code>
/// IssuerAndSerialNumber ::= SEQUENCE {
///     issuer         Name,
///     serialNumber   CertificateSerialNumber
/// }
/// </code>
/// 参考 ofdrw-gm 的 sm2strut.IssuerAndSerialNumber。
/// </summary>
public sealed class IssuerAndSerialNumber : Asn1Encodable
{
    public X509Name Name { get; set; } = null!;
    public DerInteger CertSerialNumber { get; set; } = null!;

    public IssuerAndSerialNumber() { }

    public IssuerAndSerialNumber(X509Name name, DerInteger certSerialNumber)
    {
        Name = name;
        CertSerialNumber = certSerialNumber;
    }

    public static IssuerAndSerialNumber GetInstance(object? obj)
    {
        if (obj is IssuerAndSerialNumber iasn) return iasn;
        if (obj != null) return new IssuerAndSerialNumber(Asn1Sequence.GetInstance(obj));
        return null!;
    }

    private IssuerAndSerialNumber(Asn1Sequence seq)
    {
        Name = X509Name.GetInstance(seq[0]);
        CertSerialNumber = DerInteger.GetInstance(seq[1]);
    }

    public override Asn1Object ToAsn1Object()
        => new DerSequence(Name, CertSerialNumber);
}