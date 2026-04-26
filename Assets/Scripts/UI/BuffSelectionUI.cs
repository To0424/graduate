using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal screen shown after every BUFF_INTERVAL waves in marathon mode.
/// Pauses the game (Time.timeScale = 0), presents 3 buff cards, applies the
/// chosen one to RunBuffs / availableTowers, then resumes.
/// </summary>
public class BuffSelectionUI : MonoBehaviour
{
    public static BuffSelectionUI Instance { get; private set; }

    GameObject root;
    Canvas canvas;
    bool isOpen;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()  { WaveSpawner.OnRoundComplete += HandleRoundComplete; }
    void OnDisable() { WaveSpawner.OnRoundComplete -= HandleRoundComplete; }

    void HandleRoundComplete(int roundIndex0)
    {
        // roundIndex0 is the just-completed 0-based round; offer after wave 2,4,6,...
        int wave1 = roundIndex0 + 1;
        if (!MarathonMode.ShouldOfferBuffsAfterWave(wave1)) return;
        Open(MarathonMode.RollBuffOffers());
    }

    public void Open(List<BuffOffer> offers)
    {
        if (isOpen || offers == null || offers.Count == 0) return;
        isOpen = true;
        Time.timeScale = 0f;

        EnsureCanvas();

        root = new GameObject("BuffSelection");
        root.transform.SetParent(canvas.transform, false);
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // Light dim only, so the picker feels in-world instead of a full page.
        var bg = root.AddComponent<Image>();
        bg.sprite = RuntimeSprite.WhiteSquare;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.03f, 0.05f, 0.09f, 0.60f);
        bg.raycastTarget = true;

        var subtitle = $"Pity {MarathonMode.PityCounter}/{MarathonMode.HERO_PITY_LIMIT}";
        MakeText(root.transform, subtitle, new Vector2(0.5f, 0.90f), 22, FontStyle.Bold,
                 new Color(0.88f, 0.91f, 0.98f));

        // Cards row only (no full page panel).
        int n = offers.Count;
        float cardW = 320f, cardH = 430f, gap = 26f;
        float totalW = n * cardW + (n - 1) * gap;
        for (int i = 0; i < n; i++)
        {
            float x = -totalW * 0.5f + cardW * 0.5f + i * (cardW + gap);
            BuildCard(root.transform, offers[i], new Vector2(x, 0f), cardW, cardH);
        }
    }

    void BuildCard(Transform parent, BuffOffer offer, Vector2 anchoredPos, float w, float h)
    {
        var card = new GameObject($"Card_{offer.title}");
        card.transform.SetParent(parent, false);
        var rt = card.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = anchoredPos;

        var img = card.AddComponent<Image>();
        img.sprite = RuntimeSprite.WhiteSquare;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.10f, 0.13f, 0.22f, 1f);
        var cardOutline = card.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0f, 0f, 0f, 0.92f);
        cardOutline.effectDistance = new Vector2(2f, -2f);

        var cardButton = card.AddComponent<Button>();
        cardButton.targetGraphic = img;
        BuffOffer captured = offer;
        cardButton.onClick.AddListener(() => OnPick(captured));

        // Top accent strip = rarity
        var stripGO = new GameObject("Strip");
        stripGO.transform.SetParent(card.transform, false);
        var srt = stripGO.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.sizeDelta = new Vector2(0f, 18f); srt.anchoredPosition = Vector2.zero;
        var strip = stripGO.AddComponent<Image>();
        strip.sprite = RuntimeSprite.WhiteSquare;
        strip.type = Image.Type.Sliced;
        strip.color = offer.RarityColor();

        // Rarity label
        MakeLocalText(card.transform, offer.RarityLabel(), new Vector2(0.5f, 0.88f),
                      22, FontStyle.Bold, offer.RarityColor());
        // Title
        MakeLocalText(card.transform, offer.title, new Vector2(0.5f, 0.76f),
                      32, FontStyle.Bold, Color.white, w - 34f, 80f);
        // Description
        MakeLocalText(card.transform, offer.description, new Vector2(0.5f, 0.53f),
                      22, FontStyle.Normal, new Color(0.90f, 0.92f, 0.98f), w - 46f, 220f);

        MakeLocalText(card.transform, "Click card to pick", new Vector2(0.5f, 0.10f),
                      20, FontStyle.Bold, new Color(0.98f, 0.98f, 1f), w - 34f, 44f);
    }

    void OnPick(BuffOffer offer)
    {
        if (offer == null) { Close(); return; }
        ApplyOffer(offer);
        MarathonMode.RecordBuffPicked(offer);
        Close();
    }

    void ApplyOffer(BuffOffer offer)
    {
        switch (offer.kind)
        {
            case BuffOfferKind.StatBuff:
                if (RunBuffs.Instance != null && offer.statBuff != null)
                    RunBuffs.Instance.Apply(offer.statBuff);
                break;
            case BuffOfferKind.UnlockTower:
            case BuffOfferKind.UnlockHero:
                AddTowerToBuildMenu(offer.towerToUnlock);
                break;
            case BuffOfferKind.BonusGold:
                CurrencyManager.Instance?.AddGold(offer.amount);
                break;
            case BuffOfferKind.BonusLife:
                LivesManager.Instance?.RestoreLives(offer.amount);
                break;
        }
    }

    void AddTowerToBuildMenu(TowerData td)
    {
        if (td == null) return;
        // 1. Radial build menu (BuildAndUpgradeUI.availableOptions).
        var ui = FindAnyObjectByType<BuildAndUpgradeUI>();
        if (ui != null)
        {
            var existing = ui.availableOptions ?? new TowerData[0];
            bool already = false;
            foreach (var t in existing) if (t == td) { already = true; break; }
            if (!already)
            {
                var newArr = new TowerData[existing.Length + 1];
                for (int i = 0; i < existing.Length; i++) newArr[i] = existing[i];
                newArr[existing.Length] = td;
                ui.availableOptions = newArr;
            }
        }

        // 2. Side shop panel (GameplayAutoSetup.availableTowers + rebuilt panel).
        var setup = FindAnyObjectByType<GameplayAutoSetup>();
        if (setup != null)
        {
            var existing = setup.availableTowers ?? new TowerData[0];
            bool already = false;
            foreach (var t in existing) if (t == td) { already = true; break; }
            if (!already)
            {
                var newArr = new TowerData[existing.Length + 1];
                for (int i = 0; i < existing.Length; i++) newArr[i] = existing[i];
                newArr[existing.Length] = td;
                setup.availableTowers = newArr;
                setup.RebuildTowerShop();
            }
        }
    }

    void Close()
    {
        if (root != null) Destroy(root);
        isOpen = false;
        Time.timeScale = 1f;
    }

    void EnsureCanvas()
    {
        canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null) return;
        var go = new GameObject("BuffCanvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        go.AddComponent<GraphicRaycaster>();
    }

    static Text MakeText(Transform parent, string text, Vector2 anchor, int size, FontStyle style, Color color)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900, 80);
        rt.anchoredPosition = Vector2.zero;
        var t = go.AddComponent<Text>();
        t.text = text;
        t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }

    static Text MakeLocalText(Transform parent, string text, Vector2 anchor, int size, FontStyle style,
                              Color color, float width = 280, float height = 60)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = Vector2.zero;
        var t = go.AddComponent<Text>();
        t.text = text;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }
}
