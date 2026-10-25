using System;
using System.IO;
using System.IO.Compression;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Sign;

// 工作区输出目录（便于直接预览/交付）
var workDir = @"C:\Users\Administrator\WorkBuddy\2026-08-08-07-15-44";
var outDir = Path.Combine(workDir, "demo_output");
Directory.CreateDirectory(outDir);

// 1. 生成自签名 SM2 证书（演示用；生产应使用合规 CA 签发的国密证书）
var keyPair = GmCrypto.GenerateSm2KeyPair();
var cert = CertUtil.GenerateSelfSignedSm2("NetOfd 演示签章者", keyPair);
Console.WriteLine($"SM2 公钥位数: {(keyPair.Public as Org.BouncyCastle.Crypto.Parameters.ECPublicKeyParameters)?.Parameters.Curve.FieldSize}");
Console.WriteLine($"证书主题: {CertUtil.ToDotNet(cert).Subject}");

// 2. 用布局引擎生成一个示例 OFD
var unsignedPath = Path.Combine(outDir, "demo_unsigned.ofd");
if (File.Exists(unsignedPath)) File.Delete(unsignedPath);
using (var doc = new OFDDoc(unsignedPath))
{
    doc.Add(new Paragraph().AddText("NetOfd 数字签名示例 (OFD SM2 国密签章)").SetFontSize(16d));
    doc.Add(new Paragraph().AddText("本文件由 .NET 移植版 ofdrw-sign 进行整体保护签名。").SetFontSize(10d));
    doc.Add(new Paragraph().AddText("坚持就是胜利，加油！").SetFontSize(8d));
}

// 3. 签名
var signedPath = Path.Combine(outDir, "demo_signed.ofd");
if (File.Exists(signedPath)) File.Delete(signedPath);
var signTime = "2026-08-08 10:00:00";
OfdSigner.Sign(unsignedPath, signedPath, keyPair, cert, signTime);
Console.WriteLine($"已签名文件: {signedPath}");

// 4. 验证
var result = OfdValidator.Verify(signedPath);
Console.WriteLine($"验证结果: AllValid={result.AllValid}, 签名数={result.SignatureCount}");
foreach (var s in result.Signatures)
{
    Console.WriteLine($"  签名 [{s.SignId}] 类型={s.Type} 有效={s.Valid} 杂凑不符数={s.ReferenceMismatch}");
    Console.WriteLine($"    签章人={s.CertSubject}  签章时间={s.SignTime:yyyy-MM-dd HH:mm:ss}");
    if (!s.Valid && s.Error != null) Console.WriteLine($"    错误={s.Error}");
}

// 5. 篡改演示：修改受保护文件后验证应失败
var bytes = File.ReadAllBytes(signedPath);
var map = new System.Collections.Generic.Dictionary<string, byte[]>(StringComparer.Ordinal);
using (var ms = new MemoryStream(bytes))
using (var za = new ZipArchive(ms, ZipArchiveMode.Read))
    foreach (var e in za.Entries)
    {
        using var s = e.Open();
        using var buf = new MemoryStream();
        s.CopyTo(buf);
        map[e.FullName] = buf.ToArray();
    }

foreach (var key in map.Keys)
{
    var n = key.Replace('\\', '/');
    if (n.EndsWith("Doc_0/Document.xml"))
    {
        var original = map[key];
        var modified = new byte[original.Length + 1];
        Array.Copy(original, modified, original.Length);
        modified[original.Length] = (byte)' ';
        map[key] = modified;
        break;
    }
}
using var outMs = new MemoryStream();
using (var za = new ZipArchive(outMs, ZipArchiveMode.Create, true))
    foreach (var kv in map)
    {
        var e = za.CreateEntry(kv.Key);
        using var s = e.Open();
        s.Write(kv.Value, 0, kv.Value.Length);
    }
var tamperedPath = Path.Combine(outDir, "demo_tampered.ofd");
if (File.Exists(tamperedPath)) File.Delete(tamperedPath);
File.WriteAllBytes(tamperedPath, outMs.ToArray());

var tamperedResult = OfdValidator.Verify(tamperedPath);
Console.WriteLine($"篡改后验证: AllValid={tamperedResult.AllValid} (应为 False)");
if (!tamperedResult.AllValid)
    foreach (var s in tamperedResult.Signatures)
        Console.WriteLine($"  篡改检测: [{s.SignId}] 有效={s.Valid} 杂凑不符数={s.ReferenceMismatch} 错误={s.Error}");

Console.WriteLine("OK");
