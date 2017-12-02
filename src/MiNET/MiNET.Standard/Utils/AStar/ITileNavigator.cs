using System;
using System.Collections.Generic;
using System.Text;

namespace MiNET.Utils.AStar
{
	public interface ITileNavigator
	{
		IEnumerable<Tile> Navigate(Tile from, Tile to, int maxAttempts = Int32.MaxValue);
	}
}
