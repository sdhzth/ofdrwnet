using NetOfd.Core.BasicType;

namespace NetOfd.Core.Action.ActionType;

/// <summary>
/// 播放音频动作，表明播放一段音频（图 78 播放音频动作结构）。
/// 对应 Java: org.ofdrw.core.action.actionType.Sound
/// </summary>
public class Sound : OFDElement, IOFDAction
{
    public Sound() : base("Sound") { }
    public Sound(System.Xml.Linq.XElement element) : base(element) { }

    public Sound(STRefID resourceID) : this()
    {
        SetResourceID(resourceID);
    }

    /// <summary>【必选 属性】引用资源文件中的音频资源标识符</summary>
    public STRefID? ResourceID
    {
        get
        {
            var v = GetAttr("ResourceID");
            return string.IsNullOrEmpty(v) ? null : STRefID.Parse(v!);
        }
        set => SetAttr("ResourceID", value is null ? null : value.ToString());
    }

    public Sound SetResourceID(STRefID resourceID)
    {
        ResourceID = resourceID;
        return this;
    }

    /// <summary>【可选 属性】播放音量，取值范围 [0,100]（对齐 Java：先取 max(v,0) 再对 100 取模）</summary>
    public int? Volume
    {
        get
        {
            var v = GetAttr("Volume");
            return string.IsNullOrEmpty(v) ? null : int.Parse(v!);
        }
        set
        {
            if (value is null) { SetAttr("Volume", null); return; }
            var vol = Math.Max(value.Value, 0) % 100;
            SetAttr("Volume", vol.ToString());
        }
    }

    public Sound SetVolume(int volume)
    {
        Volume = volume;
        return this;
    }

    /// <summary>
    /// 【可选 属性】此音频是否需要同步播放（默认 false）。
    /// 如果此属性为 true，则 Synchronous 值无效。
    /// </summary>
    public bool Repeat
    {
        get
        {
            var str = GetAttr("Repeat");
            if (string.IsNullOrWhiteSpace(str)) return false;
            return bool.Parse(str);
        }
        set => SetAttr("Repeat", value.ToString());
    }

    public Sound SetRepeat(bool? repeat)
    {
        SetAttr("Repeat", repeat is null ? null : repeat.ToString());
        return this;
    }

    /// <summary>【可选 属性】是否同步播放（默认 false）：true 表示后续动作应等待此音频播放结束</summary>
    public bool Synchronous
    {
        get
        {
            var str = GetAttr("Synchronous");
            if (string.IsNullOrWhiteSpace(str)) return false;
            return bool.Parse(str);
        }
        set => SetAttr("Synchronous", value.ToString());
    }

    public Sound SetSynchronous(bool? synchronous)
    {
        SetAttr("Synchronous", synchronous is null ? null : synchronous.ToString());
        return this;
    }
}
