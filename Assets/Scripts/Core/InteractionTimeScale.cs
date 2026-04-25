using UnityEngine;

/// <summary>
/// Reference-counted global slow-motion controller used while the player is
/// interacting with placement / radial / upgrade menus. Time scale drops to
/// <see cref="SlowScale"/> while at least one interaction is active.
/// Pause (Time.timeScale = 0) takes precedence and is never overwritten.
/// </summary>
public static class InteractionTimeScale
{
    public const float SlowScale = 0.25f;
    private static int _refCount = 0;

    public static bool IsSlowed => _refCount > 0;

    public static void Begin()
    {
        _refCount++;
        Apply();
    }

    public static void End()
    {
        if (_refCount > 0) _refCount--;
        Apply();
    }

    public static void Reset()
    {
        _refCount = 0;
        Apply();
    }

    static void Apply()
    {
        // Don't override hard pause (Time.timeScale == 0) initiated by GameManager.
        if (Mathf.Approximately(Time.timeScale, 0f)) return;
        // When un-slowed, restore to the player's chosen game-speed multiplier.
        Time.timeScale = _refCount > 0 ? SlowScale : GameSpeedController.CurrentMultiplier;
    }
}
