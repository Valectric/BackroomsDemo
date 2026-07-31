using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Backrooms.Editor
{
    /// <summary>
    /// Removes test-only assemblies from player builds.
    /// </summary>
    /// <remarks>
    /// MooseRunner's <c>MooseRunner.helper</c> assembly is a Runtime assembly with no define
    /// constraints that lists <c>nunit.framework.dll</c> as a precompiled reference. It is therefore
    /// compiled into player builds, where nunit does not exist, and the IL2CPP linker aborts with
    /// "Failed to resolve assembly: 'nunit.framework'". Nothing in the shipped game references it —
    /// only tests do — so dropping it from the build is safe and keeps the player smaller.
    /// Remove this filter once the package constrains that assembly to test builds.
    /// </remarks>
    public sealed class BuildAssemblyFilter : IFilterBuildAssemblies
    {
        /// <summary>Runs early; the order relative to other filters does not matter here.</summary>
        public int callbackOrder => 0;

        /// <summary>Assembly name fragments that must never reach a player build.</summary>
        private static readonly string[] Excluded =
        {
            "MooseRunner.helper",
            "nunit.framework",
            ".Tests"
        };

        /// <summary>
        /// Filters the assembly list Unity is about to build into the player.
        /// </summary>
        /// <param name="buildOptions">Options for the build in progress.</param>
        /// <param name="assemblies">Assembly paths Unity intends to include.</param>
        /// <returns>The assemblies to actually include.</returns>
        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            string[] kept = assemblies
                .Where(path => !Excluded.Any(fragment => path.Contains(fragment)))
                .ToArray();

            int dropped = assemblies.Length - kept.Length;
            if (dropped > 0) Debug.Log($"[Backrooms] Excluded {dropped} test assembly/assemblies from the build");

            return kept;
        }
    }
}
