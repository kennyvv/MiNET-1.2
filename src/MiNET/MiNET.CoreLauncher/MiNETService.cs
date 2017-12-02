using System;
using log4net;
using PeterKottas.DotNetCore.WindowsService.Base;
using PeterKottas.DotNetCore.WindowsService.Interfaces;

namespace MiNET.Service
{
    public class MiNETService : MicroService, IMicroService
    {
	    private static readonly ILog Log = LogManager.GetLogger(typeof (MiNETService));
	    
		public MiNetServer Server { get; }

	    public MiNETService()
	    {
			this.StartBase();
		    Server = new MiNetServer();
	    }

	    public void Start()
	    {
		    Server.StartServer();
	    }

	    public void Stop()
	    {
			this.StopBase();
		    Server.StopServer();
	    }
    }
}
