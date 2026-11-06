using Data.Entities;
using Data.Enums;
using Data.Repositories;
using Data.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Data.Tests;

public class RelationshipTests : SqliteDatabaseFixture
{
    [Test]
    public async Task AllSevenRelationships_AreRestoredInFreshContext()
    {
        var saved = await SaveOrderGraphAsync();
        using var reader = CreateContext();
        var order = await new OrderRepository(reader).GetWithCurrentMenuAsync(saved.Id, true);
        var item = order!.Items.Single();
        var dish = item.MenuItem.Dish;
        var link = dish.DishIngredients.Single();

        Assert.Multiple(() =>
        {
            Assert.That(item.Order, Is.SameAs(order));
            Assert.That(item.OrderId, Is.EqualTo(order.Id));
            Assert.That(item.MenuItem.OrderItems, Does.Contain(item));
            Assert.That(item.MenuItemId, Is.EqualTo(item.MenuItem.Id));
            Assert.That(dish.MenuItems, Does.Contain(item.MenuItem));
            Assert.That(item.MenuItem.DishId, Is.EqualTo(dish.Id));
            Assert.That(link.Dish, Is.SameAs(dish));
            Assert.That(link.DishId, Is.EqualTo(dish.Id));
            Assert.That(link.Ingredient.DishIngredients, Does.Contain(link));
            Assert.That(link.IngredientId, Is.EqualTo(link.Ingredient.Id));
            Assert.That(order.Customer!.Orders, Does.Contain(order));
            Assert.That(order.CustomerId, Is.EqualTo(order.Customer.Id));
            Assert.That(order.Customer.LoyaltyLevel.Customers, Does.Contain(order.Customer));
            Assert.That(order.Customer.LoyaltyLevelId, Is.EqualTo(order.Customer.LoyaltyLevel.Id));
        });
    }

    [Test]
    public async Task GuestOrder_HasNoCustomerAndNullableFieldsRoundTrip()
    {
        var order = await SaveOrderGraphAsync(false);
        using var reader = CreateContext();
        var loaded = await new OrderRepository(reader).GetWithCurrentMenuAsync(order.Id);

        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Customer, Is.Null);
            Assert.That(loaded.CustomerId, Is.Null);
            Assert.That(loaded.DiscountPercentFixed, Is.Null);
            Assert.That(loaded.Items.Single().MenuItem.Dish.Description, Is.Null);
        });
    }

    [Test]
    public async Task SharedIngredientAndMultiplePortions_AreSupported()
    {
        var order = await SaveOrderGraphAsync();
        var ingredient = order.Items.Single().MenuItem.Dish.DishIngredients.Single().Ingredient;
        var secondDish = new Dish { Name = "Soup" };
        secondDish.DishIngredients.Add(new DishIngredient { Ingredient = ingredient });
        secondDish.MenuItems.Add(new MenuItem
        {
            PortionName = "Small", PortionDescription = "Cup", Price = 30m
        });
        secondDish.MenuItems.Add(new MenuItem
        {
            PortionName = "Large", PortionDescription = "Bowl", Price = 50m
        });
        Context.Dishes.Add(secondDish);
        await Context.SaveChangesAsync();
        using var reader = CreateContext();
        var shared = await reader.Ingredients.Include(entity => entity.DishIngredients).SingleAsync();

        Assert.That(shared.DishIngredients, Has.Count.EqualTo(2));
        Assert.That(await reader.MenuItems.CountAsync(entity => entity.DishId == secondDish.Id), Is.EqualTo(2));
    }

    [Test]
    public async Task UnfinishedDishAndEmptyDraft_CanBeStored()
    {
        Context.Dishes.Add(new Dish { Name = "Unfinished", IsAvailable = false });
        Context.Orders.Add(new Order { CreatedAt = DateTime.UtcNow, Status = OrderStatus.Draft });
        await Context.SaveChangesAsync();
        using var reader = CreateContext();

        Assert.That((await reader.Dishes.Include(entity => entity.DishIngredients).SingleAsync()).DishIngredients, Is.Empty);
        Assert.That((await reader.Orders.Include(entity => entity.Items).SingleAsync()).Items, Is.Empty);
    }
}
