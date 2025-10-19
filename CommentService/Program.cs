using Microsoft.EntityFrameworkCore;
using CommentService.Data;
using CommentService.Cache;
using StackExchange.Redis;
using System.Linq;

using CommentModel = CommentService.Models.Comment;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CommentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Db")));

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var host = builder.Configuration["Redis__Host"] ?? "redis-comment";
    var config = new ConfigurationOptions
    {
        EndPoints = { host + ":6379" },
        AbortOnConnectFail = false,
        ConnectRetry = 5,
        ConnectTimeout = 5000
    };
    Console.WriteLine($"[INIT] Connecting to Redis at {host}:6379");
    return ConnectionMultiplexer.Connect(config);
});

builder.Services.AddScoped<CommentCache>();

var app = builder.Build();

app.MapGet("/comments/{articleId:int}", async (int articleId, CommentDbContext db, CommentCache cache) =>
{
    var cached = await cache.GetCommentsAsync(articleId);
    if (cached is not null)
    {
        Console.WriteLine($"[CACHE HIT] Comments for article {articleId} loaded from Redis.");
        return Results.Ok(cached);
    }

    Console.WriteLine($"[CACHE MISS] Comments for article {articleId} fetched from DB.");

    var dbComments = await db.Comments
        .Where(c => c.ArticleId == articleId)
        .OrderByDescending(c => c.CreatedUtc)
        .ToListAsync();

    var comments = dbComments.Select(c => new CommentModel
    {
        Id = c.Id,
        ArticleId = c.ArticleId,
        Author = c.Author,
        Content = c.Text,
        CreatedUtc = c.CreatedUtc
    }).ToList();

    await cache.SetCommentsAsync(articleId, comments);
    return Results.Ok(comments);
});

app.MapGet("/comments", async (CommentDbContext db) =>
{
    return await db.Comments
        .OrderByDescending(c => c.Id)
        .Take(30)
        .ToListAsync();
});

app.Run();
