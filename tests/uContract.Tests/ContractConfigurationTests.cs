namespace uContract.Tests;

/// <summary>
///     Tests for <see cref="ContractConfiguration" /> class.
///     Verifies environment variable parsing, precedence, and Debug/Release defaults.
/// </summary>
[Collection("EnvironmentVariables")]
public class ContractConfigurationTests
{
    #region Setup Helpers

    private static void _SetupEnvironment(
        string? dbc = null,
        string? dbcPre = null,
        string? dbcPost = null,
        string? dbcInv = null,
        string? dbcCheck = null,
        string? dbcDoc = null
    )
    {
        // Clear all environment variables first
        Environment.SetEnvironmentVariable("DBC", null);
        Environment.SetEnvironmentVariable("DBC_PRE", null);
        Environment.SetEnvironmentVariable("DBC_POST", null);
        Environment.SetEnvironmentVariable("DBC_INV", null);
        Environment.SetEnvironmentVariable("DBC_CHECK", null);
        Environment.SetEnvironmentVariable("DBC_DOC", null);

        // Set specified variables
        if (dbc is not null)
        {
            Environment.SetEnvironmentVariable("DBC", dbc);
        }

        if (dbcPre is not null)
        {
            Environment.SetEnvironmentVariable("DBC_PRE", dbcPre);
        }

        if (dbcPost is not null)
        {
            Environment.SetEnvironmentVariable("DBC_POST", dbcPost);
        }

        if (dbcInv is not null)
        {
            Environment.SetEnvironmentVariable("DBC_INV", dbcInv);
        }

        if (dbcCheck is not null)
        {
            Environment.SetEnvironmentVariable("DBC_CHECK", dbcCheck);
        }

        if (dbcDoc is not null)
        {
            Environment.SetEnvironmentVariable("DBC_DOC", dbcDoc);
        }
    }

    #endregion

    #region Default Configuration Tests

    [Fact]
    public void Constructor_WhenNoEnvironmentVariablesSet_UsesCorrectBuildDefaults()
    {
        _SetupEnvironment();

        ContractConfiguration config = new();

#if DEBUG
        const bool expectedDefault = true;
#else
        const bool expectedDefault = false;
#endif
        Assert.Equal(expectedDefault, config.PreconditionsEnabled);
        Assert.Equal(expectedDefault, config.PostconditionsEnabled);
        Assert.Equal(expectedDefault, config.InvariantsEnabled);
        Assert.Equal(expectedDefault, config.CheckEnabled);
        Assert.False(config.DocumentationEnabled); // Always false by default
    }

    #endregion

    #region Invalid Value Handling Tests

    [Theory]
    [InlineData("maybe")]
    [InlineData("enabled")]
    [InlineData("disabled")]
    [InlineData("2")]
    [InlineData("random")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")] // Whitespace-only values
    [InlineData("\t")] // Tab character
    [InlineData("\n")] // Newline character
    public void Constructor_WhenInvalidOrEmptyValues_FallsBackToDefaults(string invalidValue)
    {
        _SetupEnvironment(invalidValue);

        ContractConfiguration config = new();

#if DEBUG
        const bool expectedDefault = true;
#else
        const bool expectedDefault = false;
#endif
        // Should ignore invalid value and use build default
        Assert.Equal(expectedDefault, config.PreconditionsEnabled);
        Assert.Equal(expectedDefault, config.PostconditionsEnabled);
        Assert.Equal(expectedDefault, config.InvariantsEnabled);
        Assert.Equal(expectedDefault, config.CheckEnabled);
    }

    #endregion

    #region Boolean Parsing Tests

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("True")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("YES")]
    [InlineData("on")]
    [InlineData("ON")]
    public void Constructor_WhenVariousTrueBooleanFormats_ParsesAsTrue(string trueValue)
    {
        _SetupEnvironment(trueValue);

        ContractConfiguration config = new();

        Assert.True(config.PreconditionsEnabled);
        Assert.True(config.PostconditionsEnabled);
        Assert.True(config.InvariantsEnabled);
        Assert.True(config.CheckEnabled);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("FALSE")]
    [InlineData("False")]
    [InlineData("0")]
    [InlineData("no")]
    [InlineData("NO")]
    [InlineData("off")]
    [InlineData("OFF")]
    public void Constructor_WhenVariousFalseBooleanFormats_ParsesAsFalse(string falseValue)
    {
        _SetupEnvironment(falseValue);

        ContractConfiguration config = new();

        Assert.False(config.PreconditionsEnabled);
        Assert.False(config.PostconditionsEnabled);
        Assert.False(config.InvariantsEnabled);
        Assert.False(config.CheckEnabled);
    }

    #endregion

    #region Global DBC Flag Tests

    [Fact]
    public void Constructor_WhenGlobalDbcTrue_EnablesAllContractTypes()
    {
        _SetupEnvironment("true");

        ContractConfiguration config = new();

        Assert.True(config.PreconditionsEnabled);
        Assert.True(config.PostconditionsEnabled);
        Assert.True(config.InvariantsEnabled);
        Assert.True(config.CheckEnabled);
    }

