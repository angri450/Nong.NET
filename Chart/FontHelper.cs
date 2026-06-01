using SkiaSharp;

namespace ChartCore;

internal static class FontHelper
{
    private static string? _cached;

    public static string GetCjkFamilyName()
    {
        if (_cached != null)
            return _cached;

        // 已知中文字体候选，按优先级排列
        string[] candidates = ["Microsoft YaHei UI", "微软雅黑", "Microsoft YaHei", "Noto Sans SC", "SimHei", "SimSun", "PingFang SC"];
        foreach (var name in candidates)
        {
            using var tf = SKTypeface.FromFamilyName(name);
            if (tf != null)
            {
                _cached = name;
                return name;
            }
        }

        // 最后手段：用汉字匹配系统字体
        using var fallback = SKFontManager.Default.MatchCharacter('汉');
        _cached = fallback?.FamilyName ?? "Arial";
        return _cached;
    }
}
