namespace NetOfd.Sign;

/// <summary>
/// 待保护文件过滤器：在执行签章时遍历 OFD 内所有文件，
/// 通过过滤器决定哪些文件需要加入签章保护范围。
/// 对应 Java: org.ofdrw.sign.ProtectFileFilter
/// </summary>
/// <param name="absPath">待保护文件在 OFD 容器内的绝对路径（以 "/" 开头，如 "/Doc_0/Pages/Page_0/Content.xml"）。</param>
/// <returns>true - 需要保护；false - 不需要保护。</returns>
public delegate bool ProtectFileFilter(string absPath);