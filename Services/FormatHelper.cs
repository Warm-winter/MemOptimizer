namespace MemOptimizer.Services;

/// <summary>
/// 文件大小/内存大小格式化工具。
/// </summary>
public static class FormatHelper
{
    /// <summary>
    /// 将字节数格式化为人类可读的字符串。
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        if (bytes < 0)
            return "0 B";

        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{Math.Round(size, 1)} {units[unitIndex]}";
    }

    /// <summary>
    /// 将字节数格式化为 MB 字符串。
    /// </summary>
    public static string FormatMB(long bytes)
    {
        return $"{Math.Round(bytes / 1024.0 / 1024.0, 0)} MB";
    }

    /// <summary>
    /// 将字节数格式化为 GB 字符串。
    /// </summary>
    public static string FormatGB(long bytes)
    {
        return $"{Math.Round(bytes / 1024.0 / 1024.0 / 1024.0, 1)} GB";
    }
}
