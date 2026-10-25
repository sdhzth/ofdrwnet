using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Sign.Sm2Strut.Builder;

/// <summary>证书与签名值的封装。</summary>
public sealed class CertSigHolder
{
    public byte[] Signature { get; set; }
    public X509Certificate Certificate { get; set; }

    private X509CertificateStructure? _asn1Obj;

    public CertSigHolder(byte[] signature, X509Certificate certificate)
    {
        Signature = signature;
        Certificate = certificate;
    }

    /// <summary>转换为 BC ASN.1 X509 证书结构。</summary>
    public X509CertificateStructure GetAsn1Cert()
    {
        if (Certificate == null)
            throw new ArgumentException("证书数据(certificate)为空");
        return _asn1Obj ??= Certificate.CertificateStructure;
    }
}