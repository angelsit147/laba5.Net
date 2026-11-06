namespace WebApi.Configuration;

public class LoyaltySeedOptions
{
    public LevelOptions? Silver { get; set; }
    public LevelOptions? Gold { get; set; }

    public class LevelOptions
    {
        public int? MinOrders { get; set; }
        public decimal? MinTotalSpent { get; set; }
        public decimal? DiscountPercent { get; set; }
    }
}
