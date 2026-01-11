using UnityEngine;
using QFramework;

namespace QFramework.Demos
{
	public partial class PlaySoundByClipExample : ViewController
	{
		public AudioClip Clip;
		void Start()
		{
			AudioKit.PlaySound(Clip);
		}
	}
}
