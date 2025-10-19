using StackExchange.Redis;
using System.Text.Json;
using CommentService.Models;

namespace CommentService.Cache
{
    public class CommentCache
    {
        private readonly IDatabase _redisDb;
        private const string LRU_KEY = "comment:lru";
        private const int MAX_ARTICLES = 30;

        public CommentCache(IConnectionMultiplexer redis)
        {
            _redisDb = redis.GetDatabase();
        }

        public async Task<List<Comment>?> GetCommentsAsync(int articleId)
        {
            var cacheKey = $"comments:{articleId}";
            var cached = await _redisDb.StringGetAsync(cacheKey);
            if (!cached.HasValue)
                return null;

            await UpdateLru(cacheKey);
            Console.WriteLine($"[CACHE HIT] Comments for article {articleId} fetched from Redis.");
            return JsonSerializer.Deserialize<List<Comment>>(cached!);
        }

        public async Task SetCommentsAsync(int articleId, List<Comment> comments)
        {
            var cacheKey = $"comments:{articleId}";
            var json = JsonSerializer.Serialize(comments);

            await _redisDb.StringSetAsync(cacheKey, json, TimeSpan.FromHours(6));
            await UpdateLru(cacheKey);
            Console.WriteLine($"[CACHE SET] Stored comments for article {articleId} in Redis.");
        }

        private async Task UpdateLru(string cacheKey)
        {
            await _redisDb.SortedSetAddAsync(LRU_KEY, cacheKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var count = await _redisDb.SortedSetLengthAsync(LRU_KEY);
            if (count > MAX_ARTICLES)
            {
                var oldestEntries = await _redisDb.SortedSetRangeByRankAsync(LRU_KEY, 0, 0, Order.Ascending);
                if (oldestEntries.Length > 0)
                {
                    var oldestKey = oldestEntries[0];
                    Console.WriteLine($"[LRU] Removing oldest cached article: {oldestKey}");
                    await _redisDb.KeyDeleteAsync(oldestKey.ToString());
                    await _redisDb.SortedSetRemoveAsync(LRU_KEY, oldestKey);
                }
            }
        }
    }
}
