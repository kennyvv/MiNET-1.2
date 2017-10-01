using MiNET.Utils;

namespace MiNET.Worlds.Generators
{
	public class AirWorldGenerator : IWorldGenerator
	{
		public void Initialize()
		{
		}

		public ChunkColumn GenerateChunkColumn(ChunkCoordinates chunkCoordinates)
		{
			return new ChunkColumn() {x = chunkCoordinates.X, z = chunkCoordinates.Z};
		}
	}
}