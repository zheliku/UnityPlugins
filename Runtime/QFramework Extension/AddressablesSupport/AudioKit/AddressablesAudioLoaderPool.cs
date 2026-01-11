#if QFRAMEWORK_RESKIT && ADDRESSABLES_SUPPORT

using System;
using UnityEngine;

namespace QFramework
{
    public class AddressablesAudioLoaderPool : AbstractAudioLoaderPool
    {
        public class AddressablesAudioLoader : IAudioLoader
        {
            private ResLoader mResLoader = null;

            public AudioClip Clip => mClip;
            private AudioClip mClip;

            public AudioClip LoadClip(AudioSearchKeys audioSearchKeys)
            {
                if (mResLoader == null)
                {
                    mResLoader = ResLoader.Allocate();
                }

                mClip = mResLoader.LoadSync<AudioClip>(AddressablesResCreator.Prefix.Single + audioSearchKeys.AssetName);

                return mClip;
            }

            public void LoadClipAsync(AudioSearchKeys audioSearchKeys, Action<bool, AudioClip> onLoad)
            {
                if (mResLoader == null)
                {
                    mResLoader = ResLoader.Allocate();
                }

                mResLoader.Add2Load<AudioClip>(AddressablesResCreator.Prefix.Single + audioSearchKeys.AssetName, (b, res) =>
                {
                    mClip = res.Asset as AudioClip;
                    onLoad(b, res.Asset as AudioClip);
                });

                mResLoader.LoadAsync();
            }

            public void Unload()
            {
                mClip = null;
                mResLoader?.Recycle2Cache();
                mResLoader = null;
            }
        }

        protected override IAudioLoader CreateLoader()
        {
            return new AddressablesAudioLoader();
        }
    }
}

#endif