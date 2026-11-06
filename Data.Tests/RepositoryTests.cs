using Data.Data;
using Data.Entities;
using Data.Enums;
using Data.Repositories;
using Data.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Data.Tests;

public class RepositoryTests : SqliteDatabaseFixture
{
    [Test]
    public async Task EmptyRepository_ReturnsEmptyListAndNull()
    {
        var repository = new Repository<Ingredient>(Context);

        Assert.That(await repository.GetAllAsync(), Is.Empty);
        Assert.That(await repository.GetByIdAsync(999), Is.Null);
    }

    [Test]
    public async Task Add_DoesNotPersistUntilUnitOfWorkSave()
    {
        var unitOfWork = new UnitOfWork(Context);
        var ingredient = new Ingredient { Name = "Salt" };
        await unitOfWork.Ingredients.AddAsync(ingredient);

        using (var reader = CreateContext())
        {
            Assert.That(await reader.Ingredients.CountAsync(), Is.Zero);
        }

        Assert.That(await unitOfWork.SaveAsync(), Is.EqualTo(1));
        Assert.That(ingredient.Id, Is.GreaterThan(0));
        using var savedReader = CreateContext();
        Assert.That((await savedReader.Ingredients.SingleAsync()).Name, Is.EqualTo("Salt"));
        Assert.That(await unitOfWork.Ingredients.GetByIdAsync(ingredient.Id), Is.SameAs(ingredient));
    }

    [Test]
    public async Task Update_IsTrackedButDoesNotPersistUntilSave()
    {
        var repository = new Repository<Ingredient>(Context);
        var ingredient = new Ingredient { Name = "Salt" };
        await repository.AddAsync(ingredient);
        await Context.SaveChangesAsync();
        ingredient.Name = "Pepper";
        repository.Update(ingredient);

        using (var reader = CreateContext())
        {
            Assert.That((await reader.Ingredients.SingleAsync()).Name, Is.EqualTo("Salt"));
        }

        await new UnitOfWork(Context).SaveAsync();
        using var savedReader = CreateContext();
        Assert.That((await savedReader.Ingredients.SingleAsync()).Name, Is.EqualTo("Pepper"));
    }

    [Test]
    public async Task Update_CanAttachDetachedEntityWithoutUpdatingNavigationGraph()
    {
        Context.Ingredients.Add(new Ingredient { Name = "Salt" });
        await Context.SaveChangesAsync();
        var ingredient = (await new Repository<Ingredient>(Context).GetAllAsync()).Single();
        Context.ChangeTracker.Clear();
        ingredient.Name = "Pepper";

        new Repository<Ingredient>(Context).Update(ingredient);
        await Context.SaveChangesAsync();

        using var reader = CreateContext();
        Assert.That((await reader.Ingredients.SingleAsync()).Name, Is.EqualTo("Pepper"));
    }

