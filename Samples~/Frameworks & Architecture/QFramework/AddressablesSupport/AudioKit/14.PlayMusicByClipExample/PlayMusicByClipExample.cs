using System;
using UnityEngine;

namespace QFramework.Example
{
    public class PlayMusicByClipExample : MonoBehaviour
    {
        public AudioClip Clip;
        
        private void Start()
        {
            AudioKit.PlayMusic(Clip);
        }
    }
}