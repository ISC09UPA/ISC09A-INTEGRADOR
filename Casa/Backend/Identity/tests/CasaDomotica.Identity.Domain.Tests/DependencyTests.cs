using System.Reflection;
using CasaDomotica.Identity.Domain.Entities;
using Xunit;

namespace CasaDomotica.Identity.Domain.Tests;

public class DependencyTests
{
    [Fact]
    public void Domain_assembly_does_not_reference_infrastructure_namespaces()
    {
        var assembly = typeof(User).Assembly;
        var references = assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, r => r.Name?.Contains("Infrastructure") == true);
        Assert.DoesNotContain(references, r => r.Name?.Contains("EntityFramework") == true);
        Assert.DoesNotContain(references, r => r.Name?.Contains("Azure") == true);
    }

    [Fact]
    public void Domain_assembly_does_not_reference_http_client_libraries()
    {
        var assembly = typeof(User).Assembly;
        var references = assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, r => r.Name?.Contains("Http") == true && r.Name != "System.Net.Http");
    }
}
