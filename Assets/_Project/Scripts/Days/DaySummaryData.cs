namespace ShooterShop.Days
{
    public readonly struct DaySummaryData
    {
        public string Title { get; }
        public int Income { get; }
        public int Expenses { get; }
        public int Profit { get; }
        public string Details { get; }

        public DaySummaryData(string title, int income, int expenses, int profit, string details)
        {
            Title = title;
            Income = income;
            Expenses = expenses;
            Profit = profit;
            Details = details;
        }
    }
}
