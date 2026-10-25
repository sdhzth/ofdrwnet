using Org.BouncyCastle.Asn1;

namespace NetOfd.Sign;

/// <summary>SES 电子签章数据版本。</summary>
public enum SesVersion
{
    /// <summary>GM/T 0031-2014 安全电子签章密码技术规范。</summary>
    V1 = 1,

    /// <summary>GB/T 38540-2020 信息安全技术 安全电子签章密码技术规范。</summary>
    V4 = 4,

    /// <summary>GM/T 0031-2025 安全电子签章密码技术规范。</summary>
    V5 = 5
}

/// <summary>版本持有者：版本号与未构造的 ASN.1 顶层序列。对应 Java: SESVersionHolder。</summary>
public sealed class SesVersionHolder
{
    public SesVersion Version { get; }
    public Asn1Sequence ObjSeq { get; }

    public SesVersionHolder(SesVersion version, Asn1Sequence objSeq)
    {
        Version = version;
        ObjSeq = objSeq;
    }
}

/// <summary>
/// 电子印章/电子签章数据版本解析。
/// 对应 Java: org.ofdrw.gm.ses.parse.VersionParser
/// </summary>
public static class SesVersionParser
{
    /// <summary>从印章序列中提取 Header 中的 version 字段（eSealInfo → header → version）。</summary>
    private static int ExtractSealHeaderVersion(Asn1Sequence seq)
    {
        try
        {
            // 印章信息 SEQUENCE 的第一个元素是 header，第二个元素是 version
            var sealInfo = Asn1Sequence.GetInstance(seq[0]);
            var header = Asn1Sequence.GetInstance(sealInfo[0]);
            var version = DerInteger.GetInstance(header[1]);
            return version.Value.IntValue;
        }
        catch (Exception)
        {
            throw new ArgumentException("未知的数据结构，无法匹配任何已知版本电子印章");
        }
    }

    /// <summary>解析电子印章数据版本（SESeal）。</summary>
    public static SesVersionHolder ParseSesSealVersion(byte[] der)
    {
        Asn1Sequence seq;
        try
        {
            seq = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(der));
        }
        catch (Exception ex)
        {
            throw new ArgumentException("未知的数据结构，无法匹配任何已知版本电子印章", ex);
        }
        return ParseSesSealVersion(seq);
    }

    /// <summary>解析电子印章数据版本（SESeal 顶层序列）。</summary>
    public static SesVersionHolder ParseSesSealVersion(Asn1Sequence seq)
    {
        SesVersion version;
        if (seq.Count == 5)
        {
            // GM/T 0031-2025：印章信息、制章者证书、签名算法标识、签名值、时间戳【可选】
            version = SesVersion.V5;
        }
        else if (seq.Count == 4)
        {
            // 可能是 V4（固定字段）或 V5（无可选 timeStamp），通过内部 header version 区分
            int headerVer = ExtractSealHeaderVersion(seq);
            version = headerVer == 5 ? SesVersion.V5 : SesVersion.V4;
        }
        else if (seq.Count == 2)
        {
            // GM/T 0031-2014：印章信息、制章人对印章签名的信息
            version = SesVersion.V1;
        }
        else
        {
            throw new ArgumentException("未知的数据结构，无法匹配任何已知版本电子印章");
        }
        return new SesVersionHolder(version, seq);
    }

    /// <summary>解析电子签章数据版本（SES_Signature）。</summary>
    public static SesVersionHolder ParseSesSignatureVersion(byte[] der)
    {
        Asn1Sequence seq;
        try
        {
            seq = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(der));
        }
        catch (Exception ex)
        {
            throw new ArgumentException("未知的数据结构，无法匹配任何已知版本电子签章数据", ex);
        }
        return ParseSesSignatureVersion(seq);
    }

    /// <summary>解析电子签章数据版本（SES_Signature 顶层序列）。</summary>
    public static SesVersionHolder ParseSesSignatureVersion(Asn1Sequence seq)
    {
        SesVersion version;
        if (seq.Count is >= 4 and <= 5)
        {
            // V4 和 V5 签章结构都是 4-5 字段，通过内部 TBS_Sign 的 eseal header version 区分
            try
            {
                var tbsSign = Asn1Sequence.GetInstance(seq[0]);
                var esealSeq = Asn1Sequence.GetInstance(tbsSign[1]);
                int headerVer = ExtractSealHeaderVersion(esealSeq);
                version = headerVer == 5 ? SesVersion.V5 : SesVersion.V4;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ArgumentException("无法解析电子签章数据版本", ex);
            }
        }
        else if (seq.Count == 2)
        {
            // GM/T 0031-2014：待电子签章数据、电子签章中签名的信息
            version = SesVersion.V1;
        }
        else
        {
            throw new ArgumentException("未知的数据结构，无法匹配任何已知版本电子签章数据");
        }
        return new SesVersionHolder(version, seq);
    }
}