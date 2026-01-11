using System.Collections;
using UnityEngine;
using QFramework;

namespace QFramework.Demos
{
	public partial class PauseResumeMusicExample : ViewController
	{
		IEnumerator Start()
		{
			AudioKit.PlayMusic("home_bg");

			yield return new WaitForSeconds(2.0f);

			AudioKit.PauseMusic();

			yield return new WaitForSeconds(2.0f);

			AudioKit.ResumeMusic();
		}
	}
}
