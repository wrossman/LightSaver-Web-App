using System.Collections.Concurrent;

public class GlobalStore
{
    public static ConcurrentBag<ImageShare> GlobalImageStore { get; set; } = new();
}