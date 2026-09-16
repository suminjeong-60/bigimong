using System;

namespace Bigimong.AR
{
    public interface ICareClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemCareClock : ICareClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
