using System.Runtime.CompilerServices;
using VerifyXunit;

namespace DesktopAutomationTest.Tests;

/// <summary>
/// Configures Verify snapshot testing once for the whole test assembly.
/// <para>
/// Snapshots are stored in a <c>Snapshots</c> folder beside each source file so approved
/// baselines show up in ordinary code review diffs.
/// </para>
/// </summary>
public static class ModuleInit
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Verifier.UseSourceFileRelativeDirectory(Helpers.SnapshotHelper.SnapshotDirectoryName);
    }
}