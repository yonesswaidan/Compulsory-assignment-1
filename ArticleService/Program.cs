using Microsoft.EntityFrameworkCore;
using ArticleService.Data;
using ArticleService.Cache;
using System.Text.Json;
using StackExchange.Redis;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ArticleDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Db")));

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var host = builder.Configuration["Redis__Host"] ?? "redis-article";
    var config = new ConfigurationOptions
    {
        EndPoints = { $"{host}:6379" },
        AbortOnConnectFail = false,
        ConnectRetry = 5,
        ConnectTimeout = 5000
    };
    Console.WriteLine($"[INIT] Connecting to Redis at {host}:6379");
    return ConnectionMultiplexer.Connect(config);
});

builder.Services.AddHostedService<ArticleCacheUpdater>();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDirectoryBrowser();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/articles", async (ArticleDbContext db, IHttpClientFactory httpClientFactory, IConnectionMultiplexer redis) =>
{
    var redisDb = redis.GetDatabase();
    var client = httpClientFactory.CreateClient();

    try
    {
        var cachedArticlesJson = await redisDb.StringGetAsync("recent:articles");
        if (cachedArticlesJson.HasValue)
        {
            Console.WriteLine("[CACHE HIT] Artikler hentet fra Redis cache");
            var cached = JsonSerializer.Deserialize<object>(cachedArticlesJson);
            return Results.Ok(cached);
        }

        Console.WriteLine("[CACHE MISS] Henter artikler fra databasen...");
        var articles = await db.Articles
            .OrderByDescending(a => a.PublishedUtc)
            .Take(10)
            .ToListAsync();

        var commentsResponse = await client.GetAsync("http://comment-service:8080/comments");
        var commentsJson = await commentsResponse.Content.ReadAsStringAsync();

        var comments = JsonSerializer.Deserialize<List<CommentDto>>(commentsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var result = articles.Select(article => new
        {
            article.Id,
            article.Title,
            article.Body,
            article.PublishedUtc,
            Comments = comments?
                .Where(c => c.ArticleId == article.Id)
                .Select(c => new { c.Id, c.Author, c.Text, c.CreatedUtc })
                .ToList()
        }).ToList();

        var serialized = JsonSerializer.Serialize(result);
        await redisDb.StringSetAsync("recent:articles", serialized, TimeSpan.FromMinutes(10));

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Failed to fetch or cache articles: {ex.Message}");
        return Results.Problem("Der opstod en fejl under hentning af artikler.");
    }
});

app.MapGet("/dashboard/stats", async (IConnectionMultiplexer redis) =>
{
    try
    {
        var db = redis.GetDatabase();

        var ping = await db.PingAsync();
        var keyCount = (await db.ExecuteAsync("DBSIZE")).ToString();
        var info = await db.ExecuteAsync("INFO", "memory");
        var infoText = info.ToString();

        return Results.Ok(new
        {
            Ping = ping.TotalMilliseconds + " ms",
            Keys = keyCount,
            Memory = infoText.Split('\n')
                .Where(l => l.StartsWith("used_memory_human") || l.StartsWith("maxmemory_human"))
                .ToArray(),
            Status = "OK",
            Time = DateTime.UtcNow.ToString("HH:mm:ss 'UTC'")
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Dashboard fetch failed: {ex.Message}");
        return Results.Problem("Kunne ikke hente Redis-statistik.");
    }
});

app.Run();

public class CommentDto
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}
