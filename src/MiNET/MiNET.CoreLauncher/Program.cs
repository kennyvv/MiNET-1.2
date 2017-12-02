using System;
using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;
using PeterKottas.DotNetCore.WindowsService;

// Configure log4net using the .config file

[assembly: XmlConfigurator(Watch = true)]
// This will cause log4net to look for a configuration file
// called TestApp.exe.config in the application base
// directory (i.e. the directory containing TestApp.exe)
// The config file will be watched for changes.

namespace MiNET.Service
{
	public class Program
    {
	    private static readonly ILog Log = LogManager.GetLogger(typeof(MiNetServer));
		static void Main(string[] args)
        {
	        var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
	        XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

	        ServiceRunner<MiNETService>.Run(config =>
	        {
		        config.SetName("MiNET");
				config.SetDisplayName("MiNET Service");
				config.SetDescription("MiNET Minecraft Pocket Edition server.");

		        config.Service(serviceConfig =>
		        {
			        serviceConfig.ServiceFactory((a, b) =>
			        {
				       return new MiNETService();
			        });
			        serviceConfig.OnStart((service, extraArguments) =>
			        {
				        service.Start();
			        });

			        serviceConfig.OnStop(service =>
			        {
				        service.Stop();
			        });

			        serviceConfig.OnError(e =>
			        {
						Log.Error("Service threw an error!", e);
			        });
		        });
	        });
        }
    }
}
