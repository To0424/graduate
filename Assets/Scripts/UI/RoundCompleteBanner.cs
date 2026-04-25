using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>Big "ROUND COMPLETE" burnt-orange flash that pops on screen
/// every time WaveSpawner finishes a round. Auto-creates its own UI on
/// the supplied canvas.</summary>
public class RoundCompleteBanner : MonoBehaviour
{
    Canvas _canvas;
    GameObject _root;
    TextMeshProUGUI _label;
    Coroutine _activeAnim;

    public void Build(Canvas canvas)
    {
        _canvas = canvas;

        _root = new GameObject("RoundCompleteBanner");
        _root.transform.SetParent(_canvas.transform, false);
        var rt = _root.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.55f);
        rt.anchorMax = new Vector2(1f, 0.85f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        _label                = _root.AddComponent<TextMeshProUGUI>();
        _label.text           = "ROUND COMPLETE";
        _label.fontSize       = 96;
        _label.fontStyle      = FontStyles.Bold;
        _label.alignment      = TextAlignmentOptions.Center;
        _label.color          = new Color(1f, 0.55f, 0.05f, 1f); // ember
        _label.outlineColor   = new Color(0.15f, 0.05f, 0f, 1f);
        _label.outlineWidth   = 0.35f;
        _label.enableWordWrapping = false;

        _root.SetActive(false);

        WaveSpawner.OnRoundComplete += HandleRoundComplete;
    }

    void OnDestroy()
    {
        WaveSpawner.OnRoundComplete -= HandleRoundComplete;
    }

    void HandleRoundComplete(int roundIndex)
    {
        if (_activeAnim != null) StopCoroutine(_activeAnim);
        _label.text = $"ROUND {roundIndex + 1} COMPLETE";
        _activeAnim = StartCoroutine(PopAndFade());
    }

    IEnumerator PopAndFade()
    {
        _root.SetActive(true);
        var rt = (RectTransform)_root.transform;
        float t = 0f;
        const float popDur = 0.35f, holdDur = 1.1f, fadeDur = 0.9f;

        // Pop in (scale + alpha)
        while (t < popDur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popDur);
            float s = Mathf.Lerp(0.4f, 1.15f, k);
            rt.localScale = new Vector3(s, s, 1f);
            SetAlpha(k);
            yield return null;
        }
        rt.localScale = Vector3.one;
        SetAlpha(1f);

        // Hold
        yield return new WaitForSecondsRealtime(holdDur);

        // Fade
        t = 0f;
        while (t < fadeDur)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(1f - Mathf.Clamp01(t / fadeDur));
            yield return null;
        }
        _root.SetActive(false);
        _activeAnim = null;
    }

    void SetAlpha(float a)
    {
        Color c = _label.color; c.a = a; _label.color = c;
        Color o = _label.outlineColor; o.a = a; _label.outlineColor = o;
    }
}
