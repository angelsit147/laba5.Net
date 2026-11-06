using Data.Data;
using Data.Entities;
using Data.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Data.Tests;

public class UnitOfWorkTests : SqliteDatabaseFixture
{
    [Test]
    public async Task Repositories_ShareTrackedGraphAndSaveAllEightEntities()
    {
        var unitOfWork = new UnitOfWork(Context);
        var order = CreateOrderGraph();
        var menu = order.Items.Single().MenuItem;
        var ingredient = menu.Dish.DishIngredients.Single().Ingredient;
        await unitOfWork.Ingredients.AddAsync(ingredient);
        await unitOfWork.Dishes.AddAsync(menu.Dish);
        await unitOfWork.MenuItems.AddAsync(menu);
        await unitOfWork.LoyaltyLevels.AddAsync(order.Customer!.LoyaltyLevel);
        await unitOfWork.Customers.AddAsync(order.Customer);
        await unitOfWork.Orders.AddAsync(order);

        Assert.That(Context.Database.CurrentTransaction, Is.Null);
        Assert.That(await unitOfWork.SaveAsync(), Is.EqualTo(8));
        Assert.That(await unitOfWork.Ingredients.GetByIdAsync(ingredient.Id), Is.SameAs(ingredient));
        Assert.That(await unitOfWork.Dishes.GetByIdAsync(menu.Dish.Id), Is.SameAs(menu.Dish));
        Assert.That(await unitOfWork.MenuItems.GetByIdAsync(menu.Id), Is.SameAs(menu));
        Assert.That(await unitOfWork.Customers.GetByIdAsync(order.Customer.Id), Is.SameAs(order.Customer));
        Assert.That(await unitOfWork.LoyaltyLevels.GetByIdAsync(order.Customer.LoyaltyLevelId), Is.SameAs(order.Customer.LoyaltyLevel));
        Assert.That(await unitOfWork.Orders.GetByIdAsync(order.Id), Is.SameAs(order));
        using var reader = CreateContext();
        Assert.That(await reader.OrderItems.CountAsync(), Is.EqualTo(1));
        Assert.That(await reader.DishIngredients.CountAsync(), Is.EqualTo(1));
        Assert.That(await reader.Customers.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task StandardSave_IsAtomicWhenUniqueConstraintFails()
    {
        Context.LoyaltyLevels.Add(new LoyaltyLevel { Name = "Basic" });
        await Context.SaveChangesAsync();
        var unitOfWork = new UnitOfWork(Context);
        await unitOfWork.Ingredients.AddAsync(new Ingredient { Name = "Should roll back" });
        await unitOfWork.LoyaltyLevels.AddAsync(new LoyaltyLevel { Name = "Basic" });

        Assert.ThrowsAsync<DbUpdateException>(async () => await unitOfWork.SaveAsync());

        using var reader = CreateContext();
        Assert.That(await reader.Ingredients.CountAsync(), Is.Zero);
        Assert.That(await reader.LoyaltyLevels.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task ExplicitTransaction_CommitsSaveControlledByCallback()
    {
        var unitOfWork = new UnitOfWork(Context);
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            Assert.That(Context.Database.CurrentTransaction, Is.Not.Null);
            await unitOfWork.Ingredients.AddAsync(new Ingredient { Name = "Salt" }, ct);
            Assert.That(await unitOfWork.SaveAsync(ct), Is.EqualTo(1));
        });

        Assert.That(Context.Database.CurrentTransaction, Is.Null);
        using var reader = CreateContext();
        Assert.That((await reader.Ingredients.SingleAsync()).Name, Is.EqualTo("Salt"));
    }

    [Test]
    public async Task ExplicitTransaction_DoesNotSaveAutomatically()
    {
        var unitOfWork = new UnitOfWork(Context);
        await unitOfWork.ExecuteInTransactionAsync(ct =>
            unitOfWork.Ingredients.AddAsync(new Ingredient { Name = "Not saved" }, ct));

        using var reader = CreateContext();
        Assert.That(await reader.Ingredients.CountAsync(), Is.Zero);
        Assert.That(Context.Entry(Context.Ingredients.Local.Single()).State, Is.EqualTo(EntityState.Added));
    }

    [Test]
    public void ExplicitTransaction_RollsBackSavedChangesOnException()
    {
        var unitOfWork = new UnitOfWork(Context);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await unitOfWork.Ingredients.AddAsync(new Ingredient { Name = "Rolled back" }, ct);
                await unitOfWork.SaveAsync(ct);
                throw new InvalidOperationException("Test failure after Save.");
            }));

