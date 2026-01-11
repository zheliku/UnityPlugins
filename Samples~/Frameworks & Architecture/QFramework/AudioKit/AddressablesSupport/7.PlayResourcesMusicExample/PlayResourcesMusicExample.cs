using System.Collections;
using UnityEngine;

namespace QFramework.Demos
{
	public partial class PlayResourcesMusicExample : ViewController
	{
		private void Start()
		{
			AudioKit.PlayMusic("game_bg");
		}
	}
}
