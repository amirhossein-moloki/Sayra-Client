using System.Diagnostics;
using System.ServiceProcess;

Console.WriteLine("Sayra Guardian starting...");

const string serviceName = "Sayra Client";

while (true)
{
    try
    {
        if (OperatingSystem.IsWindows())
        {
            var services = ServiceController.GetServices();
            var service = services.FirstOrDefault(s => s.ServiceName == serviceName);

            if (service == null)
            {
                Console.WriteLine($"Service '{serviceName}' not found!");
            }
            else if (service.Status != ServiceControllerStatus.Running && service.Status != ServiceControllerStatus.StartPending)
            {
                Console.WriteLine($"Service '{serviceName}' is {service.Status}. Attempting to start...");
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                Console.WriteLine($"Service '{serviceName}' started.");
            }
        }
        else
        {
            // For development/non-windows environments, we might check process name
            var processes = Process.GetProcessesByName("SayraClient");
            if (processes.Length == 0)
            {
                Console.WriteLine("SayraClient process not found. (Auto-restart not implemented for non-Windows)");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error checking service status: {ex.Message}");
    }

    Thread.Sleep(5000);
}
