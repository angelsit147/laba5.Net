using Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Data.Configurations;

public class DishConfiguration : IEntityTypeConfiguration<Dish>
{
    public void Configure(EntityTypeBuilder<Dish> builder)
    {
        builder.ToTable("Dishes", table =>
        {
            table.HasCheckConstraint("CK_Dishes_Id", "\"Id\" > 0");
            table.HasCheckConstraint("CK_Dishes_IsAvailable", "\"IsAvailable\" IN (0, 1)");
        });
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
        builder.Property(entity => entity.Name).IsRequired().HasMaxLength(100);
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.IsAvailable).IsRequired();
    }
}
