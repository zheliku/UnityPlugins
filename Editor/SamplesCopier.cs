/****************************************************************************
 * Samples Copier
 * 
 * 编辑器脚本，将 Assets/UnityPlugins/Samples 文件夹的内容复制到 
 * Packages/UnityPlugins/Samples~ 文件夹。
 * 
 * 特点：
 * - 仅覆盖源文件夹中已有的文件
 * - 不删除 Samples~ 中独有的文件和文件夹
 * - 自动创建不存在的目录结构
 ****************************************************************************/

#if UNITY_EDITOR

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityPlugins.Editor
{
    /// <summary>
    /// Samples 文件夹复制工具
    /// </summary>
    public static class SamplesCopier
    {
        // 源文件夹路径（Assets 下的 Samples，通过包显示名称自动获取）
        private static string SourcePath
        {
            get
            {
                // 尝试使用包显示名称作为 Assets 下的文件夹名
                string displayName = PackagePathUtility.PackageDisplayName;
                if (!string.IsNullOrEmpty(displayName))
                {
                    string path = Path.GetFullPath(Path.Combine(Application.dataPath, displayName, "Samples"));
                    if (Directory.Exists(path))
                    {
                        return path;
                    }
                }

                // 备用：使用包名的最后一段
                string packageName = PackagePathUtility.PackageName;
                if (!string.IsNullOrEmpty(packageName))
                {
                    string[] parts = packageName.Split('.');
                    string folderName = parts[parts.Length - 1];
                    string path = Path.GetFullPath(Path.Combine(Application.dataPath, folderName, "Samples"));
                    if (Directory.Exists(path))
                    {
                        return path;
                    }
                }

                // 最后备用
                return Path.GetFullPath(Path.Combine(Application.dataPath, "UnityPlugins/Samples"));
            }
        }

        // 目标文件夹路径（使用统一路径工具）
        private static string DestinationPath => PackagePathUtility.SamplesPath;

        [MenuItem("Tools/UnityPlugins/Copy Samples to Package", priority = 100)]
        public static void CopySamplesToPackage()
        {
            try
            {
                if (!Directory.Exists(SourcePath))
                {
                    EditorUtility.DisplayDialog("错误", $"源文件夹不存在：\n{SourcePath}", "确定");
                    return;
                }

                // 确保目标文件夹存在
                if (!Directory.Exists(DestinationPath))
                {
                    Directory.CreateDirectory(DestinationPath);
                    Debug.Log($"[SamplesCopier] 创建目标文件夹：{DestinationPath}");
                }

                int copiedCount = 0;
                int skippedCount = 0;
                int createdDirCount = 0;

                // 递归复制文件夹内容
                CopyDirectoryContents(SourcePath, DestinationPath, ref copiedCount, ref skippedCount, ref createdDirCount);

                string message = $"复制完成！\n\n" +
                                 $"复制文件数：{copiedCount}\n" +
                                 $"跳过 .meta 文件数：{skippedCount}\n" +
                                 $"创建文件夹数：{createdDirCount}";

                Debug.Log($"[SamplesCopier] {message.Replace("\n\n", " ").Replace("\n", ", ")}");
                EditorUtility.DisplayDialog("Samples 复制工具", message, "确定");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SamplesCopier] 复制过程中发生错误：{ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("错误", $"复制过程中发生错误：\n{ex.Message}", "确定");
            }
        }

        /// <summary>
        /// 递归复制目录内容
        /// </summary>
        /// <param name="sourceDir">源目录</param>
        /// <param name="destDir">目标目录</param>
        /// <param name="copiedCount">已复制文件计数</param>
        /// <param name="skippedCount">跳过的 .meta 文件计数</param>
        /// <param name="createdDirCount">创建的目录计数</param>
        private static void CopyDirectoryContents(string sourceDir, string destDir,
            ref int copiedCount, ref int skippedCount, ref int createdDirCount)
        {
            // 获取源目录中的所有文件
            string[] files = Directory.GetFiles(sourceDir);
            foreach (string filePath in files)
            {
                string fileName = Path.GetFileName(filePath);

                // 跳过 .meta 文件（Unity 特有的元数据文件，Samples~ 不需要）
                if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    skippedCount++;
                    continue;
                }

                // 确保目标目录存在
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                    createdDirCount++;
                    Debug.Log($"[SamplesCopier] 创建目录：{GetRelativePath(destDir)}");
                }

                string destFilePath = Path.Combine(destDir, fileName);

                // 复制文件（覆盖已存在的文件）
                File.Copy(filePath, destFilePath, overwrite: true);
                copiedCount++;
                Debug.Log($"[SamplesCopier] 复制文件：{GetRelativePath(destFilePath)}");
            }

            // 获取源目录中的所有子目录
            string[] directories = Directory.GetDirectories(sourceDir);
            foreach (string dirPath in directories)
            {
                string dirName = Path.GetFileName(dirPath);
                string destSubDir = Path.Combine(destDir, dirName);

                // 递归复制子目录
                CopyDirectoryContents(dirPath, destSubDir, ref copiedCount, ref skippedCount, ref createdDirCount);
            }
        }

        /// <summary>
        /// 获取相对于项目根目录的路径（用于日志显示）
        /// </summary>
        private static string GetRelativePath(string fullPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return fullPath;
        }

        [MenuItem("Tools/UnityPlugins/Open Samples Folder", priority = 101)]
        public static void OpenSamplesFolder()
        {
            if (Directory.Exists(SourcePath))
            {
                EditorUtility.RevealInFinder(SourcePath);
            }
            else
            {
                EditorUtility.DisplayDialog("错误", $"文件夹不存在：\n{SourcePath}", "确定");
            }
        }

        [MenuItem("Tools/UnityPlugins/Open Samples~ Folder", priority = 102)]
        public static void OpenSamplesTildeFolder()
        {
            if (Directory.Exists(DestinationPath))
            {
                EditorUtility.RevealInFinder(DestinationPath);
            }
            else
            {
                EditorUtility.DisplayDialog("错误", $"文件夹不存在：\n{DestinationPath}", "确定");
            }
        }
    }
}

#endif
