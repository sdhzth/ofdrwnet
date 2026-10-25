using System;
using System.IO;

namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// 极简图片像素尺寸探测（仅解析文件头，不解码全图）。
/// <para>支持 PNG / JPEG / BMP / GIF；无法识别时返回 null。
/// 用途：<see cref="DrawContext.CreatePattern(string,string)"/> 需要按 Java 语义
/// 用像素尺寸换算底纹单元的毫米尺寸，而 Layout 模块不引入任何图像解码库。</para>
/// </summary>
internal static class ImageSizeProbe
{
    /// <summary>尝试读取图片像素宽高；无法识别返回 null。</summary>
    public static (int Width, int Height)? TryGetPixelSize(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return TryGetPixelSize(fs);
        }
        catch
        {
            return null;
        }
    }

    public static (int Width, int Height)? TryGetPixelSize(Stream s)
    {
        Span<byte> head = stackalloc byte[32];
        int got = ReadUpTo(s, head);
        if (got < 24) return null;

        // PNG: 8 字节签名，宽高为大端 @16/@20
        if (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47)
            return (Be16(head[16..20]), Be16(head[20..24]));

        // GIF: "GIF87a"/"GIF89a"，宽高为小端 @6/@8
        if (head[0] == 'G' && head[1] == 'I' && head[2] == 'F')
            return (head[6] | head[7] << 8, head[8] | head[9] << 8);

        // BMP: "BM"，宽为小端 @18（高 @22 可能为负，取绝对值）
        if (head[0] == 'B' && head[1] == 'M')
        {
            int w = head[18] | head[19] << 8 | head[20] << 16 | head[21] << 24;
            int h = head[22] | head[23] << 8 | head[24] << 16 | head[25] << 24;
            return (w, Math.Abs(h));
        }

        // JPEG: FFD8，需扫描段直到 SOF0/1/2
        if (head[0] == 0xFF && head[1] == 0xD8)
            return ProbeJpeg(s);

        return null;
    }

    /// <summary>扫描 JPEG 段定位 SOF（C0/C1/C2），其高度/宽度为大端。</summary>
    private static (int Width, int Height)? ProbeJpeg(Stream s)
    {
        // 已消费前 2 字节（FF D8），从当前位置继续
        var buf = new byte[512];
        while (true)
        {
            int b0 = s.ReadByte();
            if (b0 < 0) return null;
            if (b0 != 0xFF)
            {
                // 填充字节 FF 00 或乱序，继续找段标记
                continue;
            }
            int marker = s.ReadByte();
            if (marker < 0) return null;
            if (marker == 0xFF) { s.Seek(-1, SeekOrigin.Current); continue; } // 填充 FF
            if (marker == 0x00 || marker == 0x01 || marker == 0xD8 || marker == 0xD9) continue;

            // 段长度（含长度自身 2 字节，大端）
            int hi = s.ReadByte();
            int lo = s.ReadByte();
            if (hi < 0 || lo < 0) return null;
            int segLen = (hi << 8) | lo;
            if (segLen < 2) return null;

            // SOF0/1/2: 高度在段数据偏移 1 处（跳过 1 字节精度），宽度偏移 3
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                var data = new byte[8];
                int n = ReadUpTo(s, data, segLen - 2);
                if (n < 5) return null;
                int height = (data[1] << 8) | data[2];
                int width = (data[3] << 8) | data[4];
                if (width <= 0 || height <= 0) return null;
                return (width, height);
            }

            // 跳过非 SOF 段
            long toSkip = segLen - 2;
            while (toSkip > 0)
            {
                int skip = (int)Math.Min(toSkip, int.MaxValue);
                s.Seek(skip, SeekOrigin.Current);
                toSkip -= skip;
            }
        }
    }

    private static int ReadUpTo(Stream s, Span<byte> buffer) => ReadUpTo(s, buffer, buffer.Length);

    private static int ReadUpTo(Stream s, Span<byte> buffer, int max)
    {
        int total = 0;
        while (total < max)
        {
            int n = s.Read(buffer[total..max]);
            if (n <= 0) break;
            total += n;
        }
        return total;
    }

    private static int Be16(Span<byte> b) => (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
}
