using System.Globalization;
using LogiTrack.Models;

namespace LogiTrack.Tests;

/// <summary>Part 1: the two business classes and their output formats.</summary>
public class Part1ModelTests
{
    private static InventoryItem PalletJack(int id = 1) =>
        new() { ItemId = id, Name = "Pallet Jack", Quantity = 12, Location = "Warehouse A" };

    [Test]
    public void DisplayInfo_PrintsThePageFormat()
    {
        var original = Console.Out;
        using var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            PalletJack().DisplayInfo();
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.That(output.ToString().TrimEnd(),
            Is.EqualTo("Item: Pallet Jack | Quantity: 12 | Location: Warehouse A"));
    }

    [Test]
    public void GetOrderSummary_ReturnsThePageFormat()
    {
        var order = new Order { OrderId = 1001, CustomerName = "Samir", DatePlaced = new DateTime(2025, 4, 5) };
        order.AddItem(PalletJack(1));
        order.AddItem(PalletJack(2));

        Assert.That(order.GetOrderSummary(),
            Is.EqualTo("Order #1001 for Samir | Items: 2 | Placed: 4/5/2025"));
    }

    [TestCase("es-PE")]
    [TestCase("en-GB")]
    [TestCase("")]
    public void GetOrderSummary_DateFormatDoesNotDependOnCulture(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try
        {
            var order = new Order { OrderId = 7, CustomerName = "Ana", DatePlaced = new DateTime(2025, 4, 5) };
            Assert.That(order.GetOrderSummary(), Does.EndWith("Placed: 4/5/2025"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Test]
    public void AddItem_AddsEachItemOnce()
    {
        var order = new Order();
        var item = PalletJack(5);

        order.AddItem(item);
        order.AddItem(item);
        order.AddItem(PalletJack(5));

        Assert.That(order.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public void AddItem_KeepsDistinctUnsavedItems()
    {
        var order = new Order();

        order.AddItem(PalletJack(0));
        order.AddItem(PalletJack(0));

        Assert.That(order.Items, Has.Count.EqualTo(2));
    }

    [Test]
    public void AddItem_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Order().AddItem(null!));
    }

    [Test]
    public void RemoveItem_RemovesByIdAndReportsWhetherItWasThere()
    {
        var order = new Order();
        order.AddItem(PalletJack(1));
        order.AddItem(PalletJack(2));

        Assert.Multiple(() =>
        {
            Assert.That(order.RemoveItem(1), Is.True);
            Assert.That(order.RemoveItem(99), Is.False);
            Assert.That(order.Items.Select(i => i.ItemId), Is.EqualTo(new[] { 2 }));
        });
    }
}
