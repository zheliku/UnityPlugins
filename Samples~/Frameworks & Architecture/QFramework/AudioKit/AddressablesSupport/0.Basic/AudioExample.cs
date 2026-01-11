using UnityEngine;

namespace QFramework.Example
{
    public partial class AudioExample : ViewController
    {
        private void Awake()
        {

            BtnPlayHome.onClick.AddListener(() => { AudioKit.PlayMusic("home_bg"); });

            BtnPlayGame.onClick.AddListener(() => { AudioKit.PlayMusic("game_bg"); });

            BtnPlaySoundClick.onClick.AddListener(() => { AudioKit.PlaySound("game_bg"); });
            BtnPlayVoice.onClick.AddListener(() =>
            {
                AudioKit.PlayVoice("game_bg",onBeganCallback:(() =>
                {
                    Debug.Log("voice game_bg called");
                    
                }));
            });

            BtnSoundOn.onClick.AddListener(() => { AudioKit.Settings.IsSoundOn.Value = true; });

            BtnSoundOff.onClick.AddListener(() => { AudioKit.Settings.IsSoundOn.Value = false; });

            BtnMusicOn.onClick.AddListener(() => { AudioKit.Settings.IsMusicOn.Value = true; });

            BtnMusicOff.onClick.AddListener(() => { AudioKit.Settings.IsMusicOn.Value = false; });

            BtnVoiceOn.onClick.AddListener(() => { AudioKit.Settings.IsVoiceOn.Value = true; });

            BtnVoiceOff.onClick.AddListener(() => { AudioKit.Settings.IsVoiceOn.Value = false; });
            
            BtnStopAllSounds.onClick.AddListener(AudioKit.StopAllSound);
            BtnStopMusic.onClick.AddListener(AudioKit.StopMusic);
            BtnStopVoice.onClick.AddListener(AudioKit.StopVoice);
            BtnPauseMusic.onClick.AddListener(AudioKit.PauseMusic);
            BtnResumeMusic.onClick.AddListener(AudioKit.ResumeMusic);
            

            AudioKit.Settings.MusicVolume.RegisterWithInitValue(v => MusicVolume.value = v);
            AudioKit.Settings.VoiceVolume.RegisterWithInitValue(v => VoiceVolume.value = v);
            AudioKit.Settings.SoundVolume.RegisterWithInitValue(v => SoundVolume.value = v);
            
            MusicVolume.onValueChanged.AddListener(v => { AudioKit.Settings.MusicVolume.Value = v; });
            VoiceVolume.onValueChanged.AddListener(v => { AudioKit.Settings.VoiceVolume.Value = v; });
            SoundVolume.onValueChanged.AddListener(v => { AudioKit.Settings.SoundVolume.Value = v; });
        }
    }
}