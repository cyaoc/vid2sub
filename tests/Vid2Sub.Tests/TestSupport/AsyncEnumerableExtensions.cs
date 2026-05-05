namespace Vid2Sub.Tests;

internal static class AsyncEnumerableExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var results = new List<T>();
        await foreach (var item in source)
        {
            results.Add(item);
        }

        return results;
    }

    public static async Task<T> SingleAsync<T>(this IAsyncEnumerable<T> source)
    {
        T? result = default;
        var count = 0;

        await foreach (var item in source)
        {
            result = item;
            count++;
        }

        return count == 1
            ? result!
            : throw new InvalidOperationException($"Sequence contains {count} elements.");
    }
}
