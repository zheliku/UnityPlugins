// new argument was added in 19.1.4

#if UNITY_2019_3_OR_NEWER
#define CS_P2F_NEW_ARGUMENT_2
#elif (UNITY_2019_1_OR_NEWER && !UNITY_2019_1_0 && !UNITY_2019_1_1 && !UNITY_2019_1_2 && !UNITY_2019_1_3) || (UNITY_2018_4_OR_NEWER && !UNITY_2018_4_0 && !UNITY_2018_4_1 && !UNITY_2018_4_2)
#define CS_P2F_NEW_ARGUMENT
#endif

#if UNITY_2019_3_OR_NEWER
#define CS_P2F_NEW_NON_INTERACTIVE_LOGIC
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;

namespace CodeStage.PackageToFolder
{
	[InitializeOnLoad]
	public static class Package2Folder
	{
		///////////////////////////////////////////////////////////////
		// Import Queue for sequential interactive import
		// 使用 SessionState 持久化，避免域重载丢失数据
		///////////////////////////////////////////////////////////////

		private const string QUEUE_KEY = "Package2Folder_ImportQueue";
		private const string FOLDER_KEY = "Package2Folder_TargetFolder";
		private const string PROCESSING_KEY = "Package2Folder_IsProcessing";
		private const string WAITING_IMPORT_KEY = "Package2Folder_WaitingImport";

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
						SessionState.SetBool(PROCESSING_KEY, false);
						UnityEngine.Debug.Log("[Package2Folder] 所有包导入完成");
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

			UnityEngine.Debug.Log($"[Package2Folder] 导入完成: {packageName}");
			SessionState.SetBool(WAITING_IMPORT_KEY, false);

			// 延迟处理下一个，等待 Unity 完成所有后续操作
			EditorApplication.delayCall += ProcessNextInQueue;
		}

		private static void OnImportPackageCancelled(string packageName)
		{
			if (!SessionState.GetBool(PROCESSING_KEY, false)) return;

			UnityEngine.Debug.Log($"[Package2Folder] 导入取消: {packageName}");
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
					UnityEngine.Debug.Log("[Package2Folder] 已取消所有导入");
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

			UnityEngine.Debug.LogError($"[Package2Folder] 导入失败: {packageName} - {errorMessage}");
			SessionState.SetBool(WAITING_IMPORT_KEY, false);

			// 继续处理下一个
			EditorApplication.delayCall += ProcessNextInQueue;
		}

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
		}

		///////////////////////////////////////////////////////////////
		// Public API for external use
		///////////////////////////////////////////////////////////////

		/// <summary>
		/// 交互式批量导入多个包到指定文件夹（逐个显示导入窗口）
		/// </summary>
		/// <param name="packagePaths">包路径数组</param>
		/// <param name="targetFolderPath">目标文件夹路径</param>
		public static void ImportPackagesToFolderInteractive(string[] packagePaths, string targetFolderPath)
		{
			if (packagePaths == null || packagePaths.Length == 0) return;

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

			foreach (var packagePath in packagePaths)
			{
				if (string.IsNullOrEmpty(packagePath)) continue;
				if (!File.Exists(packagePath)) continue;
				ImportPackageToFolder(packagePath, targetFolderPath, false);
			}
		}

		///////////////////////////////////////////////////////////////
		// Delegates and properties with caching for reflection stuff
		///////////////////////////////////////////////////////////////

		#region reflection stuff

#if CS_P2F_NEW_ARGUMENT_2
		private delegate object[] ExtractAndPrepareAssetListDelegate(string packagePath, out string packageIconPath, out string packageManagerDependenciesPath);
#elif CS_P2F_NEW_ARGUMENT
		private delegate object[] ExtractAndPrepareAssetListDelegate(string packagePath, out string packageIconPath, out bool allowReInstall, out string packageManagerDependenciesPath);
#else
		private delegate object[] ExtractAndPrepareAssetListDelegate(string packagePath, out string packageIconPath, out bool allowReInstall);
