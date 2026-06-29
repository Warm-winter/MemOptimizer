﻿using System.Runtime.InteropServices;
using System.Security.Principal;

namespace MemOptimizer.Services;

/// <summary>
/// 核心内存优化逻辑，1:1 复刻自 PCL2 的 PageOtherTest.MemoryOptimizeInternal。
/// 通过 NtSetSystemInformation 系统调用清理系统文件缓存，实现系统级内存优化。
/// </summary>
public static class MemoryOptimizer
{
    /// <summary>
    /// TOKEN_PRIVILEGES 结构体，用于权限提升。
    /// 对应 PCL2 中的 PageOtherTest+PrivilegeToken。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct PrivilegeToken
    {
        public int PrivilegeCount;
        public long Luid;
        public int Attributes;
    }

    // ntdll.dll - 系统级内存优化
    [DllImport("ntdll.dll", EntryPoint = "NtSetSystemInformation", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern uint NtSetSystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength);

    // advapi32.dll - 权限提升
    // 注意：LPCSTR 是指向常量字符串的指针，必须按值传递 string，不能用 ref（ref 会创建双重指针）
    [DllImport("advapi32.dll", EntryPoint = "LookupPrivilegeValueA", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, ref long lpLuid);

    [DllImport("advapi32.dll", EntryPoint = "AdjustTokenPrivileges", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref PrivilegeToken NewState, int BufferLength, ref IntPtr PreviousState, ref int ReturnLength);

    /// <summary>
    /// 是否正在优化中（防重入标志）。
    /// </summary>
    public static bool IsOptimizing { get; private set; }

    /// <summary>
    /// 优化完成事件。参数为释放的内存量（字节）。
    /// </summary>
    public static event Action<long>? OptimizeCompleted;

    /// <summary>
    /// 优化失败事件。参数为异常信息。
    /// </summary>
    public static event Action<Exception>? OptimizeFailed;

    /// <summary>
    /// 优化进度变化事件。参数为当前步骤（2-5）。
    /// </summary>
    public static event Action<int, int>? OptimizeProgress;

    /// <summary>
    /// 执行内存优化。复刻自 PCL2 的 MemoryOptimize 方法。
    /// </summary>
    /// <returns>释放的内存量（字节）。若为负数表示优化失败。</returns>
    public static long Optimize()
    {
        if (IsOptimizing)
            return -1;

        IsOptimizing = true;
        try
        {
            // 记录优化前可用内存
            long beforeMemory = (long)GetAvailableMemory();

            // 执行核心优化
            OptimizeInternal();

            // 计算内存改变量
            long afterMemory = (long)GetAvailableMemory();
            long freed = afterMemory - beforeMemory;

            OptimizeCompleted?.Invoke(freed);
            return freed;
        }
        catch (Exception ex)
        {
            OptimizeFailed?.Invoke(ex);
            return -1;
        }
        finally
        {
            IsOptimizing = false;
        }
    }

    /// <summary>
    /// 核心优化逻辑。复刻自 PCL2 的 MemoryOptimizeInternal 方法。
    /// 1. 检查管理员权限
    /// 2. 获取 SeProfileSingleProcessPrivilege 权限
    /// 3. 循环 4 次调用 NtSetSystemInformation(class=80)
    /// </summary>
    private static void OptimizeInternal()
    {
        // 步骤 1：权限检查
        if (!HasAdminRole())
        {
            throw new Exception("内存优化功能需要管理员权限！");
        }

        // 步骤 2：获取 SeProfileSingleProcessPrivilege 权限
        AcquirePrivilege();

        // 步骤 3：执行内存优化（循环 4 次，i 从 2 到 5）
        // 对应 PCL2 IL 代码中的循环：ldc.i4.2 到 ldc.i4.4 (ble.s)
        for (int i = 2; i <= 5; i++)
        {
            OptimizeProgress?.Invoke(i - 1, 4);

            // 使用 GCHandle 固定一个 Int32 变量
            // 对应 PCL2 IL: GCHandle.Alloc(i, GCHandleType.Pinned)
            int cacheInfo = i;
            GCHandle handle = GCHandle.Alloc(cacheInfo, GCHandleType.Pinned);
            try
            {
                IntPtr systemInformation = handle.AddrOfPinnedObject();
                int length = Marshal.SizeOf<int>();

                // 调用 NtSetSystemInformation(class=80, info=addr, len=4)
                // SystemInformationClass=80 即 SystemFileCacheInformation
                uint result = NtSetSystemInformation(80, systemInformation, length);

                if (result != 0)
                {
                    throw new Exception($"内存优化操作 {i} 失败（错误代码：{result}）");
                }
            }
            finally
            {
                handle.Free();
            }
        }

        OptimizeProgress?.Invoke(4, 4);
    }

    /// <summary>
    /// 获取 SeProfileSingleProcessPrivilege 权限。
    /// 复刻自 PCL2 IL 代码中的权限获取逻辑。
    /// </summary>
    private static void AcquirePrivilege()
    {
        // 获取当前进程 Token
        // 对应 PCL2 IL: WindowsIdentity.GetCurrent(TokenAccessLevels=40)
        // TokenAccessLevels=40 即 Query | AdjustTokenPrivileges
        using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.AdjustPrivileges);

        // 构造 PrivilegeToken 结构体
        var token = new PrivilegeToken
        {
            PrivilegeCount = 1,
            Luid = 0,
            Attributes = 2  // SE_PRIVILEGE_ENABLED
        };

        // 查找 SeProfileSingleProcessPrivilege 的 LUID
        string? systemName = null;
        string privilegeName = "SeProfileSingleProcessPrivilege";

        if (!LookupPrivilegeValue(systemName, privilegeName, ref token.Luid))
        {
            int error = Marshal.GetLastWin32Error();
            throw new Exception($"获取内存优化权限失败（错误代码：{error}）");
        }

        // 启用权限
        IntPtr previousState = IntPtr.Zero;
        int returnLength = 0;

        if (!AdjustTokenPrivileges(identity.Token, false, ref token,
            Marshal.SizeOf<PrivilegeToken>(), ref previousState, ref returnLength))
        {
            int error = Marshal.GetLastWin32Error();
            throw new Exception($"获取内存优化权限失败（错误代码：{error}）");
        }

        // 检查 GetLastError（对应 PCL2 IL: call GetLastWin32Error, brfalse.s）
        int lastError = Marshal.GetLastWin32Error();
        if (lastError != 0)
        {
            throw new Exception($"获取内存优化权限失败（错误代码：{lastError}）");
        }
    }

    /// <summary>
    /// 检查当前是否拥有管理员权限。
    /// </summary>
    private static bool HasAdminRole()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取当前可用物理内存（字节）。
    /// </summary>
    public static ulong GetAvailableMemory()
    {
        var memStatus = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>()
        };
        NativeMethods.GlobalMemoryStatusEx(ref memStatus);
        return memStatus.ullAvailPhys;
    }

    /// <summary>
    /// 获取总物理内存（字节）。
    /// </summary>
    public static ulong GetTotalMemory()
    {
        var memStatus = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>()
        };
        NativeMethods.GlobalMemoryStatusEx(ref memStatus);
        return memStatus.ullTotalPhys;
    }
}

/// <summary>
/// Windows API 声明。
/// </summary>
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
