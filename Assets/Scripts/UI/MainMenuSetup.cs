using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drop this on an empty GameObject in the MainMenu scene.
/// It auto-creates the entire main menu UI at runtime.
/// </summary>
public class MainMenuSetup : MonoBehaviour
{
    void Start()
    {
        BuildUI();
    }

    void BuildUI()
    {
        // EventSystem (needed for UI clicks)
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Canvas
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // Background panel
        GameObject bg = CreatePanel(canvasObj.transform, "Background", new Color(0.03f, 0.03f, 0.03f, 1f));
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        ApplyFrontPageBackground(bg.GetComponent<Image>());

        // Marathon Mode Button (flagship 40-wave run).
        GameObject marathonBtn = CreateButton(canvasObj.transform, "MarathonModeButton",
                              "MARATHON (40 WAVES)", new Color(0.85f, 0.45f, 0.1f));
        RectTransform mRect = marathonBtn.GetComponent<RectTransform>();
        mRect.anchorMin = new Vector2(0.5f, 0.205f);
        mRect.anchorMax = new Vector2(0.5f, 0.205f);
        mRect.anchoredPosition = Vector2.zero;
        mRect.sizeDelta = new Vector2(420, 68);
        TextMeshProUGUI mLbl = marathonBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (mLbl != null) mLbl.fontSize = 24;
        marathonBtn.GetComponent<Button>().onClick.AddListener(OnMarathonMode);

        // Map Debugger button (launches marathon with in-game debug editor open).
        GameObject mapBtn = CreateButton(canvasObj.transform, "MapCreatorButton",
                          "MAP DEBUGGER", new Color(0.40f, 0.30f, 0.55f));
        RectTransform mapRect = mapBtn.GetComponent<RectTransform>();
        mapRect.anchorMin = new Vector2(0.5f, 0.125f);
        mapRect.anchorMax = new Vector2(0.5f, 0.125f);
        mapRect.anchoredPosition = Vector2.zero;
        mapRect.sizeDelta = new Vector2(420, 62);
        TextMeshProUGUI mapLbl = mapBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (mapLbl != null) mapLbl.fontSize = 22;
        mapBtn.GetComponent<Button>().onClick.AddListener(OnMapCreator);

        // Quit Button
        GameObject quitBtn = CreateButton(canvasObj.transform, "QuitButton", "QUIT", new Color(0.6f, 0.2f, 0.2f));
        RectTransform quitRect = quitBtn.GetComponent<RectTransform>();
        quitRect.anchorMin = new Vector2(0.5f, 0.045f);
        quitRect.anchorMax = new Vector2(0.5f, 0.045f);
        quitRect.anchoredPosition = Vector2.zero;
        quitRect.sizeDelta = new Vector2(420, 62);
        TextMeshProUGUI qLbl = quitBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (qLbl != null) qLbl.fontSize = 22;
        quitBtn.GetComponent<Button>().onClick.AddListener(OnQuit);
    }

    void OnMarathonMode()
    {
        MarathonMode.Launch();
    }

    void OnMapCreator()
    {
        InGameSlotDebugEditor.RequestOpenFromMainMenu(startInPathMode: true);
        MarathonMode.Launch();
    }

    void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ApplyFrontPageBackground(Image bgImage)
    {
        if (bgImage == null) return;

        // Prefer the dedicated front-page image; fall back to the previous marathon art.
        Sprite frontPage = Resources.Load<Sprite>("HKU");
        if (frontPage == null)
            frontPage = Resources.Load<Sprite>("MarathonBackground");

        if (frontPage != null)
        {
            bgImage.sprite = frontPage;
            bgImage.color = Color.white;
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = true;

            // Adapt fitting by shape: wide images fill screen, tall/boxy images stay fully visible.
            AspectRatioFitter fitter = bgImage.GetComponent<AspectRatioFitter>();
            if (fitter == null)
                fitter = bgImage.gameObject.AddComponent<AspectRatioFitter>();

            float spriteAspect = frontPage.rect.width / Mathf.Max(1f, frontPage.rect.height);
            float screenAspect = Screen.width / Mathf.Max(1f, Screen.height);
            fitter.aspectRatio = spriteAspect;
            fitter.aspectMode = spriteAspect >= screenAspect
                ? AspectRatioFitter.AspectMode.EnvelopeParent
                : AspectRatioFitter.AspectMode.FitInParent;
        }
    }

    // --- UI Helpers ---

    GameObject CreateText(Transform parent, string name, string text, int fontSize, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        return obj;
    }

    GameObject CreateButton(Transform parent, string name, string label, Color bgColor)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;
        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        // Button label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI tmp = labelObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 32;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;

        return btnObj;
    }

    GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Image img = obj.AddComponent<Image>();
        img.color = color;
        return obj;
    }
}
