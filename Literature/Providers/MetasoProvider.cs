using System.Text.Json;
using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Providers;

/// <summary>
/// Metaso (秘塔搜索) — Chinese academic web search.
/// Calls POST https://metaso.cn/api/v1/search with scope=scholar.
///
/// Response: { scholars: [{title,link,score,snippet,authors,date}], total:N }
///
/// Auth: Bearer token via NONG_LIT_METASO_KEY (keys start with mk-).
/// Get from https://metaso.cn
/// </summary>
public sealed class MetasoProvider : ILiteratureProvider
{
    const string BaseUrl = "https://metaso.cn/api/v1/search";
    readonly HttpClient _client;
    readonly Func<string, string?> _getEnv;

    public MetasoProvider() : this(ProviderHttpClientFactory.Create("metaso")) { }

    public MetasoProvider(HttpClient client, Func<string, string?>? getEnv = null)
    {
        _client = client;
        _getEnv = getEnv ?? Environment.GetEnvironmentVariable;
    }

    public string Name => "metaso";
    public LiteratureProviderCapabilities Capabilities { get; } = new()
    {
        Search = true, DoiLookup = false, SupportsLocalStrictFilter = true
    };

    string? Token() => _getEnv("NONG_LIT_METASO_KEY") ?? _getEnv("METASO_API_KEY");

    public async Task<ProviderSearchResult> SearchAsync(LiteratureSearchRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Token()))
            return Result("missing_key", 0, "Metaso requires NONG_LIT_METASO_KEY (https://metaso.cn).");

        var q = request.ProviderQuery ?? request.RoughQueries.FirstOrDefault() ?? request.Query;
        if (string.IsNullOrWhiteSpace(q)) return Result("empty_query", 0);

        var size = Math.Clamp(request.Limit <= 0 ? 10 : request.Limit, 1, 20).ToString();

        try
        {
            var body = JsonSerializer.Serialize(new
            {
                q,
                scope = "scholar",
                includeSummary = false,
                size
            });

                Console.Error.WriteLine($"[Metaso] POST body={body}");

            using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Token()}");

            using var resp = await ProviderHttpClientFactory.SendWithRetryAsync(_client, req, ct).ConfigureAwait(false);
            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            Console.Error.WriteLine($"[Metaso] status={(int)resp.StatusCode} hasScholars={doc.RootElement.TryGetProperty("scholars",out _)}");

            var root = doc.RootElement;
            var records = new List<PaperRecord>();

            if (root.TryGetProperty("scholars", out var scholars) && scholars.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in scholars.EnumerateArray())
                    records.Add(Map(item));
            }

            return new ProviderSearchResult
            {
                Provider = Name,
                Records = records,
                Diagnostics = new Dictionary<string, object>
                {
                    ["hasToken"] = true,
                    ["records"] = records.Count,
                    ["credits"] = GetInt(root, "credits") ?? 0,
                    ["total"] = GetInt(root, "total") ?? 0,
                }
            };
        }
        catch (Exception ex)
        {
            return Result("error", 0, ex.Message);
        }
    }

    public Task<PaperRecord?> GetByDoiAsync(string doi, CancellationToken ct)
        => Task.FromResult<PaperRecord?>(null);
    public Task<PaperRecord?> EnrichAsync(PaperRecord record, CancellationToken ct)
        => Task.FromResult<PaperRecord?>(record);

    // ── mapping: Metaso scholar object → PaperRecord ──

    static PaperRecord Map(JsonElement item)
    {
        var title = S(item, "title") ?? "";
        var link = S(item, "link") ?? "";
        var snippet = S(item, "snippet") ?? "";
        var date = S(item, "date") ?? "";
        var score = S(item, "score") ?? "";

        int? year = null;
        if (date.Length >= 4 && int.TryParse(date[..4], out var y)) year = y;

        var record = new PaperRecord
        {
            Id = !string.IsNullOrWhiteSpace(link) ? link : title.GetHashCode().ToString("x8"),
            Title = title,
            Abstract = snippet,
            Year = year,
            LandingPageUrl = link,
        };
        record.RetrievedFrom.Add("metaso");
        if (!string.IsNullOrWhiteSpace(link)) record.SourceIds["metaso"] = link;

        if (item.TryGetProperty("authors", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in arr.EnumerateArray())
            {
                var name = a.GetString();
                if (!string.IsNullOrWhiteSpace(name) && !record.Authors.Contains(name))
                    record.Authors.Add(name);
            }
        }
        record.FirstAuthor = record.Authors.FirstOrDefault();

        return record;
    }

    static string? S(JsonElement e, string p)
        => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    static int? GetInt(JsonElement e, string p)
        => e.TryGetProperty(p, out var v) && v.ValueKind != JsonValueKind.Null && v.TryGetInt32(out var n) ? n : null;

    static ProviderSearchResult Result(string reason, int records, string? msg = null) => new()
    {
        Provider = "metaso",
        Records = Array.Empty<PaperRecord>(),
        Issues = reason switch
        {
            "missing_key" => new[] { new LiteratureIssue { Id = "provider_credential_missing", Severity = "Warning", Provider = "metaso", Message = msg ?? "" } },
            "error" => new[] { new LiteratureIssue { Id = "provider_error", Severity = "Warning", Provider = "metaso", Message = msg ?? "" } },
            _ => Array.Empty<LiteratureIssue>()
        },
        Diagnostics = new Dictionary<string, object> { ["reason"] = reason, ["records"] = records }
    };
}
