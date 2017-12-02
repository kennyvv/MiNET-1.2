using System;
using System.Collections.Generic;
using System.Text;

namespace MiNET.Utils.AStar.Providers
{
	public interface INeighborProvider
	{
		IEnumerable<Tile> GetNeighbors(Tile tile);
	}
}
