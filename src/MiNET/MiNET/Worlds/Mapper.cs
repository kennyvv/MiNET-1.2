using System;

namespace MiNET.Worlds
{
	public class Mapper : Tuple<int, Func<int, byte, byte>>
	{
		public Mapper(int blockId, Func<int, byte, byte> dataMapper)
			: base(blockId, dataMapper)
		{
		}
	}
}