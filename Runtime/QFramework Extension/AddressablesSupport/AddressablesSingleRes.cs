/****************************************************************************
 * Addressables Single Resource for QFramework
 * 
 * 用于加载单个 Addressables 资源
 * 
 * 前缀：addr://key
 * 
 * 使用示例：
 * var loader = ResLoader.Allocate();
 * var prefab = loader.LoadSync<GameObject>("addr://Prefabs/Player");
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

namespace QFramework
{
    /// <summary>
    /// Addressables 单资源加载类
    /// </summary>
    public class AddressablesSingleRes : Res
    {
        #region Fields

        private AsyncOperationHandle mResHandle;

        #endregion

        #region Properties

        /// <summary>
        /// Addressable Key
        /// </summary>
        public string AddressableKey { get; private set; }

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

            // 只加载第一个匹配的资源
            var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(locations[0]);
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

            // 只加载第一个匹配的资源
            var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(locHandle.Result[0]);
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
            if (mResHandle.IsValid())
            {
                Addressables.Release(mResHandle);
                mResHandle = default;
            }

            mAsset = null;
        }

        public override void Recycle2Cache()
        {
            SafeObjectPool<AddressablesSingleRes>.Instance.Recycle(this);
        }

        public override void OnRecycled()
        {
            base.OnRecycled();
            mResHandle = default;
            AddressableKey = null;
        }

        #endregion

        #region Progress

        protected override float CalculateProgress()
        {
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
