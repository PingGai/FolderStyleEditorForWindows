# v0.1.11-rev.1 图标设置与别名设置细节修复 - 实现计划

## 问题总结

### 1. 图标路径处理 Bug
**当前行为**: 当拖拽 .ico 文件到文件夹编辑页面时，系统将绝对路径直接写入 desktop.ini
**期望行为**: 应该将 .ico 文件复制到 .ICON 目录并使用相对路径引用

### 2. 文本拖拽功能扩展
**当前行为**: 仅支持文件和文件夹拖拽
**期望行为**: 支持文本拖拽，解析文本内容，如果是目录路径则进入编辑界面，否则设置为别名

### 3. 别名长度验证
**当前行为**: 别名输入无长度限制提示
**期望行为**: 别名长度≥260时显示浅红色警告，<260时恢复正常

## 解决方案

### 1. 图标路径处理修复

#### 修改文件: `FolderStyleEditorForWindows_Avalonia/PathHelper.cs`

**修改方法**: `HandleExternalIconAsync`

```csharp
// 添加对 .ico 文件的特殊处理
if (Path.GetExtension(iconFilePath).Equals(".ico", StringComparison.OrdinalIgnoreCase))
{
    // 直接复制 .ico 文件到 .ICON 目录
    string iconDir = Path.Combine(targetFolderPath, ".ICON");
    if (!Directory.Exists(iconDir))
    {
        var di = Directory.CreateDirectory(iconDir);
        di.Attributes |= FileAttributes.Hidden | FileAttributes.System;
    }

    string newIconName = Path.GetFileName(iconFilePath);
    string destination = Path.Combine(iconDir, newIconName);

    // 复制文件
    File.Copy(iconFilePath, destination, overwrite: true);

    string rel = GetRelativePath(targetFolderPath, destination);

    return new[]
    {
        ("IconFile", rel),
        ("IconIndex", "0")
    };
}
```

### 2. 文本拖拽功能实现

#### 修改文件: `FolderStyleEditorForWindows_Avalonia/MainWindow.axaml.cs`

**修改方法**: `DragAndDropTarget_Drop`

```csharp
// 添加文本数据处理
if (e.Data.Contains(DataFormats.Text))
{
    string textData = e.Data.GetText()?.Trim() ?? "";
    if (!string.IsNullOrEmpty(textData))
    {
        // 处理可能的引号
        string cleanedText = textData.Trim('"', '\'');

        // 检查是否为有效目录路径
        if (Directory.Exists(cleanedText))
        {
            // 进入编辑界面
            _viewModel.StartEditSession(cleanedText);
            return;
        }
        else
        {
            // 设置为别名
            if (_editView != null && _editView.IsVisible)
            {
                var aliasInput = _editView.FindControl<TextBox>("aliasInput");
                if (aliasInput != null)
                {
                    aliasInput.Text = cleanedText;
                    aliasInput.Focus();
                    aliasInput.SelectAll();
                }
            }
            return;
        }
    }
}
```

### 3. 别名长度验证实现

#### 修改文件: `FolderStyleEditorForWindows_Avalonia/EditView.axaml.cs`

**添加方法**: `AliasInput_TextChanged`

```csharp
private void AliasInput_TextChanged(object? sender, TextChangedEventArgs e)
{
    if (sender is TextBox textBox && DataContext is ViewModels.MainViewModel vm)
    {
        if (textBox.Text?.Length >= 260)
        {
            textBox.Foreground = new SolidColorBrush(Color.Parse("#FFB6C1")); // 浅红色
        }
        else
        {
            // 恢复默认颜色
            textBox.Foreground = (SolidColorBrush)Application.Current?.FindResource("Fg1Brush") ?? Brushes.White;
        }
    }
}
```

## 实现步骤

1. **修复图标路径处理**: 修改 `PathHelper.cs` 中的 `HandleExternalIconAsync` 方法
2. **实现文本拖拽**: 修改 `MainWindow.axaml.cs` 中的 `DragAndDropTarget_Drop` 方法
3. **实现别名验证**: 修改 `EditView.axaml.cs` 添加文本变更处理
4. **测试所有功能**: 确保所有修改正常工作
5. **更新 CHANGELOG**: 记录所有变更

## 预期结果

1. 图标路径处理: .ico 文件被正确复制到 .ICON 目录并使用相对路径
2. 文本拖拽: 支持目录路径和别名文本拖拽
3. 别名验证: 长度≥260时显示警告颜色，<260时恢复正常