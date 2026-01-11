/****************************************************************************
 * Addressables Multiple Resource for QFramework
 * 
 * 用于加载多个 Addressables 资源（Union / Intersection）
 * 
 * 前缀：
 * - addru://key1;key2   : 多个 key 的并集 (MergeMode.Union)
 * - addri://key1; key2   : 多个 key 的交集 (MergeMode.Intersection)
 * 
 * 使用示例：
 * var loader = ResLoader.Allocate();
 * loader.Add2Load("addru://Characters; Unlocked", (ok, res) => {
 *     var multiRes = res as AddressablesMultipleRes;
 *     foreach (var asset in multiRes.AllAssets) { }
 * });
 * loader.LoadAsync();
 * loader.Recycle2Cache();
 * 
 * 依赖：
 * - QFramework (ResKit)
 * - Unity Addressables Package
 ****************************************************************************/

#if QFRAMEWORK_RESKIT && ADDRESSABLES_SUPPORT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace QFramework
{
    /// <summary>
    /// Addressables 多资源加载类
    /// </summary>
    public class AddressablesMultipleRes : Res
    {
        #region Fields

        private readonly List<AsyncOperationHandle> mAssetHandles = new List<AsyncOperationHandle>();

        #endregion

        #region Properties

        public static readonly string SplitChar = ";";

        /// <summary>
        /// Addressable Keys（多个 key）
        /// </summary>
        public IList<string> AddressableKeys { get; private set; }

        /// <summary>
        /// 合并模式
        /// </summary>
        public Addressables.MergeMode MergeMode { get; private set; }

        /// <summary>
        /// 所有加载的资源
        /// </summary>
        public IList<UnityEngine.Object> AllAssets { get; private set; }

        #endregion

        #region Factory

        /// <summary>
        /// 从对象池分配实例
        /// </summary>
        public static AddressablesMultipleRes Allocate(string name, Addressables.MergeMode mergeMode, string keyString)
        {
            var res = SafeObjectPool<AddressablesMultipleRes>.Instance.Allocate();
            if (res != null)
            {
                res.AssetName = name;
                res.MergeMode = mergeMode;
                // 支持逗号分隔的多个 key
                res.AddressableKeys = keyString
                    .Split(SplitChar)
                    .Select(k => k.Trim())
                    .Where(k => !string.IsNullOrEmpty(k))
                    .ToList();
            }
            return res;
        }

        public AddressablesMultipleRes() { }

        #endregion

        #region Sync Load

        public override bool LoadSync()
        {
            if (!CheckLoadAble())
            {
                return false;
            }

            if (AddressableKeys == null || AddressableKeys.Count == 0)
            {
                Debug.LogError("[AddressablesMultipleRes] AddressableKeys is null or empty");
                return false;
            }

            State = ResState.Loading;

            try
            {
                return DoLoadSync();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressablesMultipleRes] LoadSync Error: {mAssetName}, {e.Message}\n{e.StackTrace}");
                OnResLoadFaild();
                return false;
            }
        }

        private bool DoLoadSync()
        {
            // 使用支持多 key 和 MergeMode 的 API
            var locHandle = Addressables.LoadResourceLocationsAsync(
                AddressableKeys,
                MergeMode,
                AssetType);
            var locations = locHandle.WaitForCompletion();

            if (locHandle.Status != AsyncOperationStatus.Succeeded || locations.Count == 0)
            {
                Debug.LogError($"[AddressablesMultipleRes] Load Failed, no locations found: {string.Join(SplitChar, AddressableKeys)}");
                Addressables.Release(locHandle);
                OnResLoadFaild();
                return false;
            }

            // 加载所有资源
            AllAssets = new List<UnityEngine.Object>();
            foreach (var loc in locations)
            {
                var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(loc);
                var asset = handle.WaitForCompletion();

                if (handle.Status == AsyncOperationStatus.Succeeded && asset != null)
                {
                    AllAssets.Add(asset);
                    mAssetHandles.Add(handle);
                }
                else if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            Addressables.Release(locHandle);

            if (AllAssets.Count > 0)
            {
                mAsset = AllAssets[0];
                State = ResState.Ready;
                return true;
            }

            Debug.LogError($"[AddressablesMultipleRes] Load Failed: {string.Join(SplitChar, AddressableKeys)}");
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

            if (AddressableKeys == null || AddressableKeys.Count == 0)
            {
                Debug.LogError("[AddressablesMultipleRes] AddressableKeys is null or empty");
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

            // 使用支持多 key 和 MergeMode 的 API
            var locHandle = Addressables.LoadResourceLocationsAsync(
                AddressableKeys,
                MergeMode,
                AssetType);
            yield return locHandle;

            if (locHandle.Status != AsyncOperationStatus.Succeeded || locHandle.Result.Count == 0)
            {
                Debug.LogError($"[AddressablesMultipleRes] Load Failed, no locations found: {string.Join(SplitChar, AddressableKeys)}");
                Addressables.Release(locHandle);
                OnResLoadFaild();
                finishCallback();
                yield break;
            }

            var locations = locHandle.Result;

            // 加载所有资源
            AllAssets = new List<UnityEngine.Object>();
            foreach (var loc in locations)
            {
                var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(loc);
                yield return handle;

                // 加载过程中被释放
                if (RefCount <= 0)
                {
                    if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                    // 释放已加载的资源
                    ReleaseLoadedHandles();
                    Addressables.Release(locHandle);
                    OnResLoadFaild();
                    finishCallback();
                    yield break;
                }

                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                {
                    AllAssets.Add(handle.Result);
                    mAssetHandles.Add(handle);
                }
                else if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            Addressables.Release(locHandle);

            if (AllAssets.Count > 0)
            {
                mAsset = AllAssets[0];
                State = ResState.Ready;
            }
            else
            {
                Debug.LogError($"[AddressablesMultipleRes] Load Failed: {string.Join(SplitChar, AddressableKeys)}");
                OnResLoadFaild();
            }

            finishCallback();
        }

        #endregion

        #region Release & Recycle

        private void ReleaseLoadedHandles()
        {
            foreach (var handle in mAssetHandles)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
            mAssetHandles.Clear();
        }

        protected override void OnReleaseRes()
        {
            ReleaseLoadedHandles();
            mAsset = null;
            AllAssets = null;
        }

        public override void Recycle2Cache()
        {
            SafeObjectPool<AddressablesMultipleRes>.Instance.Recycle(this);
        }

        public override void OnRecycled()
        {
            base.OnRecycled();
            mAssetHandles.Clear();
            AddressableKeys = null;
            AllAssets = null;
            MergeMode = Addressables.MergeMode.Union;
        }

        #endregion

        #region Progress

        protected override float CalculateProgress()
        {
            if (mAssetHandles.Count == 0)
            {
                return 0;
            }

            float total = 0;
            int validCount = 0;

            foreach (var handle in mAssetHandles)
            {
                if (handle.IsValid())
                {
                    total += handle.PercentComplete;
                    validCount++;
                }
            }

            return validCount > 0 ? total / validCount : 0;
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            var keys = AddressableKeys != null ? string.Join(SplitChar, AddressableKeys) : "null";
            return $"[AddressablesMultiple] Keys:{keys} MergeMode:{MergeMode} Count:{AllAssets?.Count ?? 0} {base.ToString()}";
        }

        #endregion
    }
}

#endif
