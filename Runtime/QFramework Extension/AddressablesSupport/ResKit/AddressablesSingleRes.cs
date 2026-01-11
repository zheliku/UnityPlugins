/****************************************************************************
 * Addressables Single Resource for QFramework
 * 
 * 用于加载单个 Addressables 资源（包括场景）
 * 
 * 前缀：addr://key
 * 
 * 使用示例：
 * var loader = ResLoader.Allocate();
 * 
 * // 加载普通资源
 * var prefab = loader.LoadSync<GameObject>("addr://Prefabs/Player");
 * 
 * // 加载场景（自动检测）
 * var sceneRes = loader.LoadResSync(ResSearchKeys.Allocate("addr://GameScene")) as AddressablesSingleRes;
 * sceneRes.LoadSceneAsync(LoadSceneMode.Additive);
 * 
 * loader.Recycle2Cache();
 * 
 * 依赖：
 * - QFramework (ResKit)
 * - Unity Addressables Package
 ****************************************************************************/

#if QFRAMEWORK_RESKIT && ADDRESSABLES_SUPPORT

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace QFramework
{
    /// <summary>
    /// Addressables 单资源加载类
    /// </summary>
    public class AddressablesSingleRes : Res
    {
        #region Fields

        private AsyncOperationHandle mResHandle;
        private AsyncOperationHandle<SceneInstance> mSceneHandle;
        private IResourceLocation mLocation;

        #endregion

        #region Properties

        /// <summary>
        /// Addressable Key
        /// </summary>
        public string AddressableKey { get; private set; }

        /// <summary>
        /// 是否为场景资源
        /// </summary>
        public bool IsScene { get; private set; }

        /// <summary>
        /// 资源位置（用于场景加载）
        /// </summary>
        public IResourceLocation Location => mLocation;

        /// <summary>
        /// 场景实例（仅当 IsScene 为 true 时有效）
        /// </summary>
        public SceneInstance SceneInstance { get; private set; }

        #endregion

        #region Factory

        /// <summary>
        /// 从对象池分配实例
        /// </summary>
        public static AddressablesSingleRes Allocate(string name, string key)
        {
            var res = SafeObjectPool<AddressablesSingleRes>.Instance.Allocate();
            if (res != null)
            {
                res.AssetName = name;
                res.AddressableKey = key;
            }
            return res;
        }

        public AddressablesSingleRes() { }

        #endregion

        #region Sync Load

        public override bool LoadSync()
        {
            if (!CheckLoadAble())
            {
                return false;
            }

            if (string.IsNullOrEmpty(AddressableKey))
            {
                Debug.LogError("[AddressablesSingleRes] AddressableKey is null or empty");
                return false;
            }

            State = ResState.Loading;

            try
            {
                return DoLoadSync();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressablesSingleRes] LoadSync Error: {mAssetName}, {e.Message}\n{e.StackTrace}");
                OnResLoadFaild();
                return false;
            }
        }

        private bool DoLoadSync()
        {
            // 使用 LoadResourceLocationsAsync 支持 AssetType
            var locHandle = Addressables.LoadResourceLocationsAsync(AddressableKey, AssetType);
            var locations = locHandle.WaitForCompletion();

            if (locHandle.Status != AsyncOperationStatus.Succeeded || locations.Count == 0)
            {
                Debug.LogError($"[AddressablesSingleRes] Load Failed, no locations found: {AddressableKey}");
                Addressables.Release(locHandle);
                OnResLoadFaild();
                return false;
            }

            // 保存 location 供后续使用
            mLocation = locations[0];

            // 检查是否为场景资源
            IsScene = mLocation.ResourceType == typeof(SceneInstance);

            if (IsScene)
            {
                // 场景资源：标记为 Ready，实际加载由扩展方法处理
                Addressables.Release(locHandle);
                State = ResState.Ready;
                return true;
            }

            // 普通资源：加载第一个匹配的资源
            var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(mLocation);
            mAsset = handle.WaitForCompletion();
            mResHandle = handle;

            Addressables.Release(locHandle);

            if (mAsset != null)
            {
                State = ResState.Ready;
                return true;
            }

            Debug.LogError($"[AddressablesSingleRes] Load Failed: {AddressableKey}");
            OnResLoadFaild();
            return false;
        }

        #endregion

        #region Async Load

        public override void LoadAsync()
        {
            if (!CheckLoadAble())
            {
                return;
            }

            if (string.IsNullOrEmpty(AddressableKey))
            {
                Debug.LogError("[AddressablesSingleRes] AddressableKey is null or empty");
                return;
            }

            State = ResState.Loading;
            ResMgr.Instance.PushIEnumeratorTask(this);
        }

        public override IEnumerator DoLoadAsync(Action finishCallback)
        {
            if (RefCount <= 0)
            {
                OnResLoadFaild();
                finishCallback();
                yield break;
            }

            // 使用 LoadResourceLocationsAsync 支持 AssetType
            var locHandle = Addressables.LoadResourceLocationsAsync(AddressableKey, AssetType);
            yield return locHandle;

            if (locHandle.Status != AsyncOperationStatus.Succeeded || locHandle.Result.Count == 0)
            {
                Debug.LogError($"[AddressablesSingleRes] Load Failed, no locations found: {AddressableKey}");
                Addressables.Release(locHandle);
                OnResLoadFaild();
                finishCallback();
                yield break;
            }

            // 保存 location 供后续使用
            mLocation = locHandle.Result[0];

            // 检查是否为场景资源
            IsScene = mLocation.ResourceType == typeof(SceneInstance);

            if (IsScene)
            {
                // 场景资源：标记为 Ready，实际加载由扩展方法处理
                Addressables.Release(locHandle);
                State = ResState.Ready;
                finishCallback();
                yield break;
            }

            // 普通资源：加载第一个匹配的资源
            var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(mLocation);
            mResHandle = handle;
            yield return handle;

            Addressables.Release(locHandle);

            // 加载过程中被释放
            if (RefCount <= 0)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
                OnResLoadFaild();
                finishCallback();
                yield break;
            }

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                mAsset = handle.Result;
                State = ResState.Ready;
            }
            else
            {
                Debug.LogError($"[AddressablesSingleRes] Load Failed: {AddressableKey}");
                OnResLoadFaild();
            }

            finishCallback();
        }

        #endregion

        #region Release & Recycle

        protected override void OnReleaseRes()
        {
            // 释放场景
            if (mSceneHandle.IsValid())
            {
                // 检查是否是最后一个场景，如果是则不卸载（Unity 不允许卸载最后一个场景）
                if (SceneManager.sceneCount > 1)
                {
                    Addressables.UnloadSceneAsync(mSceneHandle);
                }
                mSceneHandle = default;
            }

            // 释放普通资源
            if (mResHandle.IsValid())
            {
                Addressables.Release(mResHandle);
                mResHandle = default;
            }

            mAsset = null;
            mLocation = null;
        }

        public override void Recycle2Cache()
        {
            SafeObjectPool<AddressablesSingleRes>.Instance.Recycle(this);
        }

        public override void OnRecycled()
        {
            base.OnRecycled();
            mResHandle = default;
            mSceneHandle = default;
            mLocation = null;
            AddressableKey = null;
            IsScene = false;
            SceneInstance = default;
        }

        /// <summary>
        /// 设置场景句柄（由扩展方法调用）
        /// </summary>
        public void SetSceneHandle(AsyncOperationHandle<SceneInstance> handle)
        {
            mSceneHandle = handle;
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                SceneInstance = handle.Result;
            }
            else
            {
                handle.Completed += h =>
                {
                    if (h.Status == AsyncOperationStatus.Succeeded)
                    {
                        SceneInstance = h.Result;
                    }
                };
            }
        }

        #endregion

        #region Progress

        protected override float CalculateProgress()
        {
            if (mSceneHandle.IsValid())
            {
                return mSceneHandle.PercentComplete;
            }
            if (mResHandle.IsValid())
            {
                return mResHandle.PercentComplete;
            }
            return 0;
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return $"[AddressablesSingle] Key:{AddressableKey} {base.ToString()}";
        }

        #endregion
    }
}

#endif
