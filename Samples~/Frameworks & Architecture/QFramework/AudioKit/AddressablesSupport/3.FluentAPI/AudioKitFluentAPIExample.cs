using System.Collections;
using UnityEngine;

namespace QFramework.Example
{
    public class AudioKitFluentAPIExample : MonoBehaviour
    {
        IEnumerator Start()
        {
            AudioKit.Music()
                .WithName("game_bg")
                .Loop(false)
                .VolumeScale(0.5f)
                .Play();

            yield return new WaitForSeconds(2.0f);
            AudioKit.PauseMusic();
            yield return new WaitForSeconds(0.5f);

            AudioKit.Sound()
                .WithName("button_clicked")
                .VolumeScale(0.7f)
                .Play()
                .OnFinish(() =>
                {
                    "OnSoundFinish".LogInfo();
                });
     
            yield return new WaitForSeconds(1.0f);
            AudioKit.ResumeMusic();
        }
    }
}
