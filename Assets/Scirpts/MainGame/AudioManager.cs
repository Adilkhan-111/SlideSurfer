using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Music and sound effects. Lives across restarts (the scene reloads, the music keeps playing).
///   music:  loops seamlessly by cross-fading two copies of the track between its loop points;
///           muffled (as if under water) before the run and after a crash, open while riding.
///   sfx:    coins (rising pitch on a streak), jump, landing, crash + game-over jingle,
///           shark warning and splash, UI clicks, and rushing water that speeds up with the slide.
/// Volumes are saved in PlayerPrefs so a settings screen can change them later.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    [Header("Music")]
    [SerializeField] private AudioClip music;
    [Tooltip("Where the loop starts and ends in the track (seconds); set by Tools > SlideSurfer > Setup Audio")]
    [SerializeField] private float loopStart;
    [SerializeField] private float loopEnd;
    [SerializeField] private float crossfade = 1.5f;
    [Tooltip("Low-pass cutoff while not riding (menu, game over): sounds like under water")]
    [SerializeField] private float muffledCutoff = 900f;

    [Header("Effects")]
    [SerializeField] private AudioClip coin;
    [SerializeField] private AudioClip jump;
    [SerializeField] private AudioClip land;
    [SerializeField] private AudioClip crash;
    [SerializeField] private AudioClip gameOver;
    [SerializeField] private AudioClip sharkWarning;
    [SerializeField] private AudioClip sharkSplash;
    [SerializeField] private AudioClip click;
    [SerializeField] private AudioClip water;
    public GameSettings gameSettings;

    [Header("Coin streak")]
    [Tooltip("Coins picked up within this many seconds of each other count as a streak")]
    [SerializeField] private float streakWindow = 0.6f;
    [Tooltip("Each coin in a streak plays a semitone higher, up to this many steps")]
    [SerializeField] private int streakSteps = 8;

    private const string MusicVolumeKey = "volume_music";
    private const string SfxVolumeKey = "volume_sfx";

    private AudioSource[] musicSources;
    private int currentMusic;
    private bool fading;
    private AudioLowPassFilter musicFilter;
    private AudioSource sfx;
    private AudioSource coinSource;   // its own source: its pitch rises with a streak without bending other sounds
    private AudioSource waterSource;
    private float musicVolume = 0.6f;
    private float sfxVolume = 0.7f;
    private float musicDuck = 1f;
    private float targetCutoff;
    private bool riding;
    private int streak;
    private float lastCoinTime = -10f;
    private PlayerAnimator boundPlayer;

    public float MusicVolume
    {
        get => musicVolume;
        set { musicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume); }
    }

    public float SfxVolume
    {
        get => sfxVolume;
        set { sfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume); }
    }

    // on/off switches, separate from the volume so turning sound back on restores the old level
    private const string MusicOnKey = "music_on";
    private const string SfxOnKey = "sfx_on";
    private bool musicOn = true;
    private bool sfxOn = true;

    public bool MusicOn
    {
        get => musicOn;
        set { musicOn = value; PlayerPrefs.SetInt(MusicOnKey, value ? 1 : 0); }
    }

    public bool SfxOn
    {
        get => sfxOn;
        set { sfxOn = value; PlayerPrefs.SetInt(SfxOnKey, value ? 1 : 0); }
    }

    private float MusicLevel => musicOn ? musicVolume : 0f;
    private float SfxLevel => sfxOn ? sfxVolume : 0f;

    private void Awake()
    {
        // one manager for the whole session: the copy in a reloaded scene steps aside
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, musicVolume);
        sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, sfxVolume);
        musicOn = PlayerPrefs.GetInt(MusicOnKey, 1) == 1;
        sfxOn = PlayerPrefs.GetInt(SfxOnKey, 1) == 1;

        var musicGo = new GameObject("Music");
        musicGo.transform.SetParent(transform, false);
        musicSources = new[] { NewSource(musicGo), NewSource(musicGo) };
        musicFilter = musicGo.AddComponent<AudioLowPassFilter>();
        musicFilter.cutoffFrequency = muffledCutoff;
        targetCutoff = muffledCutoff;

        sfx = NewSource(gameObject);
        coinSource = NewSource(gameObject);
        waterSource = NewSource(gameObject);
        waterSource.clip = water;
        waterSource.loop = true;
        waterSource.volume = 0f;
        if (water != null) waterSource.Play();

        if (music != null)
        {
            if (loopEnd <= loopStart) loopEnd = music.length;
            musicSources[0].clip = music;
            musicSources[1].clip = music;
            musicSources[0].time = loopStart;
            musicSources[0].Play();
        }

        EventManager.GameHasStarted += OnRunStarted;
        EventManager.PlayerDied += OnPlayerDied;
        Coin.Removed += OnCoinRemoved;
        SharkMover.Appeared += OnSharkAppeared;
        SharkMover.Leaped += OnSharkLeaped;
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindPlayer();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        EventManager.GameHasStarted -= OnRunStarted;
        EventManager.PlayerDied -= OnPlayerDied;
        Coin.Removed -= OnCoinRemoved;
        SharkMover.Appeared -= OnSharkAppeared;
        SharkMover.Leaped -= OnSharkLeaped;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    private static AudioSource NewSource(GameObject host)
    {
        var s = host.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        return s;
    }

    // ---------------------------------------------------------------- game events

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // a restart: back to the muffled "waiting to start" sound
        riding = false;
        musicDuck = 1f;
        targetCutoff = muffledCutoff;
        BindPlayer();
    }

    private void BindPlayer()
    {
        if (boundPlayer != null)
        {
            boundPlayer.Jumped -= OnJumped;
            boundPlayer.Landed -= OnLanded;
        }
        boundPlayer = FindAnyObjectByType<PlayerAnimator>();
        if (boundPlayer != null)
        {
            boundPlayer.Jumped += OnJumped;
            boundPlayer.Landed += OnLanded;
        }
    }

    private void OnRunStarted()
    {
        riding = true;
        targetCutoff = 22000f;
        musicDuck = 1f;
    }

    private void OnPlayerDied()
    {
        riding = false;
        targetCutoff = muffledCutoff;
        musicDuck = 0.45f;
        Play(crash, 1f);
        if (gameOver != null) Invoke(nameof(PlayGameOver), 0.55f);
    }

    private void PlayGameOver() => Play(gameOver, 0.8f);

    private void OnCoinRemoved(Coin c, bool collected)
    {
        if (!collected) return;
        streak = Time.time - lastCoinTime <= streakWindow ? Mathf.Min(streak + 1, streakSteps) : 0;
        lastCoinTime = Time.time;
        if (coin == null) return;
        coinSource.pitch = Mathf.Pow(2f, streak / 12f);
        coinSource.PlayOneShot(coin, 0.55f * SfxLevel);
    }

    private void OnJumped() => Play(jump, 0.8f);
    private void OnLanded() => Play(land, 0.9f);
    private void OnSharkAppeared() => Play(sharkWarning, 0.9f);
    private void OnSharkLeaped() => Play(sharkSplash, 0.75f);

    /// <summary>For UI buttons.</summary>
    public void PlayClick() => Play(click, 0.7f);

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;
        sfx.PlayOneShot(clip, volume * SfxLevel);
    }

    // ---------------------------------------------------------------- per frame

    private void Update()
    {
        UpdateMusicLoop();

        float k = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
        musicFilter.cutoffFrequency = Mathf.Lerp(musicFilter.cutoffFrequency, targetCutoff, k);
        if (!fading)
        {
            float v = MusicLevel * musicDuck;
            musicSources[currentMusic].volume = Mathf.Lerp(musicSources[currentMusic].volume, v, k);
        }

        // rushing water: louder and higher as the slide speeds up, quiet before the run
        float speed = gameSettings != null ? gameSettings.currentSpeed : 10f;
        float s01 = Mathf.InverseLerp(10f, 40f, speed);
        float waterTarget = (riding ? Mathf.Lerp(0.18f, 0.32f, s01) : 0.08f) * SfxLevel;
        waterSource.volume = Mathf.Lerp(waterSource.volume, waterTarget, k);
        waterSource.pitch = Mathf.Lerp(0.9f, 1.25f, s01);
    }

    // start the other copy a little before the loop end and cross-fade, so there is never a gap or a click
    private void UpdateMusicLoop()
    {
        if (music == null) return;
        AudioSource cur = musicSources[currentMusic];
        if (!fading && cur.isPlaying && cur.time >= loopEnd - crossfade)
        {
            AudioSource next = musicSources[1 - currentMusic];
            next.time = loopStart;
            next.volume = 0f;
            next.Play();
            fading = true;
        }
        if (fading)
        {
            AudioSource next = musicSources[1 - currentMusic];
            float t = Mathf.InverseLerp(loopEnd - crossfade, loopEnd, cur.time);
            float v = MusicLevel * musicDuck;
            // equal-power fade keeps the loudness steady through the join
            cur.volume = v * Mathf.Cos(t * Mathf.PI * 0.5f);
            next.volume = v * Mathf.Sin(t * Mathf.PI * 0.5f);
            if (t >= 1f || !cur.isPlaying)
            {
                cur.Stop();
                currentMusic = 1 - currentMusic;
                fading = false;
            }
        }
    }
}
