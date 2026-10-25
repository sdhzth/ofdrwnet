using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription.DrawParam;

/// <summary>
/// 绘制参数继承链解析（GB/T 33190 §8.1.2）：
/// 沿 <see cref="CT_DrawParam.ParentID"/> 逐级回溯，子参数未指定的属性取最近的祖先值。
/// </summary>
public static class DrawParamResolver
{
    /// <summary>
    /// 从参数池中解析指定 ID 的参数并合并其全部祖先属性。
    /// 解析失败（ID 不存在）返回 null；父链断裂或成环时以已可得部分为准。
    /// </summary>
    public static CT_DrawParam? Resolve(IEnumerable<CT_DrawParam> pool, STRefID? id)
    {
        if (id is null) return null;
        long key = (long)id;

        CT_DrawParam? first = null;
        var byId = new Dictionary<string, CT_DrawParam>(StringComparer.Ordinal);
        foreach (var dp in pool)
        {
            if (dp.Id is null) continue;
            byId[dp.Id] = dp;
            if (first is null && long.TryParse(dp.Id, out var numeric) && numeric == key)
                first = dp;
        }
        if (first is null) return null;

        // 收集链：child → parent → ...（环与断链安全）
        var chain = new List<CT_DrawParam> { first };
        var seen = new HashSet<string>(StringComparer.Ordinal) { first.Id ?? "" };
        var cur = first;
        while (!string.IsNullOrEmpty(cur.ParentID) && seen.Add(cur.ParentID))
        {
            if (!byId.TryGetValue(cur.ParentID!, out var parent)) break;
            chain.Add(parent);
            cur = parent;
        }

        // 远端祖先先入，子覆盖父
        var merged = new CT_DrawParam { Id = first.Id };
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var dp = chain[i];
            merged.RawSetLineWidth(dp.RawLineWidth ?? merged.RawLineWidth);
            merged.RawSetJoin(dp.RawJoin ?? merged.RawJoin);
            merged.RawSetCap(dp.RawCap ?? merged.RawCap);
            merged.RawSetAlpha(dp.RawAlpha ?? merged.RawAlpha);
            merged.RawSetVisible(dp.RawVisible ?? merged.RawVisible);
            if (merged.RawStrokeColor is null && dp.RawStrokeColor is not null)
                merged.StrokeColor = dp.StrokeColor;
            if (merged.RawFillColor is null && dp.RawFillColor is not null)
                merged.FillColor = dp.FillColor;
        }
        return merged;
    }
}
