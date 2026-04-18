using UnityEngine;

// Plays the airplane engine loop under the whole game. Always started at
// launch; the existing sound toggle in Settings uses AudioListener.pause
// to mute/unmute globally, so it also silences this source when the
// player turns sound off. No separate music toggle is needed.
public class BackgroundMusicManager : MonoBehaviour
{
    public static BackgroundMusicManager Instance { get; private set; }

    private const string ClipResourcePath = "Audio/airplane-engine";

    [Range(0f, 1f)] [SerializeField] private float _volume = 0.35f;

    private AudioSource _source;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        var clip = Resources.Load<AudioClip>(ClipResourcePath);
        if (clip == null)
        {
            Debug.LogError($"[BackgroundMusicManager] Missing AudioClip at Resources/{ClipResourcePath}");
            return;
        }

        _source = gameObject.AddComponent<AudioSource>();
        _source.clip = clip;
        _source.loop = true;
        _source.playOnAwake = false;
        _source.volume = _volume;
        _source.Play();
    }
}
