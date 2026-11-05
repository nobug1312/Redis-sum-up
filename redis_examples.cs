// Ví dụ Redis cho người mới. Đọc kèm Knowledge.md mục 4 và 5.
// Chạy: dotnet run --project redis_examples.csproj
using StackExchange.Redis;

var connectionString = Environment.GetEnvironmentVariable("REDIS_URL")
    ?? "localhost:6379";
// Ứng dụng thật: tạo MỘT ConnectionMultiplexer và dùng lại (Singleton), không tạo mỗi request.
using var redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
var db = redis.GetDatabase();
var subscriber = redis.GetSubscriber();
// Thêm tiền tố ngẫu nhiên để không đụng key có sẵn trong Redis của bạn.
var prefix = $"redis-learning:{Guid.NewGuid():N}";
var cacheKey = $"{prefix}:cache";
var hashKey = $"{prefix}:user";
var listKey = $"{prefix}:recent";
var setKey = $"{prefix}:tags";
var sortedSetKey = $"{prefix}:leaderboard";
var streamKey = $"{prefix}:events";
var channel = RedisChannel.Literal($"{prefix}:notifications");
var messageReceived = new TaskCompletionSource<RedisValue>(
    TaskCreationOptions.RunContinuationsAsynchronously);

try
{
    // 1) String + TTL (cache): SET key value EX 60, rồi GET key.
    await db.StringSetAsync(cacheKey, "cached product response", TimeSpan.FromMinutes(1));
    Console.WriteLine($"cache: {await db.StringGetAsync(cacheKey)}");

    // 2) Các kiểu dữ liệu: Hash (object), List (danh sách), Set (không trùng), Sorted Set (xếp hạng).
    await db.HashSetAsync(hashKey,
    [
        new HashEntry("name", "Ada"),
        new HashEntry("role", "learner")
    ]);
    await db.ListLeftPushAsync(listKey, ["opened lesson 2", "opened lesson 1"]);
    await db.SetAddAsync(setKey, ["redis", "database", "redis"]);
    await db.SortedSetAddAsync(sortedSetKey,
    [
        new SortedSetEntry("Ada", 120),
        new SortedSetEntry("Lin", 95)
    ]);
    Console.WriteLine($"hash: {Format(await db.HashGetAllAsync(hashKey))}");
    Console.WriteLine($"list: {Format(await db.ListRangeAsync(listKey, 0, 9))}");
    var setMembers = await db.SetMembersAsync(setKey);
    Console.WriteLine($"set: {Format(setMembers.OrderBy(value => value.ToString()))}");
    Console.WriteLine($"leaderboard: {Format(await db.SortedSetRangeByRankWithScoresAsync(
        sortedSetKey, 0, 9, Order.Descending))}");

    // 3) Pub/Sub: phải SUBSCRIBE trước rồi mới PUBLISH; người không nghe sẽ mất tin.
    await subscriber.SubscribeAsync(channel, (_, value) => messageReceived.TrySetResult(value));
    await subscriber.PublishAsync(channel, "refresh");
    var message = await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Console.WriteLine($"pubsub: {message}");

    // 4) Stream: XGROUP CREATE -> XADD -> XREADGROUP -> XACK (xác nhận đã xử lý).
    const string group = "lesson-workers";
    const string consumer = "worker-1";
    await db.StreamCreateConsumerGroupAsync(streamKey, group, "0-0", createStream: true);
    var entryId = await db.StreamAddAsync(streamKey,
    [
        new NameValueEntry("event", "lesson-completed"),
        new NameValueEntry("user", "Ada")
    ]);
    var entries = await db.StreamReadGroupAsync(streamKey, group, consumer, ">", count: 1);
    if (entries.Length == 0)
    {
        throw new InvalidOperationException("Did not receive the Stream entry.");
    }

    var entry = entries[0];
    if (entry.Id != entryId)
    {
        throw new InvalidOperationException("Received an unexpected Stream entry.");
    }

    Console.WriteLine($"stream: {entry.Id} {Format(entry.Values)}");
    await db.StreamAcknowledgeAsync(streamKey, group, entry.Id);
}
finally
{
    await subscriber.UnsubscribeAsync(channel);
    // Dọn dẹp: chỉ xóa đúng các key ví dụ đã tạo.
    await db.KeyDeleteAsync(
    [
        cacheKey,
        hashKey,
        listKey,
        setKey,
        sortedSetKey,
        streamKey
    ]);
}

static string Format(IEnumerable<HashEntry> entries) =>
    "{" + string.Join(", ", entries.Select(entry => $"{entry.Name}={entry.Value}")) + "}";

static string Format(IEnumerable<RedisValue> values) =>
    "[" + string.Join(", ", values) + "]";

static string Format(IEnumerable<SortedSetEntry> entries) =>
    "[" + string.Join(", ", entries.Select(entry => $"{entry.Element}={entry.Score}")) + "]";

static string Format(IEnumerable<NameValueEntry> entries) =>
    "{" + string.Join(", ", entries.Select(entry => $"{entry.Name}={entry.Value}")) + "}";
