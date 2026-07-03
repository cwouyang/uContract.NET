// Contract.Config is captured once (static readonly) from environment variables at
// first touch of the Contract class. DiagnosticLoggingTests and
// ContractConfigurationTests mutate those same process-wide variables, so any test
// class running in parallel with them can freeze a disabled configuration for the
// whole process. The suite therefore must run test collections serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
