using Dalamud.Configuration;
using System;

namespace SpecialAttributeCheck;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    /// <summary>在悬浮窗中输出检测结果（默认开启）。</summary>
    public bool ShowFloatingWindowOutput { get; set; } = true;

    /// <summary>计算补正总数（默认开启）：开启显示 2x+y，关闭显示 x★+y。</summary>
    public bool CalculateTotalCorrection { get; set; } = true;

    /// <summary>在玩家名牌的名字（称号）下方显示补正结果（默认开启）。</summary>
    public bool ShowNameplateOutput { get; set; } = true;

    /// <summary>在小队频道发送检测结果（默认关闭）。</summary>
    public bool ShowChatOutput { get; set; } = false;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
