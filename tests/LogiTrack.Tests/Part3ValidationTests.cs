using System.Net;
using System.Net.Http.Json;
using System.Text;
using LogiTrack.Dtos;
using LogiTrack.Tests.Support;

namespace LogiTrack.Tests;

/// <summary>Part 3: every write rejects bad input with a 400 that names the field and nothing internal.</summary>
public class Part3ValidationTests
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

    private static readonly string[] InternalDetail =
        { "Exception", "System.", "   at ", "SQLite", "could not be converted", "LogiTrack." };

    private static async Task AssertBadRequestAsync(HttpResponseMessage response, string field)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), body);
        Assert.That(body, Does.Contain(field));
        foreach (var leak in InternalDetail)
        {
            Assert.That(body, Does.Not.Contain(leak), $"400 body leaks '{leak}': {body}");
        }
    }

    private static string Long(int length) => new('a', length);

    private static readonly object[] BadItems =
    {
        new object[] { new { quantity = 1, location = "Warehouse A" }, "Name" },
        new object[] { new { name = "   ", quantity = 1, location = "Warehouse A" }, "Name" },
        new object[] { new { name = "Crate", quantity = 1 }, "Location" },
        new object[] { new { name = "Crate", quantity = -1, location = "Warehouse A" }, "Quantity" },
        new object[] { new { name = Long(101), quantity = 1, location = "Warehouse A" }, "Name" },
        new object[] { new { name = "Crate", quantity = 1, location = Long(101) }, "Location" }
    };

    [TestCaseSource(nameof(BadItems))]
    public async Task Inventory_InvalidBody_Returns400(object body, string field) =>
        await AssertBadRequestAsync(await _manager.PostAsJsonAsync("/api/inventory", body), field);

    [Test]
    public async Task Inventory_MalformedJson_Returns400WithoutParserDetail()
    {
        var response = await _manager.PostAsync("/api/inventory",
            new StringContent("{ \"name\": \"Crate\", \"quantity\": \"many\" }", Encoding.UTF8, "application/json"));
        await AssertBadRequestAsync(response, "errors");
    }

    [Test]
    public async Task Inventory_ValidBodyAtTheLimits_IsAccepted()
    {
        var response = await _manager.PostAsJsonAsync("/api/inventory",
            new { name = Long(100), quantity = 0, location = Long(100) });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    private async Task<int> AddItemAsync()
    {
        var response = await _manager.PostAsJsonAsync("/api/inventory", new { name = "Crate", quantity = 1, location = "A" });
        return (await response.Content.ReadFromJsonAsync<InventoryItemResponse>())!.ItemId;
    }

    [Test]
    public async Task Order_MissingCustomerName_Returns400() =>
        await AssertBadRequestAsync(
            await _manager.PostAsJsonAsync("/api/orders", new { itemIds = new[] { await AddItemAsync() } }), "CustomerName");

    [Test]
    public async Task Order_OverlongCustomerName_Returns400() =>
        await AssertBadRequestAsync(
            await _manager.PostAsJsonAsync("/api/orders",
                new { customerName = Long(101), itemIds = new[] { await AddItemAsync() } }), "CustomerName");

    [Test]
    public async Task Order_NoItems_Returns400() =>
        await AssertBadRequestAsync(
            await _manager.PostAsJsonAsync("/api/orders", new { customerName = "Ana", itemIds = Array.Empty<int>() }), "ItemIds");

    [Test]
    public async Task Order_TooManyItems_Returns400() =>
        await AssertBadRequestAsync(
            await _manager.PostAsJsonAsync("/api/orders",
                new { customerName = "Ana", itemIds = Enumerable.Range(1, 51).ToArray() }), "ItemIds");

    [Test]
    public async Task Register_InvalidEmail_Returns400()
    {
        using var client = _factory.CreateClient();
        await AssertBadRequestAsync(
            await client.PostAsJsonAsync("/api/auth/register", new { email = "not-an-email", password = AuthHelper.NewPassword() }),
            "Email");
    }

    [Test]
    public async Task Register_MissingPassword_Returns400()
    {
        using var client = _factory.CreateClient();
        await AssertBadRequestAsync(
            await client.PostAsJsonAsync("/api/auth/register", new { email = AuthHelper.NewEmail() }), "Password");
    }

    [Test]
    public async Task Login_MissingFields_Returns400()
    {
        using var client = _factory.CreateClient();
        await AssertBadRequestAsync(await client.PostAsJsonAsync("/api/auth/login", new { }), "Email");
    }
}
