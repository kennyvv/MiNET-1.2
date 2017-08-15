using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace MiNET.Net
{
	public partial class McpeAdventureSettings
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof (McpeAdventureSettings));

		
		partial void AfterDecode()
		{
			//Log.Warn("Read VarInt: " + ReadVarInt());
			Log.Warn($"Size: {Bytes.Length} | UserID: {userid} | PermissonLevel: {userpermissions} | Flags: {flags} | Userflags: {userflags}");
		 } 
	}
}
