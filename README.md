# UnityPlugins - Package2Folder 工具

## 简介

UnityPlugins 是一个 Unity 插件包，内置了 Package2Folder 工具，用于批量导入 `.unitypackage` 文件到指定的文件夹中。该工具提供了一个图形界面，支持拖拽操作、批量处理和多种导入模式。

## 功能特性

- **批量导入**：一次性导入多个 `.unitypackage` 文件
- **自定义目标文件夹**：将包导入到指定的项目文件夹中
- **拖拽支持**：支持直接拖拽 `.unitypackage` 文件到窗口中
- **导入模式**：支持静默导入和交互式导入两种模式
- **实时预览**：显示待导入的包列表及文件路径
- **文件验证**：检查包文件是否存在


## 安装方法
1. 打开 Unity 编辑器，进入项目。
2. 打开 Package Manager（菜单：Window > Package Manager）。
3. 点击左上角的 "+" 按钮，选择 "Add package from git URL..."。
4. 在弹出的输入框中，输入以下 URL：
    ```
    https://github.com/zheliku/UnityPlugins.git
    ```

## 使用方法

### 1. 打开窗口

通过以下任一方式打开插件窗口：

- `Tools/Package2Folder/Batch Import Window`
- `Assets/Import Package/Batch Import Window`

### 2. 设置目标文件夹

- **手动输入**：在文本框中直接输入目标文件夹路径
- **选择文件夹**：点击"选择..."按钮浏览并选择目标文件夹
- **使用选中**：选中 Project 窗口中的文件夹后，点击"使用选中"按钮快速设置

### 3. 添加要导入的包

有三种方式添加 `.unitypackage` 文件：

- **拖拽添加**：将 `.unitypackage` 文件直接拖拽到窗口顶部的拖拽区域
- **点击添加**：点击拖拽区域或使用"添加文件"按钮选择 `.unitypackage` 文件
- **多文件选择**：可以同时选择多个 `.unitypackage` 文件

### 4. 选择导入模式

- **静默模式**：直接导入所有包，不显示预览窗口，适合批量导入
- **交互模式**：逐个显示导入预览窗口，可以选择要导入的资源，适合需要筛选资源的情况

### 5. 开始导入

确认所有设置无误后，点击"开始导入"按钮执行导入操作。
