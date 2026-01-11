/****************************************************************************
 * Addressables Resource Creator for QFramework
 * 
 * 资源创建器，用于识别和创建 Addressables 资源
 * 完全兼容 ResLoader 接口，通过资源名前缀区分加载方式
 * 
 * 支持的资源名前缀：
 * - addr://key         : 加载单个资源 (AddressablesSingleRes)
 * - addru://k1,k2      : 多个 key 的并集 (AddressablesMultipleRes)
 * - addri://k1,k2      : 多个 key 的交集 (AddressablesMultipleRes)
 * 
 * key/label 使用相同 API，无需区分
 * 多个 key 使用逗号分隔
 * 
 * 注册方式：
 * 自动注册（通过 RuntimeInitializeOnLoadMethod）
 * 或手动注册：ResFactory.AddResCreator<AddressablesResCreator>();
 * 
 * 使用示例：
 * var loader = ResLoader.Allocate();
 * 
 * // 单资源加载
 * var prefab = loader.LoadSync<GameObject>("addr://Prefabs/Player");
 * 
 * // 多资源加载（并集）
 * loader.Add2Load("addru://Enemies,Bosses", (ok, res) => {
 *     var all = (res as AddressablesMultipleRes).AllAssets;
 * });
 * 
 * // 多资源加载（交集）
 * loader.Add2Load("addri://Characters,Unlocked", (ok, res) => {
 *     var all = (res as AddressablesMultipleRes).AllAssets;
 * });
 * 
 * loader.Recycle2Cache();
 * 
 * 依赖：
 * - QFramework (ResKit)
 * - Unity Addressables Package
 ****************************************************************************/

#if QFRAMEWORK_RESKIT && ADDRESSABLES_SUPPORT

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace QFramework
{
    public static class ResExtension
    {
        /// <summary>
        /// 使用 Addressables 资源加载扩展方法
        /// </summary>
        public static IList<Object> GetAllAssets(this IRes res)
        {
            var addrRes = res as AddressablesMultipleRes;
            if (addrRes != null)
            {
                return addrRes.AllAssets;
            }

            Debug.LogWarning("GetAllAssets: The resource is not an AddressablesMultipleRes.");
            return null;
        }
    }

    /// <summary>
    /// Addressables 资源创建器
    /// </summary>
    public class AddressablesResCreator : IResCreator
    {
        #region Auto Register

        private static bool s_Registered = false;

        /// <summary>
        /// 自动注册（在场景加载前自动调用）
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoRegister()
        {
            if (s_Registered)
            {
                return;
            }

            s_Registered = true;
            ResFactory.AddResCreator<AddressablesResCreator>();

#if UNITY_EDITOR
            Debug.Log("[AddressablesResCreator] Auto registered");
#endif
        }

        #endregion

        #region Prefix Constants

        /// <summary>
        /// 资源名前缀
        /// </summary>
        public static class Prefix
        {
            /// <summary>
            /// 加载单个资源: addr://
            /// </summary>
            public const string Single = "addr://";

            /// <summary>
            /// 多 key 并集: addru://
            /// </summary>
            public const string Union = "addru://";

            /// <summary>
            /// 多 key 交集: addri://
            /// </summary>
            public const string Intersection = "addri://";
        }

        #endregion

        #region IResCreator Implementation

        public bool Match(ResSearchKeys resSearchKeys)
        {
            if (resSearchKeys == null || string.IsNullOrEmpty(resSearchKeys.AssetName))
            {
                return false;
            }

            var name = resSearchKeys.AssetName;
            return name.StartsWith(Prefix.Single) ||
                   name.StartsWith(Prefix.Union) ||
                   name.StartsWith(Prefix.Intersection);
        }

        public IRes Create(ResSearchKeys resSearchKeys)
        {
            var assetName = resSearchKeys.AssetName;

            // 多资源：Intersection（交集）
            if (assetName.StartsWith(Prefix.Intersection))
            {
                var keysPart = assetName.Substring(Prefix.Intersection.Length);
                var res = AddressablesMultipleRes.Allocate(assetName, Addressables.MergeMode.Intersection, keysPart);
                res.AssetType = resSearchKeys.AssetType;
                return res;
            }

            // 多资源：Union（并集）
            if (assetName.StartsWith(Prefix.Union))
            {
                var keysPart = assetName.Substring(Prefix.Union.Length);
                var res = AddressablesMultipleRes.Allocate(assetName, Addressables.MergeMode.Union, keysPart);
                res.AssetType = resSearchKeys.AssetType;
                return res;
            }

            // 单资源
            {
                var key = assetName.Substring(Prefix.Single.Length);
                var res = AddressablesSingleRes.Allocate(assetName, key);
                res.AssetType = resSearchKeys.AssetType;
                return res;
            }
        }

        #endregion
    }
}

#endif
