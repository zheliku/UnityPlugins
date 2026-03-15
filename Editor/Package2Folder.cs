using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityPlugins.Editor
{
    /// <summary>
    /// 批量导入 Unity Package 到指定文件夹的核心实现
    /// 使用公开 API + 导入后移动的方式
    /// </summary>
    [InitializeOnLoad]
    public static class Package2Folder
    {
        ///////////////////////////////////////////////////////////////
        // Import Queue for sequential interactive import
        // 使用 SessionState 持久化，避免域重载丢失数据
        ///////////////////////////////////////////////////////////////

        private const string QUEUE_KEY = "UnityPlugins_Package2Folder_ImportQueue";
        private const string FOLDER_KEY = "UnityPlugins_Package2Folder_TargetFolder";
        private const string PROCESSING_KEY = "UnityPlugins_Package2Folder_IsProcessing";
        private const string WAITING_IMPORT_KEY = "UnityPlugins_Package2Folder_WaitingImport";
        private const string SILENT_MODE_KEY = "UnityPlugins_Package2Folder_SilentMode";
        private const string ASSETS_BEFORE_KEY = "UnityPlugins_Package2Folder_AssetsBeforeImport";

        private static bool isSubscribedToImportEvents = false;

        static Package2Folder()
        {
            // 订阅导入事件
            SubscribeToImportEvents();

            // 域重载后恢复队列处理
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(PROCESSING_KEY, false))
                {
                    // 如果正在等待导入完成，不要立即处理下一个
                    if (SessionState.GetBool(WAITING_IMPORT_KEY, false))
                    {
                        // 导入已完成（域重载说明导入完成了），处理下一个
                        SessionState.SetBool(WAITING_IMPORT_KEY, false);
                    }

                    // 检查是否还有未处理的包
                    var queueData = SessionState.GetString(QUEUE_KEY, "");
                    if (!string.IsNullOrEmpty(queueData))
                    {
                        ProcessNextInQueue();
                    }
                    else
                    {
                        // 队列为空，检查是否需要移动资源
                        bool isSilentMode = SessionState.GetBool(SILENT_MODE_KEY, false);
                        if (!isSilentMode)
                        {
                            // 交互模式：所有包导入完成，现在移动资源
                            MoveAllImportedAssets();
                        }

                        SessionState.SetBool(PROCESSING_KEY, false);
                        Debug.Log("[Package2Folder] 所有包导入完成");
                    }
                }
            };
        }

        private static void SubscribeToImportEvents()
        {
            if (isSubscribedToImportEvents) return;
            isSubscribedToImportEvents = true;

            AssetDatabase.importPackageCompleted += OnImportPackageCompleted;
            AssetDatabase.importPackageCancelled += OnImportPackageCancelled;
            AssetDatabase.importPackageFailed += OnImportPackageFailed;
        }

        private static void OnImportPackageCompleted(string packageName)
        {
            if (!SessionState.GetBool(PROCESSING_KEY, false)) return;

            Debug.Log($"[Package2Folder] 导入完成: {packageName}");
            SessionState.SetBool(WAITING_IMPORT_KEY, false);

            bool isSilentMode = SessionState.GetBool(SILENT_MODE_KEY, false);

            // 静默模式：立即移动当前包的资源
            if (isSilentMode)
            {
                MoveSinglePackageAssets();
            }

            // 延迟处理下一个，等待 Unity 完成所有后续操作
            EditorApplication.delayCall += ProcessNextInQueue;
        }

        private static void OnImportPackageCancelled(string packageName)
        {
            if (!SessionState.GetBool(PROCESSING_KEY, false)) return;

            Debug.Log($"[Package2Folder] 导入取消: {packageName}");
            SessionState.SetBool(WAITING_IMPORT_KEY, false);

            // 询问是否继续
            var remainingCount = GetQueueCount();
            if (remainingCount > 0)
            {
                bool continueImport = EditorUtility.DisplayDialog(
                    "继续导入?",
                    $"还有 {remainingCount} 个包等待导入。\n是否继续?",
                    "继续", "取消全部"
                );

                if (continueImport)
                {
                    EditorApplication.delayCall += ProcessNextInQueue;
                }
                else
                {
                    ClearQueue();
                    Debug.Log("[Package2Folder] 已取消所有导入");
                }
            }
            else
            {
                ClearQueue();
            }
        }

        private static void OnImportPackageFailed(string packageName, string errorMessage)
        {
            if (!SessionState.GetBool(PROCESSING_KEY, false)) return;

            Debug.LogError($"[Package2Folder] 导入失败: {packageName} - {errorMessage}");
            SessionState.SetBool(WAITING_IMPORT_KEY, false);

            // 继续处理下一个
            EditorApplication.delayCall += ProcessNextInQueue;
        }

        ///////////////////////////////////////////////////////////////
        // Queue Management
        ///////////////////////////////////////////////////////////////

        private static void EnqueuePackages(string[] packagePaths, string folderPath)
        {
            // 将路径序列化存储
            var existingQueue = SessionState.GetString(QUEUE_KEY, "");
            var paths = string.IsNullOrEmpty(existingQueue)
                ? new List<string>()
                : new List<string>(existingQueue.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));

            foreach (var path in packagePaths)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    paths.Add(path);
                }
            }

            SessionState.SetString(QUEUE_KEY, string.Join("|", paths));
            SessionState.SetString(FOLDER_KEY, folderPath);
        }

        private static (string packagePath, string folderPath)? DequeuePackage()
        {
            var queueData = SessionState.GetString(QUEUE_KEY, "");
            var folderPath = SessionState.GetString(FOLDER_KEY, "");

            if (string.IsNullOrEmpty(queueData))
                return null;

            var paths = new List<string>(queueData.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            if (paths.Count == 0)
                return null;

            var packagePath = paths[0];
            paths.RemoveAt(0);

            SessionState.SetString(QUEUE_KEY, string.Join("|", paths));

            return (packagePath, folderPath);
        }

        private static int GetQueueCount()
        {
            var queueData = SessionState.GetString(QUEUE_KEY, "");
            if (string.IsNullOrEmpty(queueData))
                return 0;
            return queueData.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private static void ClearQueue()
        {
            SessionState.EraseString(QUEUE_KEY);
            SessionState.EraseString(FOLDER_KEY);
            SessionState.SetBool(PROCESSING_KEY, false);
            SessionState.SetBool(WAITING_IMPORT_KEY, false);
            SessionState.EraseBool(SILENT_MODE_KEY);
            SessionState.EraseString(ASSETS_BEFORE_KEY);
        }

        ///////////////////////////////////////////////////////////////
        // Public API for external use
        ///////////////////////////////////////////////////////////////

        /// <summary>
        /// 交互式批量导入多个包到指定文件夹（逐个显示导入窗口）
        /// 注意：所有包导入完成后才会移动到目标文件夹
        /// </summary>
        /// <param name="packagePaths">包路径数组</param>
        /// <param name="targetFolderPath">目标文件夹路径</param>
        public static void ImportPackagesToFolderInteractive(string[] packagePaths, string targetFolderPath)
        {
            if (packagePaths == null || packagePaths.Length == 0) return;

            // 记录导入前的所有资源
            var assetsBeforeImport = new HashSet<string>(AssetDatabase.GetAllAssetPaths());
            SessionState.SetString(ASSETS_BEFORE_KEY, string.Join("|", assetsBeforeImport));

            // 设置为交互模式
            SessionState.SetBool(SILENT_MODE_KEY, false);

            EnqueuePackages(packagePaths, targetFolderPath);
            ProcessNextInQueue();
        }

        /// <summary>
        /// 静默批量导入多个包到指定文件夹（不显示导入窗口）
        /// </summary>
        /// <param name="packagePaths">包路径数组</param>
        /// <param name="targetFolderPath">目标文件夹路径</param>
        public static void ImportPackagesToFolderSilent(string[] packagePaths, string targetFolderPath)
        {
            if (packagePaths == null || packagePaths.Length == 0) return;

            // 设置为静默模式
            SessionState.SetBool(SILENT_MODE_KEY, true);

            EnqueuePackages(packagePaths, targetFolderPath);
            ProcessNextInQueue();
        }

        ///////////////////////////////////////////////////////////////
        // Queue Processing
        ///////////////////////////////////////////////////////////////

        /// <summary>
        /// 处理队列中的下一个包
        /// </summary>
        private static void ProcessNextInQueue()
        {
            // 如果正在等待导入完成，不要处理下一个
            if (SessionState.GetBool(WAITING_IMPORT_KEY, false))
            {
                return;
            }

            var queueCount = GetQueueCount();

            if (queueCount == 0)
            {
                if (SessionState.GetBool(PROCESSING_KEY, false))
                {
                    // 所有包导入完成
                    bool silentMode = SessionState.GetBool(SILENT_MODE_KEY, false);

                    // 交互模式：现在统一移动所有导入的资源
                    if (!silentMode)
                    {
                        MoveAllImportedAssets();
                    }

                    ClearQueue();
                    Debug.Log("[Package2Folder] 所有包导入完成");
                }
                return;
            }

            SessionState.SetBool(PROCESSING_KEY, true);
            var item = DequeuePackage();

            if (item == null)
            {
                ClearQueue();
                return;
            }

            var (packagePath, folderPath) = item.Value;
            bool isSilentMode = SessionState.GetBool(SILENT_MODE_KEY, false);

            Debug.Log($"[Package2Folder] 正在导入: {Path.GetFileName(packagePath)} (剩余 {GetQueueCount()} 个)");
            Debug.Log($"[Package2Folder] 模式: {(isSilentMode ? "静默" : "交互")}");

            // 设置等待标志，防止在导入完成前处理下一个
            SessionState.SetBool(WAITING_IMPORT_KEY, true);

            try
            {
                // 静默模式：记录导入前的资源（用于导入后立即移动）
                if (isSilentMode)
                {
                    var assetsBeforeImport = new HashSet<string>(AssetDatabase.GetAllAssetPaths());
                    SessionState.SetString(ASSETS_BEFORE_KEY, string.Join("|", assetsBeforeImport));
                }

                // 使用 Unity 公开 API 导入（静默或交互式）
                bool showImportWindow = !isSilentMode;
                Debug.Log($"[Package2Folder] 调用 ImportPackage, showImportWindow = {showImportWindow}");
                AssetDatabase.ImportPackage(packagePath, showImportWindow);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Package2Folder] 导入失败: {packagePath} - {ex.Message}");
                SessionState.SetBool(WAITING_IMPORT_KEY, false);
                EditorApplication.delayCall += ProcessNextInQueue;
            }
        }

        ///////////////////////////////////////////////////////////////
        // Asset Movement Helpers
        ///////////////////////////////////////////////////////////////

        /// <summary>
        /// 静默模式：移动单个包的资源
        /// </summary>
        private static void MoveSinglePackageAssets()
        {
            var targetFolderPath = SessionState.GetString(FOLDER_KEY, "");
            if (string.IsNullOrEmpty(targetFolderPath) || targetFolderPath == "Assets")
            {
                return;
            }

            try
            {
                var assetsBeforeStr = SessionState.GetString(ASSETS_BEFORE_KEY, "");
                if (string.IsNullOrEmpty(assetsBeforeStr))
                {
                    return;
                }

                var assetsBeforeImport = new HashSet<string>(assetsBeforeStr.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
                AssetDatabase.Refresh();

                var assetsAfterImport = AssetDatabase.GetAllAssetPaths();
                var newAssets = new List<string>();
                foreach (var asset in assetsAfterImport)
                {
                    if (!assetsBeforeImport.Contains(asset) && asset.StartsWith("Assets/"))
                    {
                        newAssets.Add(asset);
                    }
                }

                if (newAssets.Count > 0)
                {
                    MoveAssetsToTargetFolder(newAssets, targetFolderPath);
                    Debug.Log($"[Package2Folder] 已移动 {newAssets.Count} 个资源到 {targetFolderPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Package2Folder] 移动资源时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 交互模式：所有包导入完成后，统一移动资源到目标文件夹
        /// </summary>
        private static void MoveAllImportedAssets()
        {
            var targetFolderPath = SessionState.GetString(FOLDER_KEY, "");
            if (string.IsNullOrEmpty(targetFolderPath) || targetFolderPath == "Assets")
            {
                return;
            }

            try
            {
                // 获取最初导入前的资源列表
                var assetsBeforeStr = SessionState.GetString(ASSETS_BEFORE_KEY, "");
                if (string.IsNullOrEmpty(assetsBeforeStr))
                {
                    Debug.LogWarning("[Package2Folder] 无法找到导入前的资源列表");
                    return;
                }

                var assetsBeforeImport = new HashSet<string>(assetsBeforeStr.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));

                // 刷新资源数据库
                AssetDatabase.Refresh();

                // 获取所有新导入的资源
                var assetsAfterImport = AssetDatabase.GetAllAssetPaths();
                var newAssets = new List<string>();
                foreach (var asset in assetsAfterImport)
                {
                    if (!assetsBeforeImport.Contains(asset) && asset.StartsWith("Assets/"))
                    {
                        newAssets.Add(asset);
                    }
                }

                // 移动资源到目标文件夹
                if (newAssets.Count > 0)
                {
                    Debug.Log($"[Package2Folder] 开始移动 {newAssets.Count} 个资源到 {targetFolderPath}");
                    MoveAssetsToTargetFolder(newAssets, targetFolderPath);
                    Debug.Log($"[Package2Folder] 已将所有导入的资源移动到 {targetFolderPath}");
                }
                else
                {
                    Debug.LogWarning("[Package2Folder] 未检测到新导入的资源");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Package2Folder] 移动资源时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 移动资源到目标文件夹
        /// </summary>
        private static void MoveAssetsToTargetFolder(List<string> assetPaths, string targetFolderPath)
        {
            if (assetPaths == null || assetPaths.Count == 0) return;
            if (string.IsNullOrEmpty(targetFolderPath)) return;

            // 确保目标文件夹存在
            EnsureFolderExists(targetFolderPath);

            // 按层级排序，先处理顶层文件夹
            assetPaths.Sort((a, b) => a.Split('/').Length.CompareTo(b.Split('/').Length));

            // 记录已移动的文件夹，避免重复移动
            var movedFolders = new HashSet<string>();

            foreach (var assetPath in assetPaths)
            {
                // 跳过已经在目标文件夹中的资源
                if (assetPath.StartsWith(targetFolderPath + "/")) continue;

                // 获取资源在 Assets 下的相对路径
                string relativePath = assetPath.Substring("Assets/".Length);

                // 检查是否是某个已移动文件夹的子项
                bool isSubItemOfMovedFolder = false;
                foreach (var movedFolder in movedFolders)
                {
                    if (assetPath.StartsWith(movedFolder + "/"))
                    {
                        isSubItemOfMovedFolder = true;
                        break;
                    }
                }

                if (isSubItemOfMovedFolder) continue;

                // 构建目标路径
                string targetPath = Path.Combine(targetFolderPath, relativePath).Replace('\\', '/');

                try
                {
                    // 如果是文件夹，记录它
                    if (AssetDatabase.IsValidFolder(assetPath))
                    {
                        movedFolders.Add(assetPath);
                    }

                    // 移动资源
                    string error = AssetDatabase.MoveAsset(assetPath, targetPath);
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogWarning($"[Package2Folder] 移动资源失败: {assetPath} -> {targetPath}: {error}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Package2Folder] 移动资源异常: {assetPath} -> {targetPath}: {ex.Message}");
                }
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 确保文件夹存在，如果不存在则递归创建
        /// </summary>
        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            string currentPath = parts[0]; // "Assets" or "Packages"

            for (int i = 1; i < parts.Length; i++)
            {
                string newPath = currentPath + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(newPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                }
                currentPath = newPath;
            }
        }
    }
}
