using System.Collections;
using UnityEngine;

namespace QFramework.Demos
{
    public partial class PlaySoundFluentAPIExample : ViewController
    {
        private IEnumerator Start()
        {
            AudioKit.Sound()
                .WithName("hero_hurt")
                .Play();

            yield return new WaitForSeconds(1.0f);
            
            AudioKit.Sound()
                .WithName("button_clicked")
                .Play();
        }

        
        // A Bug Test
        // [SerializeField]
        // private AudioClip clip;
        // private AudioPlayer mPlayer;
        // void Start()
        // {
        //     clip = Resources.Load<AudioClip>("hero_hurt");
        //     AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames;
        // }
        //
        // private void Update()
        // {
        //     if (Input.GetKeyDown(KeyCode.Space))
        //     {
        //         mPlayer = AudioKit.PlaySound(clip,loop:true);
        //     }
        //
        //     if (Input.GetKeyUp(KeyCode.Space))
        //     {
        //         mPlayer.Stop();
        //         mPlayer = null;
        //     }
        // }
    }
}