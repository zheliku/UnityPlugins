using UnityEngine;

// 1.请在菜单 编辑器扩展/Namespace Settings 里设置命名空间
// 2.命名空间更改后，生成代码之后，需要把逻辑代码文件（非 Designer）的命名空间手动更改
namespace QFramework.Example
{
    public partial class v1_1_PlaySoundExmaple : ViewController
    {
        private void OnGUI()
        {
            IMGUIHelper.SetDesignResolution(640, 480);

            GUILayout.BeginVertical("Box");
            if (GUILayout.Button("Play Sound"))
            {
                AudioKit.Sound()
                    .WithName("button_clicked")
                    .VolumeScale(0.7f)
                    .PlaySoundMode(AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames)
                    .Play()
                    .OnFinish(() => { "OnSoundFinish".LogInfo(); });
            }
            
            GUILayout.EndVertical();
        }
    }
}