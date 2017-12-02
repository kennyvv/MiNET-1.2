using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiNET.Net
{
	public partial class McpeMovePlayer : Package<McpeMovePlayer>
	{
		public TeleportCause Cause;
		partial void AfterEncode()
		{
			if (mode == (int) Mode.Teleport)
			{
				Write((int)TeleportCause.Unknown);
				Write((int)1);
			}
		}
	}
}
