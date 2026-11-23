using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using LogiTrack.Data;
using LogiTrack.Models;
using LogiTrack.Tests.Support;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace LogiTrack.Tests.Timing;

/// <summary>
/// Part 4 Step 4: Stopwatch timings for GET /api/inventory and GET /api/orders/{id}, measured in process
/// (no network) on a seeded SQLite file. Excluded from the normal test run; run it with
/// <c>dotnet test -c Release --filter TestCategory=Timing</c>. Set LOGITRACK_TIMING_OUT to a file path to
/// append the rows as CSV.
/// </summary>
[Explicit("Timing run, started on purpose")]
[Category("Timing")]
public class TimingFixture
{
    private const int ItemCount = 2000;
    private const int OrderCount = 200;
    private const int ItemsPerOrder = 5;
    private const int WarmUp = 20;
    private const int Timed = 200;
    private const int ForcedMisses = 50;

    [Test]
    public async Task TimeTheTwoReadRoutes()
    {
        using var factory = new LogiTrackFactory();
        try
        {
            using var client = await AuthHelper.SignedInClientAsync(factory);
            var orderId = Seed(factory);

            var rows = new List<string>();
            foreach (var route in new[] { "/api/inventory", $"/api/orders/{orderId}" })
            {
                rows.Add(await MeasureAsync(client, route, "repeat", Timed, beforeEach: null));

                // Only the inventory list is cached, so only it has a miss to force.
                var cache = factory.Services.GetService<IMemoryCache>() as MemoryCache;
                if (cache is not null && route == "/api/inventory")
                {
                    rows.Add(await MeasureAsync(client, route, "forced-miss", ForcedMisses, () => cache.Compact(1.0)));
                }
            }

            var header = "route,mode,requests,median_ms,p95_ms,dataset,configuration,dotnet,machine,date";
            TestContext.Out.WriteLine(header);
            rows.ForEach(TestContext.Out.WriteLine);

            var outPath = Environment.GetEnvironmentVariable("LOGITRACK_TIMING_OUT");
            if (!string.IsNullOrEmpty(outPath))
            {
                if (!File.Exists(outPath))
                {
                    File.WriteAllText(outPath, header + Environment.NewLine);
                }

                File.AppendAllLines(outPath, rows);
            }
        }
        finally
        {
            factory.DeleteDatabase();
        }
    }

    private static int Seed(LogiTrackFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();

        var items = Enumerable.Range(1, ItemCount)
            .Select(i => new InventoryItem { Name = $"Item {i}", Quantity = i % 50, Location = $"Warehouse {(char)('A' + i % 4)}" })
            .ToList();
        context.InventoryItems.AddRange(items);
        context.SaveChanges();

        for (var o = 0; o < OrderCount; o++)
        {
            var order = new Order { CustomerName = $"Customer {o}", DatePlaced = DateTime.UtcNow };
            foreach (var item in items.Skip(o * ItemsPerOrder).Take(ItemsPerOrder))
            {
                order.AddItem(item);
            }

            context.Orders.Add(order);
        }

        context.SaveChanges();
        return context.Orders.OrderBy(o => o.OrderId).Skip(OrderCount / 2).First().OrderId;
    }

    private static async Task<string> MeasureAsync(HttpClient client, string route, string mode, int count, Action? beforeEach)
    {
        for (var i = 0; i < WarmUp; i++)
        {
            beforeEach?.Invoke();
            (await client.GetAsync(route)).EnsureSuccessStatusCode();
        }

        var samples = new List<double>(count);
        for (var i = 0; i < count; i++)
        {
            beforeEach?.Invoke();
            var watch = Stopwatch.StartNew();
            var response = await client.GetAsync(route);
            await response.Content.ReadAsByteArrayAsync();
            watch.Stop();
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            samples.Add(watch.Elapsed.TotalMilliseconds);
        }

        samples.Sort();
        double Percentile(double p) => samples[(int)Math.Ceiling(p * samples.Count) - 1];

        var routeLabel = route.StartsWith("/api/orders/", StringComparison.Ordinal) ? "/api/orders/{id}" : route;
        var configuration =
#if DEBUG
            "Debug";
#else
            "Release";
#endif
        return string.Join(",",
            routeLabel, mode, count,
            Percentile(0.5).ToString("F3", CultureInfo.InvariantCulture),
            Percentile(0.95).ToString("F3", CultureInfo.InvariantCulture),
            $"{ItemCount} items; {OrderCount} orders x {ItemsPerOrder} items",
            configuration,
            Environment.Version.ToString(),
            "Apple M2 16 GB macOS 26.6.2",
            DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