    [Fact]
    public void Constructor_WhenGlobalDbcFalse_DisablesAllContractTypes()
    {
        _SetupEnvironment("false");

        ContractConfiguration config = new();

        Assert.False(config.PreconditionsEnabled);
        Assert.False(config.PostconditionsEnabled);
        Assert.False(config.InvariantsEnabled);
        Assert.False(config.CheckEnabled);
    }

    #endregion

    #region Specific Contract Type Tests

    [Fact]
    public void Constructor_WhenOnlyDbcPreIsTrue_EnablesPreconditionsAndUsesDefaultsForOthers()
    {
        _SetupEnvironment(dbcPre: "true");

        ContractConfiguration config = new();

#if DEBUG
        const bool expectedDefault = true;
#else
        const bool expectedDefault = false;
#endif
        Assert.True(config.PreconditionsEnabled); // Explicitly set
        Assert.Equal(expectedDefault, config.PostconditionsEnabled); // Uses default
        Assert.Equal(expectedDefault, config.InvariantsEnabled); // Uses default
        Assert.Equal(expectedDefault, config.CheckEnabled); // Uses default
    }

    [Fact]
    public void Constructor_WhenOnlyDbcPostIsFalse_DisablesPostconditionsAndUsesDefaultsForOthers()
    {
        _SetupEnvironment(dbcPost: "false");

        ContractConfiguration config = new();

#if DEBUG
        const bool expectedDefault = true;
#else
        const bool expectedDefault = false;
#endif
        Assert.Equal(expectedDefault, config.PreconditionsEnabled); // Uses default
        Assert.False(config.PostconditionsEnabled); // Explicitly set
        Assert.Equal(expectedDefault, config.InvariantsEnabled); // Uses default
        Assert.Equal(expectedDefault, config.CheckEnabled); // Uses default
    }

    [Fact]
    public void Constructor_WhenOnlyDbcInvIsFalse_DisablesInvariantsAndUsesDefaultsForOthers()
    {
        _SetupEnvironment(dbcInv: "false");

        ContractConfiguration config = new();

#if DEBUG
        const bool expectedDefault = true;
#else
        const bool expectedDefault = false;
#endif
        Assert.Equal(expectedDefault, config.PreconditionsEnabled); // Uses default
        Assert.Equal(expectedDefault, config.PostconditionsEnabled); // Uses default
        Assert.False(config.InvariantsEnabled); // Explicitly set
        Assert.Equal(expectedDefault, config.CheckEnabled); // Uses default
    }

    [Fact]
    public void Constructor_WhenOnlyDbcCheckIsTrue_EnablesChecksAndUsesDefaultsForOthers()
    {
        _SetupEnvironment(dbcCheck: "true");

        ContractConfiguration config = new();

#if DEBUG
        const bool expectedDefault = true;
#else
        const bool expectedDefault = false;
#endif
        Assert.Equal(expectedDefault, config.PreconditionsEnabled); // Uses default
        Assert.Equal(expectedDefault, config.PostconditionsEnabled); // Uses default
        Assert.Equal(expectedDefault, config.InvariantsEnabled); // Uses default
        Assert.True(config.CheckEnabled); // Explicitly set
    }

    #endregion

    #region Precedence Rules Tests

    [Theory]
    [InlineData("true", "false", "true", "false", "true", false, true, false, true)]
    [InlineData("false", "true", "false", "true", "false", true, false, true, false)]
    [InlineData("true", "true", "true", "true", "true", true, true, true, true)]
    [InlineData("false", "false", "false", "false", "false", false, false, false, false)]
    public void Constructor_WhenAllSpecificFlagsSet_RespectsEachIndividually(
        string globalDbc,
        string dbcPre,
        string dbcPost,
        string dbcInv,
        string dbcCheck,
        bool expectedPre,
        bool expectedPost,
        bool expectedInv,
        bool expectedCheck
    )
    {
        _SetupEnvironment(globalDbc, dbcPre, dbcPost, dbcInv, dbcCheck);

        ContractConfiguration config = new();

        Assert.Equal(expectedPre, config.PreconditionsEnabled);
        Assert.Equal(expectedPost, config.PostconditionsEnabled);
        Assert.Equal(expectedInv, config.InvariantsEnabled);
        Assert.Equal(expectedCheck, config.CheckEnabled);
    }

    [Fact]
    public void Constructor_WhenRedundantSpecificSetting_SpecificStillWins()
    {
        // Even when values are the same, specific should take precedence
        // This verifies the precedence logic path is executed correctly
        _SetupEnvironment(
            "true", // Global true
            "true", // Pre also true (redundant but valid)
            null,
            null,
            null
        );

        ContractConfiguration config = new();

        Assert.True(config.PreconditionsEnabled); // Specific (same as global)
        Assert.True(config.PostconditionsEnabled); // Global
        Assert.True(config.InvariantsEnabled); // Global
        Assert.True(config.CheckEnabled); // Global
    }

