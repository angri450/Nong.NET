using System.Text.Json;
using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Providers;

/// <summary>
/// AMiner REST API provider — calls datacenter.aminer.cn directly.
/// Zero MCP/SSE/Python. Pure .NET HttpClient.
///
/// Auth: JWT token via NONG_LIT_AMINER_KEY env var, sent as
///       Authorization: &lt;token&gt; (no Bearer prefix).
///
/// Free endpoints used:
///   GET  /api/paper/list/by/search/venue?keyword=&page=&size=&order=
///   POST /api/person/search  {"query":"...","offset":0,"size":10}
/// </summary>
public sealed class AminerRestProvider : ILiteratureProvider
{
    const string BaseUrl = "https://datacenter.aminer.cn/gateway/open_platform";
    readonly HttpClient _client;
    readonly Func<string, string?> _getEnv;

    public AminerRestProvider()
        : this(ProviderHttpClientFactory.Create("aminer"))
    {
    }

    public AminerRestProvider(HttpClient client, Func<string, string?>? getEnv = null)
    {
        _client = client;
        _getEnv = getEnv ?? Environment.GetEnvironmentVariable;
    }

    public string Name => "aminer";

    public LiteratureProviderCapabilities Capabilities { get; } = new()
    {
        Search = true,
        DoiLookup = false,
        SupportsLocalStrictFilter = true
    };

    string? ResolveToken()
    {
        return _getEnv("NONG_LIT_AMINER_KEY")
            ?? _getEnv("AMINER_API_KEY");
    }

    HttpRequestMessage CreateGet(string path)
    {
        var token = ResolveToken();
        var req = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
        if (!string.IsNullOrWhiteSpace(token))
            req.Headers.TryAddWithoutValidation("Authorization", token);
        return req;
    }

