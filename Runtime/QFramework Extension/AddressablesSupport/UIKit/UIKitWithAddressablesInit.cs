#if QFRAMEWORK_RESKIT && ADDRESSABLES_SUPPORT

using UnityEngine;

namespace QFramework
{
    public class UIKitWithAddressablesInit
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Init()
        {
            UIKit.Config.PanelLoaderPool = new AddressablesPanelLoaderPool();
        }
    }
}

#endif
