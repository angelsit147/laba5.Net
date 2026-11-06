using Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Data.Configurations;

public class DishIngredientConfiguration : IEntityTypeConfiguration<DishIngredient>
{
    public void Configure(EntityTypeBuilder<DishIngredient> builder)
    {
        builder.ToTable("DishIngredients", table =>
        {
            table.HasCheckConstraint("CK_DishIngredients_Id", "\"Id\" > 0");
        });
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
        builder.HasOne(entity => entity.Dish).WithMany(entity => entity.DishIngredients)
            .HasForeignKey(entity => entity.DishId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Ingredient).WithMany(entity => entity.DishIngredients)
            .HasForeignKey(entity => entity.IngredientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.DishId, entity.IngredientId })
            .IsUnique().HasDatabaseName("UX_DishIngredients_DishId_IngredientId");
        builder.HasIndex(entity => entity.IngredientId).HasDatabaseName("IX_DishIngredients_IngredientId");
    }
}
