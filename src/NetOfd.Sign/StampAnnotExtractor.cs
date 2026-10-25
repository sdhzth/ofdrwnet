using NetOfd.Core.BasicType;
using NetOfd.Reader;
using NetOfd.Reader.Model;
using Org.BouncyCastle.Asn1;
using System.Xml.Linq;

namespace NetOfd.Sign;

/// <summary>
/// 电子印章图片提取与 OFD 签章信息读取。
/// <para>
/// 对应 Java:
/// <list type="bullet">
/// <item>低层 <see cref="ExtractSealPicture(byte[])"/> —— Java StampAnnotEntity.init（版本嗅探后按结构取 picture）；</item>
/// <item>高层 <see cref="GetStampAnnots(OFDReader)"/> —— Java OFDReader.getStampAnnots（遍历签名列表读取签名值文件）。</item>
/// </list>
/// </para>
/// <para>
/// 注意：Java OFDReader.getStampAnnots 只做版本嗅探与结构提取，<b>不做验签</b>；
/// 单条签名解析失败（文件缺失 / 非 SES 签名值等）时忽略并继续。本实现对齐该语义。
/// </para>
/// <para>
/// 版本无关的 ASN.1 提取路径（v1/v4/v5 的 eSealInfo 结构同构，均取固定索引）：
/// <code>
/// SES_Signature[0]   TBS_Sign
/// TBS_Sign[1]        SESeal（电子印章）
/// SESeal[0]          SESealInfo（印章信息：header / esID / property / picture[/ extDatas]）
/// SealInfo[3]        SES_ESPictrueInfo（图片：type(IA5String) / data(OCTET) / width / height）
/// </code>
/// </para>
/// </summary>
public static class StampAnnotExtractor
{
    /// <summary>
    /// 从电子签章值 DER 数据中嗅探版本并提取印章图片。
    /// <para>对应 Java: SESVersionHolder + StampAnnotEntity.init。</para>
    /// </summary>
    /// <param name="signatureValueDer">签名值文件原始字节（如 SignedValue.dat）。</param>
    /// <returns>印章图片实体；结构无法解析（如为电子签名值或文件损坏）时返回 null。</returns>
    public static StampAnnotEntity? ExtractSealPicture(byte[] signatureValueDer)
    {
        try
        {
            SesVersionHolder holder = SesVersionParser.ParseSesSignatureVersion(signatureValueDer);
            return ExtractSealPicture(holder);
        }
        catch (Exception)
        {
            // 无法解析的印章（签名值可能是电子签名值 CMS 等），对齐 Java 忽略语义
            return null;
        }
    }

    /// <summary>从已嗅探版本的签章顶层序列提取印章图片。</summary>
    public static StampAnnotEntity? ExtractSealPicture(SesVersionHolder holder)
    {
        try
        {
            Asn1Sequence sig = holder.ObjSeq;
            // SES_Signature[0] = TBS_Sign（v1/v4/v5 一致）
            Asn1Sequence tbs = Asn1Sequence.GetInstance(sig[0]);
            // TBS_Sign[1] = SESeal
            Asn1Sequence eseal = Asn1Sequence.GetInstance(tbs[1]);
            // SESeal[0] = SealInfo（印章信息）
            Asn1Sequence sealInfo = Asn1Sequence.GetInstance(eseal[0]);
            // SealInfo[3] = picture（前 3 个元素：header / esID / property；[4] 可选 extDatas）
            Asn1Sequence picture = Asn1Sequence.GetInstance(sealInfo[3]);
            string imgType = DerIA5String.GetInstance(picture[0]).GetString();
            byte[] imageByte = Asn1OctetString.GetInstance(picture[1]).GetOctets();
            if (imageByte.Length == 0)
                return null;
            return new StampAnnotEntity(holder.Version, imgType, imageByte);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 获取默认文档中的签章信息。
    /// <para>对应 Java: OFDReader.getStampAnnots。</para>
    /// </summary>
    /// <param name="reader">已打开的 OFD 读取器。</param>
    /// <returns>签章信息列表；无签名或无法解析时为 empty。</returns>
    public static List<StampAnnotEntity> GetStampAnnots(OFDReader reader)
    {
        var res = new List<StampAnnotEntity>();
        if (!reader.HasSignature())
        {
            // 没有签名的情况下返还空集合，防止 NPE（对齐 Java）
            return res;
        }

        List<SignatureInfo> sigInfoList = reader.GetDefaultSignatures();
        if (sigInfoList is null || sigInfoList.Count == 0)
            return res;

        ResourceLocator rl = reader.GetResourceLocator();
        foreach (SignatureInfo sigInfoItem in sigInfoList)
        {
            try
            {
                if (string.IsNullOrEmpty(sigInfoItem.BaseLoc))
                    continue;
                // 签名描述文件（如 /Doc_0/Signs/Sign_0/Signature.xml）
                var signatureBaseLoc = new STLoc(sigInfoItem.BaseLoc);
                XElement? sigDesp = rl.Get(signatureBaseLoc);
                if (sigDesp is null)
                    continue;
                // 签名值文件位置（相对签名描述文件所在目录，或包根绝对路径）
                XElement? signedValueEl = sigDesp.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "SignedValue");
                if (signedValueEl is null)
                    continue;
                string signedValueText = signedValueEl.Value.Trim();
                if (signedValueText.Length == 0)
                    continue;

                STLoc signedValueLoc = STLoc.Resolve(signatureBaseLoc.Value, signedValueText);
                byte[]? signedValueBytes = rl.GetRaw(signedValueLoc);
                if (signedValueBytes is null || signedValueBytes.Length == 0)
                    continue;

                StampAnnotEntity? entity = ExtractSealPicture(signedValueBytes);
                if (entity is not null)
                    res.Add(entity);
            }
            catch (Exception)
            {
                // 忽略错误：文件不存在 / 无法解析的印章（签名值可能是电子签名值）
            }
        }
        return res;
    }
}
