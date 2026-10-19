using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Audio management system (Singleton Pattern)
/// Controls all game sounds and music.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [System.Serializable]
    public class SoundGroup
    {
        public string soundName;
        [Tooltip("All variants belonging to this sound group")]
        public List<AudioClip> clips = new List<AudioClip>();
        public float volume = 1f;
        [Range(0f, 1f)] public float pitchVariation = 0.1f; // Amount of pitch variation
    }

    [System.Serializable]
    public class BackgroundMusicClip
    {
        public AudioClip clip;
        [Tooltip("Wait time (seconds) before moving to the next track after the music ends")]
        public float delayBetweenTracks = 0f;
    }

    [SerializeField] private List<SoundGroup> soundEffects = new List<SoundGroup>();
    [SerializeField] private List<BackgroundMusicClip> backgroundMusicList = new List<BackgroundMusicClip>();
    [SerializeField] private float backgroundMusicVolume = 0.5f;
    [SerializeField] private bool randomizeMusic = false; // Play tracks in a random order

    private AudioSource sfxAudioSource;
    private AudioSource musicAudioSource;
    private bool isMusicPlaying = false;
    private int currentMusicIndex = 0;
    private Coroutine musicPlaybackCoroutine;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Create AudioSource components
        CreateAudioSources();
    }

    private void CreateAudioSources()
    {
        // AudioSource for sound effects
        sfxAudioSource = gameObject.AddComponent<AudioSource>();
        sfxAudioSource.playOnAwake = false;
        sfxAudioSource.volume = 1f;

        // AudioSource for music
        musicAudioSource = gameObject.AddComponent<AudioSource>();
        musicAudioSource.playOnAwake = false;
        musicAudioSource.loop = false; // Disable looping; playback is managed here
        musicAudioSource.volume = backgroundMusicVolume;
    }

    /// <summary>
    /// Play a sound effect (selects a random variant)
    /// </summary>
    public void PlaySound(string soundName)
    {
        SoundGroup soundGroup = soundEffects.Find(s => s.soundName == soundName);

        if (soundGroup == null || soundGroup.clips.Count == 0)
        {
            Debug.LogWarning($"[AudioManager] Sound not found or has no variants: {soundName}");
            return;
        }

        // Select a random variant
        AudioClip selectedClip = soundGroup.clips[Random.Range(0, soundGroup.clips.Count)];

        if (selectedClip == null)
        {
            Debug.LogWarning($"[AudioManager] Null clip in the {soundName} group!");
            return;
        }

        // Add slight pitch variation for a more natural sound
        float randomPitch = 1f + Random.Range(-soundGroup.pitchVariation, soundGroup.pitchVariation);
        sfxAudioSource.pitch = randomPitch;
        sfxAudioSource.PlayOneShot(selectedClip, soundGroup.volume);
    }

    /// <summary>
    /// Start background music
    /// </summary>
    public void PlayBackgroundMusic()
    {
        if (backgroundMusicList.Count == 0)
        {
            Debug.LogWarning("[AudioManager] Background music list is empty!");
            return;
        }

        // Do not restart the coroutine if music is already playing
        if (isMusicPlaying) return;

        // Start the music playback coroutine
        isMusicPlaying = true;
        currentMusicIndex = 0;
        
        if (musicPlaybackCoroutine != null)
            StopCoroutine(musicPlaybackCoroutine);
        
        musicPlaybackCoroutine = StartCoroutine(PlayMusicSequence());
    }

    /// <summary>
    /// Play music tracks in sequence
    /// </summary>
    private IEnumerator PlayMusicSequence()
    {
        while (isMusicPlaying)
        {
            // Determine the music index
            int musicIndex = randomizeMusic ? Random.Range(0, backgroundMusicList.Count) : currentMusicIndex;
            
            if (musicIndex >= backgroundMusicList.Count)
            {
                musicIndex = 0; // Return to the beginning
                currentMusicIndex = 0;
            }

            BackgroundMusicClip musicClip = backgroundMusicList[musicIndex];

            if (musicClip.clip == null)
            {
                Debug.LogWarning($"[AudioManager] Track {musicIndex} is null!");
                currentMusicIndex++;
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            // Play the music
            musicAudioSource.clip = musicClip.clip;
            musicAudioSource.Play();

            // Wait for the music to finish
            float musicDuration = musicClip.clip.length;
            yield return new WaitForSeconds(musicDuration);

            // Wait for the configured delay
            if (musicClip.delayBetweenTracks > 0)
            {
                yield return new WaitForSeconds(musicClip.delayBetweenTracks);
            }

            // Move to the next track
            currentMusicIndex++;
        }
    }

    /// <summary>
    /// Stop background music
    /// </summary>
    public void StopBackgroundMusic()
    {
        isMusicPlaying = false;
        musicAudioSource.Stop();
        
        if (musicPlaybackCoroutine != null)
        {
            StopCoroutine(musicPlaybackCoroutine);
            musicPlaybackCoroutine = null;
        }
    }

    /// <summary>
    /// Play a specific track by index
    /// </summary>
    public void PlayMusicByIndex(int index)
    {
        if (index < 0 || index >= backgroundMusicList.Count)
        {
            Debug.LogWarning($"[AudioManager] Invalid music index: {index}");
            return;
        }

        StopBackgroundMusic();
        currentMusicIndex = index;
        PlayBackgroundMusic();
    }

    /// <summary>
    /// Enable or disable random music playback
    /// </summary>
    public void SetRandomizeMusic(bool randomize)
    {
        randomizeMusic = randomize;
    }

    /// <summary>
    /// Set the background music volume
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicAudioSource.volume = Mathf.Clamp01(volume);
    }

    /// <summary>
    /// Set the sound effects volume
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxAudioSource.volume = Mathf.Clamp01(volume);
    }
}
