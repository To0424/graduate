using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player-controlled game-speed multiplier (1x → 2x → 3x → 1x).
/// Sits on top of <see cref="Time.timeScale"/>; pause (timeScale == 0) is
/// preserved. <see cref="InteractionTimeScale"/> still slows the game while
/// the build/upgrade menu is open — the multiplier is restored when it ends.
///
/// The static <see cref="CurrentMultiplier"/> is the single source of truth
/// for "what speed should the game run at when not paused / not slowed".
/// </summary>
public class GameSpeedController : MonoBehaviour
{
    public static GameSpeedController Instance { get; private set; }

    /// <summary>1f, 2f, or 3f — never 0.</summary>
    public static float CurrentMultiplier { get; private set; } = 1f;

    static readonly float[] s_Steps = { 1f, 2f, 3f };
    int _stepIndex = 0;

    Button _btn;
    Text   _label;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Reset to 1x at the start of every scene so we don't carry stale speed.
        CurrentMultiplier = 1f;
        _stepIndex = 0;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Bind this controller to a UI button + label so clicking cycles speed.</summary>
    public void Bind(Button button, Text label)
    {
        _btn   = button;
        _label = label;
        if (_btn != null)
        {
            _btn.onClick.RemoveAllListeners();
            _btn.onClick.AddListener(Cycle);
        }
        UpdateLabel();
    }

    public void Cycle()
    {
        _stepIndex = (_stepIndex + 1) % s_Steps.Length;
        CurrentMultiplier = s_Steps[_stepIndex];
        ApplyToTimeScale();
        UpdateLabel();
    }

    /// <summary>Apply the current multiplier to <see cref="Time.timeScale"/>
    /// unless the game is hard-paused (== 0) or being slowed by an interaction.</summary>
    public static void ApplyToTimeScale()
    {
        if (Mathf.Approximately(Time.timeScale, 0f)) return; // paused — leave alone
        Time.timeScale = CurrentMultiplier;
    }

    void UpdateLabel()
    {
        if (_label == null) return;
        // Use ASCII chevrons so we don't depend on a unicode-capable font.
        switch (_stepIndex)
        {
            case 0: _label.text = ">    1x"; break;
            case 1: _label.text = ">>   2x"; break;
            case 2: _label.text = ">>>  3x"; break;
        }
    }
}
