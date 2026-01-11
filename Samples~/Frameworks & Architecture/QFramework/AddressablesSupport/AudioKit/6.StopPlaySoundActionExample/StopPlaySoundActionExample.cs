using System.Collections;
using UnityEngine;
using QFramework;

namespace QFramework.Demos
{
	public partial class StopPlaySoundActionExample : ViewController
	{
		private IEnumerator Start()
		{
			var sequence = ActionKit.Sequence()
				.PlaySound("game_bg")
				.Start(this);
			
			yield return new WaitForSeconds(1.0f);
			
			sequence.Deinit();
		}
	}
}
