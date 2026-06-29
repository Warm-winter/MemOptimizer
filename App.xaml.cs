using System.Windows;

namespace MemOptimizer;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // 设置异常处理
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"发生未处理的异常：{args.Exception.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
