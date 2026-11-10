using System;
using System.Collections.Generic;
using System.Text;

namespace PowerGridSim.Models
{
    public readonly record struct SimTime(int TotalHours)
    {
        public int DayNumber => TotalHours / 24;         
        public int HourOfDay => TotalHours % 24;                      
        public DayOfWeek DayOfWeek => (DayOfWeek)((DayNumber + 1) % 7); // hour 0 = Monday
        public bool IsWeekend => DayOfWeek == DayOfWeek.Saturday || DayOfWeek == DayOfWeek.Sunday;

        public SimTime Advance(int hours = 1) => new(TotalHours + hours);
        public override string ToString() => $"Day {DayNumber + 1} {HourOfDay:D2}:00";
    }
}
