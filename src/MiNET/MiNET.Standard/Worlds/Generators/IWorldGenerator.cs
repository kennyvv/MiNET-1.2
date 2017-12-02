using MiNET.Utils;

namespace MiNET.Worlds.Generators
{
	public interface IWorldGenerator
	{
		void Initialize();

		ChunkColumn GenerateChunkColumn(ChunkCoordinates chunkCoordinates);
	}
}