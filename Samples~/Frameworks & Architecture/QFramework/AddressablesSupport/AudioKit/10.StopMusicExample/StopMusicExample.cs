using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFramework;

namespace QFramework.Demos
{
	public partial class StopMusicExample : ViewController
	{
		IEnumerator Start()
		{
			AudioKit.PlayMusic("home_bg");

			yield return new WaitForSeconds(2.0f);

			AudioKit.StopMusic();
		}
	}
}
