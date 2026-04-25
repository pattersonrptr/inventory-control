using NetArchTest.Rules;

namespace InventoryControl.Tests.Architecture;

/// <summary>
/// Architecture tests that enforce dependency rules for the Modular Monolith structure.
/// Tests are skipped until the corresponding code move sub-phase is complete,
/// then activated by removing the Skip attribute.
/// </summary>
public class DependencyRuleTests
{
    private static readonly Types AppTypes =
        Types.InAssembly(typeof(Program).Assembly);

    // Activated in sub-phase 5.3.10
    [Fact]
    public void Domain_HasNoDependencyOn_EntityFrameworkCore()
    {
        var result = AppTypes
            .That().ResideInNamespace("InventoryControl.Domain")
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain types must not reference EF Core directly. Violations: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    // Activated in sub-phase 5.4.5 — after Infrastructure/Persistence is wired up
    [Fact(Skip = "Activate after sub-phase 5.4: Infrastructure/Persistence complete")]
    public void Features_HaveNoDependencyOn_InfrastructurePersistenceImplementations()
    {
        var result = AppTypes
            .That().ResideInNamespace("InventoryControl.Features")
            .ShouldNot().HaveDependencyOn("InventoryControl.Infrastructure.Persistence")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Feature classes must not depend on Infrastructure.Persistence implementations. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    // Activated in sub-phase 5.4.5
    [Fact(Skip = "Activate after sub-phase 5.4: Infrastructure/Persistence complete")]
    public void Infrastructure_MustNotDependOn_Features()
    {
        var result = AppTypes
            .That().ResideInNamespace("InventoryControl.Infrastructure")
            .ShouldNot().HaveDependencyOn("InventoryControl.Features")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Infrastructure must not depend on Features. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
