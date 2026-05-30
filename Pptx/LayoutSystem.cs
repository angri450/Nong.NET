namespace PptxCore;

/// <summary>
/// 布局系统：定义统一的间距、网格、装饰元素
/// </summary>
public static class LayoutSystem
{
    // 画布尺寸 (16:9 标准)
    public const int SlideWidth = 960;
    public const int SlideHeight = 540;

    // 间距常量 (基于 8px 网格)
    public const int Spacing_XS = 8;
    public const int Spacing_SM = 16;
    public const int Spacing_MD = 24;
    public const int Spacing_LG = 32;
    public const int Spacing_XL = 48;

    // 边距
    public const int Margin_X = 48;      // 左右边距
    public const int Margin_Y = 40;      // 上下边距
    public const int ContentWidth = SlideWidth - 2 * Margin_X;  // 864

    // 字号层级
    public static class FontSizes
    {
        public const decimal Display = 56;    // 大标题（封面）
        public const decimal H1 = 40;         // 一级标题
        public const decimal H2 = 32;         // 二级标题
        public const decimal H3 = 24;         // 三级标题
        public const decimal Body_LG = 20;    // 大号正文
        public const decimal Body = 16;       // 正文
        public const decimal Body_SM = 14;    // 小号正文
        public const decimal Caption = 12;    // 注释
    }

    // 内容区域（封面页）
    public static class Cover
    {
        public const int TitleY = 180;
        public const int SubtitleY = 260;
        public const int AuthorY = 420;
        public const int DecorationY = 80;
    }

    // 内容区域（内容页）
    public static class Content
    {
        public const int TitleY = 40;
        public const int BodyY = 120;
        public const int BulletLineHeight = 40;
        public const int BulletIndent = 20;
    }

    // 图表区域
    public static class Chart
    {
        public const int TitleY = 40;
        public const int ChartY = 120;
        public const int ChartWidth = 864;
        public const int ChartHeight = 360;
    }

    // 装饰线位置
    public static class Decoration
    {
        public const int AccentLineY = 100;
        public const int AccentLineWidth = 80;
        public const int AccentLineHeight = 4;
        public const int FooterY = 500;
    }
}
