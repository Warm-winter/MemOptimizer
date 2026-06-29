using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using MemOptimizer.Services;

namespace MemOptimizer.ViewModels;

/// <summary>
/// 主窗口 ViewModel，实现 INotifyPropertyChanged 数据绑定。
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _refreshTimer;
    private DispatcherTimer? _statusClearTimer;

    private ulong _totalMemory;
    private ulong _availableMemory;
    private bool _isOptimizing;
    private long _lastFreedMemory;
    private string _statusText = "就绪";
    private int _optimizeProgress;
    private bool _hasOptimized;
    private double _animatedUsedPercent;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ulong TotalMemory
    {
        get => _totalMemory;
        set { _totalMemory = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalMemoryText)); OnPropertyChanged(nameof(UsedMemoryText)); OnPropertyChanged(nameof(UsedPercent)); }
    }

    public ulong AvailableMemory
    {
        get => _availableMemory;
        set { _availableMemory = value; OnPropertyChanged(); OnPropertyChanged(nameof(AvailableMemoryText)); OnPropertyChanged(nameof(UsedMemoryText)); OnPropertyChanged(nameof(UsedPercent)); }
    }

    public ulong UsedMemory => TotalMemory - AvailableMemory;

    public double UsedPercent => TotalMemory > 0 ? (double)UsedMemory / TotalMemory * 100 : 0;

    /// <summary>
    /// 动画过渡的使用率（用于平滑数字变化）。
    /// </summary>
    public double AnimatedUsedPercent
    {
        get => _animatedUsedPercent;
        set { _animatedUsedPercent = value; OnPropertyChanged(); }
    }

    public string TotalMemoryText => FormatHelper.FormatGB((long)TotalMemory);
    public string AvailableMemoryText => FormatHelper.FormatGB((long)AvailableMemory);
    public string UsedMemoryText => FormatHelper.FormatGB((long)UsedMemory);

    public bool IsOptimizing
    {
        get => _isOptimizing;
        set { _isOptimizing = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    public long LastFreedMemory
    {
        get => _lastFreedMemory;
        set { _lastFreedMemory = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastFreedMemoryText)); }
    }

    public string LastFreedMemoryText => _lastFreedMemory > 0
        ? $"已释放 {FormatHelper.FormatFileSize(_lastFreedMemory)}"
        : "";

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public int OptimizeProgress
    {
        get => _optimizeProgress;
        set { _optimizeProgress = value; OnPropertyChanged(); }
    }

    public bool HasOptimized
    {
        get => _hasOptimized;
        set { _hasOptimized = value; OnPropertyChanged(); }
    }

    public ICommand OptimizeCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand MinimizeCommand { get; }

    public MainViewModel()
    {
        OptimizeCommand = new RelayCommand(ExecuteOptimize, () => !IsOptimizing);
        CloseCommand = new RelayCommand(() => System.Windows.Application.Current.Shutdown());
        MinimizeCommand = new RelayCommand(() =>
        {
            foreach (var window in System.Windows.Application.Current.Windows)
            {
                if (window is System.Windows.Window w)
                {
                    w.WindowState = System.Windows.WindowState.Minimized;
                    break;
                }
            }
        });

        // 初始化定时器，每秒刷新内存信息
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += (_, _) => RefreshMemoryInfo();
        _refreshTimer.Start();

        // 初始刷新
        RefreshMemoryInfo();
        _animatedUsedPercent = UsedPercent;

        // 订阅优化事件
        MemoryOptimizer.OptimizeCompleted += OnOptimizeCompleted;
        MemoryOptimizer.OptimizeFailed += OnOptimizeFailed;
        MemoryOptimizer.OptimizeProgress += OnOptimizeProgress;
    }

    private void RefreshMemoryInfo()
    {
        TotalMemory = MemoryOptimizer.GetTotalMemory();
        AvailableMemory = MemoryOptimizer.GetAvailableMemory();

        // 平滑过渡动画目标
        if (!IsOptimizing)
        {
            // 在非优化状态下，让动画值逐步接近实际值
            double diff = UsedPercent - _animatedUsedPercent;
            if (Math.Abs(diff) > 0.1)
            {
                AnimatedUsedPercent += diff * 0.3;
            }
            else
            {
                AnimatedUsedPercent = UsedPercent;
            }
        }
    }

    private void ExecuteOptimize()
    {
        if (IsOptimizing) return;

        IsOptimizing = true;
        StatusText = "正在优化内存…";
        OptimizeProgress = 0;

        // 取消之前的清除定时器
        _statusClearTimer?.Stop();

        Task.Run(() =>
        {
            long freed = MemoryOptimizer.Optimize();
            return freed;
        }).ContinueWith(t =>
        {
            // 在 UI 线程上更新
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                IsOptimizing = false;
                RefreshMemoryInfo();
                AnimatedUsedPercent = UsedPercent;
            });
        });
    }

    private void OnOptimizeCompleted(long freed)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            LastFreedMemory = freed;
            HasOptimized = true;

            if (freed > 0)
            {
                // 显示为优化前大小的 80%（与 PCL2 一致）
                long displayFreed = (long)(freed * 0.8);
                StatusText = $"内存优化完成，可用内存增加了约 {FormatHelper.FormatFileSize(displayFreed)}！";
            }
            else
            {
                StatusText = "内存优化完成，已经优化到了最佳状态！";
            }

            RefreshMemoryInfo();
            StartStatusClearTimer();
        });
    }

    private void OnOptimizeFailed(Exception ex)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            StatusText = $"优化失败：{ex.Message}";
            StartStatusClearTimer();
        });
    }

    private void OnOptimizeProgress(int current, int total)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            OptimizeProgress = (int)((double)current / total * 100);
        });
    }

    /// <summary>
    /// 启动状态提示自动消失定时器（5秒后恢复"就绪"）。
    /// </summary>
    private void StartStatusClearTimer()
    {
        _statusClearTimer?.Stop();
        _statusClearTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _statusClearTimer.Tick += (_, _) =>
        {
            StatusText = "就绪";
            _statusClearTimer.Stop();
        };
        _statusClearTimer.Start();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>
/// 简单的 ICommand 实现。
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}
