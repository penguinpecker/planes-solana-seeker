using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Game-over popup that lets the player pay a small SOL fee to post their
// score to the on-chain-backed leaderboard. Built programmatically so the
// game-over panel in the scene stays untouched.
public class LeaderboardSubmitPopup : MonoBehaviour
{
    public static LeaderboardSubmitPopup Instance { get; private set; }

    private static readonly Color DialogBg = new Color(0.06f, 0.09f, 0.18f, 0.97f);
    private static readonly Color Dim = new Color(0f, 0f, 0f, 0.70f);
    private static readonly Color AccentBuy = new Color(0.25f, 0.70f, 0.45f, 1f);
    private static readonly Color AccentView = new Color(0.40f, 0.62f, 1f, 1f);
    private static readonly Color Danger = new Color(0.88f, 0.30f, 0.35f, 1f);
    private static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);
    private static readonly Color TextPrimary = new Color(0.98f, 0.98f, 1f, 1f);
    private static readonly Color TextMuted = new Color(0.70f, 0.75f, 0.85f, 1f);

    private Font _font;
    private Canvas _canvas;
    private GameObject _backdrop;
    private GameObject _dialog;
    private Text _scoreText;
    private Text _statusText;
    private Button _submitButton;
    private Button _viewLeaderboardButton;
    private Button _closeButton;
    private int _pendingScore;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        EnsureEventSystem();
        BuildCanvas();
        BuildBackdropAndDialog();
        _backdrop.SetActive(false);

        if (LeaderboardManager.Instance != null)
            LeaderboardManager.Instance.OnSubmitFinished += HandleSubmitFinished;
    }

    private void OnDestroy()
    {
        if (LeaderboardManager.Instance != null)
            LeaderboardManager.Instance.OnSubmitFinished -= HandleSubmitFinished;
    }

    public void Show(int score)
    {
        _pendingScore = score;
        if (_backdrop == null) return;
        _scoreText.text = score.ToString();
        _submitButton.interactable = true;
        _viewLeaderboardButton.interactable = true;
        _statusText.text = "";
        _backdrop.SetActive(true);
    }

    public void Hide()
    {
        if (_backdrop != null) _backdrop.SetActive(false);
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private void BuildCanvas()
    {
        var go = new GameObject("LeaderboardSubmitCanvas");
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 120;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
    }

    private void BuildBackdropAndDialog()
    {
        _backdrop = CreateRect("Backdrop", _canvas.transform, Dim);
        var bdRt = (RectTransform)_backdrop.transform;
        bdRt.anchorMin = Vector2.zero;
        bdRt.anchorMax = Vector2.one;
        bdRt.offsetMin = Vector2.zero;
        bdRt.offsetMax = Vector2.zero;

        _dialog = CreateRect("Dialog", _backdrop.transform, DialogBg);
        var dRt = (RectTransform)_dialog.transform;
        dRt.anchorMin = new Vector2(0.5f, 0.5f);
        dRt.anchorMax = new Vector2(0.5f, 0.5f);
        dRt.pivot = new Vector2(0.5f, 0.5f);
        dRt.sizeDelta = new Vector2(880, 1050);

        var title = CreateText(_dialog.transform, "Title", "YOUR RUN", 56, FontStyle.Bold, TextAnchor.UpperCenter);
        title.color = Gold;
        var tRt = (RectTransform)title.transform;
        tRt.anchorMin = new Vector2(0, 1); tRt.anchorMax = new Vector2(1, 1);
        tRt.pivot = new Vector2(0.5f, 1);
        tRt.sizeDelta = new Vector2(0, 90);
        tRt.anchoredPosition = new Vector2(0, -40);

        var finalLabel = CreateText(_dialog.transform, "FinalLabel", "FINAL SCORE", 34, FontStyle.Normal, TextAnchor.UpperCenter);
        finalLabel.color = TextMuted;
        var flRt = (RectTransform)finalLabel.transform;
        flRt.anchorMin = new Vector2(0, 1); flRt.anchorMax = new Vector2(1, 1);
        flRt.pivot = new Vector2(0.5f, 1);
        flRt.sizeDelta = new Vector2(0, 50);
        flRt.anchoredPosition = new Vector2(0, -160);

        _scoreText = CreateText(_dialog.transform, "Score", "0", 180, FontStyle.Bold, TextAnchor.MiddleCenter);
        _scoreText.color = Gold;
        var sRt = (RectTransform)_scoreText.transform;
        sRt.anchorMin = new Vector2(0, 1); sRt.anchorMax = new Vector2(1, 1);
        sRt.pivot = new Vector2(0.5f, 1);
        sRt.sizeDelta = new Vector2(0, 240);
        sRt.anchoredPosition = new Vector2(0, -210);

        var blurb = CreateText(_dialog.transform, "Blurb",
            $"Submit to the on-chain leaderboard — \u25CE { (LeaderboardManager.Instance != null ? LeaderboardManager.Instance.SubmissionPriceSOL : 0.1f):0.###} SOL per entry.",
            28, FontStyle.Normal, TextAnchor.MiddleCenter);
        blurb.color = TextMuted;
        var bRt = (RectTransform)blurb.transform;
        bRt.anchorMin = new Vector2(0, 1); bRt.anchorMax = new Vector2(1, 1);
        bRt.pivot = new Vector2(0.5f, 1);
        bRt.sizeDelta = new Vector2(0, 80);
        bRt.anchoredPosition = new Vector2(0, -470);

        _submitButton = CreateButton(_dialog.transform, "Submit", "SUBMIT SCORE", AccentBuy, 720, 110);
        var subRt = (RectTransform)_submitButton.transform;
        subRt.anchorMin = new Vector2(0.5f, 0); subRt.anchorMax = new Vector2(0.5f, 0);
        subRt.pivot = new Vector2(0.5f, 0);
        subRt.anchoredPosition = new Vector2(0, 320);
        _submitButton.onClick.AddListener(HandleSubmitClick);

        _viewLeaderboardButton = CreateButton(_dialog.transform, "View", "VIEW LEADERBOARD", AccentView, 720, 100);
        var viewRt = (RectTransform)_viewLeaderboardButton.transform;
        viewRt.anchorMin = new Vector2(0.5f, 0); viewRt.anchorMax = new Vector2(0.5f, 0);
        viewRt.pivot = new Vector2(0.5f, 0);
        viewRt.anchoredPosition = new Vector2(0, 200);
        _viewLeaderboardButton.onClick.AddListener(() =>
        {
            if (LeaderboardPanelBuilder.Instance != null) LeaderboardPanelBuilder.Instance.Show();
        });

        _closeButton = CreateButton(_dialog.transform, "Close", "DISMISS", Danger, 720, 100);
        var closeRt = (RectTransform)_closeButton.transform;
        closeRt.anchorMin = new Vector2(0.5f, 0); closeRt.anchorMax = new Vector2(0.5f, 0);
        closeRt.pivot = new Vector2(0.5f, 0);
        closeRt.anchoredPosition = new Vector2(0, 80);
        _closeButton.onClick.AddListener(Hide);

        _statusText = CreateText(_dialog.transform, "Status", "", 26, FontStyle.Normal, TextAnchor.MiddleCenter);
        _statusText.color = TextMuted;
        var statusRt = (RectTransform)_statusText.transform;
        statusRt.anchorMin = new Vector2(0, 0); statusRt.anchorMax = new Vector2(1, 0);
        statusRt.pivot = new Vector2(0.5f, 0);
        statusRt.sizeDelta = new Vector2(0, 60);
        statusRt.anchoredPosition = new Vector2(0, 20);
    }

    private void HandleSubmitClick()
    {
        if (LeaderboardManager.Instance == null)
        {
            SetStatus("Leaderboard manager missing.", true);
            return;
        }
        _submitButton.interactable = false;
        _viewLeaderboardButton.interactable = false;
        SetStatus("Requesting wallet signature…", false);
        LeaderboardManager.Instance.SubmitCurrentScore(_pendingScore);
    }

    private void HandleSubmitFinished(bool ok, string resultOrError)
    {
        if (ok)
        {
            SetStatus("Submitted! Tx " + ShortSignature(resultOrError), false);
            _submitButton.interactable = false;
            _viewLeaderboardButton.interactable = true;
        }
        else
        {
            SetStatus(resultOrError, true);
            _submitButton.interactable = true;
            _viewLeaderboardButton.interactable = true;
        }
    }

    private void SetStatus(string msg, bool error)
    {
        if (_statusText == null) return;
        _statusText.text = msg;
        _statusText.color = error ? Danger : TextMuted;
    }

    private static string ShortSignature(string sig)
    {
        if (string.IsNullOrEmpty(sig)) return "";
        return sig.Length <= 14 ? sig : sig.Substring(0, 6) + "…" + sig.Substring(sig.Length - 6);
    }

    // --- primitives ---

    private GameObject CreateRect(string name, Transform parent, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bg;
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
        text.color = TextPrimary;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return text;
    }

    private Button CreateButton(Transform parent, string name, string label, Color bg, float width, float height)
    {
        var go = CreateRect(name, parent, bg);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width, height);

        var button = go.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = bg;
        colors.highlightedColor = Color.Lerp(bg, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(bg, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(bg.r, bg.g, bg.b, 0.4f);
        button.colors = colors;

        var text = CreateText(go.transform, "Label", label, 34, FontStyle.Bold, TextAnchor.MiddleCenter);
        var textRt = (RectTransform)text.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        return button;
    }
}
