using System.Collections.Generic;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

public class SingleLoadExample : MonoBehaviour
{
#if QFRAMEWORK_RESKIT && ADDRESSABLES_SUPPORT

    public List<Image> images;

    private List<ResLoader> _resLoaders = new List<ResLoader>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ResKit.Init();

        for (int i = 0; i < 4; i++)
        {
            _resLoaders.Add(ResLoader.Allocate());
        }

        // ==================== 单资源加载示例 ====================
        // 使用 addr:// 前缀加载单个 key/label

        // 1. 同步 key/label 加载
        images[0].sprite = _resLoaders[0].LoadSync<Sprite>("addr://qq");
        Instantiate(
            _resLoaders[1].LoadSync<GameObject>("addr://a"),
            position: Vector3.zero,
            rotation: Quaternion.identity
        );

        // 2. 异步 key/label 加载
        _resLoaders[2].Add2Load<GameObject>("addr://Cube", (sucess, res) =>
        {
            if (!sucess)
            {
                Debug.LogError("Load Cube Failed!");
                return;
            }
            Instantiate(
                res.Asset as GameObject,
                position: Vector3.one,
                rotation: Quaternion.identity
            );
        });
        _resLoaders[2].LoadAsync();
        _resLoaders[3].Add2Load<Sprite>("addr://default", (sucess, res) =>
        {
            if (!sucess)
            {
                Debug.LogError("Load Sprite Failed!");
                return;
            }
            images[1].sprite = res.Asset as Sprite;
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
