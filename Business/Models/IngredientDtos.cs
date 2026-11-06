namespace Business.Models;

public class IngredientDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class IngredientWriteDto
{
    public string Name { get; set; } = string.Empty;
}
