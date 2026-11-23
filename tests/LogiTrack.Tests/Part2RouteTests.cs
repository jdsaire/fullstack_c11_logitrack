using System.Net;
using System.Net.Http.Json;
using LogiTrack.Dtos;
using LogiTrack.Tests.Support;

namespace LogiTrack.Tests;

/// <summary>Part 2: the inventory and order routes.</summary>
public class Part2RouteTests
{
    private LogiTrackFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public async Task StartApi()
    {
        _factory = new LogiTrackFactory();
        // Part 2 checks the routes themselves, so it calls them as the Manager, who may use every route.
        _client = await AuthHelper.SignedInClientAsync(_factory, asManager: true);
    }

    [TearDown]
    public void StopApi()
    {
        _client.Dispose();
        _factory.Dispose();
        _factory.DeleteDatabase();
    }

    private async Task<InventoryItemResponse> AddItemAsync(string name, int quantity = 5)
    {
        var response = await _client.PostAsJsonAsync("/api/inventory",
            new { name, quantity, location = "Warehouse A" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return (await response.Content.ReadFromJsonAsync<InventoryItemResponse>())!;
    }

    private Task<HttpResponseMessage> PostOrderAsync(params int[] itemIds) =>
        _client.PostAsJsonAsync("/api/orders", new { customerName = "Samir", itemIds });

    [Test]
    public async Task Inventory_CreateListDelete()
    {
        var item = await AddItemAsync("Pallet Jack", 12);

        var list = await _client.GetFromJsonAsync<List<InventoryItemResponse>>("/api/inventory");
        Assert.That(list!.Select(i => i.Name), Is.EqualTo(new[] { "Pallet Jack" }));

        var delete = await _client.DeleteAsync($"/api/inventory/{item.ItemId}");
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        list = await _client.GetFromJsonAsync<List<InventoryItemResponse>>("/api/inventory");
        Assert.That(list, Is.Empty);
    }

    [Test]
    public async Task Inventory_DeleteMissingId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/inventory/999");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Inventory_MalformedBody_Returns400()
    {
        var response = await _client.PostAsync("/api/inventory",
            new StringContent("{ \"name\": ", System.Text.Encoding.UTF8, "application/json"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Order_WithSeveralItems_IsReturnedWithItsItems()
    {
        var a = await AddItemAsync("Pallet Jack");
        var b = await AddItemAsync("Hand Truck");

        var created = await PostOrderAsync(a.ItemId, b.ItemId);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var order = (await created.Content.ReadFromJsonAsync<OrderResponse>())!;

        var fetched = await _client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.OrderId}");
        Assert.Multiple(() =>
        {
            Assert.That(fetched!.Items.Select(i => i.Name), Is.EquivalentTo(new[] { "Pallet Jack", "Hand Truck" }));
            Assert.That(fetched.Summary, Does.StartWith($"Order #{order.OrderId} for Samir | Items: 2 |"));
        });
    }

    [Test]
    public async Task Orders_ListShowsItemCounts()
    {
        var a = await AddItemAsync("Pallet Jack");
        await PostOrderAsync(a.ItemId);

        var list = await _client.GetFromJsonAsync<List<OrderSummaryResponse>>("/api/orders");
        Assert.That(list!.Single().ItemCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Order_GetMissingId_Returns404()
    {
        var response = await _client.GetAsync("/api/orders/999");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Order_DeleteMissingId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/orders/999");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [TestCase(new int[0], TestName = "Order_WithNoItems_Returns400")]
    [TestCase(new[] { 999 }, TestName = "Order_WithUnknownItem_Returns400")]
    public async Task Order_InvalidItemList_Returns400(int[] itemIds)
    {
        var response = await PostOrderAsync(itemIds);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Order_WithDuplicateItem_Returns400()
    {
        var a = await AddItemAsync("Pallet Jack");
        var response = await PostOrderAsync(a.ItemId, a.ItemId);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Order_WithItemAlreadyOnAnotherOrder_Returns400()
    {
        var a = await AddItemAsync("Pallet Jack");
        await PostOrderAsync(a.ItemId);

        var response = await PostOrderAsync(a.ItemId);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Order_Delete_ReleasesItsItemsToStock()
    {
        var a = await AddItemAsync("Pallet Jack");
        var order = (await (await PostOrderAsync(a.ItemId)).Content.ReadFromJsonAsync<OrderResponse>())!;

        var delete = await _client.DeleteAsync($"/api/orders/{order.OrderId}");
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var list = await _client.GetFromJsonAsync<List<InventoryItemResponse>>("/api/inventory");
        Assert.That(list!.Single().OrderId, Is.Null);
        Assert.That((await PostOrderAsync(a.ItemId)).StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task Order_DateIsReturnedAsUtc()
    {
        var a = await AddItemAsync("Pallet Jack");
        var order = (await (await PostOrderAsync(a.ItemId)).Content.ReadFromJsonAsync<OrderResponse>())!;

        var raw = await _client.GetStringAsync($"/api/orders/{order.OrderId}");
        Assert.That(raw, Does.Match("\"datePlaced\":\"[0-9T:.-]+Z\""));
    }
}