    HttpRequestMessage CreatePost(string path, object body)
    {
        var token = ResolveToken();
        var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                System.Text.Encoding.UTF8,
                "application/json;charset=utf-8")
        };
        if (!string.IsNullOrWhiteSpace(token))
            req.Headers.TryAddWithoutValidation("Authorization", token);
        return req;
    }

    public async Task<ProviderSearchResult> SearchAsync(LiteratureSearchRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ResolveToken()))
        {
            return new ProviderSearchResult
            {
                Provider = Name,
                IsAvailable = false,
                Records = Array.Empty<PaperRecord>(),
                Issues = new[]
                {
                    new LiteratureIssue
                    {
                        Id = "provider_credential_missing",
                        Severity = "Warning",
                        Provider = Name,
                        Message = "AMiner requires NONG_LIT_AMINER_KEY (JWT from https://open.aminer.cn)."
                    }
                },
                Diagnostics = new Dictionary<string, object> { ["hasToken"] = false }
            };
        }

        var query = request.ProviderQuery
            ?? request.RoughQueries.FirstOrDefault()
            ?? request.Query;

        if (string.IsNullOrWhiteSpace(query))
            return Empty();

        var size = Math.Clamp(request.Limit <= 0 ? 10 : request.Limit, 1, 20);

        try
        {
            // Paper search via keyword in venue-search endpoint (free tier)
            var encoded = Uri.EscapeDataString(query);
            using var httpReq = CreateGet($"/api/paper/list/by/search/venue?keyword={encoded}&page=0&size={size}");
            using var resp = await ProviderHttpClientFactory.SendWithRetryAsync(_client, httpReq, ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                return Unavailable((int)resp.StatusCode, await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            // AMiner response: { code:0/200, success:true, data:[{...}], total:N }
            var code = GetInt(root, "code") ?? -1;
            if (code != 0 && code != 200)
            {
                return new ProviderSearchResult
                {
                    Provider = Name,
                    Records = Array.Empty<PaperRecord>(),
                    Diagnostics = new Dictionary<string, object> { ["aminer_code"] = code, ["msg"] = GetString(root, "msg") ?? "" }
                };
            }

            var records = new List<PaperRecord>();
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                    records.Add(MapPaper(item));
            }

            return new ProviderSearchResult
            {
                Provider = Name,
                Records = records,
                Diagnostics = Diagnostics(records.Count)
            };
        }
        catch (Exception ex)
        {
            return new ProviderSearchResult
            {
                Provider = Name,
                Records = Array.Empty<PaperRecord>(),
                Issues = new[]
                {
                    new LiteratureIssue
                    {
                        Id = "provider_error",
                        Severity = "Warning",
                        Provider = Name,
                        Message = $"AMiner REST call failed: {ex.Message}"
                    }
                }
            };
        }
    }

    public Task<PaperRecord?> GetByDoiAsync(string doi, CancellationToken ct)
    {
        // AMiner free tier has no DOI lookup
        return Task.FromResult<PaperRecord?>(null);
    }

    public Task<PaperRecord?> EnrichAsync(PaperRecord record, CancellationToken ct)
    {
        // AMiner free tier has no enrichment
        return Task.FromResult<PaperRecord?>(record);
    }

    // ── response mapping ──

    static PaperRecord MapPaper(JsonElement item)
    {
        var record = new PaperRecord
        {
            Id = GetString(item, "id"),
            Title = GetString(item, "title") ?? GetString(item, "title_zh"),
            Doi = NormalizeDoi(GetString(item, "doi")),
            Year = GetInt(item, "year"),
            CitationCount = GetInt(item, "n_citation"),
            Abstract = GetString(item, "abstract") ?? GetString(item, "abstract_zh"),
            Journal = GetVenueName(item),
            Venue = GetVenueName(item),
        };

        if (!string.IsNullOrWhiteSpace(record.Doi))
            record.SourceIds["aminer"] = record.Doi;
        else if (!string.IsNullOrWhiteSpace(record.Id))
            record.SourceIds["aminer"] = record.Id;
        record.RetrievedFrom.Add("aminer");

        // Authors
        if (item.TryGetProperty("authors", out var authors) && authors.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in authors.EnumerateArray())
            {
                var name = GetString(a, "name") ?? GetString(a, "name_zh");
                if (!string.IsNullOrWhiteSpace(name) && !record.Authors.Contains(name))
                    record.Authors.Add(name);

                var org = GetString(a, "org") ?? GetString(a, "org_zh");
                if (!string.IsNullOrWhiteSpace(org) && !record.Affiliations.Contains(org))
                    record.Affiliations.Add(org);
            }
        }
        record.FirstAuthor = record.Authors.FirstOrDefault();

        // Keywords
        if (item.TryGetProperty("keywords", out var keywords) && keywords.ValueKind == JsonValueKind.Array)
        {
            foreach (var kw in keywords.EnumerateArray())
            {
                var k = kw.GetString();
                if (!string.IsNullOrWhiteSpace(k) && !record.Keywords.Contains(k))
                    record.Keywords.Add(k);
            }
        }

        // Chinese keywords
        if (item.TryGetProperty("keywords_zh", out var keywordsZh) && keywordsZh.ValueKind == JsonValueKind.Array)
        {
            foreach (var kw in keywordsZh.EnumerateArray())
            {
                var k = kw.GetString();
                if (!string.IsNullOrWhiteSpace(k) && !record.Keywords.Contains(k))
                    record.Keywords.Add(k);
            }
        }

        return record;
    }

    static string? GetVenueName(JsonElement item)
    {
        if (item.TryGetProperty("venue", out var venue) && venue.ValueKind == JsonValueKind.Object)
        {
            return GetString(venue, "name_en") ?? GetString(venue, "name_zh") ?? GetString(venue, "alias");
        }
        return null;
    }

    // ── JSON helpers ──

    static string? GetString(JsonElement item, string property)
    {
        return item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
    }

    static int? GetInt(JsonElement item, string property)
    {
        return item.TryGetProperty(property, out var value)
            && value.ValueKind != JsonValueKind.Null
            && value.TryGetInt32(out var n) ? n : null;
    }

    static string? NormalizeDoi(string? doi)
    {
        if (string.IsNullOrWhiteSpace(doi)) return null;
        var d = doi.Trim();
        if (d.StartsWith("https://doi.org/", StringComparison.OrdinalIgnoreCase))
            d = d[16..];
        return d.ToLowerInvariant();
    }

    // ── result helpers ──

    static ProviderSearchResult Empty() => new()
    {
        Provider = "aminer",
        Records = Array.Empty<PaperRecord>(),
        Diagnostics = new Dictionary<string, object> { ["records"] = 0 }
    };

    static ProviderSearchResult Unavailable(int status, string body) => new()
    {
        Provider = "aminer",
        Records = Array.Empty<PaperRecord>(),
        Diagnostics = new Dictionary<string, object> { ["httpStatus"] = status, ["body"] = body[..Math.Min(200, body.Length)] }
    };

    static Dictionary<string, object> Diagnostics(int records) => new()
    {
        ["hasToken"] = true,
        ["records"] = records
    };
}
