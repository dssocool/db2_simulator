namespace SizzlingDb.Core;

/// <summary>Database-specific simulator backend (DRDA for DB2, etc.).</summary>
internal interface ISimulatorBackend
{
    void Run(CancellationToken cancellation);
}
