using System;
using System.Collections.Generic;
using System.Text;

namespace MiNET.Utils.AStar.Providers
{
	public class EmptyBlockedProvider : IBlockedProvider
	{
		public bool IsBlocked(Tile coord) => false;
	}
}
