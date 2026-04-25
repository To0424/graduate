using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Per-hero "card" UI shown along the bottom-left of the screen. Cards are
/// created when a HeroTower is registered and destroyed when it's removed —
/// so there is NO empty bar visible while no heroes are deployed.
///
/// Activation flow (placeable AOE skills):
///  • DRAG from the card onto the map → enters deployment mode and commits
///    at the drop position when the drag ends.
///  • CLICK the card once → enters deployment mode (reticle follows cursor)
///    and a small ✕ cancel button appears just above the card.
///      ◦ Click ✕ to cancel the skill.
///      ◦ Click the card again to commit the skill at the cursor position.
///
/// Non-placeable skills (e.g. RestoreLives) still fire instantly on click.
///
/// A radial cooldown fill darkens the card while the skill is on cooldown.
/// The cooldown is paused during the preparation phase between rounds — see
/// <see cref="HeroTower.IsRoundInProgress"/>.
/// </summary>
public class HeroSkillUI : MonoBehaviour
{
    public void Build(Transform canvasParent)
    {
        // Container is invisible — children (cards) carry their own visuals.
        _container = new GameObject("HeroCards");
        _container.transform.SetParent(canvasParent, false);
        var rt = _container.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.13f, 0f);
        rt.anchorMax = new Vector2(0.85f, 0.13f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var hlg = _container.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                = new RectOffset(8, 8, 6, 6);
        hlg.spacing                = 10f;
        hlg.childAlignment         = TextAnchor.LowerLeft;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        HeroTower.OnHeroRegistered   += AddHeroCard;
        HeroTower.OnHeroUnregistered += RemoveHeroCard;
    }

    void OnDestroy()
    {
        HeroTower.OnHeroRegistered   -= AddHeroCard;
        HeroTower.OnHeroUnregistered -= RemoveHeroCard;
    }

    // ── Per-hero card ─────────────────────────────────────────────────────────

    private GameObject _container;

    private struct HeroCard
    {
        public GameObject root;
        public Image      cardBg;
        public Image      cooldownOverlay;
        public TextMeshProUGUI nameLabel;
        public TextMeshProUGUI cooldownLabel;
    }

    private readonly Dictionary<HeroTower, HeroCard> _cards = new Dictionary<HeroTower, HeroCard>();

    void AddHeroCard(HeroTower hero)
    {
        if (hero == null || _container == null || _cards.ContainsKey(hero)) return;

        // Card root
        GameObject root = new GameObject($"Card_{hero.skillData?.skillName ?? "Hero"}");
        root.transform.SetParent(_container.transform, false);
        var le = root.AddComponent<LayoutElement>();
        le.preferredWidth = 110;
        le.preferredHeight = 130;

        // Background
        Image bg = root.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.10f, 0.16f, 0.95f);

        // Sprite tile
        GameObject icon = new GameObject("Icon");
        icon.transform.SetParent(root.transform, false);
        var iconRT = icon.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.08f, 0.32f);
        iconRT.anchorMax = new Vector2(0.92f, 0.95f);
        iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
        Image iconImg = icon.AddComponent<Image>();
        // Use the tower's sprite if it has one, else a coloured square
        SpriteRenderer towerSR = hero.tower != null ? hero.tower.GetComponent<SpriteRenderer>() : null;
        iconImg.sprite = (towerSR != null && towerSR.sprite != null)
                         ? towerSR.sprite
                         : RuntimeSprite.WhiteSquare;
        iconImg.color  = (towerSR != null) ? towerSR.color : Color.white;
        iconImg.preserveAspect = true;

        // Cooldown overlay (radial fill on top of icon)
        GameObject cd = new GameObject("Cooldown");
        cd.transform.SetParent(root.transform, false);
        var cdRT = cd.AddComponent<RectTransform>();
        cdRT.anchorMin = iconRT.anchorMin;
        cdRT.anchorMax = iconRT.anchorMax;
        cdRT.offsetMin = cdRT.offsetMax = Vector2.zero;
        Image cdImg = cd.AddComponent<Image>();
        cdImg.sprite = RuntimeSprite.WhiteSquare;
        cdImg.color  = new Color(0f, 0f, 0f, 0.55f);
        cdImg.type        = Image.Type.Filled;
        cdImg.fillMethod  = Image.FillMethod.Radial360;
        cdImg.fillOrigin  = (int)Image.Origin360.Top;
        cdImg.fillClockwise = false;
        cdImg.raycastTarget = false;

        // Name label
        GameObject nm = new GameObject("Name");
        nm.transform.SetParent(root.transform, false);
        var nmRT = nm.AddComponent<RectTransform>();
        nmRT.anchorMin = new Vector2(0, 0); nmRT.anchorMax = new Vector2(1, 0.30f);
        nmRT.offsetMin = nmRT.offsetMax = Vector2.zero;
        var nmTmp = nm.AddComponent<TextMeshProUGUI>();
        nmTmp.text = hero.skillData?.skillName ?? "Skill";
        nmTmp.fontSize = 14;
        nmTmp.alignment = TextAlignmentOptions.Center;
        nmTmp.color = Color.white;

        // Cooldown numeric label (over the icon)
        GameObject cdLbl = new GameObject("CdLbl");
        cdLbl.transform.SetParent(root.transform, false);
        var cdLRT = cdLbl.AddComponent<RectTransform>();
        cdLRT.anchorMin = iconRT.anchorMin;
        cdLRT.anchorMax = iconRT.anchorMax;
        cdLRT.offsetMin = cdLRT.offsetMax = Vector2.zero;
        var cdTmp = cdLbl.AddComponent<TextMeshProUGUI>();
        cdTmp.alignment = TextAlignmentOptions.Center;
        cdTmp.fontSize = 28;
        cdTmp.color = Color.white;
        cdTmp.fontStyle = FontStyles.Bold;
        cdTmp.raycastTarget = false;

        // Click + drag handler for the new arm/cancel/drag-to-deploy flow.
        var click = root.AddComponent<HeroCardClickHandler>();
        click.hero = hero;
        click.card = root;

        _cards[hero] = new HeroCard
        {
            root            = root,
            cardBg          = bg,
            cooldownOverlay = cdImg,
            nameLabel       = nmTmp,
            cooldownLabel   = cdTmp,
        };
    }

    void RemoveHeroCard(HeroTower hero)
    {
        if (!_cards.TryGetValue(hero, out var card)) return;
        if (card.root != null) Destroy(card.root);
        _cards.Remove(hero);
    }

    void Update()
    {
        foreach (var kv in _cards)
        {
            HeroTower hero = kv.Key;
            HeroCard  card = kv.Value;
            if (hero == null || card.root == null) continue;

            float frac = hero.CooldownFraction;
            if (card.cooldownOverlay != null) card.cooldownOverlay.fillAmount = frac;
            if (card.cooldownLabel   != null)
            {
                if (hero.SkillReady) card.cooldownLabel.text = "";
                else                 card.cooldownLabel.text =
                    $"{Mathf.CeilToInt(frac * (hero.skillData?.cooldown ?? 0f))}";
            }
            if (card.cardBg != null)
                card.cardBg.color = hero.SkillReady
                    ? new Color(0.10f, 0.18f, 0.26f, 0.95f)
                    : new Color(0.08f, 0.10f, 0.16f, 0.95f);
        }
    }
}

