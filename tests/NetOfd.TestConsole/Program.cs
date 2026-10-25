using System.Text;
using NetOfd.Converter;
using NetOfd.Core.BasicStructure.OFD;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Layout.Edit;
using NetOfd.Reader;
using NetOfd.Sign;
using NetOfd.Tool;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

try
{
    return args[0].ToLowerInvariant() switch
    {
        "info" => CmdInfo(args),
        "verify" => CmdVerify(args),
        "text" => CmdText(args),
        "render" => CmdRender(args),
        "merge" => CmdMerge(args),
        "sample" => CmdSample(args),
        "sign" => CmdSign(args),
        "protect" => CmdProtect(args),
        "decrypt" => CmdDecrypt(args),
        "ofda-check" => CmdOfdaCheck(args),
        "ofda-convert" => CmdOfdaConvert(args),
        _ => Unknown(args[0]),
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"错误: {ex.Message}");
    return 2;
}

static int Unknown(string cmd)
{
    Console.Error.WriteLine($"未知命令: {cmd}");
    PrintUsage();
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("""
        NetOfd 命令行工具
        用法:
          netofd info <file.ofd>                     查看文档信息（页数/元数据/结构部件）
          netofd verify <file.ofd>                   验证数字签名
          netofd text <file.ofd>                     提取全文文本
          netofd render <file.ofd> <outDir> [ppm]    渲染为 PNG 图像（默认 15ppm）
          netofd merge <out.ofd> <in1.ofd> <in2.ofd> ... 合并多个文档
          netofd sample <out.ofd>                    生成示例文档
          netofd sign <in.ofd> <out.ofd> --p12 <cert.p12> --pwd <密码>
                     [--seal <seal.der>] [--time "yyyy-MM-dd HH:mm:ss"] [--version 1|4|5]
                                                    数字签名/电子签章（SES）
          netofd protect <in.ofd> <out.ofd> --user <用户名> --pwd <口令>
                                                    口令加密文档
          netofd decrypt <in.ofd> <out.ofd> [--user <用户名>] --pwd <口令>
                                                    口令解密文档
          netofd ofda-check <file.ofd>               OFD-A 归档合规检查（GB/T 42133）
          netofd ofda-convert <in.ofd> <out.ofd> [--user <u> --pwd <p>]
                                                    OFD-A 去技术化转换
        """);
}

static int CmdInfo(string[] args)
{
    if (args.Length < 2) { Console.Error.WriteLine("缺少文件路径"); return 1; }
    var path = args[1];
    using var reader = new OFDReader(path);

    var docInfo = reader.GetOfd().GetFirstDocBody()?.DocInfo;
    Console.WriteLine($"文件: {path}");
    if (docInfo is not null)
    {
        Console.WriteLine($"  DocID:         {docInfo.DocID ?? "-"}");
        Console.WriteLine($"  标题:          {docInfo.Title ?? "-"}");
        Console.WriteLine($"  作者:          {docInfo.Author ?? "-"}");
        Console.WriteLine($"  创建者:        {docInfo.Creator ?? "-"}");
        Console.WriteLine($"  创建时间:      {docInfo.CreationDate ?? "-"}");
        Console.WriteLine($"  MaxUnitID:     {docInfo.MaxUnitID?.Value.ToString() ?? "-"}");
    }
    Console.WriteLine($"  页数:          {reader.GetPageCount()}");
    foreach (var (name, label) in new[]
    {
        ("Signatures.xml", "数字签名"),
        ("Attachments.xml", "附件"),
        ("CustomTags.xml", "自定义标"),
        ("Versions/V_1/DocVersion.xml", "版本快照"),
    })
    {
        var n = CountParts(path, name);
        if (n > 0) Console.WriteLine($"  {label}:          {n} 处");
    }
    return 0;
}

static int CountParts(string path, string entrySuffix)
{
    using var za = System.IO.Compression.ZipFile.OpenRead(path);
    return za.Entries.Count(e =>
        e.FullName.Replace('\\', '/').EndsWith(entrySuffix, StringComparison.OrdinalIgnoreCase));
}

