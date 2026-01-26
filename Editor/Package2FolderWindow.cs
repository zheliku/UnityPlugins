using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeStage.PackageToFolder
{
    /// <summary>
    /// 批量导入 Unity Package 的编辑器窗口
    /// 支持拖拽多个 .unitypackage 文件，指定目标文件夹，选择导入模式
    /// </summary>
    public class Package2FolderWindow : EditorWindow
    {
        private List<string> packagePaths = new List<string>();
        private string targetFolderPath = "Assets";
        private Vector2 packageListScrollPosition;
        private Vector2 windowScrollPosition;
        private bool silentMode = true;

        private GUIStyle dropAreaStyle;
        private GUIStyle packageItemStyle;
        private GUIStyle headerStyle;
        private bool stylesInitialized = false;

        [MenuItem("Tools/Package2Folder Window")]
        [MenuItem("Assets/Import Package/Package2Folder Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<Package2FolderWindow>("批量导入 Package");
            window.minSize = new Vector2(400, 350);
            window.Show();
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            dropAreaStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };
            dropAreaStyle.normal.textColor = Color.gray;

            packageItemStyle = new GUIStyle(EditorStyles.label)
            {
                padding = new RectOffset(5, 5, 2, 2),
                margin = new RectOffset(0, 0, 1, 1)
            };
            packageItemStyle.normal.background = EditorGUIUtility.whiteTexture;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13
            };

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            // 整个窗口支持滚动
            windowScrollPosition = EditorGUILayout.BeginScrollView(windowScrollPosition);

            EditorGUILayout.Space(10);

            // 标题
            EditorGUILayout.LabelField("批量导入 Unity Package", headerStyle);
            EditorGUILayout.Space(5);

            // 目标文件夹选择
            DrawTargetFolderSection();

            EditorGUILayout.Space(10);

            // 拖拽区域
            DrawDropArea();

            EditorGUILayout.Space(10);

            // 包列表
            DrawPackageList();

            EditorGUILayout.Space(10);

            // 导入选项和按钮
            DrawImportOptions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawTargetFolderSection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标文件夹:", GUILayout.Width(80));

            // 显示当前选择的文件夹
            EditorGUILayout.TextField(targetFolderPath);

            // 选择文件夹按钮
            if (GUILayout.Button("选择...", GUILayout.Width(60)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("选择目标文件夹", "Assets", "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    // 标准化路径格式进行比较
                    string normalizedSelected = NormalizePath(selectedPath);
                    string assetsPath = NormalizePath(Application.dataPath);
                    string projectRoot = NormalizePath(Path.GetDirectoryName(Application.dataPath));

                    if (normalizedSelected.StartsWith(assetsPath, System.StringComparison.OrdinalIgnoreCase))
                    {
                        // Assets 文件夹内
                        if (normalizedSelected.Length == assetsPath.Length)
                        {
                            targetFolderPath = "Assets";
                        }
                        else
                        {
                            targetFolderPath = "Assets" + normalizedSelected.Substring(assetsPath.Length);
                        }
                    }
                    else if (normalizedSelected.StartsWith(projectRoot, System.StringComparison.OrdinalIgnoreCase))
                    {
                        // 项目根目录内（可能是 Packages 等）
                        targetFolderPath = normalizedSelected.Substring(projectRoot.Length + 1);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "请选择项目内的文件夹 (Assets 或 Packages)", "确定");
                    }
                }
            }

            // 使用当前选中的文件夹
            if (GUILayout.Button("使用选中", GUILayout.Width(70)))
            {
                string selected = GetSelectedFolderInProject();
                if (!string.IsNullOrEmpty(selected))
                {
                    targetFolderPath = selected;
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "请在 Project 窗口中选中一个文件夹", "确定");
                }
            }

            EditorGUILayout.EndHorizontal();

            // 验证路径
            if (!AssetDatabase.IsValidFolder(targetFolderPath))
            {
                EditorGUILayout.HelpBox("目标文件夹不存在，导入时将自动创建", MessageType.Warning);
            }
        }

        private void DrawDropArea()
        {
            Rect dropArea = GUILayoutUtility.GetRect(0, 80, GUILayout.ExpandWidth(true));

            GUI.Box(dropArea, "拖拽 .unitypackage 文件到这里\n或点击添加", dropAreaStyle);

            // 处理点击事件
            if (Event.current.type == EventType.MouseDown && dropArea.Contains(Event.current.mousePosition))
            {
                AddPackagesFromDialog();
                Event.current.Use();
            }

            // 处理拖拽事件
            HandleDragAndDrop(dropArea);
        }

        private void HandleDragAndDrop(Rect dropArea)
        {
            Event evt = Event.current;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        foreach (string path in DragAndDrop.paths)
                        {
                            if (path.EndsWith(".unitypackage", System.StringComparison.OrdinalIgnoreCase))
                            {
                                if (!packagePaths.Contains(path))
                                {
                                    packagePaths.Add(path);
                                }
                            }
                        }
                        Repaint();
                    }
                    evt.Use();
                    break;
            }
        }

        private void AddPackagesFromDialog()
        {
            string path = EditorUtility.OpenFilePanel("选择 Unity Package", "", "unitypackage");
            if (!string.IsNullOrEmpty(path) && !packagePaths.Contains(path))
            {
                packagePaths.Add(path);
            }
        }

        private void DrawPackageList()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"待导入列表 ({packagePaths.Count} 个)", headerStyle);

            if (packagePaths.Count > 0)
            {
                if (GUILayout.Button("清空", GUILayout.Width(50)))
                {
                    packagePaths.Clear();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (packagePaths.Count == 0)
            {
                EditorGUILayout.HelpBox("列表为空，请拖拽或点击上方区域添加 .unitypackage 文件", MessageType.Info);
                return;
            }

            // 计算列表高度
            float itemHeight = EditorGUIUtility.singleLineHeight + 4;
            float listHeight = Mathf.Clamp(packagePaths.Count * (itemHeight + 2) + 2, 60, 180);

            // 滚动列表
            packageListScrollPosition = EditorGUILayout.BeginScrollView(packageListScrollPosition, GUILayout.Height(listHeight));

            int indexToRemove = -1;
            int hoveredIndex = -1;
            Rect hoveredRect = Rect.zero;

            for (int i = 0; i < packagePaths.Count; i++)
            {
                Rect itemRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(itemHeight));

                // 序号
                GUILayout.Label($"{i + 1}.", GUILayout.Width(25), GUILayout.Height(itemHeight));

                // 文件名
                string fileName = Path.GetFileName(packagePaths[i]);
                GUILayout.Label(fileName, GUILayout.Height(itemHeight));

                // 弹性空间
                // GUILayout.FlexibleSpace();

                // 文件是否存在的状态
                bool exists = File.Exists(packagePaths[i]);
                if (!exists)
                {
                    GUILayout.Label("⚠", GUILayout.Width(20), GUILayout.Height(itemHeight));
                }

                // 删除按钮
                if (GUILayout.Button("×", GUILayout.Width(22), GUILayout.Height(itemHeight - 2)))
                {
                    indexToRemove = i;
                }

                EditorGUILayout.EndHorizontal();

                // 检查鼠标悬停
                if (itemRect.Contains(Event.current.mousePosition))
                {
                    hoveredIndex = i;
                    hoveredRect = itemRect;
                }
            }

            EditorGUILayout.EndScrollView();

            // 在滚动区域外绘制 Tooltip，避免被遮挡
            if (hoveredIndex >= 0 && Event.current.type == EventType.Repaint)
            {
                string tooltipText = packagePaths[hoveredIndex];
                GUIStyle tooltipStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    wordWrap = true,
                    fontSize = 10
                };

                Vector2 tooltipSize = tooltipStyle.CalcSize(new GUIContent(tooltipText));
                tooltipSize.x = Mathf.Min(tooltipSize.x, 350);
                tooltipSize.y = tooltipStyle.CalcHeight(new GUIContent(tooltipText), tooltipSize.x);

                // 获取鼠标位置并在其上方显示tooltip
                Vector2 mousePos = Event.current.mousePosition;
                Rect tooltipRect = new Rect(
                    mousePos.x,
                    mousePos.y - tooltipSize.y - 10,  // 在鼠标上方稍微偏上的位置
                    tooltipSize.x,
                    tooltipSize.y
                );

                EditorGUI.LabelField(tooltipRect, tooltipText, tooltipStyle);

                // 请求重绘以保持 tooltip 更新
                Repaint();
            }

            if (indexToRemove >= 0)
            {
                packagePaths.RemoveAt(indexToRemove);
            }
        }

        private void DrawImportOptions()
        {
            // 导入模式选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("导入模式:", GUILayout.Width(80));

            if (GUILayout.Toggle(silentMode, "静默 (直接导入)", EditorStyles.radioButton))
            {
                silentMode = true;
            }

            if (GUILayout.Toggle(!silentMode, "交互式 (逐个预览)", EditorStyles.radioButton))
            {
                silentMode = false;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 模式说明
            if (silentMode)
            {
                EditorGUILayout.HelpBox("静默模式：直接导入所有包，不显示预览窗口", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("交互模式：逐个显示导入预览窗口，可以选择要导入的资源", MessageType.Info);
            }

            EditorGUILayout.Space(10);

            // 导入按钮
            EditorGUI.BeginDisabledGroup(packagePaths.Count == 0);

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button($"开始导入 ({packagePaths.Count} 个包)", GUILayout.Height(35)))
            {
                StartImport();
            }
            GUI.backgroundColor = Color.white;

            EditorGUI.EndDisabledGroup();
        }

        private void StartImport()
        {
            // 验证
            var validPaths = packagePaths.Where(p => File.Exists(p)).ToArray();

            if (validPaths.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "没有有效的 .unitypackage 文件", "确定");
                return;
            }

            if (validPaths.Length != packagePaths.Count)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "警告",
                    $"有 {packagePaths.Count - validPaths.Length} 个文件不存在，是否继续导入其余 {validPaths.Length} 个包?",
                    "继续", "取消"
                );
                if (!proceed) return;
            }

            // 确保目标文件夹存在
            if (!AssetDatabase.IsValidFolder(targetFolderPath))
            {
                bool create = EditorUtility.DisplayDialog(
                    "创建文件夹",
                    $"目标文件夹 '{targetFolderPath}' 不存在，是否创建?",
                    "创建", "取消"
                );

                if (create)
                {
                    CreateFolderRecursively(targetFolderPath);
                }
                else
                {
                    return;
                }
            }

            // 开始导入
            if (silentMode)
            {
                // 静默批量导入
                foreach (var path in validPaths)
                {
                    Package2Folder.ImportPackageToFolder(path, targetFolderPath, false);
                }

                Debug.Log($"[Package2Folder] 已静默导入 {validPaths.Length} 个包到 {targetFolderPath}");

                // 清空列表
                packagePaths.Clear();

                EditorUtility.DisplayDialog("完成", $"已静默导入 {validPaths.Length} 个包", "确定");
            }
            else
            {
                // 交互式导入 - 使用队列
                Package2Folder.ImportPackagesToFolderInteractive(validPaths, targetFolderPath);

                Debug.Log($"[Package2Folder] 开始交互式导入 {validPaths.Length} 个包到 {targetFolderPath}");

                // 清空列表
                packagePaths.Clear();
            }
        }

        private void CreateFolderRecursively(string folderPath)
        {
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

        private string GetSelectedFolderInProject()
        {
            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
                return null;

            var assetGuid = Selection.assetGUIDs[0];
            var path = AssetDatabase.GUIDToAssetPath(assetGuid);

            if (Directory.Exists(path))
                return path;

            // 如果选中的是文件，返回其所在文件夹
            if (File.Exists(path))
                return Path.GetDirectoryName(path).Replace('\\', '/');

            return null;
        }

        /// <summary>
        /// 标准化路径格式，统一使用正斜杠并移除末尾斜杠
        /// </summary>
        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
