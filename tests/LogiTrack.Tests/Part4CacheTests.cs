using System.Net.Http.Json;
using LogiTrack.Data;
using LogiTrack.Dtos;
using LogiTrack.Models;
using LogiTrack.Tests.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;

namespace LogiTrack.Tests;

/// <summary>Part 4: the inventory list is served from memory for 30 seconds and refreshed after any write.</summary>
public class Part4CacheTests
{
    private LogiTrackFactory _factory = null!;
    private HttpClient _manager = null!;

    [SetUp]
    public async Task StartApi()
    {
        _factory = new LogiTrackFactory();
        _manager = await AuthHelper.SignedInClientAsync(_factory, asManager: true);
    }

    [TearDown]
    public void StopApi()
    {
        _manager.Dispose();
        _factory.Dispose();
        _factory.DeleteDatabase();
    }

    /// <summary>Writes straight to the database, bypassing the API, so only a cache refresh can reveal it.</summary>
    private static void InsertBehindTheApi(IServiceProvider services, string name)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();
        context.InventoryItems.Add(new InventoryItem { Name = name, Quantity = 1, Location = "Warehouse A" });
        context.SaveChanges();
    }

    private static async Task<List<InventoryItemResponse>> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<InventoryItemResponse>>("/api/inventory"))!;

    [Test]
    public async Task SecondReadWithin30Seconds_IsServedFromMemory()
    {
        var first = await ListAsync(_manager);
        InsertBehindTheApi(_factory.Services, "Hidden Crate");

        var second = await ListAsync(_manager);

        Assert.That(second.Select(i => i.Name), Is.EqualTo(first.Select(i => i.Name)));
        Assert.That(second.Select(i => i.Name), Does.Not.Contain("Hidden Crate"));
    }

    [Test]
    public async Task AddingAnItem_RefreshesTheList()
    {
        await ListAsync(_manager);
        InsertBehindTheApi(_factory.Services, "Hidden Crate");

        await _manager.PostAsJsonAsync("/api/inventory", new { name = "Posted Crate", quantity = 1, location = "Warehouse A" });

        Assert.That((await ListAsync(_manager)).Select(i => i.Name), Is.SupersetOf(new[] { "Hidden Crate", "Posted Crate" }));
    }

    [Test]
    public async Task DeletingAnItem_RefreshesTheList()
    {
        var created = await _manager.PostAsJsonAsync("/api/inventory", new { name = "Crate", quantity = 1, location = "A" });
        var item = (await created.Content.ReadFromJsonAsync<InventoryItemResponse>())!;
        await ListAsync(_manager);

        await _manager.DeleteAsync($"/api/inventory/{item.ItemId}");

        Assert.That((await ListAsync(_manager)).Select(i => i.ItemId), Does.Not.Contain(item.ItemId));
    }

    [Test]
    public async Task CreatingAndDeletingAnOrder_RefreshesTheItemsOrderIds()
    {
        var created = await _manager.PostAsJsonAsync("/api/inventory", new { name = "Crate", quantity = 1, location = "A" });
        var item = (await created.Content.ReadFromJsonAsync<InventoryItemResponse>())!;
        await ListAsync(_manager);

        var order = await _manager.PostAsJsonAsync("/api/orders", new { customerName = "Ana", itemIds = new[] { item.ItemId } });
        var orderId = (await order.Content.ReadFromJsonAsync<OrderResponse>())!.OrderId;
        var afterCreate = (await ListAsync(_manager)).Single(i => i.ItemId == item.ItemId).OrderId;

        await _manager.DeleteAsync($"/api/orders/{orderId}");
        var afterDelete = (await ListAsync(_manager)).Single(i => i.ItemId == item.ItemId).OrderId;

        Assert.That(afterCreate, Is.EqualTo(orderId));
        Assert.That(afterDelete, Is.Null);
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    [Test]
    public async Task CachedList_ExpiresAfter30Seconds()
    {
        var clock = new TestClock();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions { Clock = clock }))));
        using var client = await AuthHelper.SignedInClientAsync(factory);

        await ListAsync(client);
        InsertBehindTheApi(factory.Services, "Late Crate");

        clock.UtcNow = clock.UtcNow.AddSeconds(29);
        var at29 = await ListAsync(client);
        clock.UtcNow = clock.UtcNow.AddSeconds(2);
        var at31 = await ListAsync(client);

        Assert.That(at29.Select(i => i.Name), Does.Not.Contain("Late Crate"));
        Assert.That(at31.Select(i => i.Name), Does.Contain("Late Crate"));
    }
}
