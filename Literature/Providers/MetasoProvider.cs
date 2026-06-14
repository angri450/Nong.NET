using System.Text.Json;
using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Providers;

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
    { Search = true, DoiLookup = false, SupportsLocalStrictFilter = true };

    public async Task<ProviderSearchResult> SearchAsync(LiteratureSearchRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Token()))
            return Err("missing_key", "Metaso requires NONG_LIT_METASO_KEY (https://metaso.cn).");

        var q = request.ProviderQuery ?? request.RoughQueries.FirstOrDefault() ?? request.Query;
        if (string.IsNullOrWhiteSpace(q)) return Ok(0);

        var size = Math.Clamp(request.Limit <= 0 ? 10 : request.Limit, 1, 20).ToString();

        try
        {
            var json = JsonSerializer.Serialize(new { q, scope = "scholar", size });
            using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Token()}");

            using var resp = await _client.SendAsync(req, ct).ConfigureAwait(false);
            var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (!root.TryGetProperty("scholars", out var scholars) || scholars.ValueKind != JsonValueKind.Array)
                return Ok(0);

            var records = new List<PaperRecord>();
            foreach (var item in scholars.EnumerateArray())
                records.Add(Map(item));

            return new ProviderSearchResult
            {
                Provider = Name,
                Records = records,
                Diagnostics = new Dictionary<string, object>
                {
                    ["token"] = true, ["queried"] = q, ["records"] = records.Count,
                    ["credits"] = GetInt(root, "credits") ?? 0, ["total"] = GetInt(root, "total") ?? 0
                }
            };
        }
        catch (Exception ex) { return Err("error", ex.Message); }
    }

    public Task<PaperRecord?> GetByDoiAsync(string d, CancellationToken c) => Task.FromResult<PaperRecord?>(null);
    public Task<PaperRecord?> EnrichAsync(PaperRecord r, CancellationToken c) => Task.FromResult<PaperRecord?>(r);

    static PaperRecord Map(JsonElement e)
    {
        var t = S(e, "title") ?? ""; var l = S(e, "link") ?? "";
        var sn = S(e, "snippet") ?? ""; var d = S(e, "date") ?? "";
        int? y = null; if (d.Length >= 4 && int.TryParse(d[..4], out var yr)) y = yr;

        var r = new PaperRecord
        {
            Id = l.Length > 0 ? l : t.GetHashCode().ToString("x8"), Title = t,
            Abstract = sn, Year = y, LandingPageUrl = l
        };
        r.RetrievedFrom.Add("metaso"); if (l.Length > 0) r.SourceIds["metaso"] = l;
        if (e.TryGetProperty("authors", out var a) && a.ValueKind == JsonValueKind.Array)
            foreach (var au in a.EnumerateArray())
            { var n = au.GetString(); if (!string.IsNullOrWhiteSpace(n) && !r.Authors.Contains(n)) r.Authors.Add(n); }
        r.FirstAuthor = r.Authors.FirstOrDefault();
        return r;
    }

    string? Token() => _getEnv("NONG_LIT_METASO_KEY") ?? _getEnv("METASO_API_KEY");

    static ProviderSearchResult Ok(int n, string? msg = null) => new()
    { Provider = "metaso", Records = Array.Empty<PaperRecord>(),
        Issues = msg != null ? new[]{new LiteratureIssue{Id="metaso",Severity="Warning",Provider="metaso",Message=msg}} : Array.Empty<LiteratureIssue>(),
        Diagnostics = new Dictionary<string,object>{{"records",n}} };

    static ProviderSearchResult Err(string reason, string msg) => new()
    { Provider = "metaso", Records = Array.Empty<PaperRecord>(),
        Issues = new[]{new LiteratureIssue{Id=reason,Severity="Warning",Provider="metaso",Message=msg}},
        Diagnostics = new Dictionary<string,object>{{"reason",reason}} };

    static string? S(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    static int? GetInt(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind != JsonValueKind.Null && v.TryGetInt32(out var n) ? n : null;
}
