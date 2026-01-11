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
 * - 每次包相关资源导入/删除后
 * - 手动通过菜单 QFramework > Addressables Support > Refresh Symbols
 ****************************************************************************/

#if UNITY_EDITOR

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityPlugins.Editor
{
    /// <summary>
    /// 自动添加条件编译符号
    /// </summary>
    public static class AddressablesResKitSymbolDefiner
    {
        private const string QFRAMEWORK_RESKIT_SYMBOL = "QFRAMEWORK_RESKIT";
        private const string ADDRESSABLES_SYMBOL = "ADDRESSABLES_SUPPORT";

        [MenuItem("QFramework/Addressables Support/Refresh Symbols")]
        public static void RefreshSymbols()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[AddressablesResKit] Cannot refresh while compiling or updating.");
                return;
            }

            var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (targetGroup == BuildTargetGroup.Unknown)
            {
                return;
            }

            // 检测当前状态
            bool hasQFrameworkResKit = HasQFrameworkResKit();
            bool hasAddressables = HasAddressables();

#if UNITY_2023_1_OR_NEWER
            var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            var currentSymbols = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
#else
            var currentSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
#endif

            var symbols = currentSymbols.Split(';').Where(s => !string.IsNullOrEmpty(s)).ToList();

            // 更新符号
            bool changed = false;

            if (hasQFrameworkResKit && !symbols.Contains(QFRAMEWORK_RESKIT_SYMBOL))
            {
                symbols.Add(QFRAMEWORK_RESKIT_SYMBOL);
                changed = true;
            }
            else if (!hasQFrameworkResKit && symbols.Contains(QFRAMEWORK_RESKIT_SYMBOL))
            {
                symbols.Remove(QFRAMEWORK_RESKIT_SYMBOL);
                changed = true;
            }

            if (hasAddressables && !symbols.Contains(ADDRESSABLES_SYMBOL))
            {
                symbols.Add(ADDRESSABLES_SYMBOL);
                changed = true;
            }
            else if (!hasAddressables && symbols.Contains(ADDRESSABLES_SYMBOL))
            {
                symbols.Remove(ADDRESSABLES_SYMBOL);
                changed = true;
            }

            if (!changed)
            {
                Debug.Log($"[AddressablesResKit] Symbols already up to date: QFRAMEWORK_RESKIT={hasQFrameworkResKit}, ADDRESSABLES_SUPPORT={hasAddressables}");
                return;
            }

            var newSymbols = string.Join(";", symbols);
#if UNITY_2023_1_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, newSymbols);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, newSymbols);
#endif
            Debug.Log($"[AddressablesResKit] Updated symbols: QFRAMEWORK_RESKIT={hasQFrameworkResKit}, ADDRESSABLES_SUPPORT={hasAddressables}");
        }

        private static bool HasQFrameworkResKit()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        if (assembly.IsDynamic)
                            continue;

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
            }
            catch
            {
                // 忽略任何异常
            }
            return false;
        }

        private static bool HasAddressables()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        if (assembly.IsDynamic)
                            continue;

                        if (assembly.GetName().Name == "Unity.Addressables")
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // 忽略
                    }
                }
            }
            catch
            {
                // 忽略任何异常
            }
            return false;
        }
    }

    /// <summary>
    /// 资源导入后自动检测符号（只在导入包相关文件时触发）
    /// </summary>
    public class AddressablesResKitPostprocessor : AssetPostprocessor
    {
        // 防止重复触发的标记
        private static bool _pendingRefresh = false;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            // 如果已经有待处理的刷新，跳过
            if (_pendingRefresh)
            {
                return;
            }

            // 如果正在编译，不处理
            if (EditorApplication.isCompiling)
            {
                return;
            }

            // 检测是否有相关文件被导入或删除
            bool shouldCheck = false;

            foreach (var asset in importedAssets.Concat(deletedAssets))
            {
                // 检测 Package 变化（Addressables 安装/卸载）
                if (asset.Contains("Packages/manifest.json") ||
                    asset.Contains("Packages/packages-lock.json"))
                {
                    shouldCheck = true;
                    break;
                }

                // 检测 QFramework ResKit 相关文件
                if (asset.Contains("ResFactory") ||
                    asset.Contains("ResKit.asmdef") ||
                    asset.Contains("ResKit/"))
                {
                    shouldCheck = true;
                    break;
                }
            }

            if (shouldCheck)
            {
                _pendingRefresh = true;

                // 使用延迟调用，确保资源导入完全结束后再检测
                EditorApplication.delayCall += () =>
                {
                    _pendingRefresh = false;

                    // 再次检查是否正在编译
                    if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                    {
                        AddressablesResKitSymbolDefiner.RefreshSymbols();
                    }
                };
            }
        }
    }
}

#endif
