/****************************************************************************
 * Package Path Utility
 * 
 * 统一的包路径管理工具，自动检测包信息，避免硬编码。
 * 
 * 功能：
 * - 自动从 package.json 读取包名
 * - 支持本地开发和 Package Manager 安装两种模式
 * - 提供统一的路径访问接口
 ****************************************************************************/

#if UNITY_EDITOR

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityPlugins.Editor
{
    /// <summary>
    /// 包路径工具类
    /// 提供统一的路径管理，自动检测包位置
    /// </summary>
    public static class PackagePathUtility
    {
        /// <summary>
        /// 菜单路径前缀（供 MenuItem 使用的常量）
        /// 注意：由于 MenuItem 需要编译时常量，此值在更改包名时需要手动更新
        /// </summary>
        public const string MENU_PATH = "Tools/UnityPlugins";

        // 缓存
        private static string _cachedPackageName;
        private static string _cachedPackagePath;
        private static string _cachedSamplesPath;
        private static string _cachedPackageDisplayName;
        private static bool _isInitialized;

        /// <summary>
        /// 包名（从 package.json 读取，如 com.zheliku.unityplugins）
        /// </summary>
        public static string PackageName
        {
            get
            {
                EnsureInitialized();
                return _cachedPackageName;
            }
        }

        /// <summary>
        /// 包显示名称（从 package.json 读取，如 Unity Plugins）
        /// </summary>
        public static string PackageDisplayName
        {
            get
            {
                EnsureInitialized();
                return _cachedPackageDisplayName;
            }
        }

        /// <summary>
        /// 包的根路径（本地开发时为 Packages/xxx，安装后为 Library/PackageCache/xxx@version）
        /// </summary>
        public static string PackagePath
        {
            get
            {
                EnsureInitialized();
                if (string.IsNullOrEmpty(_cachedPackagePath) || !Directory.Exists(_cachedPackagePath))
                {
                    _cachedPackagePath = FindPackagePath();
                }
                return _cachedPackagePath;
            }
        }

        /// <summary>
        /// Samples~ 文件夹路径
        /// </summary>
        public static string SamplesPath
        {
            get
            {
                EnsureInitialized();
                if (string.IsNullOrEmpty(_cachedSamplesPath) || !Directory.Exists(_cachedSamplesPath))
                {
                    string pkgPath = PackagePath;
                    if (!string.IsNullOrEmpty(pkgPath))
                    {
                        _cachedSamplesPath = Path.Combine(pkgPath, "Samples~");
                    }
                }
                return _cachedSamplesPath;
            }
        }

        /// <summary>
        /// Assets/Plugins 路径
        /// </summary>
        public static string PluginsPath => Path.Combine(Application.dataPath, "Plugins");

        /// <summary>
        /// 项目根目录路径
        /// </summary>
        public static string ProjectPath => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>
        /// 本地开发路径（Packages 文件夹）
        /// </summary>
        public static string LocalPackagesPath => Path.Combine(ProjectPath, "Packages");

        /// <summary>
        /// 是否为本地开发模式（包在 Packages 文件夹中）
        /// </summary>
        public static bool IsLocalDevelopment
        {
            get
            {
                string pkgPath = PackagePath;
                return !string.IsNullOrEmpty(pkgPath) && 
                       pkgPath.StartsWith(LocalPackagesPath, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// 强制刷新缓存
        /// </summary>
        public static void RefreshCache()
        {
            _isInitialized = false;
            _cachedPackageName = null;
            _cachedPackagePath = null;
            _cachedSamplesPath = null;
            _cachedPackageDisplayName = null;
            EnsureInitialized();
        }

        /// <summary>
        /// 确保已初始化
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_isInitialized) return;

            // 通过脚本自身位置找到包路径
            string scriptPath = GetCurrentScriptPath();
            if (!string.IsNullOrEmpty(scriptPath))
            {
                // 脚本在 Editor 文件夹中，包根目录是上一级
                string editorDir = Path.GetDirectoryName(scriptPath);
                string packageRoot = Path.GetDirectoryName(editorDir);
                
                if (!string.IsNullOrEmpty(packageRoot))
                {
                    _cachedPackagePath = packageRoot;
                    ReadPackageJson(packageRoot);
                }
            }

            // 如果上面的方法失败，尝试其他方法
            if (string.IsNullOrEmpty(_cachedPackageName))
            {
                FindPackageInfoFallback();
            }

            _isInitialized = true;
        }

        /// <summary>
        /// 获取当前脚本的文件路径
        /// </summary>
        private static string GetCurrentScriptPath()
        {
            // 方法1: 通过 MonoScript 获取
            try
            {
                var guids = AssetDatabase.FindAssets("t:MonoScript PackagePathUtility");
                foreach (var guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (assetPath.EndsWith("PackagePathUtility.cs"))
                    {
                        return Path.GetFullPath(assetPath);
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 从 package.json 读取包信息
        /// </summary>
        private static void ReadPackageJson(string packageRoot)
        {
            string packageJsonPath = Path.Combine(packageRoot, "package.json");
            
            if (!File.Exists(packageJsonPath))
            {
                Debug.LogWarning($"[PackagePathUtility] package.json 不存在: {packageJsonPath}");
                return;
            }

            try
            {
                string json = File.ReadAllText(packageJsonPath);
                
                // 简单解析 JSON（避免依赖外部库）
                _cachedPackageName = ExtractJsonValue(json, "name");
                _cachedPackageDisplayName = ExtractJsonValue(json, "displayName");

                if (string.IsNullOrEmpty(_cachedPackageDisplayName))
                {
                    _cachedPackageDisplayName = _cachedPackageName;
                }

                Debug.Log($"[PackagePathUtility] 已加载包信息: {_cachedPackageDisplayName} ({_cachedPackageName})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PackagePathUtility] 读取 package.json 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 JSON 字符串中提取值（简单实现，避免依赖）
        /// </summary>
        private static string ExtractJsonValue(string json, string key)
        {            
            // 手动查找
            string searchKey = $"\"{key}\"";
            int keyIndex = json.IndexOf(searchKey, StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            int colonIndex = json.IndexOf(':', keyIndex + searchKey.Length);
            if (colonIndex < 0) return null;

            int quoteStart = json.IndexOf('"', colonIndex + 1);
            if (quoteStart < 0) return null;

            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0) return null;

            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }

        /// <summary>
        /// 备用方法：查找包信息
        /// </summary>
        private static void FindPackageInfoFallback()
        {
            // 遍历 Packages 文件夹查找包含 Editor/PackagePathUtility.cs 的包
            string packagesPath = LocalPackagesPath;
            if (Directory.Exists(packagesPath))
            {
                foreach (var dir in Directory.GetDirectories(packagesPath))
                {
                    string utilityPath = Path.Combine(dir, "Editor", "PackagePathUtility.cs");
                    if (File.Exists(utilityPath))
                    {
                        _cachedPackagePath = dir;
                        ReadPackageJson(dir);
                        return;
                    }
                }
            }

            // 检查 Library/PackageCache
            string packageCachePath = Path.Combine(ProjectPath, "Library", "PackageCache");
            if (Directory.Exists(packageCachePath))
            {
                foreach (var dir in Directory.GetDirectories(packageCachePath))
                {
                    string utilityPath = Path.Combine(dir, "Editor", "PackagePathUtility.cs");
                    if (File.Exists(utilityPath))
                    {
                        _cachedPackagePath = dir;
                        ReadPackageJson(dir);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 查找包路径
        /// </summary>
        private static string FindPackagePath()
        {
            if (!string.IsNullOrEmpty(_cachedPackageName))
            {
                // 1. 检查本地开发路径
                var localDirs = Directory.GetDirectories(LocalPackagesPath);
                foreach (var dir in localDirs)
                {
                    string pkgJson = Path.Combine(dir, "package.json");
                    if (File.Exists(pkgJson))
                    {
                        string json = File.ReadAllText(pkgJson);
                        string name = ExtractJsonValue(json, "name");
                        if (name == _cachedPackageName)
                        {
                            return dir;
                        }
                    }
                }

                // 2. 通过 UPM API
                try
                {
                    var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{_cachedPackageName}");
                    if (packageInfo != null)
                    {
                        return packageInfo.resolvedPath;
                    }
                }
                catch { }

                // 3. 检查 PackageCache
                string packageCachePath = Path.Combine(ProjectPath, "Library", "PackageCache");
                if (Directory.Exists(packageCachePath))
                {
                    var packageDirs = Directory.GetDirectories(packageCachePath, $"{_cachedPackageName}@*");
                    if (packageDirs.Length > 0)
                    {
                        return packageDirs[0];
                    }
                }
            }

            return _cachedPackagePath;
        }

        /// <summary>
        /// 获取相对于项目根目录的路径
        /// </summary>
        public static string GetProjectRelativePath(string fullPath)
        {
            if (fullPath.StartsWith(ProjectPath, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(ProjectPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return fullPath;
        }

        /// <summary>
        /// 获取相对于 Assets 的路径
        /// </summary>
        public static string GetAssetsRelativePath(string fullPath)
        {
            if (fullPath.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets" + fullPath.Substring(Application.dataPath.Length);
            }
            return fullPath;
        }
    }
}

#endif
