using System;
using System.ServiceProcess;

namespace ASTAWebServer
{
    static class Program
    {
        static ASTAWebServer service = null;

        /// <summary>
        /// The main entry point for the application
        /// </summary>
        /// <param name="args"> Parameters for install: ASTAService.exe -i, uninstall: ASTAService.exe -u </param>
        static void Main(string[] args)
        {
            AssemblyLoader.RegisterAssemblyLoader();

            WindowsServiceClass uninstallService = new WindowsServiceClass();
            uninstallService.EvntInfoMessage += UninstallService_EvntInfoMessage;

            service = new ASTAWebServer();

            if (args?.Length > 0)
            {
                foreach (var str in args)
                    service.WriteString($"Got environment argument '{str}'");
            }

            ServiceBase[] ServicesToRun;

            ServicesToRun = new ServiceBase[]
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
                                //  MessageBox.Show("Failed to install service");
                            }
                            else
                            {
                                service.Start();
                                //   MessageBox.Show("Running service");
                            }
                            break;
                        case "uninstall":
                        case "u":
                            ServiceInstallerUtility.StopService();

                            uninstallService.Uninstall(serviceName);
                            if (!ServiceInstallerUtility.Uninstall())
                            {
                                //  MessageBox.Show("Failed to uninstall service");
                            }
                            else
                            {
                                //      MessageBox.Show("Service stopped. Goodbye.");
                            }

                            string processName = System.IO.Path.GetFileName(ServiceInstallerUtility.serviceExePath);
                            System.Diagnostics.Process.Start("taskkill", $"/F /IM {processName}");

                            break;
                        default:
                            service.Start();
                            // ServiceInstallerUtility.Install();
                            break;
                    }
                }

                //Console.CancelKeyPress += (x, y) => service.Stop();
                //ServiceInstallerUtility.Install();
                //Console.WriteLine("Running service, press a key to stop");
                //Console.ReadKey();
                //service.Stop();
                //Console.WriteLine("Service stopped. Goodbye.");
            }
            else
            {
                ServiceBase.Run(ServicesToRun);
            }
        }

        private static void UninstallService_EvntInfoMessage(object sender, TextEventArgs e)
        {
            service.WriteString(e.Message);
        }
    }
}