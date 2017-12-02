using System;
using System.Collections.Generic;
using System.Text;

namespace MiNET.Utils.AStar.Algorithms
{
	public interface IDistanceAlgorithm
	{
		double Calculate(Tile from, Tile to);
	}
}
