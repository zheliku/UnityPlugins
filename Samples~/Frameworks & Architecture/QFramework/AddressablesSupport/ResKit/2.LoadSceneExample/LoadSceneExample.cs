using UnityEngine;

namespace QFramework.Example
{
	public class LoadSceneExample : MonoBehaviour
	{
		private ResLoader mResLoader = null;

		void Start()
		{
			ResKit.Init();

			mResLoader = ResLoader.Allocate();

			// 同步加载，加载场景无需指定 addr:// 前缀
			// mResLoader.LoadAddressableSceneSync("SceneRes");

			// 异步加载
			// mResLoader.LoadAddressableSceneAsync("SceneRes");

			// 异步加载
			mResLoader.LoadAddressableSceneAsync("SceneRes", onStartLoading: handle =>
			{
				// 做一些加载操作
				handle.Completed += asyncOperation =>
				{
					Debug.Log("SceneRes loaded");
				};
			});
		}

		private void OnDestroy()
		{
			mResLoader.Recycle2Cache();
			mResLoader = null;
		}
	}
}