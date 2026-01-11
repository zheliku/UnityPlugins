using UnityEngine;

namespace QFramework.Example
{
    public class ResKitExample : MonoBehaviour
    {
        // 每个脚本都需要
        private ResLoader mResLoader = ResLoader.Allocate();

        private void Start()
        {
            // 项目启动只调用一次即可
            ResKit.Init();
            
            // 加载资源需要指定 addr:// 前缀，以区分是从 Addressables 加载
            // 通过 Key + Type 搜索并加载资源
            var prefab = mResLoader.LoadSync<GameObject>("addr://AssetObj");
            var gameObj = Instantiate(prefab);
            gameObj.name = "这是使用通过 Key 加载的对象";

            // 通过 Label + Type 搜索并加载资源
            prefab = mResLoader.LoadSync<GameObject>("addr://Prefab");
            gameObj = Instantiate(prefab);
            gameObj.name = "这是使用通过 Label 加载的对象";
        }

        private void OnDestroy()
        {
            // 释放所有本脚本加载过的资源
            // 释放只是释放资源的引用
            // 当资源的引用数量为 0 时，会进行真正的资源卸载操作
            mResLoader.Recycle2Cache();
            mResLoader = null;
        }
    }
}