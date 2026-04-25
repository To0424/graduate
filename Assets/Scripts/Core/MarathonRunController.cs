using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Lives only during a marathon run. Responsible for:
///   • Loading an optional campus background sprite from
///     <c>Resources/MarathonBackground</c> (or <c>marathon_bg</c>) and
///     scaling it to fill the play area.
///   • Hiding spawn-point markers / per-spawn path lines until they unlock
///     (waves 10 and 25). The mid spawn (index 0) is always active.
///   • Showing a custom Marathon Victory panel when the 40th wave is
///     completed and routing the Continue button back to the Main Menu
///     instead of the Overworld.
/// </summary>
public class MarathonRunController : MonoBehaviour
{
    public static MarathonRunController Instance { get; private set; }

    int _lastUnlockedCount = 1;
    GameObject _victoryPanel;
    Canvas _ownCanvas;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        WaveSpawner.OnRoundStart        += HandleRoundStart;
        WaveSpawner.OnAllRoundsComplete += HandleVictory;
    }

    void OnDisable()
    {
        WaveSpawner.OnRoundStart        -= HandleRoundStart;
        WaveSpawner.OnAllRoundsComplete -= HandleVictory;
    }

    void Start()
    {
        // Wait one frame so PathManager has finished building the path.
        StartCoroutine(InstallVisuals());
    }

    IEnumerator InstallVisuals()
    {
        yield return null;
        TryInstallBackground();
        ApplySpawnLockState(MarathonMode.ActiveSpawnCount(Mathf.Max(1, MarathonMode.CurrentWave)));
    }

    /// <summary>Look for a user-supplied background image at
    /// <c>Resources/MarathonBackground</c>. If present, fill the play area.</summary>
    void TryInstallBackground()
    {
        Sprite sprite = Resources.Load<Sprite>("MarathonBackground");
        if (sprite == null) sprite = Resources.Load<Sprite>("marathon_bg");
        if (sprite == null)
        {
            Debug.Log("[Marathon] No background image found at Resources/MarathonBackground. " +
                      "Drop a PNG named MarathonBackground.png into Assets/Resources to use one.");
            return;
        }
        if (GameObject.Find("MarathonBackground") != null) return;

        GameObject bg = new GameObject("MarathonBackground");
        SpriteRenderer sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(1f, 1f, 1f, 0.55f); // translucent so paths/UI read on top
        sr.sortingOrder = -10;

        // Map the world bounds we use for the marathon path: roughly x∈[-15,14], y∈[-6,6].
        const float worldW = 30f;
        const float worldH = 12f;
        float spriteW = sprite.bounds.size.x;
        float spriteH = sprite.bounds.size.y;
        if (spriteW > 0f && spriteH > 0f)
            bg.transform.localScale = new Vector3(worldW / spriteW, worldH / spriteH, 1f);
        bg.transform.position = new Vector3(0f, 0f, 0f);
    }

    void HandleRoundStart(int round0)
    {
        // round0 is the 0-based index of the wave that JUST started.
        int wave1 = round0 + 1;
        int active = MarathonMode.ActiveSpawnCount(wave1);
        if (active != _lastUnlockedCount)
        {
            ApplySpawnLockState(active);
            FlashSpawnUnlock(active - 1); // last unlocked index
            _lastUnlockedCount = active;
        }
    }

    /// <summary>Show / hide spawn markers and per-spawn path lines based on
    /// how many spawn points are currently active.</summary>
    void ApplySpawnLockState(int activeCount)
    {
        // Spawn markers are named "SpawnPoint{i}" by PathManager.
        for (int i = 0; i < 8; i++)
        {
            GameObject marker = GameObject.Find($"SpawnPoint{i}");
            if (marker == null) continue;
            bool active = i < activeCount;
            var sr = marker.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = active;
        }

        // Per-spawn path waypoints + drawn lines. PathManager names them
        // "Spawn{s}_WP{j}" and the lines that connect them have indices
        // starting at 1000 + s*100 + j ("PathLine{index}").
        for (int s = 1; s < 8; s++)
        {
            bool show = s < activeCount;
            for (int j = 0; j < 32; j++)
            {
                GameObject wp = GameObject.Find($"Spawn{s}_WP{j}");
                if (wp == null) break;
                var wpSR = wp.GetComponent<SpriteRenderer>();
                if (wpSR != null && wp.name.EndsWith($"WP{j}") && j == GetLastIndexFor(s))
                    wpSR.enabled = show; // home marker on locked routes is hidden
                int lineIdx = 1000 + s * 100 + j;
                GameObject line = GameObject.Find($"PathLine{lineIdx}");
                if (line == null) continue;
                var lsr = line.GetComponent<SpriteRenderer>();
                if (lsr != null) lsr.enabled = show;
            }
        }
    }

    /// <summary>Best-effort lookup of the highest WP index for a given spawn
    /// route by walking the names in the scene.</summary>
    int GetLastIndexFor(int s)
    {
        int last = 0;
        for (int j = 0; j < 64; j++)
        {
            if (GameObject.Find($"Spawn{s}_WP{j}") != null) last = j;
        }
        return last;
    }

    void FlashSpawnUnlock(int spawnIndex)
    {
        GameObject marker = GameObject.Find($"SpawnPoint{spawnIndex}");
        if (marker == null) return;
        StartCoroutine(FlashRoutine(marker));
    }

    IEnumerator FlashRoutine(GameObject obj)
    {
        var sr = obj.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;
        Color orig = sr.color;
        for (int i = 0; i < 6; i++)
        {
            sr.color = Color.white;
            yield return new WaitForSeconds(0.18f);
            sr.color = orig;
            yield return new WaitForSeconds(0.18f);
        }
    }

    void HandleVictory()
    {
        if (!MarathonMode.IsActive) return;
        ShowVictoryPanel();
    }

    void ShowVictoryPanel()
    {
        if (_victoryPanel != null) return;

        // Use a high-priority overlay canvas so we draw on top of the existing
        // levelWonPanel from GameplayAutoSetup.
        var go = new GameObject("MarathonVictoryCanvas");
        _ownCanvas = go.AddComponent<Canvas>();
        _ownCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _ownCanvas.sortingOrder = 6000;
        go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        go.AddComponent<GraphicRaycaster>();

        _victoryPanel = new GameObject("VictoryPanel");
        _victoryPanel.transform.SetParent(go.transform, false);
        var rt = _victoryPanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var bg = _victoryPanel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.92f);

        MakeText(_victoryPanel.transform, "GRADUATED!", 0.72f, 72, FontStyle.Bold,
                 new Color(1f, 0.85f, 0.3f));
        MakeText(_victoryPanel.transform, "You survived all 40 waves of campus life.", 0.6f, 28,
                 FontStyle.Normal, Color.white);
        MakeText(_victoryPanel.transform,
                 $"Buffs picked: {MarathonMode.BuffSelectionsTaken}", 0.52f, 22,
                 FontStyle.Normal, new Color(0.8f, 0.85f, 1f));

        AddButton(_victoryPanel.transform, "MAIN MENU", 0.34f,
                  new Color(0.2f, 0.55f, 0.85f), () =>
                  {
                      Time.timeScale = 1f;
                      MarathonMode.EndRun();
                      GameManager.Instance?.GoToMainMenu();
                  });

        Time.timeScale = 0f; // freeze gameplay until player picks
    }

    static void MakeText(Transform parent, string text, float yAnchor, int size,
                         FontStyle style, Color color)
    {
        var go = new GameObject("VictoryText");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, yAnchor);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900, 100);
        rt.anchoredPosition = Vector2.zero;
        var t = go.AddComponent<Text>();
        t.text = text; t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = size; t.fontStyle = style; t.color = color;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    static void AddButton(Transform parent, string label, float yAnchor, Color color,
                          System.Action onClick)
    {
        var go = new GameObject("VictoryBtn");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, yAnchor);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(360, 70);
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var t = txtGO.AddComponent<Text>();
        t.text = label; t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = 30; t.fontStyle = FontStyle.Bold; t.color = Color.white;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void OnDestroy()
    {
        if (_ownCanvas != null) Destroy(_ownCanvas.gameObject);
        if (Instance == this) Instance = null;
    }
}
