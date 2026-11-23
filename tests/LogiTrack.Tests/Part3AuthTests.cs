using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using LogiTrack.Dtos;
using LogiTrack.Tests.Support;
using Microsoft.IdentityModel.Tokens;

namespace LogiTrack.Tests;

/// <summary>Part 3: sign-in tokens, the Manager role and which routes each caller may use.</summary>
public class Part3AuthTests
{
    private LogiTrackFactory _factory = null!;

    [SetUp]
    public void StartApi() => _factory = new LogiTrackFactory();

    [TearDown]
    public void StopApi()
    {
        _factory.Dispose();
        _factory.DeleteDatabase();
    }

    private static readonly object[] InventoryAndOrderRoutes =
    {
        new object[] { "GET", "/api/inventory" },
        new object[] { "POST", "/api/inventory" },
        new object[] { "DELETE", "/api/inventory/1" },
        new object[] { "GET", "/api/orders" },
        new object[] { "GET", "/api/orders/1" },
        new object[] { "POST", "/api/orders" },
        new object[] { "DELETE", "/api/orders/1" }
    };

    private static HttpRequestMessage Request(string method, string path) =>
        new(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };

    private async Task<int> AddItemAsManagerAsync(string name = "Pallet Jack")
    {
        using var manager = await AuthHelper.SignedInClientAsync(_factory, asManager: true);
        var response = await manager.PostAsJsonAsync("/api/inventory", new { name, quantity = 3, location = "Warehouse A" });
        return (await response.Content.ReadFromJsonAsync<InventoryItemResponse>())!.ItemId;
    }

    [Test]
    public async Task Login_ReturnsASignedTokenWithTheUsersId()
    {
        using var client = _factory.CreateClient();
        var email = AuthHelper.NewEmail();
        var token = await AuthHelper.RegisterAndLoginAsync(client, email);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Multiple(() =>
        {
            Assert.That(jwt.Claims.Single(c => c.Type == "email").Value, Is.EqualTo(email));
            Assert.That(jwt.Claims.Any(c => c.Type == "sub"), Is.True);
            Assert.That(jwt.Claims.Any(c => c.Type == "role"), Is.False);
            Assert.That(jwt.ValidTo, Is.GreaterThan(DateTime.UtcNow));
        });
    }

