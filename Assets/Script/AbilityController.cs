using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Tracks the one active in-game ability (Magnet OR Shield, never both
// at once) and its countdown. Builds its own top-center HUD at runtime
// -- a red plaque card with the ability's icon and a "0:30" timer --
// so we don't have to edit the scene YAML. Also drives the shield
// bubble sprite on the Player and hands the Magnet's runtime radius
// to Player.ApplyPlanePerks.
public class AbilityController : MonoBehaviour
{
    public static AbilityController Instance { get; private set; }

    public enum AbilityType { None, Magnet, Shield }

    public const float AbilityDuration   = 15f;    // seconds
    public const float MagnetAbilityRadius = 5.0f; // world units, big enough that players feel it
    // Sprite GUIDs written by tools/make-ability-icons.py + meta template.
    private const string MagnetSpriteGuid       = "a1b2c3d4e5f60708192a3b4c5d6e7f80";
    private const string ShieldSpriteGuid       = "b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e0";
    private const string ShieldBubbleSpriteGuid = "c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f0";

    public AbilityType Current { get; private set; } = AbilityType.None;
    public float Remaining { get; private set; }
    public bool IsMagnetActive => Current == AbilityType.Magnet && Remaining > 0f;
    public bool IsShieldActive => Current == AbilityType.Shield && Remaining > 0f;

    // Cached so we can load them once rather than per-activation.
    private Sprite _magnetSprite;
    private Sprite _shieldSprite;
    private Sprite _shieldBubbleSprite;

    // HUD (built once on first use, reused across runs).
    private GameObject _hudRoot;
    private Image _hudIcon;
    private Text  _hudTimerText;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadSprites();
    }

    private void LoadSprites()
    {
        // The three ability sprites live under Assets/Resources/ so
        // they're guaranteed to be bundled into the build and reachable
        // via Resources.Load by name -- no scene-ref plumbing needed.
        _magnetSprite       = Resources.Load<Sprite>("ability_magnet");
        _shieldSprite       = Resources.Load<Sprite>("ability_shield");
        _shieldBubbleSprite = Resources.Load<Sprite>("ability_shield_bubble");
    }

    // Called from AbilityPickup.OnCollision. Returns true if the
    // pickup was consumed, false if the player already had an ability
    // active (in which case the pickup stays alive).
    public bool Activate(AbilityType type)
    {
        if (type == AbilityType.None) return false;
        if (Current != AbilityType.None && Remaining > 0f) return false;

        Current   = type;
        Remaining = AbilityDuration;

        EnsureHud();
        if (_hudIcon != null)
        {
            _hudIcon.sprite = type == AbilityType.Magnet ? _magnetSprite : _shieldSprite;
            _hudIcon.enabled = _hudIcon.sprite != null;
        }
        SetHudVisible(true);

        // Push state into the Player so Magnet bumps CoinMagnet radius
        // and Shield lights up the bubble.
        if (Player.Instance != null) Player.Instance.OnAbilityStateChanged();

        Debug.Log($"[AbilityController] Activated {type} for {AbilityDuration}s");
        return true;
    }

    public void Clear()
    {
        Current = AbilityType.None;
        Remaining = 0f;
        SetHudVisible(false);
        if (Player.Instance != null) Player.Instance.OnAbilityStateChanged();
    }

    private void Update()
    {
        if (Current == AbilityType.None) return;

        // Respect game pause (Time.timeScale = 0 when GameOver is shown).
        Remaining -= Time.deltaTime;
        if (Remaining <= 0f)
        {
            Debug.Log($"[AbilityController] {Current} expired");
            Clear();
            return;
        }

        if (_hudTimerText != null)
        {
            int secs = Mathf.CeilToInt(Remaining);
            _hudTimerText.text = string.Format("{0}:{1:D2}", secs / 60, secs % 60);
        }
    }

    // Expose the sprite guid ids so a helper can import them at
    // asset-bundle time; not used in runtime but keeps the constants
    // discoverable.
    public string GetMagnetGuid()       => MagnetSpriteGuid;
    public string GetShieldGuid()       => ShieldSpriteGuid;
    public string GetShieldBubbleGuid() => ShieldBubbleSpriteGuid;

    public Sprite GetMagnetSprite()       { if (_magnetSprite == null) _magnetSprite = Resources.Load<Sprite>("ability_magnet"); return _magnetSprite; }
    public Sprite GetShieldSprite()       { if (_shieldSprite == null) _shieldSprite = Resources.Load<Sprite>("ability_shield"); return _shieldSprite; }
    public Sprite GetShieldBubbleSprite() { if (_shieldBubbleSprite == null) _shieldBubbleSprite = Resources.Load<Sprite>("ability_shield_bubble"); return _shieldBubbleSprite; }

    // -------- HUD (runtime-built Canvas, top-center) ----------------
    // A small red plaque card that sits under the notch, showing the
    // active ability's icon and a M:SS timer in bold white. Style-
    // matched to the home-screen red banner so it feels like part of
    // the game rather than a debug overlay.
    private void EnsureHud()
    {
        if (_hudRoot != null) return;

        var canvasGO = new GameObject("AbilityHud");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // above game UI
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;
        canvasGO.AddComponent<GraphicRaycaster>();

        _hudRoot = new GameObject("AbilityHudPanel");
        _hudRoot.transform.SetParent(canvasGO.transform, false);
        var rt = _hudRoot.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -60f);
        rt.sizeDelta = new Vector2(280f, 120f);

        // Red plaque background (solid red rounded-rect using built-in UI sprite)
        var bg = _hudRoot.AddComponent<Image>();
        bg.color = new Color(215f / 255f, 38f / 255f, 61f / 255f, 1f);
        // Use Unity's built-in UISprite (the square with rounded corners)
        bg.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        bg.type = Image.Type.Sliced;

        // Thin white outline (just an inner rect with no fill, transparent)
        var outlineGO = new GameObject("outline");
        outlineGO.transform.SetParent(_hudRoot.transform, false);
        var outlineRT = outlineGO.AddComponent<RectTransform>();
        outlineRT.anchorMin = Vector2.zero;
        outlineRT.anchorMax = Vector2.one;
        outlineRT.offsetMin = new Vector2(6, 6);
        outlineRT.offsetMax = new Vector2(-6, -6);
        var outlineImg = outlineGO.AddComponent<Image>();
        outlineImg.color = new Color(1f, 1f, 1f, 0.18f);
        outlineImg.sprite = bg.sprite;
        outlineImg.type = Image.Type.Sliced;

        // Icon on the left
        var iconGO = new GameObject("icon");
        iconGO.transform.SetParent(_hudRoot.transform, false);
        var iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0f, 0.5f);
        iconRT.anchorMax = new Vector2(0f, 0.5f);
        iconRT.pivot = new Vector2(0f, 0.5f);
        iconRT.anchoredPosition = new Vector2(18f, 0f);
        iconRT.sizeDelta = new Vector2(84f, 84f);
        _hudIcon = iconGO.AddComponent<Image>();
        _hudIcon.preserveAspect = true;

        // Timer text on the right
        var textGO = new GameObject("timer");
        textGO.transform.SetParent(_hudRoot.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0f, 0f);
        textRT.anchorMax = new Vector2(1f, 1f);
        textRT.offsetMin = new Vector2(110f, 10f);
        textRT.offsetMax = new Vector2(-18f, -10f);
        _hudTimerText = textGO.AddComponent<Text>();
        _hudTimerText.alignment = TextAnchor.MiddleCenter;
        _hudTimerText.color = Color.white;
        _hudTimerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _hudTimerText.fontSize = 72;
        _hudTimerText.fontStyle = FontStyle.Bold;
        _hudTimerText.resizeTextForBestFit = true;
        _hudTimerText.resizeTextMinSize = 24;
        _hudTimerText.resizeTextMaxSize = 80;
        _hudTimerText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _hudTimerText.verticalOverflow = VerticalWrapMode.Overflow;
        _hudTimerText.text = "0:30";

        _hudRoot.SetActive(false);
    }

    private void SetHudVisible(bool visible)
    {
        EnsureHud();
        if (_hudRoot != null) _hudRoot.SetActive(visible);
    }
}
