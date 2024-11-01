using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class UISettings : MonoBehaviour
{
    private const string MIXER_MASTER = "MasterVolume";
    private const string MIXER_MUSIC = "MusicVolume";

    private const string PREF_MASTER_VOLUME = "MasterVolume";
    private const string PREF_MUSIC_VOLUME = "MusicVolume";

    private const float MIN_VOLUME_DB = -80f; // mute

    [Header("References")]
    public AudioMixer audioMixer;
    public Slider masterSlider;
    public Slider musicSlider;
    public TMP_Text masterPercentageText;
    public TMP_Text musicPercentageText;

    private void Start()
    {
        LoadVolume();

        // listen for sliders value change to update the volumes
        masterSlider.onValueChanged.AddListener(value => UpdateMasterVolume(value));
        musicSlider.onValueChanged.AddListener(value => UpdateMusicVolume(value));
    }

    private void UpdateUI()
    {
        masterPercentageText.text = $"{masterSlider.value * 100:N0}%";
        musicPercentageText.text = $"{musicSlider.value * 100:N0}%";
    }

    public void UpdateMasterVolume(float volume)
    {
        volume = Mathf.Clamp(volume, 0.001f, 1f);

        audioMixer.SetFloat(MIXER_MASTER, Mathf.Log10(volume) * 20);

        SaveVolume();
        UpdateUI();
    }

    public void UpdateMusicVolume(float volume)
    {
        volume = Mathf.Clamp(volume, 0.001f, 1f);

        // calculate volume in dB based on log scale
        float logRange = 3f; // logarithmic difference, constant
        float normalizedVolume = (Mathf.Log10(volume) - Mathf.Log10(0.001f)) / logRange;

        // convert to dB and set the value in the audio mixer
        float volumeInDB = Mathf.Lerp(MIN_VOLUME_DB, MusicManager.Instance.maximumMusicVolumeDb, normalizedVolume);

        audioMixer.SetFloat(MIXER_MUSIC, volumeInDB);
        //audioMixer.SetFloat(MIXER_MUSIC, Mathf.Log10(volume) * 20); // only if it goes from -80 to 0 db

        SaveVolume();
        UpdateUI();
    }

    public void SaveVolume()
    {
        //audioMixer.GetFloat(MIXER_MASTER, out float masterVolume);
        //var masterMixerToSliderValue = Mathf.Pow(10, masterVolume / 20);
        PlayerPrefs.SetFloat(PREF_MASTER_VOLUME, masterSlider.value);

        //audioMixer.GetFloat(MIXER_MUSIC, out float musicVolume);
        //var musicMixerToSliderValue = Mathf.Pow(10, musicVolume / 20);
        PlayerPrefs.SetFloat(PREF_MUSIC_VOLUME, musicSlider.value);

        PlayerPrefs.Save();
    }

    public void LoadVolume()
    {
        var savedMasterVolume = Mathf.Clamp(PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, AudioManager.Instance.defaultMasterVolume), 0.001f, 1f);
        var savedMusicVolume = Mathf.Clamp(PlayerPrefs.GetFloat(PREF_MUSIC_VOLUME, MusicManager.Instance.defaultMusicVolume), 0.001f, 1f);
        masterSlider.value = savedMasterVolume;
        musicSlider.value = savedMusicVolume;

        UpdateUI();
    }
}
