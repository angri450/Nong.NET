using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// ── Program ──

if (args.Length == 0 || args[0] != "--project-root" || args.Length < 2)
{
    Console.Error.WriteLine("Usage: nong-progress --project-root <path>");
    return 1;
}

var root = Path.GetFullPath(args[1]);
var logDir = Path.Combine(root, "log");
var reportsDir = Path.Combine(logDir, "reports");
var pagesDir = Path.Combine(reportsDir, "pages");

Directory.CreateDirectory(reportsDir);
Directory.CreateDirectory(pagesDir);

var categories = new[] {
    new { Key = "plans",     Label = "施工方案",   Color = "#2563eb", BgColor = "#eff6ff" },
    new { Key = "changelog", Label = "变更记录",   Color = "#16a34a", BgColor = "#f0fdf4" },
    new { Key = "debug",     Label = "用户反馈",   Color = "#dc2626", BgColor = "#fef2f2" },
    new { Key = "guidance",  Label = "开发指导",   Color = "#7c3aed", BgColor = "#f5f3ff" },
};

// ── Phase 1: Parse all index files ──

var allEntries = new List<(string Category, IndexEntry Entry)>();

foreach (var cat in categories)
{
    var indexPath = Path.Combine(logDir, cat.Key, "index.md");
    if (!File.Exists(indexPath)) continue;

    var lines = File.ReadAllLines(indexPath);
    foreach (var line in lines)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith("- ")) continue;

        var entry = ParseIndexLine(trimmed);
        if (entry != null)
        {
            allEntries.Add((cat.Key, entry));
        }
    }
}

// ── Phase 2: Read each entry's full .md file ──

var logFiles = new List<LogFile>();

foreach (var (cat, entry) in allEntries)
{
    var mdPath = ResolveLogFile(logDir, cat, entry.Date, entry.FileName);
    if (mdPath == null) { Console.WriteLine($"  SKIP: {cat}/{entry.FileName} not found"); continue; }

    var content = File.ReadAllText(mdPath, Encoding.UTF8);
    var title = ExtractTitle(content) ?? entry.Summary;
    logFiles.Add(new LogFile(entry.Date, entry.FileName, title, content, cat));

    // Generate individual page
    var pageHtml = BuildPageHtml(cat, entry, title, content, categories);
    var pageFileName = $"{cat}-{SanitizeFileName(entry.FileName)}.html";
    File.WriteAllText(Path.Combine(pagesDir, pageFileName), pageHtml, Encoding.UTF8);
}

// ── Phase 3: Generate category index pages ──

foreach (var cat in categories)
{
    var catFiles = logFiles.Where(f => f.Category == cat.Key)
        .OrderByDescending(f => f.Date)
        .ToList();
    var catHtml = BuildCategoryHtml(cat, catFiles);
    File.WriteAllText(Path.Combine(reportsDir, $"{cat.Key}.html"), catHtml, Encoding.UTF8);
}

// ── Phase 4: Generate master index.html ──

var indexHtml = BuildIndexHtml(logFiles, categories);
File.WriteAllText(Path.Combine(reportsDir, "index.html"), indexHtml, Encoding.UTF8);

// ── Phase 5: Generate shared CSS ──

var css = BuildCss();
File.WriteAllText(Path.Combine(reportsDir, "style.css"), css, Encoding.UTF8);

Console.WriteLine($"Generated {logFiles.Count} pages + index + 4 category pages + CSS in {reportsDir}");
return 0;

// ═══════════════════════════════════════════
//  Parsing helpers
// ═══════════════════════════════════════════

static IndexEntry? ParseIndexLine(string line)
{
    // Format: "- YYYY-MM-DD | filename.md | summary | status"
    var parts = line[2..].Split('|', 4);
    if (parts.Length < 2) return null;

    var date = parts[0].Trim();
    var fileName = parts[1].Trim();
    var summary = parts.Length > 2 ? parts[2].Trim() : "";
    var status = parts.Length > 3 ? parts[3].Trim() : "";

    // Validate date
    if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
    {
        if (date.Length < 10) return null; // skip non-date lines (templates, headers)
    }

    return new IndexEntry(date, fileName, summary, status);
}

static string? ExtractTitle(string markdown)
{
    var firstLine = markdown.Split('\n')[0].Trim();
    if (firstLine.StartsWith("# "))
        return firstLine[2..].Trim();
    return null;
}

