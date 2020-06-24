using System;
using System.ServiceProcess;

namespace ASTAWebServer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application
        /// </summary>
        /// <param name="args"> Parameters for install: ASTAService.exe -i, uninstall: ASTAService.exe -u </param>
        static void Main(string[] args)
        {
            AssemblyLoader.RegisterAssemblyLoader();

            IServiceManageable serviceManagable = new ServiceManager();

            WindowsServiceClass uninstallService = new WindowsServiceClass();
            uninstallService.EvntInfoMessage += (x, e) => serviceManagable.AddInfo(e.Message);

            //class Name matched installing services (public partial class from Program.cs)
            ASTAWebServer service = new ASTAWebServer(serviceManagable);

            if (args?.Length > 0)
            {
                foreach (var str in args)
                    serviceManagable.AddInfo($"Got environment argument '{str}'");
            }

            ServiceBase[] ServicesToRun = new ServiceBase[]
            {
                service
            };

            string serviceName = ServiceInstallerUtility.serviceName;

            if (Environment.UserInteractive)
            {
                // Разбор пути для саморегистрации
                if (args?.Length > 0 && args[0].Length > 1
                    && (args[0].StartsWith("-") || args[0].StartsWith("/")))
                {
                    switch (args[0].Substring(1).ToLower())
                    {
                        case "install":
                        case "i":
                            if (!ServiceInstallerUtility.Install())
                            {
                                serviceManagable.AddInfo("Failed to install service");
                            }
                            else
                            {
                                serviceManagable.OnStart();
                                serviceManagable.AddInfo("Running service");
                            }
                            break;

                        case "uninstall":
                        case "u":
                            ServiceInstallerUtility.StopService();

                            uninstallService.Uninstall(serviceName);
                            if (!ServiceInstallerUtility.Uninstall())
                            {
                                serviceManagable.AddInfo("Failed to uninstall service");
                            }
                            else
                            {
                                serviceManagable.AddInfo("Service stopped. Goodbye.");
                            }

                            string processName = System.IO.Path.GetFileName(ServiceInstallerUtility.serviceExePath);
                            System.Diagnostics.Process.Start("taskkill", $"/F /IM {processName}");
                            break;

                        default:
                            serviceManagable.OnStart();
                            // ServiceInstallerUtility.Install();
                            break;
                    }
                }
            }
            else
            {
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}