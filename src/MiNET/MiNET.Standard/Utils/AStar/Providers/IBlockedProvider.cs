using System;
using System.Collections.Generic;
using System.Text;

namespace MiNET.Utils.AStar.Providers
{
	public interface IBlockedProvider
	{
		bool IsBlocked(Tile coord);
	}
}
