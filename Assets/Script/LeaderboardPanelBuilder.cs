using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Runtime leaderboard UI. Transparent dim backdrop with a plain black/white
// table so it reads as an overlay on top of the game, matching the simple
// final-score card rather than a heavy themed panel.
public class LeaderboardPanelBuilder : MonoBehaviour
{
    public static LeaderboardPanelBuilder Instance { get; private set; }

    private static readonly Color Dim = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color CardBg = new Color(1f, 1f, 1f, 0.97f);
    private static readonly Color Border = new Color(0f, 0f, 0f, 1f);
    private static readonly Color HeaderBg = new Color(0f, 0f, 0f, 1f);
    private static readonly Color HeaderText = new Color(1f, 1f, 1f, 1f);
    private static readonly Color RowText = new Color(0f, 0f, 0f, 1f);
    private static readonly Color RowMuted = new Color(0.30f, 0.30f, 0.30f, 1f);
    private static readonly Color RowAltBg = new Color(0.94f, 0.94f, 0.94f, 1f);
    private static readonly Color Danger = new Color(0.85f, 0.20f, 0.25f, 1f);

    private Font _font;
    private Canvas _canvas;
    private GameObject _backdrop;
    private GameObject _card;
    private RectTransform _listRoot;
    private Text _pageLabel;
    private Button _prevButton;
    private Button _nextButton;
    private Text _statusText;
    private int _currentPage;

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
        BuildBackdrop();
        BuildCard();
        _backdrop.SetActive(false);
    }

    public void Show()
    {
        if (_backdrop == null) return;
        _backdrop.SetActive(true);
        _currentPage = 0;
        FetchCurrentPage();
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
        var go = new GameObject("LeaderboardCanvas");
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 110;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
    }

    private void BuildBackdrop()
    {
        _backdrop = CreateRect("Backdrop", _canvas.transform, Dim);
        var rt = (RectTransform)_backdrop.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private void BuildCard()
    {
        _card = CreateRect("Card", _backdrop.transform, CardBg);
        var outline = _card.AddComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(3, -3);
        var rt = (RectTransform)_card.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(960, 1580);
        rt.anchoredPosition = Vector2.zero;

        BuildHeaderBar();
        BuildColumnHeader();
        BuildListArea();
        BuildFooter();
    }

    private void BuildHeaderBar()
    {
        var header = CreateRect("TitleBar", _card.transform, HeaderBg);
        var rt = (RectTransform)header.transform;
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, 120);
        rt.anchoredPosition = Vector2.zero;

        var title = CreateText(header.transform, "Title", "LEADERBOARD", 60, FontStyle.Bold, TextAnchor.MiddleCenter);
        title.color = HeaderText;
        var titleRt = (RectTransform)title.transform;
        titleRt.anchorMin = Vector2.zero; titleRt.anchorMax = Vector2.one;
        titleRt.offsetMin = new Vector2(20, 0); titleRt.offsetMax = new Vector2(-130, 0);

        var close = CreateButton(header.transform, "Close", "X", Danger, 90, 90, Color.white);
        var closeRt = (RectTransform)close.transform;
        closeRt.anchorMin = new Vector2(1, 0.5f); closeRt.anchorMax = new Vector2(1, 0.5f);
        closeRt.pivot = new Vector2(1, 0.5f);
        closeRt.anchoredPosition = new Vector2(-15, 0);
        close.onClick.AddListener(Hide);
    }

    private void BuildColumnHeader()
    {
        var row = CreateRect("ColumnHeader", _card.transform, CardBg);
        AddBottomBorder(row, Border);
        var rt = (RectTransform)row.transform;
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, 60);
        rt.anchoredPosition = new Vector2(0, -120);

        AddColumnText(row.transform, "#",       0.00f, 0.15f, TextAnchor.MiddleCenter, RowText);
        AddColumnText(row.transform, "WALLET",  0.15f, 0.72f, TextAnchor.MiddleLeft,   RowText);
        AddColumnText(row.transform, "SCORE",   0.72f, 1.00f, TextAnchor.MiddleRight,  RowText);
    }

    private void AddColumnText(Transform parent, string label, float xMin, float xMax, TextAnchor anchor, Color color)
    {
        var t = CreateText(parent, "Col_" + label, label, 30, FontStyle.Bold, anchor);
        t.color = color;
        var rt = (RectTransform)t.transform;
        rt.anchorMin = new Vector2(xMin, 0); rt.anchorMax = new Vector2(xMax, 1);
        rt.offsetMin = new Vector2(25, 0); rt.offsetMax = new Vector2(-25, 0);
    }

    private void BuildListArea()
    {
        var list = CreateRect("List", _card.transform, CardBg);
        var rt = (RectTransform)list.transform;
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = new Vector2(0, 180);
        rt.offsetMax = new Vector2(0, -180);

        var layout = list.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 0;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        _listRoot = (RectTransform)list.transform;
    }

    private void BuildFooter()
    {
        var footer = CreateRect("Footer", _card.transform, CardBg);
        AddTopBorder(footer, Border);
        var rt = (RectTransform)footer.transform;
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.sizeDelta = new Vector2(0, 170);

        _prevButton = CreateButton(footer.transform, "Prev", "< PREV", HeaderBg, 230, 90, HeaderText);
        var pRt = (RectTransform)_prevButton.transform;
        pRt.anchorMin = new Vector2(0, 0.5f); pRt.anchorMax = new Vector2(0, 0.5f);
        pRt.pivot = new Vector2(0, 0.5f);
        pRt.anchoredPosition = new Vector2(25, 10);
        _prevButton.onClick.AddListener(() => { if (_currentPage > 0) { _currentPage--; FetchCurrentPage(); } });

        _pageLabel = CreateText(footer.transform, "PageLabel", "Page 1", 32, FontStyle.Bold, TextAnchor.MiddleCenter);
        _pageLabel.color = RowText;
        var plRt = (RectTransform)_pageLabel.transform;
        plRt.anchorMin = new Vector2(0.25f, 0); plRt.anchorMax = new Vector2(0.75f, 1);
        plRt.offsetMin = Vector2.zero; plRt.offsetMax = Vector2.zero;

        _nextButton = CreateButton(footer.transform, "Next", "NEXT >", HeaderBg, 230, 90, HeaderText);
        var nRt = (RectTransform)_nextButton.transform;
        nRt.anchorMin = new Vector2(1, 0.5f); nRt.anchorMax = new Vector2(1, 0.5f);
        nRt.pivot = new Vector2(1, 0.5f);
        nRt.anchoredPosition = new Vector2(-25, 10);
        _nextButton.onClick.AddListener(() => { _currentPage++; FetchCurrentPage(); });

        _statusText = CreateText(footer.transform, "Status", "", 24, FontStyle.Normal, TextAnchor.MiddleCenter);
        _statusText.color = RowMuted;
        var sRt = (RectTransform)_statusText.transform;
        sRt.anchorMin = new Vector2(0, 0); sRt.anchorMax = new Vector2(1, 0);
        sRt.pivot = new Vector2(0.5f, 0);
        sRt.sizeDelta = new Vector2(0, 34);
        sRt.anchoredPosition = new Vector2(0, 6);
    }

    private void FetchCurrentPage()
    {
        if (LeaderboardManager.Instance == null)
        {
            SetStatus("Leaderboard manager missing.", true);
            return;
        }
        ClearRows();
        SetStatus("Loading…", false);
        _pageLabel.text = "Page " + (_currentPage + 1);

        LeaderboardManager.Instance.FetchPage(_currentPage, (entries, err) =>
        {
            if (!string.IsNullOrEmpty(err))
            {
                SetStatus("Error: " + err, true);
                return;
            }
            RenderEntries(entries);
        });
    }

    private void RenderEntries(SupabaseLeaderboardClient.LeaderboardEntry[] entries)
    {
        ClearRows();
        if (entries == null || entries.Length == 0)
        {
            SetStatus(_currentPage == 0 ? "No scores yet." : "End.", false);
            _nextButton.interactable = false;
            _prevButton.interactable = _currentPage > 0;
            return;
        }

        int rankStart = _currentPage * LeaderboardManager.Instance.PageSize;
        for (int i = 0; i < entries.Length; i++)
        {
            BuildRow(rankStart + i + 1, entries[i], (rankStart + i) % 2 == 0 ? CardBg : RowAltBg);
        }

        SetStatus("", false);
        _nextButton.interactable = entries.Length == LeaderboardManager.Instance.PageSize;
        _prevButton.interactable = _currentPage > 0;
    }

    private void BuildRow(int rank, SupabaseLeaderboardClient.LeaderboardEntry entry, Color bg)
    {
        var row = CreateRect("Row_" + rank, _listRoot, bg);
        AddBottomBorder(row, Border);
        var rt = (RectTransform)row.transform;
        rt.sizeDelta = new Vector2(0, 58);

        var rankText = CreateText(row.transform, "Rank", rank.ToString(), 28, FontStyle.Bold, TextAnchor.MiddleCenter);
        rankText.color = RowText;
        var rRt = (RectTransform)rankText.transform;
        rRt.anchorMin = new Vector2(0, 0); rRt.anchorMax = new Vector2(0.15f, 1);
        rRt.offsetMin = Vector2.zero; rRt.offsetMax = Vector2.zero;

        var walletText = CreateText(row.transform, "Wallet", ShortAddress(entry.pl_wallet), 26, FontStyle.Normal, TextAnchor.MiddleLeft);
        walletText.color = RowText;
        var wRt = (RectTransform)walletText.transform;
        wRt.anchorMin = new Vector2(0.15f, 0); wRt.anchorMax = new Vector2(0.72f, 1);
        wRt.offsetMin = new Vector2(18, 0); wRt.offsetMax = Vector2.zero;

        var scoreText = CreateText(row.transform, "Score", entry.pl_score.ToString(), 30, FontStyle.Bold, TextAnchor.MiddleRight);
        scoreText.color = RowText;
        var sRt = (RectTransform)scoreText.transform;
        sRt.anchorMin = new Vector2(0.72f, 0); sRt.anchorMax = new Vector2(1, 1);
        sRt.offsetMin = Vector2.zero; sRt.offsetMax = new Vector2(-25, 0);
    }

    private void ClearRows()
    {
        if (_listRoot == null) return;
        for (int i = _listRoot.childCount - 1; i >= 0; i--) Destroy(_listRoot.GetChild(i).gameObject);
    }

    private void SetStatus(string message, bool error)
    {
        if (_statusText == null) return;
        _statusText.text = message;
        _statusText.color = error ? Danger : RowMuted;
    }

    private static string ShortAddress(string address)
    {
        if (string.IsNullOrEmpty(address)) return "";
        return address.Length <= 12 ? address : $"{address.Substring(0, 4)}…{address.Substring(address.Length - 4)}";
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
        text.color = RowText;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return text;
    }

    private Button CreateButton(Transform parent, string name, string label, Color bg, float width, float height, Color textColor)
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

        var text = CreateText(go.transform, "Label", label, 30, FontStyle.Bold, TextAnchor.MiddleCenter);
        text.color = textColor;
        var textRt = (RectTransform)text.transform;
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero; textRt.offsetMax = Vector2.zero;
        return button;
    }

    private void AddBottomBorder(GameObject parent, Color color)
    {
        var border = CreateRect("BottomBorder", parent.transform, color);
        var rt = (RectTransform)border.transform;
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.sizeDelta = new Vector2(0, 2);
        rt.anchoredPosition = Vector2.zero;
    }

    private void AddTopBorder(GameObject parent, Color color)
    {
        var border = CreateRect("TopBorder", parent.transform, color);
        var rt = (RectTransform)border.transform;
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, 2);
        rt.anchoredPosition = Vector2.zero;
    }
}
