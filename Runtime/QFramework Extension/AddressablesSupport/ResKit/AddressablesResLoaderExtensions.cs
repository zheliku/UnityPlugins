/****************************************************************************
 * Addressables ResLoader Extensions for QFramework
 * 
 * 为 ResLoader 提供 Addressables 场景加载扩展方法
 * 
 * 使用示例：
 * var loader = ResLoader.Allocate();
 * 
 * // 同步加载场景（仿 QFramework API）
 * loader.LoadAddressableSceneSync("GameScene");
 * loader.LoadAddressableSceneSync("GameScene", LoadSceneMode.Additive);
 * loader.LoadAddressableSceneSync("GameScene", LoadSceneMode.Additive, LocalPhysicsMode.Physics2D);
 * 
 * // 异步加载场景（仿 QFramework API）
 * loader.LoadAddressableSceneAsync("GameScene");
 * loader.LoadAddressableSceneAsync("GameScene", LoadSceneMode.Additive);
 * loader.LoadAddressableSceneAsync("GameScene", LoadSceneMode.Additive, LocalPhysicsMode.Physics2D);
 * loader.LoadAddressableSceneAsync("GameScene", LoadSceneMode.Additive, LocalPhysicsMode.None, handle => {
 *     Debug.Log("Scene loaded: " + handle.Result.Scene.name);
 * });
 * 
 * loader.Recycle2Cache();
 * 
 * 依赖：
 * - QFramework (ResKit)
 * - Unity Addressables Package
 ****************************************************************************/

#if QFRAMEWORK_RESKIT && ADDRESSABLES_SUPPORT

