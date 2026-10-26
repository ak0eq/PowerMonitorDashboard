using System.IO;
using System.Text;
using PowerMonitorDashboard.Core.Abstractions;

namespace PowerMonitorDashboard.Infrastructure;

/// <summary>CSV 文件写入器。写入带 BOM 的 UTF-8，确保 Excel 直接双击打开不乱码。</summary>
public static class CsvFileWriter
{
    /// <summary>把内容写入目标文件。</summary>
    public static void Write(string path, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }
}

/// <summary>文件选择服务抽象，便于在单元测试中替换为假实现。</summary>
public interface IFileDialogService
{
    /// <summary>弹出“另存为”对话框，返回用户选择的路径；取消时返回 <c>null</c>。</summary>
    string? PickSaveFile(string title, string filter, string defaultFileName);

    /// <summary>弹出“打开文件”对话框，返回用户选择的路径；取消时返回 <c>null</c>。</summary>
    string? PickOpenFile(string title, string filter, string initialDirectory);
}

/// <summary>基于 WPF 原生对话框的实现。</summary>
public sealed class FileDialogService : IFileDialogService
{
    /// <inheritdoc />
    public string? PickSaveFile(string title, string filter, string defaultFileName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = defaultFileName,
            AddExtension = true,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <inheritdoc />
    public string? PickOpenFile(string title, string filter, string initialDirectory)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Filter = filter,
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : null,
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
