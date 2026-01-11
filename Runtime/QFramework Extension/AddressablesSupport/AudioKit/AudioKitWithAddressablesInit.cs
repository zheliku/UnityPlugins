#if QFRAMEWORK_RESKIT && ADDRESSABLES_SUPPORT

using UnityEngine;

namespace QFramework
{
    public class AudioKitWithAddressablesInit
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Init()
        {
            AudioKit.Config.AudioLoaderPool = new AddressablesAudioLoaderPool();
        }
    }
}

#endif