    [Test]
    public async Task Login_WrongPasswordAndUnknownEmail_GetTheSameGenericAnswer()
    {
        using var client = _factory.CreateClient();
        var email = AuthHelper.NewEmail();
        await AuthHelper.RegisterAndLoginAsync(client, email);

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong!Pass1" });
        var unknownEmail = await client.PostAsJsonAsync("/api/auth/login",
            new { email = AuthHelper.NewEmail(), password = "Wrong!Pass1" });

        Assert.Multiple(async () =>
        {
            Assert.That(wrongPassword.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(unknownEmail.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(await wrongPassword.Content.ReadAsStringAsync(), Does.Contain("Invalid email or password."));
            Assert.That(await unknownEmail.Content.ReadAsStringAsync(), Does.Contain("Invalid email or password."));
        });
    }

    [Test]
    public async Task Register_TakenEmail_GetsAGenericMessage()
    {
        using var client = _factory.CreateClient();
        var email = AuthHelper.NewEmail();
        await AuthHelper.RegisterAndLoginAsync(client, email);

        var again = await client.PostAsJsonAsync("/api/auth/register", new { email, password = AuthHelper.NewPassword() });
        var body = await again.Content.ReadAsStringAsync();

        Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(body, Does.Contain("Registration failed."));
        Assert.That(body, Does.Not.Contain("is already taken"));
    }

    [Test]
    public async Task Register_WeakPassword_Returns400()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = AuthHelper.NewEmail(), password = "abc" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCaseSource(nameof(InventoryAndOrderRoutes))]
    public async Task EveryRoute_WithoutAToken_Returns401(string method, string path)
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.SendAsync(Request(method, path));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.Location, Is.Null);
    }

    [TestCaseSource(nameof(InventoryAndOrderRoutes))]
    public async Task EveryRoute_WithAForgedToken_Returns401(string method, string path)
    {
        var forged = new JwtSecurityToken(
            issuer: "LogiTrack",
            audience: "LogiTrack.Api",
            claims: new[] { new Claim("sub", "someone"), new Claim("role", "Manager") },
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('x', 48))), SecurityAlgorithms.HmacSha256));

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(forged));

        var response = await client.SendAsync(Request(method, path));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task PlainUser_IsRefusedOnManagerRoutes()
    {
        var itemId = await AddItemAsManagerAsync();
        using var user = await AuthHelper.SignedInClientAsync(_factory);

        var addItem = await user.PostAsJsonAsync("/api/inventory", new { name = "Crate", quantity = 1, location = "Warehouse A" });
        var deleteItem = await user.DeleteAsync($"/api/inventory/{itemId}");
        var order = await user.PostAsJsonAsync("/api/orders", new { customerName = "Ana", itemIds = new[] { itemId } });
        var orderId = (await order.Content.ReadFromJsonAsync<OrderResponse>())!.OrderId;
        var deleteOrder = await user.DeleteAsync($"/api/orders/{orderId}");

        Assert.Multiple(() =>
        {
            Assert.That(addItem.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(deleteItem.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(deleteOrder.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    [Test]
    public async Task PlainUser_CanReadEverythingAndCreateOrders_StampedWithTheirId()
    {
        var itemId = await AddItemAsManagerAsync();
        using var client = _factory.CreateClient();
        var token = await AuthHelper.RegisterAndLoginAsync(client, AuthHelper.NewEmail());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var userId = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Single(c => c.Type == "sub").Value;

        var inventory = await client.GetAsync("/api/inventory");
        var created = await client.PostAsJsonAsync("/api/orders", new { customerName = "Ana", itemIds = new[] { itemId } });
        var order = (await created.Content.ReadFromJsonAsync<OrderResponse>())!;
        var list = await client.GetAsync("/api/orders");
        var byId = await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.OrderId}");

        Assert.Multiple(() =>
        {
            Assert.That(inventory.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(byId!.CreatedByUserId, Is.EqualTo(userId));
        });
    }

    [Test]
    public async Task Manager_CanAddAndDeleteItemsAndOrders()
    {
        using var manager = await AuthHelper.SignedInClientAsync(_factory, asManager: true);

        var add = await manager.PostAsJsonAsync("/api/inventory", new { name = "Crate", quantity = 1, location = "Warehouse A" });
        var item = (await add.Content.ReadFromJsonAsync<InventoryItemResponse>())!;
        var order = await manager.PostAsJsonAsync("/api/orders", new { customerName = "Ana", itemIds = new[] { item.ItemId } });
        var orderId = (await order.Content.ReadFromJsonAsync<OrderResponse>())!.OrderId;

        Assert.Multiple(async () =>
        {
            Assert.That(add.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That((await manager.DeleteAsync($"/api/orders/{orderId}")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That((await manager.DeleteAsync($"/api/inventory/{item.ItemId}")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        });
    }

    [Test]
    public async Task Register_IgnoresAPostedRole()
    {
        using var client = _factory.CreateClient();
        var email = AuthHelper.NewEmail();
        var password = AuthHelper.NewPassword();

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password, role = "Manager", roles = new[] { "Manager" } });
        Assert.That(register.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var token = await AuthHelper.LoginAsync(client, email, password);
        Assert.That(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Any(c => c.Type == "role"), Is.False);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var addItem = await client.PostAsJsonAsync("/api/inventory", new { name = "Crate", quantity = 1, location = "Warehouse A" });
        Assert.That(addItem.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public void Startup_WithoutASigningKey_Fails()
    {
        using var factory = new LogiTrackFactory { JwtKey = null };
        try
        {
            var ex = Assert.Catch(() => factory.CreateClient());
            Assert.That(ex!.ToString(), Does.Contain("Jwt:Key"));
        }
        finally
        {
            factory.DeleteDatabase();
        }
    }
}
