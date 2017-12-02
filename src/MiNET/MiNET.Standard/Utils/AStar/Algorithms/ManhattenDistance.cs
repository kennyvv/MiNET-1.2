using System;
using System.Collections.Generic;
using System.Text;

namespace MiNET.Utils.AStar.Algorithms
{
	public class ManhattanHeuristicAlgorithm : IDistanceAlgorithm
	{
		public double Calculate(Tile from, Tile to) =>
			Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);
	}
}
