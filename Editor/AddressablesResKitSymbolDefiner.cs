/****************************************************************************
 * Addressables ResKit Symbol Definer
 * 
 * 编辑器脚本，自动检测项目中是否存在 QFramework ResKit 和 Addressables，
 * 并自动添加对应的条件编译符号。
 * 
 * 定义的符号：
 * - QFRAMEWORK_RESKIT    : 当 QFramework.ResKit 存在时
 * - ADDRESSABLES_SUPPORT : 当 Unity Addressables 包存在时
 * 
 * 检测时机：
 * - 编辑器启动时
 * - 每次资源导入后
 * - 手动通过菜单 Tools > Addressables ResKit > Refresh Symbols
 ****************************************************************************/

#if UNITY_EDITOR

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace QFramework.Editor
{
    /// <summary>
    /// 自动添加条件编译符号
    /// </summary>
    [InitializeOnLoad]
    public static class AddressablesResKitSymbolDefiner
    {
        private const string QFRAMEWORK_RESKIT_SYMBOL = "QFRAMEWORK_RESKIT";
        private const string ADDRESSABLES_SYMBOL = "ADDRESSABLES_SUPPORT";

        private static bool _updatePending = false;

        static AddressablesResKitSymbolDefiner()
        {
            // 延迟执行，确保所有程序集都已加载
            RequestUpdateSymbols();
        }

        /// <summary>
        /// 请求更新符号（防止重复添加到 delayCall）
        /// </summary>
        public static void RequestUpdateSymbols()
        {
            if (_updatePending) return;
            _updatePending = true;
            EditorApplication.delayCall += () =>
            {
                _updatePending = false;
                UpdateSymbols();
            };
        }

        [MenuItem("Tools/Addressables ResKit/Refresh Symbols")]
        public static void UpdateSymbols()
        {
            var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (targetGroup == BuildTargetGroup.Unknown)
            {
                return;
            }

#if UNITY_2023_1_OR_NEWER
            var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            var currentSymbols = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
#else
            var currentSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
#endif

            var symbols = currentSymbols.Split(';').Where(s => !string.IsNullOrEmpty(s)).ToList();
            bool changed = false;

            // 检测 QFramework ResKit
            bool hasQFrameworkResKit = HasQFrameworkResKit();
            changed |= UpdateSymbol(symbols, QFRAMEWORK_RESKIT_SYMBOL, hasQFrameworkResKit);

            // 检测 Addressables
            bool hasAddressables = HasAddressables();
            changed |= UpdateSymbol(symbols, ADDRESSABLES_SYMBOL, hasAddressables);

            if (changed)
            {
                var newSymbols = string.Join(";", symbols);
#if UNITY_2023_1_OR_NEWER
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, newSymbols);
#else
                PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, newSymbols);
#endif
                Debug.Log($"[AddressablesResKit] Updated symbols: QFRAMEWORK_RESKIT={hasQFrameworkResKit}, ADDRESSABLES_SUPPORT={hasAddressables}");
            }
        }

        private static bool UpdateSymbol(System.Collections.Generic.List<string> symbols, string symbol, bool shouldExist)
        {
            bool exists = symbols.Contains(symbol);

            if (shouldExist && !exists)
            {
                symbols.Add(symbol);
                return true;
            }
            else if (!shouldExist && exists)
            {
                symbols.Remove(symbol);
                return true;
            }

            return false;
        }

        private static bool HasQFrameworkResKit()
        {
            // 方法1：检测类型是否存在
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    if (assembly.GetType("QFramework.ResFactory") != null)
                    {
                        return true;
                    }
                }
                catch
                {
                    // 忽略无法加载的程序集
                }
            }

            // 方法2：检测文件是否存在
            var guids = AssetDatabase.FindAssets("ResFactory t:Script");
            return guids.Length > 0;
        }

        private static bool HasAddressables()
        {
            // 方法1：检测类型是否存在
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (assembly.GetName().Name == "Unity.Addressables")
                {
                    return true;
                }
            }

            // 方法2：检测包是否安装
#if UNITY_2019_4_OR_NEWER
            var listRequest = UnityEditor.PackageManager.Client.List(true);
            while (!listRequest.IsCompleted)
            {
                System.Threading.Thread.Sleep(10);
            }
            if (listRequest.Status == UnityEditor.PackageManager.StatusCode.Success)
            {
                return listRequest.Result.Any(p => p.name == "com.unity.addressables");
            }
#endif
            return false;
        }
    }

    /// <summary>
    /// 资源导入后自动检测符号
    /// </summary>
    public class AddressablesResKitPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            // 检测是否有相关文件被导入或删除
            bool shouldCheck = false;

            foreach (var asset in importedAssets.Concat(deletedAssets))
            {
                if (asset.Contains("QFramework") ||
                    asset.Contains("ResKit") ||
                    asset.Contains("Addressables") ||
                    asset.EndsWith(".asmdef"))
                {
                    shouldCheck = true;
                    break;
                }
            }

            if (shouldCheck)
            {
                // 延迟执行，确保资源导入完成
                AddressablesResKitSymbolDefiner.RequestUpdateSymbols();
            }
        }
    }
}

#endif