#endif

		private static Type packageUtilityType;
		private static Type PackageUtilityType
		{
			get
			{
				if (packageUtilityType == null)
					packageUtilityType = typeof(MenuItem).Assembly.GetType("UnityEditor.PackageUtility");
				return packageUtilityType;
			}
		}

		private static ExtractAndPrepareAssetListDelegate extractAndPrepareAssetList;
		private static ExtractAndPrepareAssetListDelegate ExtractAndPrepareAssetList
		{
			get
			{
				if (extractAndPrepareAssetList == null)
				{
					var method = PackageUtilityType.GetMethod("ExtractAndPrepareAssetList");
					if (method == null)
						throw new Exception("Couldn't extract method with ExtractAndPrepareAssetListDelegate delegate!");

					extractAndPrepareAssetList = (ExtractAndPrepareAssetListDelegate)Delegate.CreateDelegate(
					   typeof(ExtractAndPrepareAssetListDelegate),
					   null,
					   method);
				}

				return extractAndPrepareAssetList;
			}
		}

		private static FieldInfo destinationAssetPathFieldInfo;
		private static FieldInfo DestinationAssetPathFieldInfo
		{
			get
			{
				if (destinationAssetPathFieldInfo == null)
				{
					var importPackageItem = typeof(MenuItem).Assembly.GetType("UnityEditor.ImportPackageItem");
					destinationAssetPathFieldInfo = importPackageItem.GetField("destinationAssetPath");
				}
				return destinationAssetPathFieldInfo;
			}
		}

		private static MethodInfo importPackageAssetsMethodInfo;
		private static MethodInfo ImportPackageAssetsMethodInfo
		{
			get
			{
				if (importPackageAssetsMethodInfo == null)
					importPackageAssetsMethodInfo = PackageUtilityType.GetMethod("ImportPackageAssets");

				return importPackageAssetsMethodInfo;
			}
		}

		private static MethodInfo importPackageAssetsWithOriginMethodInfo;
		private static MethodInfo ImportPackageAssetsWithOriginMethodInfo
		{
			get
			{
				if (importPackageAssetsWithOriginMethodInfo == null)
					importPackageAssetsWithOriginMethodInfo = PackageUtilityType.GetMethod("ImportPackageAssetsWithOrigin");

				return importPackageAssetsWithOriginMethodInfo;
			}
		}

		private static MethodInfo showImportPackageMethodInfo;
		private static MethodInfo ShowImportPackageMethodInfo
		{
			get
			{
				if (showImportPackageMethodInfo == null)
				{
					var packageImport = typeof(MenuItem).Assembly.GetType("UnityEditor.PackageImport");
					showImportPackageMethodInfo = packageImport.GetMethod("ShowImportPackage");
				}

				return showImportPackageMethodInfo;
			}
		}

		#endregion reflection stuff

		///////////////////////////////////////////////////////////////
		// Unity Editor menus integration
		///////////////////////////////////////////////////////////////

		[MenuItem("Assets/Import Package/Here...", true)]
		[MenuItem("Assets/Import Package/Here... (Silent)", true)]
		private static bool IsImportToFolderCheck()
		{
			var selectedFolderPath = GetSelectedFolderPath();
			return !string.IsNullOrEmpty(selectedFolderPath);
		}

		[MenuItem("Assets/Import Package/Here...", false)]
		private static void Package2FolderCommand()
		{
			string[] packagePaths = OpenMultipleUnityPackages();
			if (packagePaths == null || packagePaths.Length == 0) return;

			var selectedFolderPath = GetSelectedFolderPath();

			// 使用队列式交互导入 - 逐个显示导入窗口
			EnqueuePackages(packagePaths, selectedFolderPath);
			ProcessNextInQueue();
		}

		[MenuItem("Assets/Import Package/Here... (Silent)", false)]
		private static void Package2FolderSilentCommand()
		{
			string[] packagePaths = OpenMultipleUnityPackages();
			if (packagePaths == null || packagePaths.Length == 0) return;

			var selectedFolderPath = GetSelectedFolderPath();

			// 静默批量导入 - 不显示导入窗口
			foreach (var packagePath in packagePaths)
			{
				if (string.IsNullOrEmpty(packagePath)) continue;
				if (!File.Exists(packagePath)) continue;
				ImportPackageToFolder(packagePath, selectedFolderPath, false);
			}

			UnityEngine.Debug.Log($"[Package2Folder] 已静默导入 {packagePaths.Length} 个包到 {selectedFolderPath}");
		}

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
					ClearQueue();
					UnityEngine.Debug.Log("[Package2Folder] 所有包导入完成");
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

			UnityEngine.Debug.Log($"[Package2Folder] 正在导入: {Path.GetFileName(packagePath)} (剩余 {GetQueueCount()} 个)");

			// 设置等待标志，防止在导入完成前处理下一个
			SessionState.SetBool(WAITING_IMPORT_KEY, true);

			ImportPackageToFolder(packagePath, folderPath, true);
		}

		/// <summary>
		/// Opens a file dialog that supports multiple file selection for .unitypackage files.
		/// </summary>
		/// <returns>Array of selected file paths, or null if cancelled.</returns>
		private static string[] OpenMultipleUnityPackages()
		{
#if UNITY_EDITOR_WIN
			return OpenFileDialogWin32.ShowOpenFileDialog(
				"Import package(s) ...",
				"Unity Package (*.unitypackage)\0*.unitypackage\0All Files (*.*)\0*.*\0\0",
				true
			);
#else
			// Fallback to single file selection on non-Windows platforms
			var packagePath = EditorUtility.OpenFilePanel("Import package ...", "", "unitypackage");
			if (string.IsNullOrEmpty(packagePath)) return null;
			return new[] { packagePath };
#endif
		}

