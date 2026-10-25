using System;
using System.IO;
using NetOfd.Core.Annotation;
using NetOfd.Core.Annotation.PageAnnot;
using NetOfd.Layout.Edit;
using NetOfd.Layout.Element.Canvas;
using Xunit;

namespace NetOfd.Tests.Stress;

/// <summary>
/// 回归护栏：任何编辑器操作都不得丢弃 OFD.xml 中已登记的版本入口（DocBody/Versions）。
/// </summary>
public class VersionsPreserveProbeTest
{
    private class D : IDrawer
    {
        public void Draw(DrawContext ctx) => ctx.FillText("x", 1, 1);
    }

    private static byte[] Base()
    {
        var p = Path.Combine(Path.GetTempPath(), "probe_base.ofd");
        using (var doc = new NetOfd.Layout.OFDDoc(p)) { doc.AddPage(); }
        return File.ReadAllBytes(p);
    }

    private static byte[] Versioned() => OfdVersioner.AddVersion(Base(), "v", out _);

    [Fact]
    public void Editors_ShouldPreserveVersionsEntry()
    {
        var att = Path.Combine(Path.GetTempPath(), "probe_att.bin");
        File.WriteAllBytes(att, new byte[10]);

        Assert.Single(OfdVersioner.GetVersions(OfdAnnotator.AddAnnotation(Versioned(), 1,
            new Annotation(1, 1, 5, 5, AnnotType.Path, new D()))));

        Assert.Single(OfdVersioner.GetVersions(OfdEditor.AppendPage(Versioned())));

        Assert.Single(OfdVersioner.GetVersions(
            OfdAttacher.AddAttachment(Versioned(), new Attachment("a", att))));

        Assert.Single(OfdVersioner.GetVersions(
            OfdTagger.AddCustomTag(Versioned(), "urn:t", new byte[] { 1 })));

        // 二次快照应追加而非覆盖
        Assert.Equal(2, OfdVersioner.GetVersions(OfdVersioner.AddVersion(Versioned(), "v2", out _)).Count);
    }
}
