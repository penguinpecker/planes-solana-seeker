using UnityEngine;

#if FIREBASE_MESSAGING_INSTALLED
using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Extensions;
using Firebase.Messaging;
#endif

// Cloud push notifications via Firebase Cloud Messaging (FCM).
//
// Why FCM and not a dApp-Store feature: the Solana dApp Store has no native
// notification channel, but the Seeker ships with Google Play Services, so
// standard Android push works for a dApp-Store-installed build. See
// docs/PUSH_NOTIFICATIONS.md for the full setup + the verified rationale.
//
// What this does on Seeker/Android:
//   1. Requests the Android 13+ POST_NOTIFICATIONS runtime permission.
//   2. Initialises Firebase and subscribes this install to broadcast TOPICS
//      ("news", "competitions") — every subscribed device receives a blast
//      with NO per-device token bookkeeping on the server.
//   3. When the user TAPS a notification that carries a "url" (e.g. an X /
//      Twitter post), opens it. Competition pings just bring the player back
//      into the game (and raise OnCompetitionOpened for future routing).
//
// The URL rides in the message's `data` payload under the "url" key (NOT in
// the notification block) so it survives the background/killed tap path and we
// can open it from C#. The sender is the Firebase console or the
// pl-broadcast-news Supabase edge function.
//
// All Firebase calls are gated behind FIREBASE_MESSAGING_INSTALLED so the
// project keeps compiling before the Firebase Unity SDK is imported. Add that
// symbol to Player Settings > Scripting Define Symbols once the SDK is in
// (exactly how SOLANA_SDK_INSTALLED is already set for the Solana SDK). Until
// then this is an inert singleton.
//
// GameManager.EnsureLeaderboardSingletons auto-spawns this alongside the
// other singletons.
public class PushManager : MonoBehaviour
{
    public static PushManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

#if FIREBASE_MESSAGING_INSTALLED
    // Topics this install subscribes to. A server broadcast to any of these
    // reaches every subscribed device. Keep these in sync with whatever the
    // sender targets (Firebase console / pl-broadcast-news).
    private static readonly string[] Topics = { "news", "competitions" };

    private const string PostNotifications = "android.permission.POST_NOTIFICATIONS";

    // Raised when the user taps a notification tagged type=="competition".
    // Nothing subscribes yet; lets the UI later route into a competition
    // screen without touching this class. The payload is the data dictionary.
    public static event Action<IReadOnlyDictionary<string, string>> OnCompetitionOpened;

    // A notification tap may surface off the Unity main thread, so we stash the
    // intent and act on it in Update() where Unity APIs are safe to call.
    private string _pendingUrl;
    private Dictionary<string, string> _pendingCompetition;
    private readonly object _pendingLock = new object();

    private void Start()
    {
        RequestNotificationPermission();

        // Resolve Google Play Services / Firebase deps, then wire handlers.
        // Registering a MessageReceived/TokenReceived handler is what
        // initialises the FCM library, so we do it as early as possible: the
        // notification that COLD-STARTS the app from a tray tap is delivered
        // to MessageReceived shortly after init, and a late subscribe can miss
        // it.
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result != DependencyStatus.Available)
            {
                Debug.LogWarning($"[PushManager] Firebase deps unavailable: {task.Result}");
                return;
            }

            FirebaseMessaging.TokenReceived += OnTokenReceived;
            FirebaseMessaging.MessageReceived += OnMessageReceived;

            foreach (var topic in Topics)
            {
                string t = topic;
                FirebaseMessaging.SubscribeAsync(t).ContinueWithOnMainThread(st =>
                {
                    if (st.IsFaulted) Debug.LogWarning($"[PushManager] subscribe '{t}' failed: {st.Exception}");
                    else Debug.Log($"[PushManager] subscribed to topic '{t}'");
                });
            }
        });
    }

    private void OnDestroy()
    {
        FirebaseMessaging.TokenReceived -= OnTokenReceived;
        FirebaseMessaging.MessageReceived -= OnMessageReceived;
    }

    private void Update()
    {
        string url = null;
        Dictionary<string, string> competition = null;
        lock (_pendingLock)
        {
            if (_pendingUrl != null) { url = _pendingUrl; _pendingUrl = null; }
            if (_pendingCompetition != null) { competition = _pendingCompetition; _pendingCompetition = null; }
        }

        if (!string.IsNullOrEmpty(url))
        {
            // The URL comes from a remote sender and is handed to the OS, so
            // only follow http(s) links — never tel:/market:/intent:/app deep
            // links that a misconfigured or compromised sender could smuggle in.
            if (Uri.TryCreate(url, UriKind.Absolute, out var u) &&
                (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
            {
                Debug.Log($"[PushManager] opening notification url: {url}");
                Application.OpenURL(url);
            }
            else
            {
                Debug.LogWarning($"[PushManager] rejected non-http(s) notification url: {url}");
            }
        }
        if (competition != null)
        {
            // App is already foregrounded by the tap; hand off for routing.
            OnCompetitionOpened?.Invoke(competition);
        }
    }

    private static void RequestNotificationPermission()
    {
        // Android 13+ (the Seeker is Android 14-class): notifications are OFF
        // until the user grants POST_NOTIFICATIONS. Without it, FCM tray
        // notifications are dropped silently. Unity has no predefined constant
        // for this permission, so request it by its raw string.
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(PostNotifications))
        {
            UnityEngine.Android.Permission.RequestUserPermission(PostNotifications);
        }
    }

    private void OnTokenReceived(object sender, TokenReceivedEventArgs e)
    {
        // Not needed for topic broadcasts. Logged here so it can later be
        // persisted (e.g. into pl_players) if per-device targeted sends are
        // ever added. See docs/PUSH_NOTIFICATIONS.md "Targeted sends".
        Debug.Log($"[PushManager] FCM registration token: {e.Token}");
    }

    // Fires both for messages that ARRIVE while the app is foregrounded AND for
    // the message that the user opened by TAPPING the tray notification. We act
    // only on an actual tap (NotificationOpened == true) so a "news" message
    // can never yank a player to a browser mid-run.
    private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
    {
        var msg = e.Message;
        if (msg == null || !msg.NotificationOpened) return;

        var data = msg.Data; // IDictionary<string,string>; null if no data payload
        string url = null;
        if (data != null) data.TryGetValue("url", out url);
        if (string.IsNullOrEmpty(url) && msg.Link != null) url = msg.Link.ToString();

        string type = null;
        if (data != null) data.TryGetValue("type", out type);

        lock (_pendingLock)
        {
            if (!string.IsNullOrEmpty(url))
            {
                _pendingUrl = url; // e.g. https://x.com/<handle>/status/<id>
            }
            else if (type == "competition")
            {
                _pendingCompetition = data != null
                    ? new Dictionary<string, string>(data)
                    : new Dictionary<string, string>();
            }
        }
    }
#endif
}
