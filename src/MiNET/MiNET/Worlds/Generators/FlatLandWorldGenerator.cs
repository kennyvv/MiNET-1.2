#region LICENSE

// The contents of this file are subject to the Common Public Attribution
// License Version 1.0. (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// https://github.com/NiclasOlofsson/MiNET/blob/master/LICENSE. 
// The License is based on the Mozilla Public License Version 1.1, but Sections 14 
// and 15 have been added to cover use of software over a computer network and 
// provide for limited attribution for the Original Developer. In addition, Exhibit A has 
// been modified to be consistent with Exhibit B.
// 
// Software distributed under the License is distributed on an "AS IS" basis,
// WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License for
// the specific language governing rights and limitations under the License.
// 
// The Original Code is Niclas Olofsson.
// 
// The Original Developer is the Initial Developer.  The Initial Developer of
// the Original Code is Niclas Olofsson.
// 
// All portions of the code written by Niclas Olofsson are Copyright (c) 2014-2017 Niclas Olofsson. 
// All Rights Reserved.

#endregion

using MiNET.Utils;

namespace MiNET.Worlds.Generators
{
	public class FlatLandWorldGenerator : IWorldGenerator
	{
		public FlatLandWorldGenerator()
		{
		}

		public void Initialize()
		{
		}

		public ChunkColumn GenerateChunkColumn(ChunkCoordinates chunkCoordinates)
		{
			ChunkColumn chunk = new ChunkColumn();
			chunk.x = chunkCoordinates.X;
			chunk.z = chunkCoordinates.Z;

			PopulateChunk(chunk);

			return chunk;
		}

		public void PopulateChunk(ChunkColumn chunk)
		{
			for (int x = 0; x < 16; x++)
			{
				for (int z = 0; z < 16; z++)
				{
					chunk.SetBlock(x, 1, z, 7); // Bedrock
					chunk.SetBlock(x, 2, z, 3); // Dirt
					chunk.SetBlock(x, 3, z, 3); // Dirt
					chunk.SetBlock(x, 4, z, 2); // Grass
					chunk.SetHeight(x, z, 4);
				}
			}
		}
	}
}