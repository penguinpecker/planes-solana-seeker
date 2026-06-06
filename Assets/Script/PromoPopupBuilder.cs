using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

// Themed in-game popup (sky-blue / red-plaque / gold, matching the PLANES
// brand) used for review asks, "follow us on X" / tweet CTAs, and
// announcements. Content is supplied at runtime (by PromoManager, which pulls
// it from the pl_promos table) so it can change with no app update.
//
// Layout: dim backdrop -> rounded card -> red title bar -> optional banner
// image -> body text -> optional gold CTA button (opens a URL) -> "Maybe later"
// dismiss. The CTA only opens http/https links.
public class PromoPopupBuilder : MonoBehaviour
{
    public static PromoPopupBuilder Instance { get; private set; }

    // PLANES palette.
    private static readonly Color Dim     = new Color(0f, 0f, 0f, 0.62f);
    private static readonly Color Sky     = new Color(0.81f, 0.91f, 0.98f, 1f);   // card
    private static readonly Color Red      = new Color(0.87f, 0.17f, 0.17f, 1f);  // title bar / CTA accent
    private static readonly Color Gold     = new Color(0.96f, 0.71f, 0.02f, 1f);  // CTA button
    private static readonly Color Ink       = new Color(0.10f, 0.16f, 0.22f, 1f); // body text
    private static readonly Color White     = Color.white;
    private static readonly Color Border    = new Color(1f, 1f, 1f, 1f);

    private Font _font;
    private Sprite _rounded;
    private Canvas _canvas;
    private GameObject _backdrop;
    private Text _titleText;
    private Text _bodyText;
    private GameObject _imageHolder;
    private Image _image;
    private Button _ctaButton;
    private Text _ctaLabel;

