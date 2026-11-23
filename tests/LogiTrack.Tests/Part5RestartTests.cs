using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LogiTrack.Dtos;
using LogiTrack.Tests.Support;

namespace LogiTrack.Tests;

/// <summary>Part 5: state lives in SQLite, so it survives stopping and starting the API.</summary>
public class Part5RestartTests
{
    [Test]
    public async Task OrdersAndInventory_SurviveAnApiRestart()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"logitrack-restart-{Guid.NewGuid():N}.db");
        var userEmail = AuthHelper.NewEmail();
        var userPassword = AuthHelper.NewPassword();
        int orderId;
        string userId;

        // First run: the Manager stocks two items, a plain user orders them.
        var first = new LogiTrackFactory(databasePath);
        try
        {
            using var manager = await AuthHelper.SignedInClientAsync(first, asManager: true);
            var a = await (await manager.PostAsJsonAsync("/api/inventory", new { name = "Pallet Jack", quantity = 12, location = "Warehouse A" }))
                .Content.ReadFromJsonAsync<InventoryItemResponse>();
            var b = await (await manager.PostAsJsonAsync("/api/inventory", new { name = "Hand Truck", quantity = 4, location = "Warehouse B" }))
                .Content.ReadFromJsonAsync<InventoryItemResponse>();

            using var user = first.CreateClient();
            var token = await AuthHelper.RegisterAndLoginAsync(user, userEmail, userPassword);
            userId = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Single(c => c.Type == "sub").Value;
            user.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var created = await user.PostAsJsonAsync("/api/orders", new { customerName = "Samir", itemIds = new[] { a!.ItemId, b!.ItemId } });
            Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            orderId = (await created.Content.ReadFromJsonAsync<OrderResponse>())!.OrderId;
        }
        finally
        {
            first.Dispose();
        }

        // Second run on the same file, with a new signing key: sign in again and read everything back.
        var second = new LogiTrackFactory(databasePath);
        try
        {
            using var client = second.CreateClient();
            var token = await AuthHelper.LoginAsync(client, userEmail, userPassword);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var order = await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{orderId}");
            var inventory = await client.GetFromJsonAsync<List<InventoryItemResponse>>("/api/inventory");

            Assert.Multiple(() =>
            {
                Assert.That(order!.Items.Select(i => i.Name), Is.EquivalentTo(new[] { "Pallet Jack", "Hand Truck" }));
                Assert.That(order.CreatedByUserId, Is.EqualTo(userId));
                Assert.That(inventory!.Select(i => i.Name), Is.EquivalentTo(new[] { "Pallet Jack", "Hand Truck" }));
                Assert.That(inventory!.All(i => i.OrderId == orderId), Is.True);
            });
        }
        finally
        {
            second.Dispose();
            second.DeleteDatabase();
        }
    }
}