        Assert.That(Context.Database.CurrentTransaction, Is.Null);
        using var reader = CreateContext();
        Assert.That(reader.Ingredients.Count(), Is.Zero);
    }

    [Test]
    public void ExplicitTransaction_RollsBackUpdatesAndSavedInsertsTogether()
    {
        Context.Ingredients.Add(new Ingredient { Name = "Original" });
        Context.SaveChanges();
        var unitOfWork = new UnitOfWork(Context);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var original = (await unitOfWork.Ingredients.GetAllAsync(ct)).Single();
                original.Name = "Changed";
                Context.ChangeTracker.Clear();
                unitOfWork.Ingredients.Update(original);
                await unitOfWork.Dishes.AddAsync(new Dish { Name = "Rolled back" }, ct);
                await unitOfWork.SaveAsync(ct);
                throw new InvalidOperationException("Rollback the whole transaction.");
            }));

        using var reader = CreateContext();
        Assert.That(reader.Ingredients.Single().Name, Is.EqualTo("Original"));
        Assert.That(reader.Dishes.Count(), Is.Zero);
    }

    [Test]
    public void ExplicitTransaction_RollsBackWhenCancellationOccursAfterSave()
    {
        var unitOfWork = new UnitOfWork(Context);
        using var cancellation = new CancellationTokenSource();
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await unitOfWork.Ingredients.AddAsync(new Ingredient { Name = "Not committed" }, ct);
                await unitOfWork.SaveAsync(ct);
                cancellation.Cancel();
            }, cancellation.Token));

        using var reader = CreateContext();
        Assert.That(reader.Ingredients.Count(), Is.Zero);
        Assert.That(Context.Database.CurrentTransaction, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LevelAndCustomerChanges_UseTrackedIdentityAndOneSave(bool rollBack)
    {
        var order = CreateOrderGraph();
        var silver = order.Customer!.LoyaltyLevel;
        silver.Name = "Silver";
        silver.DiscountPercent = 5m;
        silver.MinOrders = 2;
        silver.MinTotalSpent = 200m;
        var gold = new LoyaltyLevel
        {
            Name = "Gold", DiscountPercent = 10m, MinOrders = 5, MinTotalSpent = 1000m
        };
        Context.Orders.Add(order);
        Context.LoyaltyLevels.AddRange(new LoyaltyLevel { Name = "Basic" }, gold);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        var unitOfWork = new UnitOfWork(Context);
        var saveCalls = 0;

        async Task Operation(CancellationToken ct)
        {
            var snapshot = (await unitOfWork.LoyaltyLevels.GetAllAsync(ct))
                .Single(entity => entity.Id == silver.Id);
            Assert.That(Context.Entry(snapshot).State, Is.EqualTo(EntityState.Detached));
            var customer = (await unitOfWork.Customers.GetAllWithLevelsAsync(ct)).Single();
            var trackedLevel = await unitOfWork.LoyaltyLevels.GetByIdAsync(snapshot.Id, ct);
            Assert.That(trackedLevel, Is.SameAs(customer.LoyaltyLevel));
            Assert.That(trackedLevel, Is.Not.SameAs(snapshot));

            trackedLevel!.DiscountPercent = 7.25m;
            customer.LoyaltyLevelId = gold.Id;
            saveCalls++;
            Assert.That(await unitOfWork.SaveAsync(ct), Is.EqualTo(2));
            if (rollBack)
            {
                throw new InvalidOperationException("Audit rollback after the single Save.");
            }
        }

        if (rollBack)
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await unitOfWork.ExecuteInTransactionAsync(Operation));
        }
        else
        {
            await unitOfWork.ExecuteInTransactionAsync(Operation);
        }

        Assert.That(saveCalls, Is.EqualTo(1));
        Assert.That(Context.Database.CurrentTransaction, Is.Null);
        using var reader = CreateContext();
        Assert.That((await reader.LoyaltyLevels.SingleAsync(entity => entity.Id == silver.Id)).DiscountPercent,
            Is.EqualTo(rollBack ? 5m : 7.25m));
        Assert.That((await reader.Customers.SingleAsync()).LoyaltyLevelId,
            Is.EqualTo(rollBack ? silver.Id : gold.Id));
    }

    [Test]
    public void ExplicitTransaction_RejectsNestedTransaction()
    {
        var unitOfWork = new UnitOfWork(Context);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await unitOfWork.ExecuteInTransactionAsync(ct =>
                unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask, ct)));

        Assert.That(Context.Database.CurrentTransaction, Is.Null);
    }
}
