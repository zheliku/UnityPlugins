using System.Collections;
using UnityEngine;

namespace QFramework.Example
{
    public class AsyncLoadExample : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return ResKit.InitAsync();
            
            var resLoader = ResLoader.Allocate();

            // Key 加载示例
            resLoader.Add2Load<GameObject>("addr://AssetObj 1",(b, res) =>
            {
                if (b)
                {
                    res.Asset.As<GameObject>().Instantiate();
                }
            });

            // Label 加载示例
            resLoader.Add2Load<GameObject>("addr://Prefab2", (b, res) =>
            {
                if (b)
                {
                    res.Asset.As<GameObject>().Instantiate();
                }
            });
            
            resLoader.Add2Load<GameObject>("addr://AssetObj 3",(b, res) =>
            {
                if (b)
                {
                    res.Asset.As<GameObject>().Instantiate();
                }
            });

            // 回调
            resLoader.LoadAsync(() =>
            {
                // 加载成功 5 秒后回收
                ActionKit.Delay(5.0f, () =>
                {
                    resLoader.Recycle2Cache();
                }).Start(this);
            });
        }
    }
}