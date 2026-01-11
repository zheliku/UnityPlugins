using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadExample : MonoBehaviour
{
#if QFRAMEWORK_RESKIT && ADDRESSABLES_SUPPORT

    private ResLoader _resLoader;

    void Start()
    {
        ResKit.Init();
        _resLoader = ResLoader.Allocate();

        // 同步加载场景
        // _resLoader.LoadAddressableSceneSync("SingleLoad");

        // 异步加载场景
        _resLoader.LoadAddressableSceneAsync("SingleLoad", sceneInstance => { }, LoadSceneMode.Additive);
    }

    private void OnDestroy()
    {
        // 释放 ResLoader 时会自动卸载已加载的场景
        _resLoader.Recycle2Cache();
    }

#endif
}