    [Fact]
    public void Constructor_WhenOnlySpecificFlagsSet_IgnoresDefaults()
    {
        _SetupEnvironment(
            null, // Global unset
            "true", // Pre set
            "false", // Post set
            "true", // Inv set
            "false" // Check set
        );

        ContractConfiguration config = new();

        // All should use specific values, none should fall back to defaults
        Assert.True(config.PreconditionsEnabled);
        Assert.False(config.PostconditionsEnabled);
        Assert.True(config.InvariantsEnabled);
        Assert.False(config.CheckEnabled);
    }

    [Fact]
    public void Constructor_WhenSpecificFlagInvalidAndGlobalValid_FallsBackToGlobal()
    {
        // Verifies the null-coalescing chain when specific flag is invalid
        // DBC=true (valid global), DBC_PRE=invalid (should fallback to global)
        _SetupEnvironment(
            "true", // DBC = true (valid global)
            "invalid_value" // DBC_PRE = invalid (should be treated as null)
        );

        ContractConfiguration config = new();

        // PreconditionsEnabled should use global DBC=true (not build default)
        // This verifies the null-coalescing chain: DBC_PRE (null) ?? DBC (true) ?? default
        Assert.True(config.PreconditionsEnabled); // Fallback to global true
        Assert.True(config.PostconditionsEnabled); // Uses global true
        Assert.True(config.InvariantsEnabled); // Uses global true
        Assert.True(config.CheckEnabled); // Uses global true
    }

    #endregion

    #region ADR Scenario Tests

    [Fact]
    public void Constructor_WhenGlobalFalseAndSpecificTrue_SpecificEnablesIndividualContracts()
    {
        // ADR-0004 Example 5: Production debugging scenario
        // Key advantage of Option B: Can enable specific types even when global is disabled
        _SetupEnvironment(
            "false", // Global disabled
            "true", // Pre enabled
            "true", // Post enabled
            "true", // Inv enabled
            "true" // Check enabled
        );

        ContractConfiguration config = new();

        // Key advantage: enable specific types even when global is false
        Assert.True(config.PreconditionsEnabled);
        Assert.True(config.PostconditionsEnabled);
        Assert.True(config.InvariantsEnabled);
        Assert.True(config.CheckEnabled);
    }

    [Fact]
    public void Constructor_WhenGlobalTrueAndSomeSpecificFalse_MixedBehavior()
    {
        // ADR-0004 Example 4: Selective disable scenario
        // Verifies null-coalescing priority in complex scenarios
        _SetupEnvironment(
            "true", // Global enabled
            "false", // Pre disabled (specific overrides)
            null, // Post uses global
            "false", // Inv disabled (specific overrides)
            null // Check uses global
        );

        ContractConfiguration config = new();

        // Mixed behavior: specific overrides, null falls back to global
        Assert.False(config.PreconditionsEnabled); // Specific false overrides global true
        Assert.True(config.PostconditionsEnabled); // Uses global true
        Assert.False(config.InvariantsEnabled); // Specific false overrides global true
        Assert.True(config.CheckEnabled); // Uses global true
    }

    #endregion

    #region Documentation Flag Tests

    [Fact]
    public void Constructor_WhenDbcDocTrue_EnablesDocumentation()
    {
        _SetupEnvironment(dbcDoc: "true");

        ContractConfiguration config = new();

        Assert.True(config.DocumentationEnabled);
    }

    [Fact]
    public void Constructor_WhenDbcDocFalse_DisablesDocumentation()
    {
        _SetupEnvironment(dbcDoc: "false");

        ContractConfiguration config = new();

        Assert.False(config.DocumentationEnabled);
    }

    [Fact]
    public void Constructor_WhenDbcDocUnset_DocumentationDisabledByDefault()
    {
        _SetupEnvironment(dbcDoc: null);

        ContractConfiguration config = new();

        Assert.False(config.DocumentationEnabled);
    }

    [Fact]
    public void Constructor_WhenDocEnabledAndContractsDisabled_DocumentationRemainsIndependent()
    {
        _SetupEnvironment(
            "false", // All contracts disabled
            null,
            null,
            null,
            null,
            "true" // Documentation enabled
        );

        ContractConfiguration config = new();

        // Documentation should be independent of contract settings
        Assert.False(config.PreconditionsEnabled);
        Assert.False(config.PostconditionsEnabled);
        Assert.False(config.InvariantsEnabled);
        Assert.False(config.CheckEnabled);
        Assert.True(config.DocumentationEnabled); // Independent of contracts
    }

    [Fact]
    public void Constructor_WhenDocDisabledAndContractsEnabled_DocumentationRemainsIndependent()
    {
        _SetupEnvironment(
            "true", // All contracts enabled
            null,
            null,
            null,
            null,
            "false" // Documentation disabled
        );

        ContractConfiguration config = new();

        // Documentation should be independent of contract settings
        Assert.True(config.PreconditionsEnabled);
        Assert.True(config.PostconditionsEnabled);
        Assert.True(config.InvariantsEnabled);
        Assert.True(config.CheckEnabled);
        Assert.False(config.DocumentationEnabled); // Independent of contracts
    }

    #endregion
}
