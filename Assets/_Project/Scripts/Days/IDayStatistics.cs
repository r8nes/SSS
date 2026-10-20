namespace ShooterShop.Days
{
    public interface IDayStatistics
    {
        void RecordSale(int income, bool isLaneRental);
        void RecordExpense(int cost);
    }
}
