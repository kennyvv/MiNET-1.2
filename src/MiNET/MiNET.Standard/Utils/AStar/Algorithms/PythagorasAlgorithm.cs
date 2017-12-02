using System;
using System.Collections.Generic;
using System.Text;

namespace MiNET.Utils.AStar.Algorithms
{
	public class PythagorasAlgorithm : IDistanceAlgorithm
	{
		public double Calculate(Tile from, Tile to)
		{
			return Math.Sqrt(
				Math.Pow(to.X - from.X, 2) +
				Math.Pow(to.Y - from.Y, 2)
			);
		}
	}
}
