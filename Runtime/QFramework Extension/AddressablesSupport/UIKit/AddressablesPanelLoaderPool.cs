#if QFRAMEWORK_RESKIT && ADDRESSABLES_SUPPORT

using System;
using UnityEngine;

namespace QFramework
{
    public class AddressablesPanelLoaderPool : AbstractPanelLoaderPool
    {
        public class AddressablesPanelLoader : IPanelLoader
        {
            private ResLoader mResLoader;

            public GameObject LoadPanelPrefab(PanelSearchKeys panelSearchKeys)
            {
                if (mResLoader == null)
                {
                    mResLoader = ResLoader.Allocate();
                }

                if (panelSearchKeys.PanelType.IsNotNull() && panelSearchKeys.GameObjName.IsNullOrEmpty())
                {
                    return mResLoader.LoadSync<GameObject>(
                        AddressablesResCreator.Prefix.Single + panelSearchKeys.PanelType.Name
                    );
                }

                return mResLoader.LoadSync<GameObject>(
                    AddressablesResCreator.Prefix.Single + panelSearchKeys.GameObjName
                );
            }

            public void LoadPanelPrefabAsync(PanelSearchKeys panelSearchKeys, Action<GameObject> onLoad)
            {
                if (mResLoader == null)
                {
                    mResLoader = ResLoader.Allocate();
                }

                if (panelSearchKeys.PanelType.IsNotNull() && panelSearchKeys.GameObjName.IsNullOrEmpty())
                {
                    mResLoader.Add2Load<GameObject>(AddressablesResCreator.Prefix.Single + panelSearchKeys.PanelType.Name, (success, res) =>
                    {
                        if (success)
                        {
                            onLoad(res.Asset as GameObject);
                        }
                    });
                    mResLoader.LoadAsync();
                    return;
                }

                mResLoader.Add2Load<GameObject>(AddressablesResCreator.Prefix.Single + panelSearchKeys.GameObjName, (success, res) =>
                {
                    if (success)
                    {
                        onLoad(res.Asset as GameObject);
                    }
                });
                mResLoader.LoadAsync();
            }

            public void Unload()
            {
                mResLoader?.Recycle2Cache();
                mResLoader = null;
            }
        }

        protected override IPanelLoader CreatePanelLoader()
        {
            return new AddressablesPanelLoader();
        }
    }
}

#endif