/// <summary>
/// Pointer-event router for a hero card. Implements three interaction paths:
///
///   1. PRESS-AND-DRAG  → Begin targeting on drag-start, commit at drag-end.
///   2. CLICK ONCE       → Arm the skill (start targeting, show ✕ cancel button).
///   3. CLICK AGAIN      → Commit the armed skill at the current cursor position.
///
/// Non-placeable skills are activated instantly on a single click.
/// </summary>
public class HeroCardClickHandler : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public HeroTower  hero;
    public GameObject card;        // root card object — used to anchor the cancel cross

    private GameObject _cancelCross;
    private bool       _armed;
    private bool       _dragging;
    private bool       _justFinishedDrag;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (hero == null || !hero.SkillReady || !hero.IsPlaceableAOE) return;
        // If we were armed via a previous click, the targeting reticle already
        // exists — just keep it. Otherwise, start it now.
        if (SkillTargetingController.Instance == null || !SkillTargetingController.Instance.IsTargeting)
            hero.BeginSkillTargeting();
        _dragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // The targeting controller already follows the mouse; nothing extra needed.
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging         = false;
        _justFinishedDrag = true;
        var ctl = SkillTargetingController.Instance;
        if (ctl != null && ctl.IsTargeting)
        {
            // If the drop happened over UI, treat as cancel; otherwise commit.
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (overUI) ctl.Cancel();
            else        ctl.CommitAtReticle();
        }
        ClearArmed();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (hero == null) return;

        // Clicks that closed a drag get a synthetic OnPointerClick — ignore it
        // so we don't immediately re-arm the skill we just deployed.
        if (_justFinishedDrag) { _justFinishedDrag = false; return; }

        if (!hero.SkillReady) return;

        // Non-placeable skills fire immediately.
        if (!hero.IsPlaceableAOE)
        {
            hero.ActivateSkill();
            return;
        }

        if (!_armed)
        {
            // First click — enter arm/targeting mode.
            if (hero.BeginSkillTargeting())
            {
                _armed = true;
                ShowCancelCross();
            }
        }
        else
        {
            // Second click on the card — commit at the current reticle position.
            var ctl = SkillTargetingController.Instance;
            if (ctl != null && ctl.IsTargeting) ctl.CommitAtReticle();
            ClearArmed();
        }
    }

    void Update()
    {
        // If the player cancels via right-click / Escape (or anything else that
        // ends targeting outside our flow), tear down the cross too.
        if (_armed && (SkillTargetingController.Instance == null
                       || !SkillTargetingController.Instance.IsTargeting))
            ClearArmed();
    }

    void ShowCancelCross()
    {
        if (card == null || _cancelCross != null) return;
        _cancelCross = new GameObject("CancelCross");
        _cancelCross.transform.SetParent(card.transform, false);
        var rt = _cancelCross.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(34, 34);
        rt.anchoredPosition = new Vector2(-2, 18);
        var img = _cancelCross.AddComponent<Image>();
        img.color = new Color(0.85f, 0.2f, 0.2f, 0.95f);

        // "✕" label
        var lblGO = new GameObject("X");
        lblGO.transform.SetParent(_cancelCross.transform, false);
        var lblRT = lblGO.AddComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;
        var t = lblGO.AddComponent<TextMeshProUGUI>();
        t.text = "✕";
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize  = 22;
        t.color     = Color.white;
        t.fontStyle = FontStyles.Bold;
        t.raycastTarget = false;

        var btn = _cancelCross.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            var ctl = SkillTargetingController.Instance;
            if (ctl != null && ctl.IsTargeting) ctl.Cancel();
            ClearArmed();
        });
        _cancelCross.transform.SetAsLastSibling();
    }

    void ClearArmed()
    {
        _armed = false;
        if (_cancelCross != null) Destroy(_cancelCross);
        _cancelCross = null;
    }
}
