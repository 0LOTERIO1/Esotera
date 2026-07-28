namespace Esotera.Infrastructure.Services;

using Esotera.Application.Interfaces;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