#if UNITY_EDITOR_WIN
		/// <summary>
		/// Windows native file dialog helper using Win32 API
		/// </summary>
		private static class OpenFileDialogWin32
		{
			[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
			private struct OpenFileName
			{
				public int lStructSize;
				public IntPtr hwndOwner;
				public IntPtr hInstance;
				public string lpstrFilter;
				public string lpstrCustomFilter;
				public int nMaxCustFilter;
				public int nFilterIndex;
				public IntPtr lpstrFile;
				public int nMaxFile;
				public string lpstrFileTitle;
				public int nMaxFileTitle;
				public string lpstrInitialDir;
				public string lpstrTitle;
				public int Flags;
				public short nFileOffset;
				public short nFileExtension;
				public string lpstrDefExt;
				public IntPtr lCustData;
				public IntPtr lpfnHook;
				public string lpTemplateName;
				public IntPtr pvReserved;
				public int dwReserved;
				public int flagsEx;
			}

			private const int OFN_EXPLORER = 0x00080000;
			private const int OFN_FILEMUSTEXIST = 0x00001000;
			private const int OFN_PATHMUSTEXIST = 0x00000800;
			private const int OFN_ALLOWMULTISELECT = 0x00000200;
			private const int OFN_NOCHANGEDIR = 0x00000008;
			private const int MAX_PATH = 65536;

			[DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
			private static extern bool GetOpenFileNameW(ref OpenFileName lpofn);

			public static string[] ShowOpenFileDialog(string title, string filter, bool multiSelect)
			{
				var ofn = new OpenFileName();
				ofn.lStructSize = Marshal.SizeOf(ofn);
				ofn.hwndOwner = IntPtr.Zero;
				ofn.lpstrFilter = filter;
				ofn.nFilterIndex = 1;
				ofn.nMaxFile = MAX_PATH;

				IntPtr fileBuffer = Marshal.AllocHGlobal(MAX_PATH * 2);
				try
				{
					for (int i = 0; i < MAX_PATH * 2; i++)
						Marshal.WriteByte(fileBuffer, i, 0);

					ofn.lpstrFile = fileBuffer;
					ofn.lpstrTitle = title;
					ofn.Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;

					if (multiSelect)
						ofn.Flags |= OFN_ALLOWMULTISELECT;

					if (GetOpenFileNameW(ref ofn))
					{
						return ParseMultiSelectResult(fileBuffer, MAX_PATH);
					}
					return null;
				}
				finally
				{
					Marshal.FreeHGlobal(fileBuffer);
				}
			}

			private static string[] ParseMultiSelectResult(IntPtr buffer, int maxLength)
			{
				var results = new List<string>();
				int offset = 0;
				string directory = null;

				while (offset < maxLength * 2)
				{
					string str = Marshal.PtrToStringUni(IntPtr.Add(buffer, offset));
					if (string.IsNullOrEmpty(str))
						break;

					if (directory == null)
					{
						directory = str;
					}
					else
					{
						results.Add(str);
					}

					offset += (str.Length + 1) * 2;
				}

				if (results.Count == 0 && directory != null)
				{
					// Single file selected
					return new[] { directory };
				}

				// Multiple files selected - combine directory with filenames
				for (int i = 0; i < results.Count; i++)
				{
					results[i] = Path.Combine(directory, results[i]);
				}

				return results.ToArray();
			}
		}
#endif

		///////////////////////////////////////////////////////////////
		// Main logic
		///////////////////////////////////////////////////////////////

		/// <summary>
		/// Allows to import package to the specified folder either via standard import window or silently.
		/// </summary>
		/// <param name="packagePath">Native path to the package.</param>
		/// <param name="selectedFolderPath">Path to the target folder where you wish to import package into.
		/// Relative to the project folder (should start with 'Assets')</param>
		/// <param name="interactive">If true - imports using standard import window, otherwise does this silently.</param>
		/// <param name="assetOrigin">An optional UnityEditor.AssetOrigin object which Unity from version 2023+ uses internally to store the source of the imported asset inside the meta file.</param>
		public static void ImportPackageToFolder(string packagePath, string selectedFolderPath, bool interactive, object assetOrigin = null)
		{
			string packageIconPath;
#if CS_P2F_NEW_ARGUMENT_2
			string packageManagerDependenciesPath;
			var assetsItems = ExtractAndPrepareAssetList(packagePath, out packageIconPath, out packageManagerDependenciesPath);
#elif CS_P2F_NEW_ARGUMENT
			bool allowReInstall;
			string packageManagerDependenciesPath;
			var assetsItems = ExtractAndPrepareAssetList(packagePath, out packageIconPath, out allowReInstall, out packageManagerDependenciesPath);
#else
			bool allowReInstall;
			var assetsItems = ExtractAndPrepareAssetList(packagePath, out packageIconPath, out allowReInstall);
#endif

			if (assetsItems == null) return;

			foreach (object item in assetsItems)
			{
				ChangeAssetItemPath(item, selectedFolderPath);
			}

			if (interactive)
			{
#if CS_P2F_NEW_ARGUMENT_2
				ShowImportPackageWindow(packagePath, assetsItems, packageIconPath, assetOrigin);
#else
				ShowImportPackageWindow(packagePath, assetsItems, packageIconPath, allowReInstall);
#endif

			}
			else
			{
				var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(packagePath);
				ImportPackageSilently(fileNameWithoutExtension, assetsItems, assetOrigin);
			}
		}

		private static void ChangeAssetItemPath(object assetItem, string selectedFolderPath)
		{
			string destinationPath = (string)DestinationAssetPathFieldInfo.GetValue(assetItem);
			if (destinationPath.StartsWith("Packages/")) return;

			int firstSlashIndex = destinationPath.IndexOf('/');
			if (firstSlashIndex >= 0)
			{
				string relativePath = destinationPath.Substring(firstSlashIndex);
				destinationPath = selectedFolderPath + relativePath;
			}
			else
			{
				destinationPath = selectedFolderPath + "/" + destinationPath;
			}

			DestinationAssetPathFieldInfo.SetValue(assetItem, destinationPath);
		}

#if CS_P2F_NEW_ARGUMENT_2
		public static void ShowImportPackageWindow(string path, object[] array, string packageIconPath, object assetOrigin = null)
		{
#if UNITY_2023_1_OR_NEWER
			int productId = 0;
			string packageName = null;
			string packageVersion = null;
			int uploadId = 0;
			if (assetOrigin != null)
			{
				Type assetOriginType = Type.GetType("UnityEditor.AssetOrigin, UnityEditor.CoreModule");
				if (assetOriginType != null)
				{
					FieldInfo productIdProp = assetOriginType.GetField("productId");
					FieldInfo packageVersionProp = assetOriginType.GetField("packageVersion");
					FieldInfo packageNameProp = assetOriginType.GetField("packageName");
					FieldInfo uploadIdProp = assetOriginType.GetField("uploadId");

					if (productIdProp != null) productId = productIdProp.GetValue(assetOrigin) as int? ?? 0;
					if (packageVersionProp != null) packageVersion = packageVersionProp.GetValue(assetOrigin) as string;
					if (packageNameProp != null) packageName = packageNameProp.GetValue(assetOrigin) as string;
					if (uploadIdProp != null) uploadId = uploadIdProp.GetValue(assetOrigin) as int? ?? 0;
				}
			}
			ShowImportPackageMethodInfo.Invoke(null, new object[]
			{
				path, array, packageIconPath, productId, packageName, packageVersion, uploadId
			});
#else
			ShowImportPackageMethodInfo.Invoke(null, new object[]
			{
				path, array, packageIconPath
			});
#endif
		}
#else
		public static void ShowImportPackageWindow(string path, object[] array, string packageIconPath, bool allowReInstall)
		{
			ShowImportPackageMethodInfo.Invoke(null, new object[] { path, array, packageIconPath, allowReInstall });
		}
#endif

		public static void ImportPackageSilently(string packageName, object[] assetsItems, object assetOrigin = null)
		{
#if CS_P2F_NEW_NON_INTERACTIVE_LOGIC
			if (assetOrigin != null)
			{
				ImportPackageAssetsWithOriginMethodInfo.Invoke(null, new[] { assetOrigin, assetsItems });
			}
			else
			{
				ImportPackageAssetsMethodInfo.Invoke(null, new object[] { packageName, assetsItems });
			}
#else
			ImportPackageAssetsMethodInfo.Invoke(null, new object[] { packageName, assetsItems, false });
#endif
		}

		///////////////////////////////////////////////////////////////
		// Utility methods
		///////////////////////////////////////////////////////////////

		private static string GetSelectedFolderPath()
		{
			if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
				return null;

			var assetGuid = Selection.assetGUIDs[0];
			var path = AssetDatabase.GUIDToAssetPath(assetGuid);
			return !Directory.Exists(path) ? null : path;
		}
	}
}