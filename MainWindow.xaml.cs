using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MemOptimizer.ViewModels;

namespace MemOptimizer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 初始化进度弧
        UpdateProgressArc(_viewModel.UsedPercent);

        // 窗口入场动画
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var scaleIn = new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        MainBorder.RenderTransform = new ScaleTransform(1, 1);
        MainBorder.RenderTransformOrigin = new Point(0.5, 0.5);
        MainBorder.Opacity = 0;

        MainBorder.BeginAnimation(OpacityProperty, fadeIn);
        ((ScaleTransform)MainBorder.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
        ((ScaleTransform)MainBorder.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.AnimatedUsedPercent) || e.PropertyName == nameof(MainViewModel.UsedPercent))
        {
            Dispatcher.Invoke(() => UpdateProgressArc(_viewModel.AnimatedUsedPercent));
        }
        else if (e.PropertyName == nameof(MainViewModel.IsOptimizing))
        {
            Dispatcher.Invoke(() =>
            {
                if (_viewModel.IsOptimizing)
                {
                    BtnText.Text = "正在优化…";
                    LoadingDot.Visibility = Visibility.Visible;
                    StartLoadingAnimation();
                }
                else
                {
                    BtnText.Text = "一键优化";
                    LoadingDot.Visibility = Visibility.Collapsed;
                    StopLoadingAnimation();
                }
            });
        }
    }

    /// <summary>
    /// 更新环形进度条的弧形。
    /// </summary>
    private void UpdateProgressArc(double percent)
    {
        double clamped = Math.Max(0, Math.Min(100, percent));
        double angle = clamped / 100 * 360;

        // 椭圆 160x160，StrokeThickness=10，圆环中心半径 = 80 - 5 = 75
        double radius = 75;
        double centerX = 80;
        double centerY = 80;

        // 从顶部开始（-90度），顺时针
        double startAngle = -Math.PI / 2;
        double endAngle = startAngle + angle * Math.PI / 180;

        double startX = centerX + radius * Math.Cos(startAngle);
        double startY = centerY + radius * Math.Sin(startAngle);
        double endX = centerX + radius * Math.Cos(endAngle);
        double endY = centerY + radius * Math.Sin(endAngle);

        // 更新百分比文字
        PercentText.Text = $"{Math.Round(clamped)}%";

        if (angle >= 359.9)
        {
            // 完整圆：用两段弧避免 ArcSegment 退化为直线
            var figure = new PathFigure { StartPoint = new Point(startX, startY), IsClosed = false };
            figure.Segments.Add(new ArcSegment(
                new Point(centerX - radius, centerY),
                new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            figure.Segments.Add(new ArcSegment(
                new Point(startX, startY),
                new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            ProgressArc.Data = geometry;
        }
        else if (angle > 0.5)
        {
            var figure = new PathFigure { StartPoint = new Point(startX, startY), IsClosed = false };
            figure.Segments.Add(new ArcSegment(
                new Point(endX, endY),
                new Size(radius, radius), 0, angle > 180, SweepDirection.Clockwise, true));
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            ProgressArc.Data = geometry;
        }
        else
        {
            // 空路径
            ProgressArc.Data = new PathGeometry();
        }
    }

    /// <summary>
    /// 开始加载动画（旋转）。
    /// </summary>
    private void StartLoadingAnimation()
    {
        var rotate = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(800))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };

        var transform = new RotateTransform();
        LoadingDot.RenderTransform = transform;
        LoadingDot.RenderTransformOrigin = new Point(0.5, 0.5);
        transform.BeginAnimation(RotateTransform.AngleProperty, rotate);
    }

    /// <summary>
    /// 停止加载动画。
    /// </summary>
    private void StopLoadingAnimation()
    {
        if (LoadingDot.RenderTransform is RotateTransform transform)
        {
            transform.BeginAnimation(RotateTransform.AngleProperty, null);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) => Close();
        MainBorder.BeginAnimation(OpacityProperty, fadeOut);
    }

    /// <summary>
    /// 齿轮按钮点击：切换关于弹窗。
    /// </summary>
    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        AboutPopup.IsOpen = !AboutPopup.IsOpen;
    }

    /// <summary>
    /// GitHub 按钮点击：打开 GitHub 仓库。
    /// </summary>
    private void BtnGitHub_Click(object sender, RoutedEventArgs e)
    {
        AboutPopup.IsOpen = false;
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/Warm-winter/MemOptimizer",
            UseShellExecute = true
        });
    }
}
