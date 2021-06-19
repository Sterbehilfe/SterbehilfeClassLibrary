﻿namespace Sterbehilfe.Time.Interfaces
{
    public interface ITimeUnit
    {
        public int Count { get; set; }

        public long ToMilliseconds();

        public long ToSeconds();
    }
}