using UnityEngine;

namespace QFramework.Example
{
    public class AudioKitActionExample : MonoBehaviour
    {
        public AudioClip heroClip;

        private void Start()
        {
            PlaySoundAction
                .Allocate("button_clicked", () =>
                {
                    Debug.Log("button_clicked finish");
                })
                .Start(this);

            // var heroClip = Resources.Load<AudioClip>("hero_hurt");

            ActionKit.Sequence()
                .Delay(1.0f)
                .PlaySound("button_clicked")
                .Delay(1.0f)
                .PlaySound(heroClip)
                .Start(this);
        }
    }
}