static int CmdVerify(string[] args)
{
    if (args.Length < 2) { Console.Error.WriteLine("缺少文件路径"); return 1; }
    var signed = File.ReadAllBytes(args[1]);
    var result = OfdValidator.Verify(signed);
    Console.WriteLine($"文档: {args[1]}");
    Console.WriteLine($"签名数量: {result.SignatureCount}");
    if (result.Error is not null) Console.WriteLine($"错误: {result.Error}");
    foreach (var s in result.Signatures)
    {
        Console.WriteLine($"  [{s.SignId}] {(s.Valid ? "有效" : "无效")}" +
            $" 签名者={s.CertSubject}{(s.Error is null ? "" : $" 错误={s.Error}")}");
    }
    Console.WriteLine(result.AllValid ? "结果: 全部有效" : "结果: 验证失败");
    return result.AllValid ? 0 : 3;
}

static int CmdText(string[] args)
{
    if (args.Length < 2) { Console.Error.WriteLine("缺少文件路径"); return 1; }
    using var reader = new OFDReader(args[1]);
    var pages = reader.GetPages().ToList();
    var texts = ContentExtractor.ExtractAllPagesText(pages);
    var sb = new StringBuilder();
    foreach (var t in texts) sb.AppendLine(t);
    Console.Write(sb.ToString());
    return 0;
}

static int CmdRender(string[] args)
{
    if (args.Length < 3) { Console.Error.WriteLine("用法: netofd render <file.ofd> <outDir> [ppm] [-p]"); return 1; }
    double ppm = 15;
    bool parallel = false;
    for (int i = 3; i < args.Length; i++)
    {
        if (args[i] == "-p") parallel = true;
        else if (double.TryParse(args[i], out var p)) ppm = p;
    }
    Directory.CreateDirectory(args[2]);
    using var exporter = new ImageExporter(args[1], args[2], "png", ppm) { ParallelExport = parallel };
    var paths = exporter.Export();
    Console.WriteLine($"已渲染 {paths.Count} 页 -> {args[2]}{(parallel ? $"（并行，{Environment.ProcessorCount} 线程）" : "")}");
    foreach (var f in paths) Console.WriteLine($"  {f}");
    return 0;
}

static int CmdMerge(string[] args)
{
    if (args.Length < 4) { Console.Error.WriteLine("用法: netofd merge <out.ofd> <in1.ofd> <in2.ofd> ..."); return 1; }
    using var merger = new OfdMerger(args[1]);
    foreach (var input in args.Skip(2)) merger.Add(input);
    Console.WriteLine($"已合并 {args.Length - 2} 个文档 -> {args[1]}");
    return 0;
}

static int CmdSample(string[] args)
{
    if (args.Length < 2) { Console.Error.WriteLine("缺少输出路径"); return 1; }
    using (var doc = new OFDDoc(args[1]))
    {
        doc.AddPage();
        doc.Add(new Paragraph().SetWidth(100)
            .Add(new Span("NetOfd 示例文档").SetFontSize(6d).SetBold())
            .Add(new Span("——由 netofd sample 命令生成。")).SetFontSize(4.5d));
    }
    Console.WriteLine($"已生成示例文档: {args[1]}");
    return 0;
}

// ── 签名 / 加解密 ─────────────────────────────────────────────

static Dictionary<string, string> ParseOptions(string[] args, int start)
{
    var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (int i = start; i + 1 < args.Length; i += 2)
        if (args[i].StartsWith("--")) opts[args[i][2..]] = args[i + 1];
    return opts;
}

