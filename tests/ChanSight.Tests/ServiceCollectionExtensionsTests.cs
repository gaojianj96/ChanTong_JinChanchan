using ChanSight.Core.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ChanSight.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddChanSightCore_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddChanSightCore();

        result.Should().BeSameAs(services);
    }
}
