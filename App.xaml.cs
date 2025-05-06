using System;
using System.Windows;

namespace DG2072_USB_Control
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Handle unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Exception ex = (Exception)args.ExceptionObject;
                MessageBox.Show($"An unhandled exception occurred: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
            
            // Handle UI thread exceptions
            this.DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"An unhandled exception occurred: {args.Exception.Message}\n\nStack Trace:\n{args.Exception.StackTrace}", 
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
}
