using Data.Entities;
using Data.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Data.Tests;

public class DeleteBehaviorTests : SqliteDatabaseFixture
{
    [TestCase("DishIngredient.Dish")]
    [TestCase("DishIngredient.Ingredient")]
    [TestCase("MenuItem.Dish")]
    [TestCase("OrderItem.MenuItem")]
    [TestCase("OrderItem.Order")]
    [TestCase("Customer.LoyaltyLevel")]
    public async Task RequiredRelationships_RestrictDeletingUnloadedPrincipal(string relationship)
    {
        var graph = CreateOrderGraph();
        var item = graph.Items.Single();
        var menu = item.MenuItem;
        var link = menu.Dish.DishIngredients.Single();
        BaseEntity principal;
        object dependent;
        switch (relationship)
        {
            case "DishIngredient.Dish":
                principal = link.Dish;
                dependent = link;
                break;
            case "DishIngredient.Ingredient":
                principal = link.Ingredient;
                dependent = link;
                break;
            case "MenuItem.Dish":
                principal = menu.Dish;
                dependent = menu;
                break;
            case "OrderItem.MenuItem":
                principal = menu;
                dependent = graph;
                break;
            case "OrderItem.Order":
                principal = graph;
                dependent = graph;
                break;
            default:
                principal = graph.Customer!.LoyaltyLevel;
                dependent = graph.Customer;
                break;
        }

        Context.Add(dependent);
        await Context.SaveChangesAsync();
        var id = principal.Id;
        var type = principal.GetType();
        Context.ChangeTracker.Clear();
        var loaded = await Context.FindAsync(type, id);
        Context.Remove(loaded!);

        var exception = Assert.ThrowsAsync<DbUpdateException>(async () => await Context.SaveChangesAsync());

        var sqliteException = (SqliteException)exception!.InnerException!;
        Assert.That(sqliteException.SqliteExtendedErrorCode, Is.EqualTo(1811));
        Assert.That(sqliteException.Message, Does.Contain("FOREIGN KEY constraint failed"));
        using var reader = CreateContext();
        Assert.That(await reader.FindAsync(type, id), Is.Not.Null);
    }

    [Test]
    public async Task RequiredRelationship_WithTrackedDependents_DoesNotCascade()
    {
        var order = await SaveOrderGraphAsync();
        var itemId = order.Items.Single().Id;
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            Context.Orders.Remove(order);
            await Context.SaveChangesAsync();
        });

        using var reader = CreateContext();
        Assert.That(await reader.Orders.CountAsync(), Is.EqualTo(1));
        Assert.That((await reader.OrderItems.SingleAsync()).Id, Is.EqualTo(itemId));
    }

    [TestCase("tracked")]
    [TestCase("not_loaded")]
    [TestCase("no_tracking")]
    public async Task ClientNoAction_PreservesCustomerIdRegardlessOfDependentTracking(string mode)
    {
        var saved = await SaveOrderGraphAsync();
        var customerId = saved.CustomerId!.Value;
        Context.ChangeTracker.Clear();
        Customer customer;
        if (mode == "tracked")
        {
            customer = await Context.Customers.Include(entity => entity.Orders).SingleAsync();
        }
        else
        {
            customer = await Context.Customers.SingleAsync();
            if (mode == "no_tracking")
            {
                var detachedOrders = await Context.Orders.AsNoTracking().ToListAsync();
                Assert.That(detachedOrders, Has.Count.EqualTo(1));
                Assert.That(Context.ChangeTracker.Entries<Order>(), Is.Empty);
            }
        }

        Context.Customers.Remove(customer);
        var exception = Assert.ThrowsAsync<DbUpdateException>(async () => await Context.SaveChangesAsync());
        Assert.That(((SqliteException)exception!.InnerException!).SqliteExtendedErrorCode, Is.EqualTo(787));
        if (mode == "tracked")
        {
            Assert.That(customer.Orders.Single().CustomerId, Is.EqualTo(customerId));
        }

        using var reader = CreateContext();
        Assert.That(await reader.Customers.CountAsync(), Is.EqualTo(1));
        Assert.That((await reader.Orders.SingleAsync()).CustomerId, Is.EqualTo(customerId));
    }

    [Test]
    public async Task UnusedDishAndItsLinks_CanBeExplicitlyDeletedTogether()
    {
        var dish = new Dish { Name = "Unfinished" };
        dish.DishIngredients.Add(new DishIngredient { Ingredient = new Ingredient { Name = "Salt" } });
        Context.Dishes.Add(dish);
        await Context.SaveChangesAsync();

        Context.DishIngredients.RemoveRange(dish.DishIngredients);
        Context.Dishes.Remove(dish);
        await Context.SaveChangesAsync();

        using var reader = CreateContext();
        Assert.That(await reader.Dishes.CountAsync(), Is.Zero);
        Assert.That(await reader.DishIngredients.CountAsync(), Is.Zero);
        Assert.That(await reader.Ingredients.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task ExplicitLineRemoval_DoesNotDeleteOrderOrMenu()
    {
        var order = await SaveOrderGraphAsync();
        Context.OrderItems.Remove(order.Items.Single());
        await Context.SaveChangesAsync();
        using var reader = CreateContext();

        Assert.That(await reader.OrderItems.CountAsync(), Is.Zero);
        Assert.That(await reader.Orders.CountAsync(), Is.EqualTo(1));
        Assert.That(await reader.MenuItems.CountAsync(), Is.EqualTo(1));
    }
}
