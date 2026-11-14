using System.Reflection;
using CasaDomotica.Home.Domain.Entities;
using Xunit;

namespace CasaDomotica.Home.Domain.Tests;

public class DependencyTests
{
    [Fact]
    public void Domain_assembly_does_not_reference_infrastructure_or_azure()
    {
        var assembly = typeof(Light).Assembly;
        var references = assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, r => r.Name?.Contains("Infrastructure") == true);
        Assert.DoesNotContain(references, r => r.Name?.Contains("EntityFramework") == true);
        Assert.DoesNotContain(references, r => r.Name?.Contains("Azure") == true);
    }

    [Fact]
    public void Light_does_not_roll_back_on_older_revision()
    {
        var light = new Light { Id = "sala", HouseId = "house-1" };
        light.ApplyState(true, "manual", 5);
        light.ApplyState(false, "auto", 3);

        Assert.True(light.IsOn);
        Assert.Equal("manual", light.Mode);
        Assert.Equal(5, light.StateRevision);
    }
}
