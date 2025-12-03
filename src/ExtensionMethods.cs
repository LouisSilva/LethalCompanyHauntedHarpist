using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;

namespace LethalCompanyHarpGhost;

/// <summary>
/// Provides extension methods for various types to enhance functionality.
/// </summary>
internal static class ExtensionMethods
{
    /// <summary>
    /// Safely updates a NetworkVariable value if different from the current value.
    /// </summary>
    /// <typeparam name="T">The type implementing IEquatable.</typeparam>
    /// <param name="networkVariable">The NetworkVariable to update.</param>
    /// <param name="newValue">The new value to potentially set.</param>
    /// <remarks> Prevents unnecessary network updates by checking equality before setting.</remarks>
    public static void SafeSet<T>(this NetworkVariable<T> networkVariable, T newValue)
        where T : IEquatable<T>
    {
        if (!EqualityComparer<T>.Default.Equals(networkVariable.Value, newValue))
            networkVariable.Value = newValue;
    }

    /// <summary>
    /// Safely retrieves all loadable types from an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <returns>An Enumerable collection of valid types.</returns>
    /// <exception cref="ArgumentNullException">Thrown if assembly is null.</exception>
    /// <remarks>
    /// Handles ReflectionTypeLoadException by returning only valid types.
    /// </remarks>
    internal static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));

        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null);
        }
    }
}