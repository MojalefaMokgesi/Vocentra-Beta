using System;


namespace Vocentra.Services
{
    public static class Pricing
    {
        public const decimal RatePerDayZar = 7m;


        public static int DaysInclusive(DateTime today, DateTime deadline)
        {
            var start = today.Date;
            var end = deadline.Date;


            if (end < start) return 0;
            return (end - start).Days + 1; // inclusive
        }


        public static decimal Price(DateTime today, DateTime deadline)
        {
            var days = DaysInclusive(today, deadline);
            return days <= 0 ? 0m : days * RatePerDayZar;
        }
    }
}