using System;

namespace uContract;

/// <summary>
///     Manages runtime configuration for Design by Contract enforcement.
///     Reads settings from environment variables and applies Debug/Release defaults.
/// </summary>
/// <remarks>
///     <para>
///         Environment variables control which contract types are enforced:
///         <list type="bullet">
///             <item>
///                 <term>DBC</term><description>Global on/off switch (controls all contracts)</description>
///             </item>
///             <item>
///                 <term>DBC_PRE</term><description>Preconditions only</description>
///             </item>
///             <item>
///                 <term>DBC_POST</term><description>Postconditions only</description>
///             </item>
///             <item>
///                 <term>DBC_INV</term><description>Invariants only</description>
///             </item>
///             <item>
///                 <term>DBC_CHECK</term><description>Check statements only</description>
///             </item>
///             <item>
///                 <term>DBC_DOC</term><description>Documentation generation flag</description>
///             </item>
///         </list>
///     </para>
///     <para>
///         Default behavior:
///         <list type="bullet">
///             <item>Debug builds: All contracts enabled by default</item>
///             <item>Release builds: All contracts disabled by default (unless explicitly enabled)</item>
///         </list>
///     </para>
///     <para>
///         Precedence order:
///         <list type="number">
///             <item>Environment variables (if set)</item>
///             <item>Configuration defaults (Debug=true, Release=false)</item>
///         </list>
///     </para>
/// </remarks>
public class ContractConfiguration
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ContractConfiguration" /> class.
    ///     Reads environment variables and applies Debug/Release defaults.
    /// </summary>
    public ContractConfiguration()
    {
        // Determine default based on build configuration
        bool defaultEnabled = IsDebugBuild();

        // Read global DBC setting (acts as default when specific flags are not set)
        bool? globalDbc = ParseEnvironmentVariable("DBC");

        // Read specific settings (override global DBC when explicitly set)
        bool? preconditionsDbc = ParseEnvironmentVariable("DBC_PRE");
        bool? postconditionsDbc = ParseEnvironmentVariable("DBC_POST");
        bool? invariantsDbc = ParseEnvironmentVariable("DBC_INV");
        bool? checkDbc = ParseEnvironmentVariable("DBC_CHECK");

        // Apply precedence: specific type flags > global DBC > build-specific defaults
        // null coalescing (??) ensures we use the first non-null value:
        // - If DBC_PRE is set, use it (highest priority)
        // - Else if DBC is set, use it (middle priority)
        // - Else use build default (lowest priority)
        PreconditionsEnabled = preconditionsDbc ?? globalDbc ?? defaultEnabled;
        PostconditionsEnabled = postconditionsDbc ?? globalDbc ?? defaultEnabled;
        InvariantsEnabled = invariantsDbc ?? globalDbc ?? defaultEnabled;
        CheckEnabled = checkDbc ?? globalDbc ?? defaultEnabled;

        // Documentation is off by default
        DocumentationEnabled = ParseEnvironmentVariable("DBC_DOC") ?? false;
    }

    /// <summary>
    ///     Gets a value indicating whether preconditions are enabled.
    /// </summary>
    /// <remarks>
    ///     Determined by (in order):
    ///     <list type="number">
    ///         <item>DBC_PRE environment variable (if set)</item>
    ///         <item>DBC environment variable (if set, overrides specific settings)</item>
    ///         <item>Debug/Release default (Debug=true, Release=false)</item>
    ///     </list>
    /// </remarks>
    public bool PreconditionsEnabled { get; }

    /// <summary>
    ///     Gets a value indicating whether postconditions are enabled.
    /// </summary>
    /// <remarks>
    ///     Determined by (in order):
    ///     <list type="number">
    ///         <item>DBC_POST environment variable (if set)</item>
    ///         <item>DBC environment variable (if set, overrides specific settings)</item>
    ///         <item>Debug/Release default (Debug=true, Release=false)</item>
    ///     </list>
    /// </remarks>
    public bool PostconditionsEnabled { get; }

    /// <summary>
    ///     Gets a value indicating whether invariants are enabled.
    /// </summary>
    /// <remarks>
    ///     Determined by (in order):
    ///     <list type="number">
    ///         <item>DBC_INV environment variable (if set)</item>
    ///         <item>DBC environment variable (if set, overrides specific settings)</item>
    ///         <item>Debug/Release default (Debug=true, Release=false)</item>
    ///     </list>
    /// </remarks>
    public bool InvariantsEnabled { get; }

    /// <summary>
    ///     Gets a value indicating whether check statements are enabled.
    /// </summary>
    /// <remarks>
    ///     Determined by (in order):
    ///     <list type="number">
    ///         <item>DBC_CHECK environment variable (if set)</item>
    ///         <item>DBC environment variable (if set, overrides specific settings)</item>
    ///         <item>Debug/Release default (Debug=true, Release=false)</item>
    ///     </list>
    /// </remarks>
    public bool CheckEnabled { get; }

    /// <summary>
    ///     Gets a value indicating whether documentation generation is enabled.
    /// </summary>
    /// <remarks>
    ///     Determined by DBC_DOC environment variable.
    ///     Default: false
    /// </remarks>
    public bool DocumentationEnabled { get; }

    /// <summary>
    ///     Parses an environment variable as a boolean.
    /// </summary>
    /// <param name="variableName">Name of the environment variable.</param>
    /// <returns>
    ///     <c>true</c> if variable is set to "true", "1", "yes", or "on" (case-insensitive);
    ///     <c>false</c> if variable is set to "false", "0", "no", or "off" (case-insensitive);
    ///     <c>null</c> if variable is not set or has an invalid value.
    /// </returns>
    /// <remarks>
    ///     Invalid values are silently ignored, returning null to allow fallback to defaults.
    /// </remarks>
    private static bool? ParseEnvironmentVariable(string variableName)
    {
        string? value = Environment.GetEnvironmentVariable(variableName);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "on" => true,
            "false" or "0" or "no" or "off" => false,
            _ => null
        };
    }

    /// <summary>
    ///     Determines if the current build is a Debug build.
    /// </summary>
    /// <returns>
    ///     <c>true</c> if running a Debug build; <c>false</c> if Release build.
    /// </returns>
    /// <remarks>
    ///     Uses conditional compilation to detect build configuration.
    /// </remarks>
    private static bool IsDebugBuild()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }
}