static string SanitizeFileName(string name)
{
    return name.Replace(".md", "").Replace(" ", "-")
        .Replace(":", "").Replace("/", "-").Replace("\\", "-");
}

static string? ResolveLogFile(string logDir, string cat, string date, string fileName)
{
    // 1. Exact match
    var path = Path.Combine(logDir, cat, fileName);
    if (File.Exists(path)) return path;

    // 2. With date prefix: YYYY-MM-DD-filename
    var datePrefix = $"{date}-{fileName}";
    path = Path.Combine(logDir, cat, datePrefix);
    if (File.Exists(path)) return path;

    // 3. Try with .md extension
    if (!fileName.EndsWith(".md"))
    {
        path = Path.Combine(logDir, cat, fileName + ".md");
        if (File.Exists(path)) return path;
        path = Path.Combine(logDir, cat, $"{date}-{fileName}.md");
        if (File.Exists(path)) return path;
    }

    return null;
}

// ═══════════════════════════════════════════
//  Markdown to HTML (basic, no external deps)
// ═══════════════════════════════════════════

static string MarkdownToHtml(string md)
{
    var sb = new StringBuilder();
    var lines = md.Split('\n');
    var inCodeBlock = false;
    var inTable = false;
    var inList = false;
    var codeLang = "";

    for (int i = 0; i < lines.Length; i++)
    {
        var line = lines[i];
        var trimmed = line.Trim();

        // Code block fence
        if (trimmed.StartsWith("```"))
        {
            if (inCodeBlock) { sb.AppendLine("</code></pre>"); inCodeBlock = false; }
            else { codeLang = trimmed[3..].Trim(); sb.Append("<pre><code"); if (codeLang.Length > 0) sb.Append($" class=\"language-{EscapeHtml(codeLang)}\""); sb.AppendLine(">"); inCodeBlock = true; }
            continue;
        }

        if (inCodeBlock) { sb.AppendLine(EscapeHtml(line)); continue; }

        // Table
        if (trimmed.StartsWith("|") && trimmed.EndsWith("|"))
        {
            if (trimmed.Contains("---") && trimmed.Replace("-", "").Replace("|", "").Trim().Length == 0) continue; // separator row
            if (!inTable) { sb.AppendLine("<table>"); inTable = true; }
            var cells = trimmed.Trim('|').Split('|').Select(c => c.Trim()).ToList();
            var isHeader = i + 1 < lines.Length && lines[i + 1].Trim().StartsWith("|") && lines[i + 1].Contains("---");
            sb.Append("<tr>");
            foreach (var cell in cells)
            {
                var tag = isHeader ? "th" : "td";
                sb.Append($"<{tag}>{InlineMarkdown(cell)}</{tag}>");
            }
            sb.AppendLine("</tr>");
            continue;
        }
        else if (inTable) { sb.AppendLine("</table>"); inTable = false; }

        // Headers
        if (trimmed.StartsWith("#### ")) { sb.AppendLine($"<h4>{InlineMarkdown(trimmed[5..])}</h4>"); continue; }
        if (trimmed.StartsWith("### "))  { sb.AppendLine($"<h3>{InlineMarkdown(trimmed[4..])}</h3>"); continue; }
        if (trimmed.StartsWith("## "))   { sb.AppendLine($"<h2>{InlineMarkdown(trimmed[3..])}</h2>"); continue; }
        if (trimmed.StartsWith("# "))    { sb.AppendLine($"<h1>{InlineMarkdown(trimmed[2..])}</h1>"); continue; }

        // Horizontal rule
        if (trimmed == "---" || trimmed == "***" || trimmed == "___") { sb.AppendLine("<hr>"); continue; }

        // Unordered list
        if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
        {
            if (!inList) { sb.AppendLine("<ul>"); inList = true; }
            sb.AppendLine($"<li>{InlineMarkdown(trimmed[2..])}</li>");
            continue;
        }
        else if (inList) { sb.AppendLine("</ul>"); inList = false; }

        // Blockquote
        if (trimmed.StartsWith("> ")) { sb.AppendLine($"<blockquote>{InlineMarkdown(trimmed[2..])}</blockquote>"); continue; }

        // Empty line
        if (trimmed.Length == 0) { sb.AppendLine("<br>"); continue; }

        // Regular paragraph
        sb.AppendLine($"<p>{InlineMarkdown(trimmed)}</p>");
    }

    if (inCodeBlock) sb.AppendLine("</code></pre>");
    if (inTable) sb.AppendLine("</table>");
    if (inList) sb.AppendLine("</ul>");

    return sb.ToString();
}

