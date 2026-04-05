using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
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
        GameObject bg = CreatePanel(canvasObj.transform, "Background", new Color(0.15f, 0.15f, 0.25f, 1f));
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Title
        GameObject titleObj = CreateText(canvasObj.transform, "Title", "GRADUATION", 72, Color.white);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.7f);
        titleRect.anchorMax = new Vector2(0.5f, 0.7f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(800, 100);

        // Subtitle
        GameObject subObj = CreateText(canvasObj.transform, "Subtitle", "A Tower Defense Game\nHKU - Group 23", 28, new Color(0.8f, 0.8f, 0.8f));
        RectTransform subRect = subObj.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 0.55f);
        subRect.anchorMax = new Vector2(0.5f, 0.55f);
        subRect.anchoredPosition = Vector2.zero;
        subRect.sizeDelta = new Vector2(600, 80);

        // Play Button (Continue)
        GameObject playBtn = CreateButton(canvasObj.transform, "PlayButton", "CONTINUE", new Color(0.2f, 0.6f, 0.3f));
        RectTransform playRect = playBtn.GetComponent<RectTransform>();
        playRect.anchorMin = new Vector2(0.5f, 0.38f);
        playRect.anchorMax = new Vector2(0.5f, 0.38f);
        playRect.anchoredPosition = Vector2.zero;
        playRect.sizeDelta = new Vector2(300, 70);
        playBtn.GetComponent<Button>().onClick.AddListener(OnPlay);

        // New Game Button
        GameObject newBtn = CreateButton(canvasObj.transform, "NewGameButton", "NEW GAME", new Color(0.6f, 0.4f, 0.1f));
        RectTransform newRect = newBtn.GetComponent<RectTransform>();
        newRect.anchorMin = new Vector2(0.5f, 0.27f);
        newRect.anchorMax = new Vector2(0.5f, 0.27f);
        newRect.anchoredPosition = Vector2.zero;
        newRect.sizeDelta = new Vector2(300, 70);
        newBtn.GetComponent<Button>().onClick.AddListener(OnNewGame);

        // Quit Button
        GameObject quitBtn = CreateButton(canvasObj.transform, "QuitButton", "QUIT", new Color(0.6f, 0.2f, 0.2f));
        RectTransform quitRect = quitBtn.GetComponent<RectTransform>();
        quitRect.anchorMin = new Vector2(0.5f, 0.16f);
        quitRect.anchorMax = new Vector2(0.5f, 0.16f);
        quitRect.anchoredPosition = Vector2.zero;
        quitRect.sizeDelta = new Vector2(300, 70);
        quitBtn.GetComponent<Button>().onClick.AddListener(OnQuit);
    }

    void OnPlay()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.GoToOverworld();
        else
            SceneManager.LoadScene("Overworld");
    }

    void OnNewGame()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetAllProgress();
            GameManager.Instance.GoToOverworld();
        }
        else
        {
            PlayerPrefs.DeleteAll();
            SceneManager.LoadScene("Overworld");
        }
    }

    void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
