using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using DynamicDarkMode.Windows.ThemeCatalog;

namespace DynamicDarkMode.Windows.ThemeActivation;

internal interface IStaExecutionContext
{
    T Run<T>(Func<T> operation);
}

internal sealed class WindowsStaExecutionContext : IStaExecutionContext
{
    public T Run<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        T? result = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            int initializationResult = ThemeManagerNativeMethods.CoInitializeEx(
                reserved: IntPtr.Zero,
                coInit: ThemeManagerNativeMethods.CoInitApartmentThreaded);

            if (initializationResult < 0)
            {
                failure = Marshal.GetExceptionForHR(initializationResult)
                    ?? new COMException(
                        "COM could not initialize the activation apartment.",
                        initializationResult);
                return;
            }

            try
            {
                result = operation();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                ThemeManagerNativeMethods.CoUninitialize();
            }
        })
        {
            Name = "Dynamic Dark Mode theme activation COM",
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return result!;
    }
}