static string InlineMarkdown(string text)
{
    // Bold **text**
    text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
    // Italic *text*
    text = Regex.Replace(text, @"\*(.+?)\*", "<em>$1</em>");
    // Inline code `text`
    text = Regex.Replace(text, @"`([^`]+)`", "<code>$1</code>");
    // Links [text](url)
    text = Regex.Replace(text, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\">$1</a>");
    return text;
}

static string EscapeHtml(string text)
{
    return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}

// ═══════════════════════════════════════════
//  HTML builders
// ═══════════════════════════════════════════

static string BuildIndexHtml(List<LogFile> allFiles, dynamic[] categories)
{
    var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
    var plansCount = allFiles.Count(f => f.Category == "plans");
    var changelogCount = allFiles.Count(f => f.Category == "changelog");
    var debugCount = allFiles.Count(f => f.Category == "debug");
    var guidanceCount = allFiles.Count(f => f.Category == "guidance");

    var recent = allFiles.OrderByDescending(f => f.Date).Take(20).ToList();

    var sb = new StringBuilder();
    sb.AppendLine("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"UTF-8\">");
    sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
    sb.AppendLine("<title>Nong.NET 开发进展</title>");
    sb.AppendLine("<link rel=\"stylesheet\" href=\"style.css\">");
    sb.AppendLine("</head><body>");
    sb.AppendLine("<div class=\"container\">");

    // Header
    sb.AppendLine("<header><h1>Nong.NET 开发进展报告</h1>");
    sb.AppendLine($"<p class=\"subtitle\">生成时间: {now} | 共 {allFiles.Count} 条记录</p></header>");

    // Dashboard cards
    sb.AppendLine("<div class=\"dashboard\">");
    foreach (var cat in categories)
    {
        var count = allFiles.Count(f => f.Category == cat.Key);
        sb.AppendLine($"<a href=\"{cat.Key}.html\" class=\"card\" style=\"border-left: 4px solid {cat.Color}\">");
        sb.AppendLine($"<div class=\"card-count\">{count}</div>");
        sb.AppendLine($"<div class=\"card-label\">{cat.Label}</div></a>");
    }
    sb.AppendLine("</div>");

    // Recent activity timeline
    sb.AppendLine("<section><h2>最近动态</h2><div class=\"timeline\">");
    foreach (var f in recent)
    {
        var cat = categories.First(c => c.Key == f.Category);
        var pageLink = $"pages/{f.Category}-{SanitizeFileName(f.FileName)}.html";
        sb.AppendLine($"<div class=\"timeline-item\" style=\"border-left-color: {cat.Color}\">");
        sb.AppendLine($"<span class=\"timeline-date\">{f.Date}</span>");
        sb.AppendLine($"<span class=\"timeline-cat\" style=\"color: {cat.Color}\">[{cat.Label}]</span>");
        sb.AppendLine($"<a href=\"{pageLink}\" class=\"timeline-title\">{EscapeHtml(f.Title)}</a>");
        sb.AppendLine("</div>");
    }
    sb.AppendLine("</div></section>");

    // Category tables
    foreach (var cat in categories)
    {
        var catFiles = allFiles.Where(f => f.Category == cat.Key)
            .OrderByDescending(f => f.Date).Take(10).ToList();
        if (!catFiles.Any()) continue;

        sb.AppendLine($"<section><h2>{cat.Label}</h2>");
        sb.AppendLine("<table><thead><tr><th>日期</th><th>标题</th><th>状态</th></tr></thead><tbody>");
        foreach (var f in catFiles)
        {
            var pageLink = $"pages/{f.Category}-{SanitizeFileName(f.FileName)}.html";
            var statusBadge = string.IsNullOrEmpty(f.Title) ? "" :
                f.Title.Contains("done") || f.Title.Contains("完成") ?
                "<span class=\"badge badge-done\">done</span>" :
                "<span class=\"badge badge-active\">active</span>";
            sb.AppendLine($"<tr><td>{f.Date}</td><td><a href=\"{pageLink}\">{EscapeHtml(f.Title)}</a></td><td>{statusBadge}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
        if (allFiles.Count(f => f.Category == cat.Key) > 10)
            sb.AppendLine($"<a href=\"{cat.Key}.html\" class=\"more-link\">查看全部 →</a>");
        sb.AppendLine("</section>");
    }

    sb.AppendLine("<footer><p>由 progress-report 自动生成 | 源数据: log/plans/ log/changelog/ log/debug/ log/guidance/</p></footer>");
    sb.AppendLine("</div></body></html>");
    return sb.ToString();
}

static string BuildCategoryHtml(dynamic cat, List<LogFile> files)
{
    var sb = new StringBuilder();
    sb.AppendLine("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"UTF-8\">");
    sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
    sb.AppendLine($"<title>{cat.Label} — Nong.NET</title>");
    sb.AppendLine("<link rel=\"stylesheet\" href=\"style.css\">");
    sb.AppendLine("</head><body><div class=\"container\">");

    sb.AppendLine($"<header><a href=\"index.html\" class=\"back-link\">← 返回总览</a>");
    sb.AppendLine($"<h1>{cat.Label}</h1><p class=\"subtitle\">{files.Count} 条记录</p></header>");

    if (files.Count == 0)
    {
        sb.AppendLine("<p class=\"empty\">暂无记录</p>");
    }
    else
    {
        sb.AppendLine("<table><thead><tr><th>日期</th><th>标题</th><th>摘要</th></tr></thead><tbody>");
        foreach (var f in files)
        {
            var pageLink = $"pages/{f.Category}-{SanitizeFileName(f.FileName)}.html";
            var summary = f.Content.Length > 100 ? f.Content[..Math.Min(200, f.Content.Length)].Replace('\n', ' ') + "..." : "";
            sb.AppendLine($"<tr><td>{f.Date}</td><td><a href=\"{pageLink}\">{EscapeHtml(f.Title)}</a></td><td>{EscapeHtml(summary)}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
    }

    sb.AppendLine("<footer><p>由 progress-report 自动生成</p></footer>");
    sb.AppendLine("</div></body></html>");
    return sb.ToString();
}

static string BuildPageHtml(string cat, IndexEntry entry, string title, string content, dynamic[] categories)
{
    var catMeta = categories.First(c => c.Key == cat);
    var bodyHtml = MarkdownToHtml(content);

    var sb = new StringBuilder();
    sb.AppendLine("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"UTF-8\">");
    sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
    sb.AppendLine($"<title>{EscapeHtml(title)} — Nong.NET</title>");
    sb.AppendLine("<link rel=\"stylesheet\" href=\"../style.css\">");
    sb.AppendLine("</head><body><div class=\"container\">");

    sb.AppendLine($"<header><a href=\"../index.html\" class=\"back-link\">← 返回总览</a> | ");
    sb.AppendLine($"<a href=\"../{cat}.html\" class=\"back-link\">← {catMeta.Label}</a>");
    sb.AppendLine($"<h1>{EscapeHtml(title)}</h1>");
    sb.AppendLine($"<p class=\"subtitle\">{entry.Date} | <span style=\"color: {catMeta.Color}\">{catMeta.Label}</span></p></header>");

    sb.AppendLine("<article class=\"content\">");
    sb.AppendLine(bodyHtml);
    sb.AppendLine("</article>");

    sb.AppendLine("<footer><p>由 progress-report 自动生成</p></footer>");
    sb.AppendLine("</div></body></html>");
    return sb.ToString();
}

static string BuildCss()
{
    return @"
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Noto Sans SC', sans-serif; line-height: 1.6; color: #1f2937; background: #f9fafb; }
.container { max-width: 960px; margin: 0 auto; padding: 2rem 1rem; }
header { margin-bottom: 2rem; padding-bottom: 1rem; border-bottom: 2px solid #e5e7eb; }
header h1 { font-size: 1.75rem; color: #111827; }
.subtitle { color: #6b7280; font-size: 0.9rem; margin-top: 0.25rem; }
.back-link { color: #3b82f6; text-decoration: none; font-size: 0.875rem; }
.back-link:hover { text-decoration: underline; }

/* Dashboard */
.dashboard { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; margin-bottom: 2rem; }
.card { display: block; background: white; border-radius: 8px; padding: 1.5rem; text-decoration: none; box-shadow: 0 1px 3px rgba(0,0,0,0.1); transition: box-shadow 0.2s; }
.card:hover { box-shadow: 0 4px 12px rgba(0,0,0,0.15); }
.card-count { font-size: 2rem; font-weight: 700; color: #111827; }
.card-label { font-size: 0.875rem; color: #6b7280; margin-top: 0.25rem; }

/* Timeline */
.timeline { margin-bottom: 1.5rem; }
.timeline-item { padding: 0.5rem 0 0.5rem 1rem; border-left: 3px solid #e5e7eb; margin-left: 0.5rem; margin-bottom: 0.25rem; }
.timeline-date { font-size: 0.8rem; color: #9ca3af; margin-right: 0.5rem; font-family: monospace; }
.timeline-cat { font-size: 0.75rem; font-weight: 600; margin-right: 0.5rem; }
.timeline-title { color: #1f2937; text-decoration: none; }
.timeline-title:hover { text-decoration: underline; color: #3b82f6; }

/* Tables */
table { width: 100%; border-collapse: collapse; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1); margin-bottom: 1.5rem; }
th, td { padding: 0.75rem 1rem; text-align: left; border-bottom: 1px solid #f3f4f6; }
th { background: #f9fafb; font-weight: 600; font-size: 0.8rem; text-transform: uppercase; color: #6b7280; }
td a { color: #1f2937; text-decoration: none; }
td a:hover { color: #3b82f6; text-decoration: underline; }
tr:hover td { background: #f9fafb; }

/* Badges */
.badge { display: inline-block; padding: 0.125rem 0.5rem; border-radius: 9999px; font-size: 0.7rem; font-weight: 600; }
.badge-done { background: #d1fae5; color: #065f46; }
.badge-active { background: #dbeafe; color: #1e40af; }

/* Content */
.content { background: white; border-radius: 8px; padding: 2rem; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
.content h1 { font-size: 1.5rem; margin: 1.5rem 0 0.75rem; color: #111827; }
.content h2 { font-size: 1.25rem; margin: 1.25rem 0 0.5rem; color: #1f2937; border-bottom: 1px solid #e5e7eb; padding-bottom: 0.25rem; }
.content h3 { font-size: 1.1rem; margin: 1rem 0 0.5rem; color: #374151; }
.content h4 { font-size: 1rem; margin: 0.75rem 0 0.25rem; color: #4b5563; }
.content p { margin: 0.5rem 0; }
.content ul, .content ol { margin: 0.5rem 0 0.5rem 1.5rem; }
.content li { margin: 0.25rem 0; }
.content code { background: #f3f4f6; padding: 0.125rem 0.375rem; border-radius: 4px; font-family: 'Cascadia Code', 'Fira Code', monospace; font-size: 0.875rem; }
.content pre { background: #1f2937; color: #f9fafb; padding: 1rem; border-radius: 8px; overflow-x: auto; margin: 1rem 0; }
.content pre code { background: transparent; padding: 0; color: inherit; }
.content blockquote { border-left: 4px solid #d1d5db; padding: 0.5rem 1rem; margin: 1rem 0; background: #f9fafb; color: #6b7280; }
.content hr { border: none; border-top: 1px solid #e5e7eb; margin: 1.5rem 0; }
.content a { color: #3b82f6; text-decoration: none; }
.content a:hover { text-decoration: underline; }
.content strong { font-weight: 600; color: #111827; }
.content table { box-shadow: none; border: 1px solid #e5e7eb; }
.content table th { background: #f3f4f6; }

/* Section */
section { margin-bottom: 2rem; }
section h2 { font-size: 1.25rem; margin-bottom: 0.75rem; color: #1f2937; }
.more-link { display: inline-block; color: #3b82f6; text-decoration: none; font-size: 0.875rem; }
.more-link:hover { text-decoration: underline; }
.empty { color: #9ca3af; font-style: italic; padding: 1rem 0; }

/* Footer */
footer { margin-top: 3rem; padding-top: 1rem; border-top: 1px solid #e5e7eb; text-align: center; }
footer p { font-size: 0.75rem; color: #9ca3af; }

@media (max-width: 640px) {
  .container { padding: 1rem 0.5rem; }
  .dashboard { grid-template-columns: repeat(2, 1fr); }
  .content { padding: 1rem; }
  table { font-size: 0.8rem; }
  th, td { padding: 0.5rem; }
}
";
}

// ── Models (must be after top-level statements) ──

record IndexEntry(string Date, string FileName, string Summary, string Status);
record LogFile(string Date, string FileName, string Title, string Content, string Category);
