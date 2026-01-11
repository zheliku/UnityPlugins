using System.Collections.Generic;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

public class MultipleLoadExample : MonoBehaviour
{
#if QFRAMEWORK_RESKIT && ADDRESSABLES_SUPPORT

    public List<Image> images;
    public Transform spawnParent;

    private List<ResLoader> _resLoaders = new List<ResLoader>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ResKit.Init();

        for (int i = 0; i < 4; i++)
        {
            _resLoaders.Add(ResLoader.Allocate());
        }

        // ==================== 多资源加载示例 ====================
        // 使用 addru:// 前缀加载多个 key/label 的并集
        // 使用 addri:// 前缀加载多个 key/label 的交集
        // 多个 key 使用分号 ; 分隔

        // 1. 同步加载 - 并集模式 (Union)
        // 加载所有带有 "b" 或 "c" label 的资源
        var unionRes = _resLoaders[0].LoadResSync(
            ResSearchKeys.Allocate("addru://b; c", null, typeof(Sprite))
        ) as AddressablesMultipleRes;

        if (unionRes != null && unionRes.AllAssets != null)
        {
            Debug.Log($"[Union Sync] Loaded {unionRes.AllAssets.Count} assets");
            for (int i = 0; i < unionRes.AllAssets.Count && i < images.Count; i++)
            {
                if (unionRes.AllAssets[i] is Sprite sprite)
                {
                    images[i].sprite = sprite;
                }
            }
        }

        // 2. 同步加载 - 交集模式 (Intersection)
        // 加载同时带有 "a" 和 "default" label 的资源
        var intersectionRes = _resLoaders[1].LoadResSync(
            ResSearchKeys.Allocate("addri://a; default", null, typeof(GameObject))
        ) as AddressablesMultipleRes;

        if (intersectionRes != null && intersectionRes.AllAssets != null)
        {
            Debug.Log($"[Intersection Sync] Loaded {intersectionRes.AllAssets.Count} assets");
            foreach (var asset in intersectionRes.AllAssets)
            {
                if (asset is GameObject prefab)
                {
                    Instantiate(prefab, spawnParent);
                }
            }
        }

        // 3. 异步加载 - 并集模式 (Union)
        // 使用 GetAllAssets() 扩展方法获取所有资源
        _resLoaders[2].Add2Load("addru://a; default", (success, res) =>
        {
            if (!success)
            {
                Debug.LogError("[Union Async] Load Failed!");
                return;
            }

            var allAssets = res.GetAllAssets();
            Debug.Log($"[Union Async] Loaded {allAssets?.Count ?? 0} assets");

            if (allAssets != null)
            {
                float xOffset = 0;
                foreach (var asset in allAssets)
                {
                    if (asset is GameObject prefab)
                    {
                        Instantiate(prefab, new Vector3(xOffset, 0, 0), Quaternion.identity, spawnParent);
                        xOffset += 2f;
                    }
                }
            }
        });
        _resLoaders[2].LoadAsync();

        // 4. 异步加载 - 交集模式 (Intersection)
        _resLoaders[3].Add2Load("addri://a; b", (success, res) =>
        {
            if (!success)
            {
                Debug.LogError("[Intersection Async] Load Failed!");
                return;
            }

            var multiRes = res as AddressablesMultipleRes;
            Debug.Log($"[Intersection Async] Loaded {multiRes?.AllAssets?.Count ?? 0} assets");

            if (multiRes?.AllAssets != null)
            {
                foreach (var asset in multiRes.AllAssets)
                {
                    Debug.Log($"  - {asset.name}");
                }
            }
        });
        _resLoaders[3].LoadAsync();
    }

    private void OnDestroy()
    {
        foreach (var resLoader in _resLoaders)
        {
            resLoader.Recycle2Cache();
        }
    }

#endif
}
