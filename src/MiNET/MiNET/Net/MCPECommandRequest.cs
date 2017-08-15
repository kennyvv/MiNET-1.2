using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MiNET.Utils;

namespace MiNET.Net
{
	public partial class McpeCommandRequest : Package<McpeCommandRequest>
	{
		public long playerId;
		partial void AfterDecode()
		{
			if (commandType == (int) CommandRequestType.DevConsole || commandType == (int) CommandRequestType.Player)
			{
				playerId = ReadSignedVarLong();
			}
		}

		partial void AfterEncode()
		{
			if (commandType == (int) CommandRequestType.DevConsole || commandType == (int) CommandRequestType.Player)
			{
				WriteSignedVarLong(playerId);
			}
		}
	}
}