static int CmdSign(string[] args)
{
    if (args.Length < 3) { Console.Error.WriteLine("用法: netofd sign <in.ofd> <out.ofd> --p12 <cert.p12> --pwd <密码> [--seal seal.der] [--version 1|4|5]"); return 1; }
    var opts = ParseOptions(args, 3);
    if (!opts.TryGetValue("p12", out var p12) || !opts.TryGetValue("pwd", out var pwd))
    {
        Console.Error.WriteLine("缺少 --p12 或 --pwd"); return 1;
    }

    var prvKey = NetOfd.Sign.Pkcs12Tools.ReadPrvKey(p12, pwd);
    var cert = NetOfd.Sign.Pkcs12Tools.ReadUserCert(p12, pwd);
    var keyPair = new Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair(cert.GetPublicKey(), prvKey);

    Org.BouncyCastle.Asn1.Asn1Encodable? seal = null;
    if (opts.TryGetValue("seal", out var sealPath))
        seal = Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(File.ReadAllBytes(sealPath));

    int version = 1;
    if (opts.TryGetValue("version", out var vStr) && int.TryParse(vStr, out var v)) version = v;
    else if (seal is not null) version = 4;

    opts.TryGetValue("time", out var time);

    var signed = OfdSigner.Sign(inputOfd: File.ReadAllBytes(args[1]),
        keyPair: keyPair, cert: cert, signatureDateTime: time,
        seal: seal, isSeal: seal is not null, version: version);
    File.WriteAllBytes(args[2], signed);
    Console.WriteLine($"已签署（SES_V{version}{(seal is not null ? " 含印章" : "")}）-> {args[2]}");

    var result = OfdValidator.Verify(signed);
    Console.WriteLine(result.AllValid ? "自检: 签名有效" : $"自检警告: {result.Error}");
    return result.AllValid ? 0 : 3;
}

static int CmdProtect(string[] args)
{
    if (args.Length < 3) { Console.Error.WriteLine("用法: netofd protect <in.ofd> <out.ofd> --user <用户名> --pwd <口令>"); return 1; }
    var opts = ParseOptions(args, 3);
    if (!opts.TryGetValue("user", out var user) || !opts.TryGetValue("pwd", out var pwd))
    {
        Console.Error.WriteLine("缺少 --user 或 --pwd"); return 1;
    }

    using (var enc = new NetOfd.Crypto.OfdEncryptor(args[1], args[2]))
    {
        enc.AddUser(new NetOfd.Crypto.UserPasswordEncryptor(user, pwd));
        enc.Encrypt();
    }
    Console.WriteLine($"已加密 -> {args[2]}");
    return 0;
}

static int CmdDecrypt(string[] args)
{
    if (args.Length < 3) { Console.Error.WriteLine("用法: netofd decrypt <in.ofd> <out.ofd> [--user <用户名>] --pwd <口令>"); return 1; }
    var opts = ParseOptions(args, 3);
    if (!opts.TryGetValue("pwd", out var pwd))
    {
        Console.Error.WriteLine("缺少 --pwd"); return 1;
    }
    opts.TryGetValue("user", out var user);

    using (var dec = new NetOfd.Crypto.OfdDecryptor(args[1], args[2]))
    {
        dec.AddUser(new NetOfd.Crypto.UserPasswordDecryptor(user, pwd));
        dec.Decrypt();
    }
    Console.WriteLine($"已解密 -> {args[2]}");
    return 0;
}

// ── OFD-A 归档 ────────────────────────────────────────────────

static int CmdOfdaCheck(string[] args)
{
    if (args.Length < 2) { Console.Error.WriteLine("缺少文件路径"); return 1; }
    var checker = new NetOfd.Archive.OFDArchiveChecker();
    var violations = checker.Check(args[1]);
    Console.WriteLine($"OFD-A 合规检查: {args[1]}");
    if (violations.Count == 0)
    {
        Console.WriteLine("结果: 合规，未发现违规项");
        return 0;
    }
    foreach (var v in violations)
        Console.WriteLine($"  [{v.Level}] {v.RuleName}: {v.Description}" +
            $"{(string.IsNullOrEmpty(v.Location) ? "" : $" @ {v.Location}")}" +
            $"{(string.IsNullOrEmpty(v.ActualValue) ? "" : $"（实际={v.ActualValue}）")}");
    Console.WriteLine($"结果: {violations.Count} 项违规");
    return 4;
}

static int CmdOfdaConvert(string[] args)
{
    if (args.Length < 3) { Console.Error.WriteLine("用法: netofd ofda-convert <in.ofd> <out.ofd> [--user <u> --pwd <p>]"); return 1; }
    var opts = ParseOptions(args, 3);
    var converter = new NetOfd.Archive.Convert.OFDArchiveConverter();
    if (opts.TryGetValue("pwd", out var pwd))
        converter.AddDecryptor(new NetOfd.Crypto.UserPasswordDecryptor(
            opts.TryGetValue("user", out var user) ? user : null, pwd));
    converter.Convert(args[1], args[2]);
    Console.WriteLine($"已完成 OFD-A 去技术化转换 -> {args[2]}");
    return 0;
}
