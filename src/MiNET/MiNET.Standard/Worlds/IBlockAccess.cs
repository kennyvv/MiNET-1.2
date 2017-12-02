using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MiNET.Blocks;
using MiNET.Utils;

namespace MiNET.Worlds
{
	public interface IBlockAccess
	{
		ChunkColumn GetChunk(BlockCoordinates coordinates, bool cacheOnly = false);
		ChunkColumn GetChunk(ChunkCoordinates coordinates, bool cacheOnly = false);
		void SetSkyLight(BlockCoordinates coordinates, byte skyLight);
		int GetHeight(BlockCoordinates coordinates);
		Block GetBlock(BlockCoordinates coord, ChunkColumn tryChunk = null);
		void SetBlock(Block block, bool broadcast = true, bool applyPhysics = true, bool calculateLight = true);
	}
}
