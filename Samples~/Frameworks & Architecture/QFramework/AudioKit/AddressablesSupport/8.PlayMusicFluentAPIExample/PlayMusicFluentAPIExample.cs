using UnityEngine;
using QFramework;

namespace QFramework.Demos
{
	public partial class PlayMusicFluentAPIExample : ViewController
	{
		void Start()
		{
			AudioKit.Music()
				.WithName("game_bg")
				.Loop(true)
				.VolumeScale(1.0f)
				.Play();
		}
	}
}
