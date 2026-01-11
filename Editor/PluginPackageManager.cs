/****************************************************************************
 * Plugin Package Manager
 * 
 * 编辑器窗口，用于管理 Samples~ 文件夹下的 Unity 插件包。
 * 
 * 功能：
 * - 扫描并显示所有 .unitypackage 文件
 * - 支持多选并一键导入多个包
 * - 智能检测插件是否已导入（即使移动了文件夹）
 * - 将插件导入到 Assets/Plugins 文件夹
 * - 显示导入状态和进度
 ****************************************************************************/

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnityPlugins.Editor
{
    /// <summary>
    /// 插件包信息
    /// </summary>
    [Serializable]
    public class PluginPackageInfo
    {
        public string Name;                    // 插件名称（从文件名提取）
        public string DisplayName;             // 显示名称
        public string Version;                 // 版本号
        public string Category;                // 分类
        public string FilePath;                // .unitypackage 文件路径
        public string RelativePath;            // 相对路径
        public bool IsSelected;                // 是否被选中
        public PluginImportStatus ImportStatus; // 导入状态
        public string InstalledPath;           // 已安装路径
        public List<string> DetectionKeywords; // 用于检测的关键词
    }

    /// <summary>
    /// 插件导入状态
    /// </summary>
    public enum PluginImportStatus
    {
        NotImported,    // 未导入
        Imported,       // 已导入
        Importing,      // 正在导入
        Unknown         // 未知
    }

    /// <summary>
    /// 插件包管理器窗口
    /// </summary>
    public class PluginPackageManager : EditorWindow
    {
        // 路径配置 - 使用统一的路径工具
        private static string SamplesPath => PackagePathUtility.SamplesPath;
        private static string PluginsPath => PackagePathUtility.PluginsPath;
        private const string IMPORT_RECORD_KEY = "PluginPackageManager_ImportRecords";

        /// <summary>
        /// 强制刷新 Samples 路径缓存
        /// </summary>
        public static void RefreshSamplesPathCache()
        {
            PackagePathUtility.RefreshCache();
        }

        // 插件列表
        private List<PluginPackageInfo> _packages = new List<PluginPackageInfo>();
        private Dictionary<string, bool> _categoryFoldouts = new Dictionary<string, bool>();

        // UI 状态
        private Vector2 _scrollPosition;
        private bool _isScanning;
        private bool _isImporting;
        private int _importProgress;
        private int _importTotal;
        private string _currentImportingPackage;
        private string _searchFilter = "";
        private bool _showOnlyNotImported;

        // 样式
        private GUIStyle _headerStyle;
        private GUIStyle _categoryStyle;
        private GUIStyle _packageStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _toolbarSearchStyle;
        private bool _stylesInitialized;

        // 已知插件的检测规则（插件名 -> 可能的文件夹名/脚本名）
        private static readonly Dictionary<string, string[]> KnownPluginDetectionRules = new Dictionary<string, string[]>
        {
            { "Odin Inspector", new[] { "Sirenix", "Odin Inspector", "OdinInspector" } },
            { "Console Pro", new[] { "ConsolePro", "Console Pro", "Editor Console Pro" } },
            { "Script Inspector", new[] { "FlipbookGames", "Script Inspector", "ScriptInspector" } },
            { "vFolders", new[] { "vFolders", "VFolders" } },
            { "vHierarchy", new[] { "vHierarchy", "VHierarchy" } },
            { "vInspector", new[] { "vInspector", "VInspector" } },
            { "vFavorites", new[] { "vFavorites", "VFavorites" } },
            { "Asset Inventory", new[] { "Asset Inventory", "AssetInventory" } },
            { "BG Database", new[] { "BansheeGz", "BGDatabase", "BG Database" } },
            { "QFramework", new[] { "QFramework" } },
            { "Events Pro", new[] { "EventsPro", "Events Pro" } },
            { "Auto Hand", new[] { "AutoHand", "Auto Hand" } },
        };

        [MenuItem("Tools/UnityPlugins/Plugin Package Manager", priority = 200)]
        public static void ShowWindow()
        {
            var window = GetWindow<PluginPackageManager>("插件包管理器");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        [MenuItem("Tools/UnityPlugins/Move Plugins to Plugins Folder", priority = 201)]
        public static void MovePluginsToPluginsFolder()
        {
            PluginImportPostprocessor.MoveExistingPluginsToPluginsFolder();
        }

        private void OnEnable()
        {
            // 强制刷新路径缓存，确保使用最新路径
            RefreshSamplesPathCache();
            RefreshPackageList();
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(5, 5, 0, 0)
            };

            _categoryStyle = new GUIStyle(EditorStyles.foldoutHeader)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            _packageStyle = new GUIStyle(EditorStyles.label)
            {
                padding = new RectOffset(10, 5, 2, 2)
            };

            _statusStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            EditorGUILayout.BeginVertical();

            // 标题栏
            DrawHeader();

            // 工具栏
            DrawToolbar();

            // 插件列表
            DrawPackageList();

            // 底部操作栏
            DrawFooter();

            EditorGUILayout.EndVertical();

            // 处理导入进度
            if (_isImporting)
            {
                Repaint();
            }
        }

        /// <summary>
        /// 绘制标题栏
        /// </summary>
        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("插件包管理器", _headerStyle);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                RefreshPackageList();
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制工具栏
        /// </summary>
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 搜索框
            GUILayout.Label("搜索:", GUILayout.Width(40));
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(400));

            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                _searchFilter = "";
                GUI.FocusControl(null);
            }

            GUILayout.FlexibleSpace();

            // 筛选选项
            _showOnlyNotImported = GUILayout.Toggle(_showOnlyNotImported, "仅显示未导入", EditorStyles.toolbarButton);

            EditorGUILayout.EndHorizontal();

            // 快捷操作栏
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("全选", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                SelectAll(true);
            }
            if (GUILayout.Button("取消全选", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                SelectAll(false);
            }
            if (GUILayout.Button("选择未导入", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                SelectNotImported();
            }
            if (GUILayout.Button("选择已导入", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                SelectImported();
            }

            GUILayout.FlexibleSpace();

            // 统计信息
            int selectedCount = _packages.Count(p => p.IsSelected);
            int importedCount = _packages.Count(p => p.ImportStatus == PluginImportStatus.Imported);
            GUILayout.Label($"已选: {selectedCount} | 已导入: {importedCount}/{_packages.Count}", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制插件列表
        /// </summary>
        private void DrawPackageList()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (_isScanning)
            {
                EditorGUILayout.HelpBox("正在扫描插件包...", MessageType.Info);
            }
            else if (_packages.Count == 0)
            {
                string currentPath = SamplesPath;
                bool pathExists = Directory.Exists(currentPath);

                string message = "未找到插件包。\n\n";
                message += $"当前检测路径：\n{currentPath}\n\n";
                message += $"路径状态：{(pathExists ? "✓ 存在" : "✗ 不存在")}\n\n";

                if (!pathExists)
                {
                    message += "可能的原因：\n";
                    message += "1. 包中不包含 Samples~ 文件夹\n";
                    message += "2. Git LFS 未正确配置，大文件未下载\n";
                    message += "3. 包安装不完整\n\n";
                    message += "解决方案：\n";
                    message += "点击下方按钮刷新路径或手动检查包完整性";
                }

                EditorGUILayout.HelpBox(message, MessageType.Warning);

                // 添加刷新按钮
                if (GUILayout.Button("刷新路径检测", GUILayout.Height(25)))
                {
                    RefreshSamplesPathCache();
                    RefreshPackageList();
                }
            }
            else
            {
                // 按分类显示
                var categories = _packages
                    .Where(p => MatchesFilter(p))
                    .GroupBy(p => p.Category)
                    .OrderBy(g => g.Key);

                foreach (var category in categories)
                {
                    if (!_categoryFoldouts.ContainsKey(category.Key))
                    {
                        _categoryFoldouts[category.Key] = true;
                    }

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    // 分类标题
                    EditorGUILayout.BeginHorizontal();
                    _categoryFoldouts[category.Key] = EditorGUILayout.Foldout(
                        _categoryFoldouts[category.Key],
                        $"{category.Key} ({category.Count()})",
                        true,
                        EditorStyles.foldoutHeader);

                    // 分类全选按钮
                    if (GUILayout.Button("全选", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
                    {
                        foreach (var pkg in category) pkg.IsSelected = true;
                    }
                    if (GUILayout.Button("取消", EditorStyles.miniButtonRight, GUILayout.Width(40)))
                    {
                        foreach (var pkg in category) pkg.IsSelected = false;
                    }

                    EditorGUILayout.EndHorizontal();

                    // 分类内容
                    if (_categoryFoldouts[category.Key])
                    {
                        EditorGUI.indentLevel++;
                        foreach (var package in category.OrderBy(p => p.DisplayName))
                        {
                            DrawPackageItem(package);
                        }
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制单个插件项
        /// </summary>
        private void DrawPackageItem(PluginPackageInfo package)
        {
            EditorGUILayout.BeginHorizontal();

            // 选择框
            bool wasSelected = package.IsSelected;
            package.IsSelected = EditorGUILayout.Toggle(package.IsSelected, GUILayout.Width(40));

            // 状态图标
            string statusIcon = GetStatusIcon(package.ImportStatus);
            Color statusColor = GetStatusColor(package.ImportStatus);

            var oldColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Label(statusIcon, GUILayout.Width(20));
            GUI.color = oldColor;

            // 插件名称
            string displayText = string.IsNullOrEmpty(package.Version)
                ? package.DisplayName
                : $"{package.DisplayName} (v{package.Version})";

            EditorGUILayout.LabelField(displayText, GUILayout.Width(600));

            // 状态文本
            GUILayout.FlexibleSpace();
            string statusText = GetStatusText(package);
            EditorGUILayout.LabelField(statusText, _statusStyle);

            // 操作按钮
            if (package.ImportStatus == PluginImportStatus.NotImported)
            {
                if (GUILayout.Button("导入", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    ImportSinglePackage(package);
                }
            }
            else if (package.ImportStatus == PluginImportStatus.Imported)
            {
                if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    LocateInstalledPlugin(package);
                }
                if (GUILayout.Button("卸载", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    UninstallSinglePlugin(package);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制底部操作栏
        /// </summary>
        private void DrawFooter()
        {
            EditorGUILayout.Space(5);

            // 导入进度
            if (_isImporting)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"正在导入: {_currentImportingPackage}");

                float progress = _importTotal > 0 ? (float)_importProgress / _importTotal : 0;
                EditorGUI.ProgressBar(
                    EditorGUILayout.GetControlRect(GUILayout.Height(20)),
                    progress,
                    $"{_importProgress}/{_importTotal}");

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.BeginHorizontal();

            // 打开文件夹按钮
            if (GUILayout.Button("打开 Samples~ 文件夹", GUILayout.Height(30)))
            {
                if (Directory.Exists(SamplesPath))
                {
                    EditorUtility.RevealInFinder(SamplesPath);
                }
            }

            if (GUILayout.Button("打开 Plugins 文件夹", GUILayout.Height(30)))
            {
                if (!Directory.Exists(PluginsPath))
                {
                    Directory.CreateDirectory(PluginsPath);
                    AssetDatabase.Refresh();
                }
                EditorUtility.RevealInFinder(PluginsPath);
            }

            if (GUILayout.Button("移动现有插件到Plugins", GUILayout.Height(30)))
            {
                PluginImportPostprocessor.MoveExistingPluginsToPluginsFolder();
            }

            GUILayout.FlexibleSpace();

            // 自动移动开关
            bool autoMove = PluginImportPostprocessor.EnableAutoMove;
            bool newAutoMove = GUILayout.Toggle(autoMove, "导入后自动移动到Plugins", GUILayout.Height(30));
            if (newAutoMove != autoMove)
            {
                PluginImportPostprocessor.EnableAutoMove = newAutoMove;
            }

            // 导入和卸载按钮
            int selectedCount = _packages.Count(p => p.IsSelected);
            int selectedNotImportedCount = _packages.Count(p => p.IsSelected && p.ImportStatus == PluginImportStatus.NotImported);
            int selectedImportedCount = _packages.Count(p => p.IsSelected && p.ImportStatus == PluginImportStatus.Imported);

            // 卸载按钮
            GUI.enabled = selectedImportedCount > 0 && !_isImporting;
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button($"卸载选中的插件 ({selectedImportedCount})", GUILayout.Width(180), GUILayout.Height(30)))
            {
                UninstallSelectedPlugins();
            }
            GUI.backgroundColor = Color.white;

            // 导入按钮
            GUI.enabled = selectedNotImportedCount > 0 && !_isImporting;
            if (GUILayout.Button($"导入选中的插件 ({selectedNotImportedCount})", GUILayout.Width(180), GUILayout.Height(30)))
            {
                ImportSelectedPackages();
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 刷新插件列表
        /// </summary>
        private void RefreshPackageList()
        {
            _isScanning = true;
            _packages.Clear();
            _categoryFoldouts.Clear();

            try
            {
                if (Directory.Exists(SamplesPath))
                {
                    ScanPackages(SamplesPath, "");
                }

                // 检测导入状态
                foreach (var package in _packages)
                {
                    DetectImportStatus(package);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PluginPackageManager] 扫描插件包时出错: {ex.Message}");
            }
            finally
            {
                _isScanning = false;
            }

            Repaint();
        }

        /// <summary>
        /// 递归扫描插件包
        /// </summary>
        private void ScanPackages(string path, string category)
        {
            // 扫描 .unitypackage 文件
            var files = Directory.GetFiles(path, "*.unitypackage");
            foreach (var file in files)
            {
                var package = CreatePackageInfo(file, category);
                if (package != null)
                {
                    _packages.Add(package);
                }
            }

            // 递归扫描子目录
            var directories = Directory.GetDirectories(path);
            foreach (var dir in directories)
            {
                string dirName = Path.GetFileName(dir);
                string newCategory = string.IsNullOrEmpty(category) ? dirName : $"{category}/{dirName}";
                ScanPackages(dir, newCategory);
            }
        }

        /// <summary>
        /// 创建插件包信息
        /// </summary>
        private PluginPackageInfo CreatePackageInfo(string filePath, string category)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            // 解析插件名称和版本
            var (name, version) = ParsePackageName(fileName);

            var package = new PluginPackageInfo
            {
                Name = name,
                DisplayName = name,
                Version = version,
                Category = string.IsNullOrEmpty(category) ? "未分类" : category,
                FilePath = filePath,
                RelativePath = GetRelativePath(filePath),
                IsSelected = false,
                ImportStatus = PluginImportStatus.Unknown,
                DetectionKeywords = GetDetectionKeywords(name)
            };

            return package;
        }

        /// <summary>
        /// 解析包名和版本号
        /// </summary>
        private (string name, string version) ParsePackageName(string fileName)
        {
            // 尝试匹配常见的版本号模式
            // 例如: "Odin Inspector and Serializer 4.0.1.2" 或 "vFolders 2 v2.1.13"

            // 模式1: name v1.2.3 或 name 1.2.3
            var versionMatch = Regex.Match(fileName, @"^(.+?)\s+v?(\d+\.\d+[\.\d]*)\s*.*$", RegexOptions.IgnoreCase);
            if (versionMatch.Success)
            {
                return (versionMatch.Groups[1].Value.Trim(), versionMatch.Groups[2].Value);
            }

            // 模式2: name [1.2.3, ...] 
            var bracketMatch = Regex.Match(fileName, @"^(.+?)\s*\[(\d+\.\d+[\.\d]*)", RegexOptions.IgnoreCase);
            if (bracketMatch.Success)
            {
                return (bracketMatch.Groups[1].Value.Trim(), bracketMatch.Groups[2].Value);
            }

            return (fileName, "");
        }

        /// <summary>
        /// 获取检测关键词
        /// </summary>
        private List<string> GetDetectionKeywords(string packageName)
        {
            var keywords = new List<string>();

            // 检查已知规则
            foreach (var rule in KnownPluginDetectionRules)
            {
                if (packageName.IndexOf(rule.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    keywords.AddRange(rule.Value);
                    return keywords;
                }
            }

            // 生成默认关键词
            keywords.Add(packageName);

            // 移除版本号和特殊字符生成变体
            string cleanName = Regex.Replace(packageName, @"[\s\-_\.]+", "");
            if (cleanName != packageName)
            {
                keywords.Add(cleanName);
            }

            // 取第一个单词作为关键词
            string firstWord = packageName.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrEmpty(firstWord) && !keywords.Contains(firstWord))
            {
                keywords.Add(firstWord);
            }

            return keywords;
        }

        /// <summary>
        /// 检测插件导入状态
        /// </summary>
        private void DetectImportStatus(PluginPackageInfo package)
        {
            package.ImportStatus = PluginImportStatus.NotImported;
            package.InstalledPath = "";

            // 搜索 Assets 目录下是否存在匹配的文件夹
            var searchPaths = new[]
            {
                Application.dataPath,                    // Assets
                Path.Combine(Application.dataPath, "Plugins"),  // Assets/Plugins
            };

            foreach (var searchPath in searchPaths)
            {
                if (!Directory.Exists(searchPath)) continue;

                foreach (var keyword in package.DetectionKeywords)
                {
                    // 搜索匹配的目录
                    var matchingDirs = FindMatchingDirectories(searchPath, keyword);
                    if (matchingDirs.Any())
                    {
                        package.ImportStatus = PluginImportStatus.Imported;
                        package.InstalledPath = matchingDirs.First();
                        return;
                    }

                    // 搜索匹配的 asmdef 文件
                    var matchingAsmdefs = FindMatchingAsmdefs(searchPath, keyword);
                    if (matchingAsmdefs.Any())
                    {
                        package.ImportStatus = PluginImportStatus.Imported;
                        package.InstalledPath = Path.GetDirectoryName(matchingAsmdefs.First());
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 查找匹配的目录
        /// </summary>
        private IEnumerable<string> FindMatchingDirectories(string searchPath, string keyword)
        {
            try
            {
                return Directory.GetDirectories(searchPath, "*", SearchOption.AllDirectories)
                    .Where(d => Path.GetFileName(d).Equals(keyword, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// 查找匹配的 asmdef 文件
        /// </summary>
        private IEnumerable<string> FindMatchingAsmdefs(string searchPath, string keyword)
        {
            try
            {
                return Directory.GetFiles(searchPath, "*.asmdef", SearchOption.AllDirectories)
                    .Where(f => Path.GetFileNameWithoutExtension(f).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// 导入单个插件包
        /// </summary>
        private void ImportSinglePackage(PluginPackageInfo package)
        {
            if (!File.Exists(package.FilePath))
            {
                EditorUtility.DisplayDialog("错误", $"插件包文件不存在：\n{package.FilePath}", "确定");
                return;
            }

            // 确保 Plugins 文件夹存在
            EnsurePluginsFolderExists();

            // 注册关键词用于导入后自动移动
            if (PluginImportPostprocessor.EnableAutoMove)
            {
                PluginImportPostprocessor.AddPendingPlugin(package.DetectionKeywords);
            }

            // 导入包
            AssetDatabase.ImportPackage(package.FilePath, false);

            // 注意：ImportPackage 是异步的，状态更新需要在导入完成后刷新
        }

        /// <summary>
        /// 导入选中的插件包
        /// </summary>
        private void ImportSelectedPackages()
        {
            var selectedPackages = _packages.Where(p => p.IsSelected && p.ImportStatus != PluginImportStatus.Imported).ToList();

            if (selectedPackages.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有需要导入的插件包。\n已导入的插件不会重复导入。", "确定");
                return;
            }

            // 确保 Plugins 文件夹存在
            EnsurePluginsFolderExists();

            // 显示确认对话框
            string packageNames = string.Join("\n", selectedPackages.Select(p => $"• {p.DisplayName}"));
            bool confirm = EditorUtility.DisplayDialog(
                "确认导入",
                $"即将导入以下 {selectedPackages.Count} 个插件包：\n\n{packageNames}\n\n" +
                "注意：导入过程中请不要进行其他操作。",
                "开始导入",
                "取消");

            if (!confirm) return;

            // 开始批量导入
            _isImporting = true;
            _importProgress = 0;
            _importTotal = selectedPackages.Count;

            // 收集所有插件的检测关键词，用于导入后自动移动
            if (PluginImportPostprocessor.EnableAutoMove)
            {
                var allKeywords = selectedPackages.SelectMany(p => p.DetectionKeywords).Distinct();
                PluginImportPostprocessor.AddPendingPlugin(allKeywords);
            }

            // 逐个导入（因为 Unity 的 ImportPackage 需要用户确认）
            foreach (var package in selectedPackages)
            {
                _currentImportingPackage = package.DisplayName;
                _importProgress++;

                if (File.Exists(package.FilePath))
                {
                    package.ImportStatus = PluginImportStatus.Importing;
                    AssetDatabase.ImportPackage(package.FilePath, false);
                }

                Repaint();
            }

            _isImporting = false;

            // 导入完成后刷新状态
            EditorApplication.delayCall += () =>
            {
                RefreshPackageList();
                EditorUtility.DisplayDialog("完成", "插件包导入流程已完成。\n请检查各插件是否正确导入。", "确定");
            };
        }

        /// <summary>
        /// 确保 Plugins 文件夹存在
        /// </summary>
        private void EnsurePluginsFolderExists()
        {
            if (!Directory.Exists(PluginsPath))
            {
                Directory.CreateDirectory(PluginsPath);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// 定位已安装的插件
        /// </summary>
        private void LocateInstalledPlugin(PluginPackageInfo package)
        {
            if (string.IsNullOrEmpty(package.InstalledPath) || !Directory.Exists(package.InstalledPath))
            {
                // 重新检测
                DetectImportStatus(package);
            }

            if (!string.IsNullOrEmpty(package.InstalledPath) && Directory.Exists(package.InstalledPath))
            {
                // 转换为 Assets 相对路径
                string relativePath = "Assets" + package.InstalledPath.Substring(Application.dataPath.Length);
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relativePath);
                if (obj != null)
                {
                    EditorGUIUtility.PingObject(obj);
                    Selection.activeObject = obj;
                }
                else
                {
                    EditorUtility.RevealInFinder(package.InstalledPath);
                }
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "无法定位插件安装位置。", "确定");
            }
        }

        /// <summary>
        /// 检查是否匹配筛选条件
        /// </summary>
        private bool MatchesFilter(PluginPackageInfo package)
        {
            // 检查导入状态筛选
            if (_showOnlyNotImported && package.ImportStatus == PluginImportStatus.Imported)
            {
                return false;
            }

            // 检查搜索关键词
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                bool matchesName = package.DisplayName.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                bool matchesCategory = package.Category.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                return matchesName || matchesCategory;
            }

            return true;
        }

        /// <summary>
        /// 全选/取消全选
        /// </summary>
        private void SelectAll(bool selected)
        {
            foreach (var package in _packages.Where(p => MatchesFilter(p)))
            {
                package.IsSelected = selected;
            }
        }

        /// <summary>
        /// 选择所有未导入的插件
        /// </summary>
        private void SelectNotImported()
        {
            foreach (var package in _packages)
            {
                package.IsSelected = package.ImportStatus == PluginImportStatus.NotImported && MatchesFilter(package);
            }
        }

        /// <summary>
        /// 选择所有已导入的插件
        /// </summary>
        private void SelectImported()
        {
            foreach (var package in _packages)
            {
                package.IsSelected = package.ImportStatus == PluginImportStatus.Imported && MatchesFilter(package);
            }
        }

        /// <summary>
        /// 卸载单个插件
        /// </summary>
        private void UninstallSinglePlugin(PluginPackageInfo package)
        {
            if (package.ImportStatus != PluginImportStatus.Imported)
            {
                EditorUtility.DisplayDialog("提示", "该插件未安装。", "确定");
                return;
            }

            // 重新检测安装路径
            if (string.IsNullOrEmpty(package.InstalledPath) || !Directory.Exists(package.InstalledPath))
            {
                DetectImportStatus(package);
            }

            if (string.IsNullOrEmpty(package.InstalledPath) || !Directory.Exists(package.InstalledPath))
            {
                EditorUtility.DisplayDialog("错误", "无法定位插件安装位置，无法卸载。", "确定");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "确认卸载",
                $"确定要卸载插件 \"{package.DisplayName}\" 吗？\n\n" +
                $"将删除文件夹：\n{GetAssetsRelativePath(package.InstalledPath)}\n\n" +
                "此操作不可撤销！",
                "确认卸载",
                "取消");

            if (!confirm) return;

            DeletePluginFolder(package);
        }

        /// <summary>
        /// 卸载选中的插件
        /// </summary>
        private void UninstallSelectedPlugins()
        {
            var selectedPackages = _packages.Where(p => p.IsSelected && p.ImportStatus == PluginImportStatus.Imported).ToList();

            if (selectedPackages.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有选中已导入的插件。", "确定");
                return;
            }

            // 验证所有插件的安装路径
            foreach (var package in selectedPackages)
            {
                if (string.IsNullOrEmpty(package.InstalledPath) || !Directory.Exists(package.InstalledPath))
                {
                    DetectImportStatus(package);
                }
            }

            var validPackages = selectedPackages.Where(p => !string.IsNullOrEmpty(p.InstalledPath) && Directory.Exists(p.InstalledPath)).ToList();

            if (validPackages.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "无法定位选中插件的安装位置。", "确定");
                return;
            }

            // 显示确认对话框
            string packageDetails = string.Join("\n", validPackages.Select(p => $"• {p.DisplayName}\n   路径: {GetAssetsRelativePath(p.InstalledPath)}"));
            bool confirm = EditorUtility.DisplayDialog(
                "确认批量卸载",
                $"确定要卸载以下 {validPackages.Count} 个插件吗？\n\n{packageDetails}\n\n" +
                "此操作不可撤销！",
                "确认卸载",
                "取消");

            if (!confirm) return;

            int successCount = 0;
            int failCount = 0;

            foreach (var package in validPackages)
            {
                try
                {
                    DeletePluginFolder(package, false);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PluginPackageManager] 卸载 {package.DisplayName} 失败: {ex.Message}");
                    failCount++;
                }
            }

            AssetDatabase.Refresh();
            RefreshPackageList();

            EditorUtility.DisplayDialog(
                "卸载完成",
                $"成功卸载: {successCount} 个\n失败: {failCount} 个",
                "确定");
        }

        /// <summary>
        /// 删除插件文件夹
        /// </summary>
        private void DeletePluginFolder(PluginPackageInfo package, bool refreshAfter = true)
        {
            if (string.IsNullOrEmpty(package.InstalledPath) || !Directory.Exists(package.InstalledPath))
            {
                return;
            }

            string relativePath = GetAssetsRelativePath(package.InstalledPath);

            // 使用 AssetDatabase 删除（这样会同时删除 .meta 文件）
            if (AssetDatabase.DeleteAsset(relativePath))
            {
                Debug.Log($"[PluginPackageManager] 已卸载: {package.DisplayName} ({relativePath})");
                package.ImportStatus = PluginImportStatus.NotImported;
                package.InstalledPath = "";
            }
            else
            {
                // 如果 AssetDatabase 删除失败，尝试直接删除
                try
                {
                    Directory.Delete(package.InstalledPath, true);

                    // 删除对应的 .meta 文件
                    string metaPath = package.InstalledPath + ".meta";
                    if (File.Exists(metaPath))
                    {
                        File.Delete(metaPath);
                    }

                    Debug.Log($"[PluginPackageManager] 已卸载 (直接删除): {package.DisplayName}");
                    package.ImportStatus = PluginImportStatus.NotImported;
                    package.InstalledPath = "";
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PluginPackageManager] 删除失败: {ex.Message}");
                    throw;
                }
            }

            if (refreshAfter)
            {
                AssetDatabase.Refresh();
                RefreshPackageList();
            }
        }

        /// <summary>
        /// 获取 Assets 相对路径
        /// </summary>
        private string GetAssetsRelativePath(string fullPath)
        {
            if (fullPath.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets" + fullPath.Substring(Application.dataPath.Length);
            }
            return fullPath;
        }

        /// <summary>
        /// 获取状态图标
        /// </summary>
        private string GetStatusIcon(PluginImportStatus status)
        {
            switch (status)
            {
                case PluginImportStatus.Imported: return "✓";
                case PluginImportStatus.NotImported: return "○";
                case PluginImportStatus.Importing: return "↻";
                default: return "?";
            }
        }

        /// <summary>
        /// 获取状态颜色
        /// </summary>
        private Color GetStatusColor(PluginImportStatus status)
        {
            switch (status)
            {
                case PluginImportStatus.Imported: return Color.green;
                case PluginImportStatus.NotImported: return Color.gray;
                case PluginImportStatus.Importing: return Color.yellow;
                default: return Color.white;
            }
        }

        /// <summary>
        /// 获取状态文本
        /// </summary>
        private string GetStatusText(PluginPackageInfo package)
        {
            switch (package.ImportStatus)
            {
                case PluginImportStatus.Imported:
                    return $"已导入";
                case PluginImportStatus.NotImported:
                    return "未导入";
                case PluginImportStatus.Importing:
                    return "导入中...";
                default:
                    return "未知";
            }
        }

        /// <summary>
        /// 获取相对路径
        /// </summary>
        private string GetRelativePath(string fullPath)
        {
            if (fullPath.StartsWith(SamplesPath, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(SamplesPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return fullPath;
        }
    }

    /// <summary>
    /// 插件导入后处理器
    /// 用于在插件导入完成后自动将其移动到 Assets/Plugins 文件夹
    /// </summary>
    public class PluginImportPostprocessor : AssetPostprocessor
    {
        // 待处理的插件关键词（由 PluginPackageManager 设置）
        private static HashSet<string> _pendingPluginKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 需要排除的文件夹（不应该移动到 Plugins）
        private static readonly HashSet<string> ExcludedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Plugins",
            "Editor",
            "Resources",
            "StreamingAssets",
            "Gizmos",
            "Standard Assets",
            "Pro Standard Assets",
            "Samples",
            "QFramework",
            "QFrameworkData"
        };

        // 插件移动配置
        private const string PREFS_KEY_ENABLE_AUTO_MOVE = "PluginPackageManager_EnableAutoMove";

        /// <summary>
        /// 启用/禁用自动移动功能
        /// </summary>
        public static bool EnableAutoMove
        {
            get => EditorPrefs.GetBool(PREFS_KEY_ENABLE_AUTO_MOVE, true);
            set => EditorPrefs.SetBool(PREFS_KEY_ENABLE_AUTO_MOVE, value);
        }

        /// <summary>
        /// 添加待处理的插件关键词
        /// </summary>
        public static void AddPendingPlugin(IEnumerable<string> keywords)
        {
            foreach (var keyword in keywords)
            {
                _pendingPluginKeywords.Add(keyword);
            }
        }

        /// <summary>
        /// 清除待处理的插件关键词
        /// </summary>
        public static void ClearPendingPlugins()
        {
            _pendingPluginKeywords.Clear();
        }

        /// <summary>
        /// 获取当前待处理的关键词数量
        /// </summary>
        public static int PendingCount => _pendingPluginKeywords.Count;

        /// <summary>
        /// 资源导入完成后的回调
        /// </summary>
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!EnableAutoMove || _pendingPluginKeywords.Count == 0)
            {
                return;
            }

            // 查找新导入的顶级文件夹
            var topLevelFolders = new HashSet<string>();

            foreach (var asset in importedAssets)
            {
                // 解析顶级文件夹
                if (!asset.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relativePath = asset.Substring("Assets/".Length);
                int slashIndex = relativePath.IndexOf('/');

                string topFolder;
                if (slashIndex > 0)
                {
                    topFolder = relativePath.Substring(0, slashIndex);
                }
                else if (AssetDatabase.IsValidFolder(asset))
                {
                    topFolder = relativePath;
                }
                else
                {
                    continue;
                }

                // 排除不应移动的文件夹
                if (ExcludedFolders.Contains(topFolder))
                {
                    continue;
                }

                // 检查是否在 Plugins 文件夹下
                if (asset.StartsWith("Assets/Plugins/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                topLevelFolders.Add(topFolder);
            }

            // 检查并移动匹配的文件夹
            var foldersToMove = new List<string>();

            foreach (var folder in topLevelFolders)
            {
                bool shouldMove = _pendingPluginKeywords.Any(keyword =>
                    folder.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    keyword.IndexOf(folder, StringComparison.OrdinalIgnoreCase) >= 0);

                if (shouldMove)
                {
                    foldersToMove.Add(folder);
                }
            }

            if (foldersToMove.Count > 0)
            {
                EditorApplication.delayCall += () =>
                {
                    MoveFoldersToPlugins(foldersToMove);
                    _pendingPluginKeywords.Clear();
                };
            }
        }

        /// <summary>
        /// 将文件夹移动到 Plugins 目录
        /// </summary>
        private static void MoveFoldersToPlugins(List<string> folderNames)
        {
            string pluginsPath = "Assets/Plugins";

            if (!AssetDatabase.IsValidFolder(pluginsPath))
            {
                AssetDatabase.CreateFolder("Assets", "Plugins");
            }

            int movedCount = 0;
            var failedMoves = new List<string>();

            foreach (var folderName in folderNames)
            {
                string sourcePath = $"Assets/{folderName}";
                string destPath = $"Assets/Plugins/{folderName}";

                if (!AssetDatabase.IsValidFolder(sourcePath))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(destPath))
                {
                    Debug.Log($"[PluginImportPostprocessor] 目标文件夹已存在，将覆盖: {destPath}");
                    AssetDatabase.DeleteAsset(destPath);
                }

                string error = AssetDatabase.MoveAsset(sourcePath, destPath);

                if (string.IsNullOrEmpty(error))
                {
                    Debug.Log($"[PluginImportPostprocessor] 已移动插件: {sourcePath} -> {destPath}");
                    movedCount++;
                }
                else
                {
                    Debug.LogWarning($"[PluginImportPostprocessor] 移动失败: {sourcePath} -> {destPath}, 错误: {error}");
                    failedMoves.Add(folderName);
                }
            }

            if (movedCount > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log($"[PluginImportPostprocessor] 已将 {movedCount} 个插件移动到 Assets/Plugins");
            }

            if (failedMoves.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "插件移动警告",
                    $"以下插件无法自动移动到 Plugins 文件夹：\n" +
                    string.Join("\n", failedMoves.Select(f => $"• {f}")) +
                    "\n\n请手动移动这些文件夹。",
                    "确定");
            }
        }

        /// <summary>
        /// 手动触发移动操作（用于已导入但未移动的插件）
        /// </summary>
        public static void MoveExistingPluginsToPluginsFolder()
        {
            var assetsPath = Application.dataPath;
            var directories = Directory.GetDirectories(assetsPath);
            var pluginCandidates = new List<string>();

            foreach (var dir in directories)
            {
                string folderName = Path.GetFileName(dir);

                if (ExcludedFolders.Contains(folderName))
                {
                    continue;
                }

                if (IsLikelyPluginFolder(dir))
                {
                    pluginCandidates.Add(folderName);
                }
            }

            if (pluginCandidates.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有找到需要移动的插件文件夹。", "确定");
                return;
            }

            string folderList = string.Join("\n", pluginCandidates.Select(f => $"• {f}"));
            bool confirm = EditorUtility.DisplayDialog(
                "移动插件到 Plugins 文件夹",
                $"找到 {pluginCandidates.Count} 个可能的插件文件夹：\n\n{folderList}\n\n" +
                "是否将它们移动到 Assets/Plugins 文件夹？",
                "移动",
                "取消");

            if (confirm)
            {
                MoveFoldersToPlugins(pluginCandidates);
            }
        }

        /// <summary>
        /// 检查文件夹是否可能是插件
        /// </summary>
        private static bool IsLikelyPluginFolder(string folderPath)
        {
            try
            {
                if (Directory.GetFiles(folderPath, "*.asmdef", SearchOption.AllDirectories).Any())
                    return true;

                if (Directory.GetDirectories(folderPath, "Editor", SearchOption.AllDirectories).Any())
                    return true;

                if (Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories).Any())
                    return true;

                if (Directory.GetFiles(folderPath, "*.dll", SearchOption.AllDirectories).Any())
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}

#endif