    [Test]
    public async Task Remove_DoesNotPersistUntilSave()
    {
        var repository = new Repository<Ingredient>(Context);
        var ingredient = new Ingredient { Name = "Salt" };
        await repository.AddAsync(ingredient);
        await Context.SaveChangesAsync();
        repository.Remove(ingredient);

        using (var reader = CreateContext())
        {
            Assert.That(await reader.Ingredients.CountAsync(), Is.EqualTo(1));
        }

        await new UnitOfWork(Context).SaveAsync();
        using var savedReader = CreateContext();
        Assert.That(await savedReader.Ingredients.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task GetAll_ReturnsPersistedEntitiesWithoutTracking()
    {
        Context.Ingredients.AddRange(new Ingredient { Name = "Salt" }, new Ingredient { Name = "Pepper" });
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var result = await new Repository<Ingredient>(Context).GetAllAsync();

        Assert.That(result.Select(entity => entity.Name), Is.EquivalentTo(new[] { "Salt", "Pepper" }));
        Assert.That(Context.ChangeTracker.Entries(), Is.Empty);
    }

    [Test]
    public async Task UntrackedLoyaltyLevel_CanBeExplicitlyUpdatedAndSaved()
    {
        Context.LoyaltyLevels.Add(new LoyaltyLevel
        {
            Name = "Silver", DiscountPercent = 5m, MinOrders = 2, MinTotalSpent = 200m
        });
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        var unitOfWork = new UnitOfWork(Context);
        var level = (await unitOfWork.LoyaltyLevels.GetAllAsync()).Single();
        level.DiscountPercent = 7.25m;
        level.MinTotalSpent = 350.50m;

        Assert.That(Context.Entry(level).State, Is.EqualTo(EntityState.Detached));
        Assert.That(await unitOfWork.SaveAsync(), Is.Zero);
        using (var reader = CreateContext())
        {
            Assert.That((await reader.LoyaltyLevels.SingleAsync()).DiscountPercent, Is.EqualTo(5m));
        }

        unitOfWork.LoyaltyLevels.Update(level);
        Assert.That(await unitOfWork.SaveAsync(), Is.EqualTo(1));
        using var savedReader = CreateContext();
        var saved = await savedReader.LoyaltyLevels.SingleAsync();
        Assert.That(saved.DiscountPercent, Is.EqualTo(7.25m));
        Assert.That(saved.MinTotalSpent, Is.EqualTo(350.50m));
    }

    [Test]
    public async Task IngredientLinkReplacement_RequiresExplicitDeletionAndPreservesPrincipals()
    {
        var order = await SaveOrderGraphAsync();
        var dishId = order.Items.Single().MenuItem.DishId;
        Context.ChangeTracker.Clear();
        var dish = await new DishRepository(Context).GetWithIngredientsAsync(dishId);
        dish!.DishIngredients.Remove(dish.DishIngredients.Single());

        Assert.ThrowsAsync<InvalidOperationException>(async () => await Context.SaveChangesAsync());
        using var reader = CreateContext();
        Assert.That(await reader.DishIngredients.CountAsync(), Is.EqualTo(1));

        using var writer = CreateContext();
        var repository = new DishRepository(writer);
        var editable = await repository.GetWithIngredientsAsync(dishId);
        var oldLink = editable!.DishIngredients.Single();
        repository.RemoveIngredientLink(oldLink);
        editable.DishIngredients.Remove(oldLink);
        editable.DishIngredients.Add(new DishIngredient
        {
            Dish = editable, Ingredient = new Ingredient { Name = "Salt" }
        });
        await new UnitOfWork(writer).SaveAsync();

        using var savedReader = CreateContext();
        Assert.That(await savedReader.DishIngredients.CountAsync(), Is.EqualTo(1));
        Assert.That((await savedReader.DishIngredients.Include(link => link.Ingredient).SingleAsync()).Ingredient.Name,
            Is.EqualTo("Salt"));
        Assert.That(await savedReader.Ingredients.CountAsync(), Is.EqualTo(2));
        Assert.That(await savedReader.Dishes.CountAsync(), Is.EqualTo(1));
        Assert.That(await savedReader.Orders.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task RemoveOrderItem_DeletesOnlyTheRequestedLineOnSave()
    {
        var saved = await SaveOrderGraphAsync();
        Context.ChangeTracker.Clear();
        var unitOfWork = new UnitOfWork(Context);
        var order = await unitOfWork.Orders.GetWithItemsAsync(saved.Id);
        var item = order!.Items.Single();
        unitOfWork.Orders.RemoveOrderItem(item);
        order.Items.Remove(item);

        using (var beforeSave = CreateContext())
        {
            Assert.That(await beforeSave.OrderItems.CountAsync(), Is.EqualTo(1));
        }

        Assert.That(await unitOfWork.SaveAsync(), Is.EqualTo(1));
        using var reader = CreateContext();
        Assert.That(await reader.OrderItems.CountAsync(), Is.Zero);
        Assert.That(await reader.Orders.CountAsync(), Is.EqualTo(1));
        Assert.That(await reader.MenuItems.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task DishQueries_LoadIngredientsAndUsage()
    {
        var order = await SaveOrderGraphAsync();
        var dishId = order.Items.Single().MenuItem.DishId;
        var ingredientId = order.Items.Single().MenuItem.Dish.DishIngredients.Single().IngredientId;
        Context.ChangeTracker.Clear();
        var repository = new DishRepository(Context);

        var dish = await repository.GetWithIngredientsAsync(dishId);

        Assert.That(dish!.DishIngredients.Single().Ingredient.Name, Is.EqualTo("Tomato"));
        Assert.That(await repository.HasMenuItemsAsync(dishId), Is.True);
        Assert.That(await repository.IsIngredientUsedAsync(ingredientId), Is.True);
        Assert.That(await repository.HasMenuItemsAsync(999), Is.False);
        Assert.That(await repository.IsIngredientUsedAsync(999), Is.False);
        Assert.That(await repository.GetWithIngredientsAsync(999), Is.Null);
        Context.ChangeTracker.Clear();
        Assert.That((await repository.GetAllWithIngredientsAsync()).Single().DishIngredients, Has.Count.EqualTo(1));
        Assert.That(Context.ChangeTracker.Entries(), Is.Empty);
    }

    [TestCase(true, true, true, 1)]
    [TestCase(false, true, true, 0)]
    [TestCase(true, false, true, 0)]
    [TestCase(false, false, true, 0)]
    [TestCase(true, true, false, 0)]
    public async Task PriceList_FiltersAvailabilityAndNonemptyDish(
        bool dishAvailable, bool portionAvailable, bool hasIngredients, int expectedCount)
    {
        var order = await SaveOrderGraphAsync();
        var item = order.Items.Single().MenuItem;
        item.IsAvailable = portionAvailable;
        item.Dish.IsAvailable = dishAvailable;
        if (!hasIngredients)
        {
            Context.DishIngredients.RemoveRange(item.Dish.DishIngredients);
        }

        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        var repository = new MenuItemRepository(Context);

        Assert.That(await repository.GetPriceListAsync(), Has.Count.EqualTo(expectedCount));
        Assert.That(await repository.GetPriceListAsync(false), Has.Count.EqualTo(1));
        Assert.That(Context.ChangeTracker.Entries(), Is.Empty);
        Assert.That((await repository.GetWithDishAsync(item.Id))!.Dish.Name, Is.EqualTo("Salad"));
        Assert.That(await repository.HasOrderItemsAsync(item.Id), Is.True);
        Assert.That(await repository.GetWithDishAsync(999), Is.Null);
        Assert.That(await repository.HasOrderItemsAsync(999), Is.False);
    }

    [Test]
    public async Task CustomerAndLevelQueries_LoadLevelsAndUseExactCodes()
    {
        var order = await SaveOrderGraphAsync();
        var code = order.Customer!.LoyaltyCode;
        Context.ChangeTracker.Clear();
        var customers = new CustomerRepository(Context);
        var levels = new LoyaltyLevelRepository(Context);

        Assert.That((await customers.FindByLoyaltyCodeAsync(code))!.LoyaltyLevel.Name, Is.EqualTo("Basic"));
        Assert.That(await customers.FindByLoyaltyCodeAsync("unknown"), Is.Null);
        Assert.That(await customers.FindByLoyaltyCodeAsync("' OR 1=1 --"), Is.Null);
        Assert.That(await customers.FindByLoyaltyCodeAsync(code.ToLowerInvariant()), Is.Null);
        Assert.That((await customers.GetAllWithLevelsAsync()).Single().LoyaltyLevel.Name, Is.EqualTo("Basic"));
        Assert.That((await levels.FindByNameAsync("Basic"))!.DiscountPercent, Is.Zero);
        Assert.That(await levels.FindByNameAsync("basic"), Is.Null);
        Assert.That(await levels.FindByNameAsync("unknown"), Is.Null);
        Assert.That(await levels.FindByNameAsync("Basic' OR 1=1 --"), Is.Null);
        Assert.That(await levels.GetAllAsync(), Has.Count.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CurrentMenuQuery_LoadsEntireGraphWithRequestedTracking(bool tracking)
    {
        var order = await SaveOrderGraphAsync();
        Context.ChangeTracker.Clear();
        var result = await new OrderRepository(Context).GetWithCurrentMenuAsync(order.Id, tracking);

        Assert.That(result!.Customer!.LoyaltyLevel.Name, Is.EqualTo("Basic"));
        Assert.That(result.Items.Single().MenuItem.Dish.DishIngredients.Single().Ingredient.Name, Is.EqualTo("Tomato"));
        Assert.That(result.Items.Single().MenuItem.Price, Is.EqualTo(123.45m));
        Assert.That(Context.ChangeTracker.Entries().Any(), Is.EqualTo(tracking));
    }

    [Test]
    public async Task OrderQueries_ReturnItemsAndOnlyCompletedForSelectedCustomers()
    {
        var completed = await SaveOrderGraphAsync(status: OrderStatus.Completed);
        var customer = completed.Customer!;
        foreach (var status in new[] { OrderStatus.Draft, OrderStatus.Confirmed, OrderStatus.Cancelled })
        {
            Context.Orders.Add(new Order
            {
                Customer = customer, CreatedAt = DateTime.UtcNow, Status = status,
                DiscountPercentFixed = status == OrderStatus.Confirmed ? 0m : null
            });
        }

        Context.Orders.Add(new Order
        {
            CreatedAt = DateTime.UtcNow, Status = OrderStatus.Completed, DiscountPercentFixed = 0
        });
        var otherCustomer = new Customer
        {
            FirstName = "Other", LastName = "Customer",
            LoyaltyCode = new string('B', 32), LoyaltyLevel = customer.LoyaltyLevel
        };
        Context.Orders.Add(new Order
        {
            Customer = otherCustomer, CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Completed, DiscountPercentFixed = 0
        });
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        var repository = new OrderRepository(Context);

        Assert.That((await repository.GetWithItemsAsync(completed.Id))!.Items, Has.Count.EqualTo(1));
        Assert.That(await repository.GetWithItemsAsync(999), Is.Null);
        Assert.That(await repository.GetWithCurrentMenuAsync(999), Is.Null);
        Context.ChangeTracker.Clear();
        var history = await repository.GetCompletedByCustomerAsync(customer.Id);
        Assert.That(history.Select(entity => entity.Id), Is.EqualTo(new[] { completed.Id }));
        Assert.That(history.Single().Items.Single().UnitPrice, Is.EqualTo(123.45m));
        Assert.That(await repository.GetCompletedForCustomersAsync(new[] { customer.Id }), Has.Count.EqualTo(1));
        Assert.That(await repository.GetCompletedForCustomersAsync(new[] { customer.Id, otherCustomer.Id }), Has.Count.EqualTo(2));
        Assert.That(await repository.GetCompletedForCustomersAsync(Array.Empty<int>()), Is.Empty);
        Assert.That(await repository.GetCompletedByCustomerAsync(999), Is.Empty);
        Assert.That(Context.ChangeTracker.Entries(), Is.Empty);
    }

    [Test]
    public void AsyncQueries_HonorCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var repository = new Repository<Ingredient>(Context);

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await repository.GetAllAsync(cancellation.Token));
    }
}
