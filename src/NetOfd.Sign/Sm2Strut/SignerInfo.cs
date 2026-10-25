using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;

namespace NetOfd.Sign.Sm2Strut;

/// <summary>
/// GM/T 35275-2017 signerInfo 结构。
/// <code>
/// SignerInfo ::= SEQUENCE {
///     version                    Version,
///     issuerAndSerialNumber      IssuerAndSerialNumber,
///     digestAlgorithm            DigestAlgorithmIdentifier,
///     authenticatedAttributes    [0] IMPLICIT Attributes OPTIONAL,
///     digestEncryptionAlgorithm  DigestEncryptionAlgorithmIdentifier,
///     encryptedDigest            EncryptedDigest,   -- OCTET STRING
///     unauthenticatedAttributes  [1] IMPLICIT Attributes OPTIONAL
/// }
/// </code>
/// 参考 ofdrw-gm 的 sm2strut.SignerInfo。
/// </summary>
public sealed class SignerInfo : Asn1Encodable
{
    public static readonly DerInteger Version1 = new(1);

    public DerInteger Version { get; set; } = Version1;
    public IssuerAndSerialNumber IssuerAndSerialNumber { get; set; } = null!;
    public AlgorithmIdentifier DigestAlgorithm { get; set; } = null!;
    public Asn1Set? AuthenticatedAttributes { get; set; }
    public AlgorithmIdentifier DigestEncryptionAlgorithm { get; set; } = null!;
    public Asn1OctetString EncryptedDigest { get; set; } = null!;
    public Asn1Set? UnauthenticatedAttributes { get; set; }

    public SignerInfo() { }

    public SignerInfo(
        IssuerAndSerialNumber issuerAndSerialNumber,
        AlgorithmIdentifier digestAlgorithm,
        AlgorithmIdentifier digestEncryptionAlgorithm,
        Asn1OctetString encryptedDigest)
    {
        Version = Version1;
        IssuerAndSerialNumber = issuerAndSerialNumber;
        DigestAlgorithm = digestAlgorithm;
        DigestEncryptionAlgorithm = digestEncryptionAlgorithm;
        EncryptedDigest = encryptedDigest;
    }

    public static SignerInfo GetInstance(object? obj)
    {
        if (obj is SignerInfo si) return si;
        if (obj != null) return new SignerInfo(Asn1Sequence.GetInstance(obj));
        return null!;
    }

    private SignerInfo(Asn1Sequence seq)
    {
        int i = 0;
        Version = DerInteger.GetInstance(seq[i++]);
        IssuerAndSerialNumber = IssuerAndSerialNumber.GetInstance(seq[i++]);
        DigestAlgorithm = AlgorithmIdentifier.GetInstance(seq[i++]);
        var obj = seq[i++];
        if (obj is Asn1TaggedObject)
        {
            AuthenticatedAttributes = Asn1Set.GetInstance((Asn1TaggedObject)obj, false);
            DigestEncryptionAlgorithm = AlgorithmIdentifier.GetInstance(seq[i++]);
        }
        else
        {
            AuthenticatedAttributes = null;
            DigestEncryptionAlgorithm = AlgorithmIdentifier.GetInstance(obj);
        }
        EncryptedDigest = Asn1OctetString.GetInstance(seq[i++]);
        if (i < seq.Count)
            UnauthenticatedAttributes = Asn1Set.GetInstance((Asn1TaggedObject)seq[i++], false);
    }

    public override Asn1Object ToAsn1Object()
    {
        var v = new Asn1EncodableVector { Version, IssuerAndSerialNumber, DigestAlgorithm };
        if (AuthenticatedAttributes != null)
            v.Add(new DerTaggedObject(false, 0, AuthenticatedAttributes));
        v.Add(DigestEncryptionAlgorithm);
        v.Add(EncryptedDigest);
        if (UnauthenticatedAttributes != null)
            v.Add(new DerTaggedObject(false, 1, UnauthenticatedAttributes));
        return new DerSequence(v);
    }
}