namespace MiNET.Worlds
{
	public interface ICachingWorldProvider
	{
		ChunkColumn[] GetCachedChunks();
		void ClearCachedChunks();
	}
}
