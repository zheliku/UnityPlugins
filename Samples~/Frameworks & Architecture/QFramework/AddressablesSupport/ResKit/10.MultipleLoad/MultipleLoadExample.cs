using System.Collections.Generic;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

public class MultipleLoadExample : MonoBehaviour
{
#if QFRAMEWORK_RESKIT && ADDRESSABLES_SUPPORT

    private List<ResLoader> _resLoaders = new List<ResLoader>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ResKit.Init();

        for (int i = 0; i < 2; i++)
        {
            _resLoaders.Add(ResLoader.Allocate());
        }

        // ==================== 多资源加载示例 ====================
        // 使用 addru:// 前缀加载多个 key/label 的并集
        // 使用 addri:// 前缀加载多个 key/label 的交集
        // 多个 key 使用分号 ; 分隔

        // 1. 并集模式 (Union)
        // 加载所有带有 "b" 或 "c" label 的资源
        var unionRes = _resLoaders[0].LoadResSync(
            ResSearchKeys.Allocate("addru://default; Prefab", null, typeof(GameObject))
        ) as AddressablesMultipleRes;

        if (unionRes != null && unionRes.AllAssets != null)
        {
            Debug.Log($"[Union Sync] Loaded {unionRes.AllAssets.Count} assets");
            var spawnParent = new GameObject($"UnionSync_SpawnParent").transform;
            for (int i = 0; i < unionRes.AllAssets.Count; i++)
            {
                if (unionRes.AllAssets[i] is GameObject prefab)
                {
                    var obj = Instantiate(prefab, Vector3.one * i, Quaternion.identity, spawnParent);
                    obj.GetComponent<MeshRenderer>().material.color = new Color(1, 0, 0); // Red
                }
            }
        }

        // 2. 异步加载 - 交集模式 (Intersection)
        _resLoaders[1].Add2Load("addri://default; Prefab", (success, res) =>
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
                var spawnParent = new GameObject($"IntersectionAsync_SpawnParent").transform;
                for (int i = 0; i < multiRes.AllAssets.Count; i++)
                {
                    Object asset = multiRes.AllAssets[i];
                    if (asset is GameObject prefab)
                    {
                        var obj = Instantiate(prefab, -Vector3.one * (i + 1), Quaternion.identity, spawnParent);
                        obj.GetComponent<MeshRenderer>().material.color = new Color(0, 0, 1); // Blue
                    }
                }
            }
        });
        _resLoaders[1].LoadAsync();
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
