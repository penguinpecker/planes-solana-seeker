using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Runtime leaderboard UI. Built programmatically so the scene doesn't need
// a second panel hand-laid out, but themed with the same navy + star palette
// the rest of the game uses so it still feels in-world.
public class LeaderboardPanelBuilder : MonoBehaviour
{
    public static LeaderboardPanelBuilder Instance { get; private set; }

    private static readonly Color PanelBg = new Color(0.06f, 0.09f, 0.18f, 0.97f);
    private static readonly Color RowBg = new Color(0.12f, 0.16f, 0.28f, 1f);
    private static readonly Color RowBgAlt = new Color(0.15f, 0.20f, 0.34f, 1f);
    private static readonly Color Accent = new Color(1f, 0.82f, 0.25f, 1f); // star gold
    private static readonly Color AccentAlt = new Color(0.40f, 0.62f, 1f, 1f); // sky blue
    private static readonly Color TextPrimary = new Color(0.98f, 0.98f, 1f, 1f);
    private static readonly Color TextMuted = new Color(0.70f, 0.75f, 0.85f, 1f);
    private static readonly Color Danger = new Color(0.88f, 0.30f, 0.35f, 1f);

    private Font _font;
    private Canvas _canvas;
    private GameObject _panel;
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
        BuildPanel();
        _panel.SetActive(false);
    }

    public void Show()
    {
        if (_panel == null) return;
        _panel.SetActive(true);
        _currentPage = 0;
        FetchCurrentPage();
    }

    public void Hide()
    {
        if (_panel != null) _panel.SetActive(false);
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

    private void BuildPanel()
    {
        _panel = CreateRect("LeaderboardPanel", _canvas.transform, PanelBg);
        var rt = (RectTransform)_panel.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1000, 1650);
        rt.anchoredPosition = Vector2.zero;

        BuildHeader();
        BuildListArea();
        BuildFooter();
    }

    private void BuildHeader()
    {
        var header = CreateRect("Header", _panel.transform, new Color(0, 0, 0, 0));
        var rt = (RectTransform)header.transform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, 130);
        rt.anchoredPosition = new Vector2(0, 0);

        var title = CreateText(header.transform, "Title", "LEADERBOARD", 64, FontStyle.Bold, TextAnchor.MiddleLeft);
        title.color = Accent;
        var titleRt = (RectTransform)title.transform;
        titleRt.anchorMin = new Vector2(0, 0);
        titleRt.anchorMax = new Vector2(1, 1);
        titleRt.offsetMin = new Vector2(40, 0);
        titleRt.offsetMax = new Vector2(-140, 0);

        var close = CreateButton(header.transform, "Close", "X", Danger, 90, 90);
        var closeRt = (RectTransform)close.transform;
        closeRt.anchorMin = new Vector2(1, 0.5f);
        closeRt.anchorMax = new Vector2(1, 0.5f);
        closeRt.pivot = new Vector2(1, 0.5f);
        closeRt.anchoredPosition = new Vector2(-25, 0);
        close.onClick.AddListener(Hide);

        var columns = CreateRect("Columns", _panel.transform, new Color(0, 0, 0, 0));
        var colRt = (RectTransform)columns.transform;
        colRt.anchorMin = new Vector2(0, 1);
        colRt.anchorMax = new Vector2(1, 1);
        colRt.pivot = new Vector2(0.5f, 1);
        colRt.sizeDelta = new Vector2(0, 60);
        colRt.anchoredPosition = new Vector2(0, -140);
        AddColumnText(columns.transform, "#",       0.00f, 0.12f, TextAnchor.MiddleCenter);
        AddColumnText(columns.transform, "WALLET",  0.12f, 0.70f, TextAnchor.MiddleLeft);
        AddColumnText(columns.transform, "SCORE",   0.70f, 1.00f, TextAnchor.MiddleRight);
    }

    private void AddColumnText(Transform parent, string label, float xMin, float xMax, TextAnchor anchor)
    {
        var t = CreateText(parent, "Col_" + label, label, 32, FontStyle.Bold, anchor);
        t.color = TextMuted;
        var rt = (RectTransform)t.transform;
        rt.anchorMin = new Vector2(xMin, 0);
        rt.anchorMax = new Vector2(xMax, 1);
        rt.offsetMin = new Vector2(30, 0);
        rt.offsetMax = new Vector2(-30, 0);
    }

    private void BuildListArea()
    {
        var list = CreateRect("List", _panel.transform, new Color(0, 0, 0, 0));
        var rt = (RectTransform)list.transform;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = new Vector2(25, 180);
        rt.offsetMax = new Vector2(-25, -220);

        var layout = list.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 6;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        _listRoot = (RectTransform)list.transform;
    }

    private void BuildFooter()
    {
        var footer = CreateRect("Footer", _panel.transform, new Color(0, 0, 0, 0));
        var rt = (RectTransform)footer.transform;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.sizeDelta = new Vector2(0, 180);
        rt.anchoredPosition = new Vector2(0, 20);

        _prevButton = CreateButton(footer.transform, "Prev", "< PREV", AccentAlt, 260, 100);
        var pRt = (RectTransform)_prevButton.transform;
        pRt.anchorMin = new Vector2(0, 0.5f);
        pRt.anchorMax = new Vector2(0, 0.5f);
        pRt.pivot = new Vector2(0, 0.5f);
        pRt.anchoredPosition = new Vector2(30, 0);
        _prevButton.onClick.AddListener(() => { if (_currentPage > 0) { _currentPage--; FetchCurrentPage(); } });

        _pageLabel = CreateText(footer.transform, "PageLabel", "Page 1", 36, FontStyle.Bold, TextAnchor.MiddleCenter);
        var plRt = (RectTransform)_pageLabel.transform;
        plRt.anchorMin = new Vector2(0.25f, 0);
        plRt.anchorMax = new Vector2(0.75f, 1);
        plRt.offsetMin = Vector2.zero;
        plRt.offsetMax = Vector2.zero;

        _nextButton = CreateButton(footer.transform, "Next", "NEXT >", AccentAlt, 260, 100);
        var nRt = (RectTransform)_nextButton.transform;
        nRt.anchorMin = new Vector2(1, 0.5f);
        nRt.anchorMax = new Vector2(1, 0.5f);
        nRt.pivot = new Vector2(1, 0.5f);
        nRt.anchoredPosition = new Vector2(-30, 0);
        _nextButton.onClick.AddListener(() => { _currentPage++; FetchCurrentPage(); });

        _statusText = CreateText(footer.transform, "Status", "", 26, FontStyle.Normal, TextAnchor.MiddleCenter);
        _statusText.color = TextMuted;
        var sRt = (RectTransform)_statusText.transform;
        sRt.anchorMin = new Vector2(0, 0);
        sRt.anchorMax = new Vector2(1, 0);
        sRt.pivot = new Vector2(0.5f, 0);
        sRt.sizeDelta = new Vector2(0, 40);
        sRt.anchoredPosition = new Vector2(0, 10);
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
            SetStatus(_currentPage == 0 ? "No scores yet — be the first!" : "End of the board.", false);
            _nextButton.interactable = false;
            _prevButton.interactable = _currentPage > 0;
            return;
        }

        int rankStart = _currentPage * LeaderboardManager.Instance.PageSize;
        for (int i = 0; i < entries.Length; i++)
        {
            BuildRow(rankStart + i + 1, entries[i], (rankStart + i) % 2 == 0 ? RowBg : RowBgAlt);
        }

        SetStatus("", false);
        _nextButton.interactable = entries.Length == LeaderboardManager.Instance.PageSize;
        _prevButton.interactable = _currentPage > 0;
    }

    private void BuildRow(int rank, SupabaseLeaderboardClient.LeaderboardEntry entry, Color bg)
    {
        var row = CreateRect("Row_" + rank, _listRoot, bg);
        var rt = (RectTransform)row.transform;
        rt.sizeDelta = new Vector2(0, 60);

        var rankText = CreateText(row.transform, "Rank", rank.ToString(), 30, FontStyle.Bold, TextAnchor.MiddleCenter);
        rankText.color = rank <= 3 ? Accent : TextPrimary;
        var rRt = (RectTransform)rankText.transform;
        rRt.anchorMin = new Vector2(0, 0); rRt.anchorMax = new Vector2(0.12f, 1);
        rRt.offsetMin = Vector2.zero; rRt.offsetMax = Vector2.zero;

        var walletText = CreateText(row.transform, "Wallet", ShortAddress(entry.pl_wallet), 28, FontStyle.Normal, TextAnchor.MiddleLeft);
        var wRt = (RectTransform)walletText.transform;
        wRt.anchorMin = new Vector2(0.12f, 0); wRt.anchorMax = new Vector2(0.70f, 1);
        wRt.offsetMin = new Vector2(20, 0); wRt.offsetMax = Vector2.zero;

        var scoreText = CreateText(row.transform, "Score", entry.pl_score.ToString(), 32, FontStyle.Bold, TextAnchor.MiddleRight);
        scoreText.color = Accent;
        var sRt = (RectTransform)scoreText.transform;
        sRt.anchorMin = new Vector2(0.70f, 0); sRt.anchorMax = new Vector2(1, 1);
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
        _statusText.color = error ? Danger : TextMuted;
    }

    private static string ShortAddress(string address)
    {
        if (string.IsNullOrEmpty(address)) return "";
        return address.Length <= 12 ? address : $"{address.Substring(0, 4)}…{address.Substring(address.Length - 4)}";
    }

    // --- UI primitives (mirrors ShopUIBuilder so the two feel like siblings) ---

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
        if (width > 0) rt.sizeDelta = new Vector2(width, height);
        else rt.sizeDelta = new Vector2(0, height);

        var button = go.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = bg;
        colors.highlightedColor = Color.Lerp(bg, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(bg, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(bg.r, bg.g, bg.b, 0.4f);
        button.colors = colors;

        var text = CreateText(go.transform, "Label", label, 32, FontStyle.Bold, TextAnchor.MiddleCenter);
        var textRt = (RectTransform)text.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        return button;
    }
}
