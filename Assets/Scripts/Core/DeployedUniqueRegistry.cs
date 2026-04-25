using System.Collections.Generic;
using System;

/// <summary>
/// Tracks unique TowerData assets that have already been deployed in the
/// current level so the radial / sidebar menus can hide them. Cleared on
/// scene load via <see cref="Reset"/> (called from GameplayAutoSetup).
/// </summary>
public static class DeployedUniqueRegistry
{
    private static readonly HashSet<TowerData> _deployed = new HashSet<TowerData>();

    public static event Action OnChanged;

    public static bool IsDeployed(TowerData data) => data != null && _deployed.Contains(data);

    public static void MarkDeployed(TowerData data)
    {
        if (data == null) return;
        if (_deployed.Add(data)) OnChanged?.Invoke();
    }

    public static void Unmark(TowerData data)
    {
        if (data == null) return;
        if (_deployed.Remove(data)) OnChanged?.Invoke();
    }

    public static void Reset()
    {
        _deployed.Clear();
        OnChanged?.Invoke();
    }
}
