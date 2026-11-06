using System.Text.Json.Serialization;

namespace Business.Models;

public class DishDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsAvailable { get; set; }
    public IReadOnlyList<IngredientDto> Ingredients { get; set; } = Array.Empty<IngredientDto>();
}

public class DishWriteDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool? IsAvailable { get; set; }
    [JsonRequired]
    public IReadOnlyList<int> IngredientIds { get; set; } = Array.Empty<int>();
}