    private string _ctaUrl;
    private Action _onDismiss;
    private Coroutine _imageJob;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        _rounded = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        EnsureEventSystem();
        BuildCanvas();
        BuildBackdrop();
        BuildCard();
        _backdrop.SetActive(false);
    }

    // Populate + show. imageUrl / ctaLabel / ctaUrl are optional ("" or null to
    // omit). onDismiss fires when the user closes or taps the CTA.
    public void Show(string title, string body, string imageUrl, string ctaLabel, string ctaUrl, Action onDismiss = null)
    {
        if (_backdrop == null) return;
        _onDismiss = onDismiss;
        _ctaUrl = ctaUrl;

        _titleText.text = string.IsNullOrEmpty(title) ? "" : title;
        _bodyText.text = string.IsNullOrEmpty(body) ? "" : body;

        bool hasCta = !string.IsNullOrEmpty(ctaLabel) && !string.IsNullOrEmpty(ctaUrl);
        _ctaButton.gameObject.SetActive(hasCta);
        if (hasCta) _ctaLabel.text = ctaLabel;

        // Image: hidden until/unless it loads.
        _imageHolder.SetActive(false);
        if (_imageJob != null) { StopCoroutine(_imageJob); _imageJob = null; }
        if (!string.IsNullOrEmpty(imageUrl)) _imageJob = StartCoroutine(LoadImage(imageUrl));

        _backdrop.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (_backdrop != null) _backdrop.SetActive(false);
        var cb = _onDismiss; _onDismiss = null;
        cb?.Invoke();
    }

    private void OpenCta()
    {
        var url = _ctaUrl;
        // Only follow http/https — never a tel:/intent:/app-scheme smuggled in.
        if (!string.IsNullOrEmpty(url) &&
            Uri.TryCreate(url, UriKind.Absolute, out var u) &&
            (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
        {
            Application.OpenURL(url);
        }
        Hide();
    }

    private IEnumerator LoadImage(string url)
    {
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;
            var tex = DownloadHandlerTexture.GetContent(req);
            if (tex == null) yield break;
            _image.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            _image.preserveAspect = true;
            _imageHolder.SetActive(true);
        }
        _imageJob = null;
    }

    // ---- build ----

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private void BuildCanvas()
    {
        var go = new GameObject("PromoCanvas");
        go.transform.SetParent(transform, false);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 130; // above leaderboard (110)
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
    }

    private void BuildBackdrop()
    {
        _backdrop = CreateRect("Backdrop", _canvas.transform, Dim, null);
        var rt = (RectTransform)_backdrop.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private void BuildCard()
    {
        var card = CreateRect("Card", _backdrop.transform, Sky, _rounded);
        var outline = card.AddComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(4, -4);
        var rt = (RectTransform)card.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900, 1180);
        rt.anchoredPosition = Vector2.zero;

        // Title bar (red plaque).
        var bar = CreateRect("TitleBar", card.transform, Red, _rounded);
        var barRt = (RectTransform)bar.transform;
        barRt.anchorMin = new Vector2(0, 1); barRt.anchorMax = new Vector2(1, 1);
        barRt.pivot = new Vector2(0.5f, 1);
        barRt.sizeDelta = new Vector2(-40, 150);
        barRt.anchoredPosition = new Vector2(0, -20);
        _titleText = CreateText(bar.transform, "Title", "", 56, FontStyle.Bold, TextAnchor.MiddleCenter);
        _titleText.color = White;
        Fill(_titleText.transform, 30, 0);

        // Banner image (optional).
        _imageHolder = CreateRect("ImageHolder", card.transform, new Color(1, 1, 1, 0), null);
        var ihRt = (RectTransform)_imageHolder.transform;
        ihRt.anchorMin = new Vector2(0.5f, 1); ihRt.anchorMax = new Vector2(0.5f, 1);
        ihRt.pivot = new Vector2(0.5f, 1);
        ihRt.sizeDelta = new Vector2(760, 460);
        ihRt.anchoredPosition = new Vector2(0, -200);
        _image = _imageHolder.GetComponent<Image>();
        _image.color = White;

        // Body text.
        _bodyText = CreateText(card.transform, "Body", "", 38, FontStyle.Normal, TextAnchor.UpperCenter);
        _bodyText.color = Ink;
        _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        var bodyRt = (RectTransform)_bodyText.transform;
        bodyRt.anchorMin = new Vector2(0, 0); bodyRt.anchorMax = new Vector2(1, 1);
        bodyRt.offsetMin = new Vector2(60, 320);  // above the buttons
        bodyRt.offsetMax = new Vector2(-60, -690); // below image area

        // CTA button (gold).
        _ctaButton = CreateButton(card.transform, "CTA", "", Gold, 720, 130, Ink, _rounded);
        var ctaRt = (RectTransform)_ctaButton.transform;
        ctaRt.anchorMin = new Vector2(0.5f, 0); ctaRt.anchorMax = new Vector2(0.5f, 0);
        ctaRt.pivot = new Vector2(0.5f, 0);
        ctaRt.anchoredPosition = new Vector2(0, 200);
        _ctaLabel = _ctaButton.GetComponentInChildren<Text>();
        _ctaButton.onClick.AddListener(OpenCta);

        // "Maybe later" dismiss.
        var later = CreateButton(card.transform, "Later", "Maybe later", new Color(1, 1, 1, 0), 720, 90, new Color(0.30f, 0.42f, 0.52f, 1f), null);
        var laterRt = (RectTransform)later.transform;
        laterRt.anchorMin = new Vector2(0.5f, 0); laterRt.anchorMax = new Vector2(0.5f, 0);
        laterRt.pivot = new Vector2(0.5f, 0);
        laterRt.anchoredPosition = new Vector2(0, 80);
        later.onClick.AddListener(Hide);
    }

    // ---- primitives ----

    private GameObject CreateRect(string name, Transform parent, Color bg, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = bg;
        if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; }
        return go;
    }

    private Text CreateText(Transform parent, string name, string content, int fontSize, FontStyle style, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = _font;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Ink;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return text;
    }

    private Button CreateButton(Transform parent, string name, string label, Color bg, float width, float height, Color textColor, Sprite sprite)
    {
        var go = CreateRect(name, parent, bg, sprite);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width, height);
        var button = go.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = bg;
        colors.highlightedColor = Color.Lerp(bg, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(bg, Color.black, 0.15f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        var text = CreateText(go.transform, "Label", label, 38, FontStyle.Bold, TextAnchor.MiddleCenter);
        text.color = textColor;
        Fill(text.transform, 10, 0);
        return button;
    }

    private static void Fill(Transform t, float padX, float padY)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, padY); rt.offsetMax = new Vector2(-padX, -padY);
    }
}
