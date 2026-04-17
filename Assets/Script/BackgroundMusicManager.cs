using UnityEngine;

// Plays the airplane engine loop under the whole game. GameManager
// auto-spawns this alongside the other singletons, so the scene doesn't
// need a scene-placed audio source. Persists the on/off choice to
// PlayerPrefs under "MusicOn" (1/0).
public class BackgroundMusicManager : MonoBehaviour
{
    public static BackgroundMusicManager Instance { get; private set; }

    private const string PrefsKey = "MusicOn";
    private const string ClipResourcePath = "Audio/airplane-engine";

    [Range(0f, 1f)] [SerializeField] private float _volume = 0.35f;

    private AudioSource _source;

    public bool IsOn => _source != null && _source.isPlaying;

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

        if (PlayerPrefs.GetInt(PrefsKey, 1) == 1) _source.Play();
    }

    public void SetMusicOn(bool on)
    {
        if (_source == null) return;
        if (on)
        {
            if (!_source.isPlaying) _source.Play();
        }
        else
        {
            _source.Stop();
        }
        PlayerPrefs.SetInt(PrefsKey, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ToggleMusic()
    {
        SetMusicOn(!IsOn);
    }
}
