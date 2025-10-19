using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;
using ArticleService.Data;

namespace ArticleService.Cache
{
    public class ArticleCacheUpdater : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<ArticleCacheUpdater> _logger;

        public ArticleCacheUpdater(IServiceScopeFactory scopeFactory, IConnectionMultiplexer redis, ILogger<ArticleCacheUpdater> logger)
        {
            _scopeFactory = scopeFactory;
            _redis = redis;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var redisDb = _redis.GetDatabase();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ArticleDbContext>();

                    var recentArticles = await db.Articles
                        .Where(a => a.PublishedUtc >= DateTime.UtcNow.AddDays(-14))
                        .OrderByDescending(a => a.PublishedUtc)
                        .ToListAsync(stoppingToken);

                    var serialized = JsonSerializer.Serialize(recentArticles);
                    await redisDb.StringSetAsync("recent:articles", serialized, TimeSpan.FromMinutes(10));

                    _logger.LogInformation($"[CACHE REFRESH] {recentArticles.Count} articles cached at {DateTime.UtcNow} UTC");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update Redis article cache.");
                }

                // Venter 5 minutter før næste opdatering
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