using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace QFramework
{
    /// <summary>
    /// Addressables ResLoader 扩展方法
    /// </summary>
    public static class AddressablesResLoaderExtensions
    {
        /// <summary>
        /// 同步加载 Addressables 场景
        /// </summary>
        /// <param name="self">ResLoader</param>
        /// <param name="sceneKey">Addressables 场景 Key</param>
        /// <param name="mode">场景加载模式</param>
        /// <param name="physicsMode">物理模式</param>
        /// <returns>场景实例</returns>
        public static SceneInstance LoadAddressableSceneSync(
            this IResLoader self,
            string sceneKey,
            LoadSceneMode mode = LoadSceneMode.Single,
            LocalPhysicsMode physicsMode = LocalPhysicsMode.None
        )
        {
            var resSearchKeys = ResSearchKeys.Allocate(AddressablesResCreator.Prefix.Single + sceneKey);
            var result = self.LoadAddressableSceneSync(resSearchKeys, mode, physicsMode);
            resSearchKeys.Recycle2Cache();
            return result;
        }

        /// <summary>
        /// 同步加载 Addressables 场景（使用 ResSearchKeys）
        /// </summary>
        public static SceneInstance LoadAddressableSceneSync(
            this IResLoader self,
            ResSearchKeys resSearchKeys,
            LoadSceneMode mode = LoadSceneMode.Single,
            LocalPhysicsMode physicsMode = LocalPhysicsMode.None
        )
        {
            var res = self.LoadResSync(resSearchKeys) as AddressablesSingleRes;

            if (res == null)
            {
                Debug.LogError($"[Addressables] Failed to load scene resource: {resSearchKeys.OriginalAssetName}");
                return default;
            }

            if (!res.IsScene)
            {
                Debug.LogError($"[Addressables] Resource is not a scene: {resSearchKeys.OriginalAssetName}");
                return default;
            }

            var location = res.Location;
            if (location == null)
            {
                Debug.LogError($"[Addressables] Scene location not found: {resSearchKeys.OriginalAssetName}");
                return default;
            }

            // 使用 LoadSceneParameters 和 IResourceLocation 加载场景
            var loadSceneParameters = new LoadSceneParameters(mode, physicsMode);
            var handle = Addressables.LoadSceneAsync(location, loadSceneParameters,
                SceneReleaseMode.ReleaseSceneWhenSceneUnloaded, activateOnLoad: true);
            var sceneInstance = handle.WaitForCompletion();

            // WaitForCompletion 后检查状态
            // 注意：某些情况下 WaitForCompletion 返回后状态可能仍是 None，需要检查 IsValid
            if (!handle.IsValid() || (handle.Status != AsyncOperationStatus.Succeeded && handle.Status != AsyncOperationStatus.None))
            {
                Debug.LogError($"[Addressables] Load Scene Failed: {resSearchKeys.OriginalAssetName}, Status: {handle.Status}");
                return default;
            }

            // 更新 Res 的场景句柄
            res.SetSceneHandle(handle);
            return sceneInstance;
        }

        /// <summary>
        /// 异步加载 Addressables 场景
        /// </summary>
        /// <param name="self">ResLoader</param>
        /// <param name="sceneKey">Addressables 场景 Key</param>
        /// <param name="loadSceneMode">场景加载模式</param>
        /// <param name="physicsMode">物理模式</param>
        /// <param name="onStartLoading">开始加载场景时的回调</param>
        public static void LoadAddressableSceneAsync(this IResLoader self, string sceneKey,
            LoadSceneMode loadSceneMode = LoadSceneMode.Single,
            LocalPhysicsMode physicsMode = LocalPhysicsMode.None,
            Action<AsyncOperationHandle<SceneInstance>> onStartLoading = null)
        {
            var resSearchKeys = ResSearchKeys.Allocate(AddressablesResCreator.Prefix.Single + sceneKey);
            self.LoadAddressableSceneAsync(resSearchKeys, loadSceneMode, physicsMode, onStartLoading);
            resSearchKeys.Recycle2Cache();
        }

        /// <summary>
        /// 异步加载 Addressables 场景（使用 ResSearchKeys）
        /// </summary>
        public static void LoadAddressableSceneAsync(this IResLoader self, ResSearchKeys resSearchKeys,
            LoadSceneMode loadSceneMode = LoadSceneMode.Single,
            LocalPhysicsMode physicsMode = LocalPhysicsMode.None,
            Action<AsyncOperationHandle<SceneInstance>> onStartLoading = null)
        {
            var sceneKey = resSearchKeys.OriginalAssetName;

            self.Add2Load(resSearchKeys, (success, res) =>
            {
                if (!success)
                {
                    Debug.LogError($"[Addressables] Failed to load scene resource: {sceneKey}");
                    return;
                }

                var singleRes = res as AddressablesSingleRes;
                if (singleRes == null || !singleRes.IsScene)
                {
                    Debug.LogError($"[Addressables] Resource is not a scene: {sceneKey}");
                    return;
                }

                var location = singleRes.Location;
                if (location == null)
                {
                    Debug.LogError($"[Addressables] Scene location not found: {sceneKey}");
                    return;
                }

                // 使用 LoadSceneParameters 和 IResourceLocation 加载场景
                var loadSceneParameters = new LoadSceneParameters(loadSceneMode, physicsMode);
                var handle = Addressables.LoadSceneAsync(location, loadSceneParameters,
                    SceneReleaseMode.ReleaseSceneWhenSceneUnloaded, activateOnLoad: true);

                // 更新 Res 的场景句柄
                singleRes.SetSceneHandle(handle);

                onStartLoading?.Invoke(handle);
            });

            self.LoadAsync();
        }

        /// <summary>
        /// 异步加载 Addressables 场景（简化版，带 SceneInstance 回调）
        /// </summary>
        public static void LoadAddressableSceneAsync(this IResLoader self, string sceneKey,
            Action<SceneInstance> onLoaded,
            LoadSceneMode mode = LoadSceneMode.Single)
        {
            self.LoadAddressableSceneAsync(sceneKey, mode, LocalPhysicsMode.None, handle =>
            {
                handle.Completed += h =>
                {
                    if (h.Status == AsyncOperationStatus.Succeeded)
                    {
                        onLoaded?.Invoke(h.Result);
                    }
                };
            });
        }
    }
}

#endif
