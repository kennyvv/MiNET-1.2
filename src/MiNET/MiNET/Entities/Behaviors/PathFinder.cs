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
// The Original Code is MiNET.
// 
// The Original Developer is the Initial Developer.  The Initial Developer of
// the Original Code is Niclas Olofsson.
// 
// All portions of the code written by Niclas Olofsson are Copyright (c) 2014-2017 Niclas Olofsson. 
// All Rights Reserved.

#endregion

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using log4net;
using MiNET.Blocks;
using MiNET.Particles;
using MiNET.Utils;
using MiNET.Worlds;

namespace MiNET.Entities.Behaviors
{
	public class Pathfinder
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(Pathfinder));
		private Dictionary<ImprovedTile, Block> _blockCache = new Dictionary<ImprovedTile, Block>();

		public List<ImprovedTile> FindPath(Entity source, Entity target, double distance)
		{
			return FindPath(source, (BlockCoordinates)target.KnownPosition, distance);
		}

		public List<ImprovedTile> FindPath(Entity source, BlockCoordinates target, double distance)
		{
			try
			{
				//new EmptyBlockedProvider(), // Instance of: IBockedProvider
				var blockAccess = new CachedBlockAccess(source.Level);

				var navigator = new ImprovedTileNavigator(
					new LevelNavigator(source, blockAccess, distance, _blockCache),
					new BlockDiagonalNeighborProvider(blockAccess, (int)Math.Truncate(source.KnownPosition.Y), _blockCache, source), // Instance of: INeighborProvider
					new FastBlockDistanceAlgorithm(_blockCache)
				);

				BlockCoordinates targetPos = target;
				BlockCoordinates sourcePos = (BlockCoordinates)source.KnownPosition;
				var from = new ImprovedTile(sourcePos.X, sourcePos.Z);
				var to = new ImprovedTile(targetPos.X, targetPos.Z);

				return navigator.Navigate(from, to).ToList() ?? new List<ImprovedTile>();
			}
			catch (Exception e)
			{
				Log.Error("Navigate", e);
			}

			return new List<ImprovedTile>();
		}

		public void PrintPath(Level level, HashSet<ImprovedTile> currentPath)
		{
			if (Config.GetProperty("Pathfinder.PrintPath", false))

				foreach (var tile in currentPath)
				{
					//Log.Debug($"Steps to: {next.X}, {next.Y}");
					Block block = GetBlock(tile);
					var particle = new RedstoneParticle(level);
					particle.Position = (Vector3)block.Coordinates + new Vector3(0.5f, 0.5f, 0.5f);
					particle.Spawn();
				}
		}


		public Block GetBlock(ImprovedTile tile)
		{
			Block block;
			if (!_blockCache.TryGetValue(tile, out block))
			{
				// Do something?
				return null;
			}

			return block;
		}
	}

	public class ImprovedTile
	{
		public int X { get; }
		public int Y { get; }

		public ImprovedTile(int x, int y)
		{
			X = x;
			Y = y;
		}

		public ImprovedTile()
		{
		}

		public static bool operator ==(ImprovedTile a, ImprovedTile b) => a.X == b.X && a.Y == b.Y;

		public static bool operator !=(ImprovedTile a, ImprovedTile b) => !(a == b);

		public override bool Equals(object obj) => (this == (ImprovedTile)obj);

		public override int GetHashCode()
		{
			unchecked // Overflow is fine, just wrap
			{
				var hash = 17;
				// Suitable nullity checks etc, of course :)
				hash = hash * 23 + X.GetHashCode();
				hash = hash * 23 + Y.GetHashCode();
				return hash;
			}
		}
	}

	public class ImprovedTileNavigator
	{
		private readonly LevelNavigator blockedProvider;
		private readonly BlockDiagonalNeighborProvider neighborProvider;
		private readonly FastBlockDistanceAlgorithm distanceAlgorithm;

		public ImprovedTileNavigator(LevelNavigator blockedProvider, BlockDiagonalNeighborProvider neighborProvider, FastBlockDistanceAlgorithm distanceAlgorithm)
		{
			this.blockedProvider = blockedProvider;
			this.neighborProvider = neighborProvider;
			this.distanceAlgorithm = distanceAlgorithm;
		}

		public HashSet<ImprovedTile> Navigate(ImprovedTile from, ImprovedTile to)
		{
			var closed = new HashSet<ImprovedTile>();
			var open = new HashSet<ImprovedTile> { from };

			var path = new Dictionary<ImprovedTile, ImprovedTile>();

			var gScore = new Hashtable();
			//var gScore = new Dictionary<Tile, int>();
			gScore[from] = 0;

			var fScore = new Hashtable();
			fScore[from] = Calculate(from, to);

			while (open.Any())
			{
				var current = open
					.OrderBy(c => fScore[c])
					.First();

				if (current == to)
				{
					return new HashSet<ImprovedTile>(ReconstructPath(path, current));
				}

				open.Remove(current);
				closed.Add(current);

				foreach (ImprovedTile neighbor in neighborProvider.GetNeighbors(current))
				{
					if (closed.Contains(neighbor) || blockedProvider.IsBlocked(neighbor))
					{
						continue;
					}

					var tentativeG = (int)gScore[current] + distanceAlgorithm.Calculate(current, neighbor);

					if (!open.Contains(neighbor))
					{
						open.Add(neighbor);
					}
					else if (tentativeG >= (int)gScore[neighbor])
					{
						continue;
					}

					path[neighbor] = current;

					gScore[neighbor] = tentativeG;
					fScore[neighbor] = (int)gScore[neighbor] + Calculate(neighbor, to);
				}
			}

			return null;
		}

		private int Calculate(ImprovedTile from, ImprovedTile to)
		{
			return Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);
		}

		private IEnumerable<ImprovedTile> ReconstructPath(IDictionary<ImprovedTile, ImprovedTile> path, ImprovedTile current)
		{
			List<ImprovedTile> tileList = new List<ImprovedTile>
			{
		current
	  };
			while (path.ContainsKey(current))
			{
				current = path[current];
				tileList.Add(current);
			}
			tileList.Reverse();
			tileList.RemoveAt(0);
			return tileList;
		}
	}

	public class FastBlockDistanceAlgorithm
	{
		private readonly Dictionary<ImprovedTile, Block> _blockCache;

		public FastBlockDistanceAlgorithm(Dictionary<ImprovedTile, Block> blockCache)
		{
			_blockCache = blockCache;
		}

		public int Calculate(ImprovedTile from, ImprovedTile to)
		{
			Vector3 vFrom = GetBlock(from).Coordinates;
			Vector3 vTo = GetBlock(to).Coordinates;
			return (int) Vector3.Distance(vFrom, vTo);
		}

		public Block GetBlock(ImprovedTile tile)
		{
			Block block;
			if (!_blockCache.TryGetValue(tile, out block))
			{
				return null;
			}

			return block;
		}
	}

	public class BlockDiagonalNeighborProvider
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(BlockDiagonalNeighborProvider));

		private readonly CachedBlockAccess _level;
		private readonly int _startY;
		private readonly Entity _entity;
		private readonly Dictionary<ImprovedTile, Block> _blockCache;

		public BlockDiagonalNeighborProvider(CachedBlockAccess level, int startY, Dictionary<ImprovedTile, Block> blockCache, Entity entity)
		{
			_level = level;
			_startY = startY;
			_blockCache = blockCache;
			_entity = entity;
		}

		private static readonly int[,] Neighbors = {
			{
				0,
				-1
			},
			{
				1,
				0
			},
			{
				0,
				1
			},
			{
				-1,
				0
			},
			{
				-1,
				-1
			},
			{
				1,
				-1
			},
			{
				1,
				1
			},
			{
				-1,
				1
			}
		};

		public IEnumerable<ImprovedTile> GetNeighbors(ImprovedTile tile)
		{
			Block block;
			if (!_blockCache.TryGetValue(tile, out block))
			{
				block = _level.GetBlock(new BlockCoordinates(tile.X, _startY, tile.Y));
				_blockCache.Add(tile, block);
			}

			List<ImprovedTile> list = new List<ImprovedTile>();
			for (int index = 0; index < Neighbors.GetLength(0); ++index)
			{
				var item = new ImprovedTile(tile.X + Neighbors[index, 0], tile.Y + Neighbors[index, 1]);

				// Check for too high steps
				BlockCoordinates coord = new BlockCoordinates(item.X, block.Coordinates.Y, item.Y);
				if (_level.GetBlock(coord).IsSolid)
				{
					Block blockUp = _level.GetBlock(coord + BlockCoordinates.Up);
					if (blockUp.IsSolid)
					{
						// Can't jump
						continue;
					}

					if (IsObstructed(blockUp.Coordinates)) continue;

					_blockCache[item] = blockUp;
				}
				else
				{
					var blockDown = _level.GetBlock(coord + BlockCoordinates.Down);
					if (!blockDown.IsSolid)
					{
						if (!_level.GetBlock(coord + BlockCoordinates.Down + BlockCoordinates.Down).IsSolid)
						{
							// Will fall
							continue;
						}

						if (IsObstructed(blockDown.Coordinates)) continue;

						_blockCache[item] = blockDown;
					}
					else
					{
						if (IsObstructed(coord)) continue;

						_blockCache[item] = _level.GetBlock(coord);
					}
				}

				list.Add(item);
			}

			CheckDiagonals(block, list);

			return list;
		}

		private bool IsObstructed(BlockCoordinates coord)
		{
			for (int i = 1; i < _entity.Height; i++)
			{
				if (IsBlocked(coord + (BlockCoordinates.Up * i))) return true;
			}

			return false;
		}

		private bool IsBlocked(BlockCoordinates coord)
		{
			var block = _level.GetBlock(coord);

			if (block == null || block.IsSolid)
			{
				return true;
			}
			return false;
		}

		private void CheckDiagonals(Block block, List<ImprovedTile> list)
		{
			// if no north, remove all north
			if (!list.Contains(TileFromBlock(block.Coordinates + BlockCoordinates.North)))
			{
				//Log.Debug("Removed north");
				list.Remove(TileFromBlock(block.Coordinates + BlockCoordinates.North + BlockCoordinates.East));
				list.Remove(TileFromBlock(block.Coordinates + BlockCoordinates.North + BlockCoordinates.West));
			}
			// if no south, remove all south
			if (!list.Contains(TileFromBlock(block.Coordinates + BlockCoordinates.South)))
			{
				//Log.Debug("Removed south");
				list.Remove(TileFromBlock(block.Coordinates + BlockCoordinates.South + BlockCoordinates.East));
				list.Remove(TileFromBlock(block.Coordinates + BlockCoordinates.South + BlockCoordinates.West));
			}
			// if no west, remove all west
			if (!list.Contains(TileFromBlock(block.Coordinates + BlockCoordinates.West)))
			{
				//Log.Debug("Removed west");
				list.Remove(TileFromBlock(block.Coordinates + BlockCoordinates.West + BlockCoordinates.North));
				list.Remove(TileFromBlock(block.Coordinates + BlockCoordinates.West + BlockCoordinates.South));
			}
			// if no east, remove all east
			if (!list.Contains(TileFromBlock(block.Coordinates + BlockCoordinates.East)))
			{
				//Log.Debug("Removed east");
				list.Remove(TileFromBlock(block.Coordinates + BlockCoordinates.East + BlockCoordinates.North));
				list.Remove(TileFromBlock(block.Coordinates + BlockCoordinates.East + BlockCoordinates.South));
			}
		}

		private ImprovedTile TileFromBlock(BlockCoordinates coord)
		{
			return new ImprovedTile(coord.X, coord.Z);
		}
	}

	public class LevelNavigator
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(LevelNavigator));

		private readonly Entity _entity;
		private readonly IBlockAccess _level;
		private readonly double _distance;
		private readonly Dictionary<ImprovedTile, Block> _blockCache;

		public LevelNavigator(Entity entity, IBlockAccess level, double distance, Dictionary<ImprovedTile, Block> blockCache)
		{
			_entity = entity;
			_level = level;
			_distance = distance;
			_blockCache = blockCache;
		}

		public bool IsBlocked(ImprovedTile coord)
		{
			Block block;
			if (!_blockCache.TryGetValue(coord, out block))
			{
				return true;
			}

			if (block.IsSolid) return true;

			if (Math.Abs(_entity.KnownPosition.Y - block.Coordinates.Y) > _entity.Height + 3) return true;

			Vector2 entityPos = new Vector2(_entity.KnownPosition.X, _entity.KnownPosition.Z);
			Vector2 tilePos = new Vector2(coord.X, coord.Y);

			if (Vector2.Distance(entityPos, tilePos) > _distance) return true;

			BlockCoordinates blockCoordinates = block.Coordinates;

			if (IsObstructed(blockCoordinates)) return true;

			return false;
		}

		private bool IsObstructed(BlockCoordinates coord)
		{
			for (int i = 1; i < _entity.Height; i++)
			{
				if (IsBlocked(coord + (BlockCoordinates.Up * i))) return true;
			}

			return false;
		}

		private bool IsBlocked(BlockCoordinates coord)
		{
			var block = _level.GetBlock(coord);

			if (block == null || block.IsSolid)
			{
				return true;
			}
			return false;
		}
	}

	public class CachedBlockAccess : IBlockAccess
	{
		private Level _level;
		private IDictionary<BlockCoordinates, Block> _blockCache = new ConcurrentDictionary<BlockCoordinates, Block>();

		public CachedBlockAccess(Level level)
		{
			_level = level;
		}

		public ChunkColumn GetChunk(BlockCoordinates coordinates, bool cacheOnly = false)
		{
			return _level.GetChunk(coordinates, cacheOnly);
		}

		public ChunkColumn GetChunk(ChunkCoordinates coordinates, bool cacheOnly = false)
		{
			return _level.GetChunk(coordinates, cacheOnly);
		}

		public void SetSkyLight(BlockCoordinates coordinates, byte skyLight)
		{
			_blockCache.Remove(coordinates);
			_level.SetSkyLight(coordinates, skyLight);
		}

		public int GetHeight(BlockCoordinates coordinates)
		{
			return _level.GetHeight(coordinates);
		}

		public Block GetBlock(BlockCoordinates coord, ChunkColumn tryChunk = null)
		{
			Block block;
			if (!_blockCache.TryGetValue(coord, out block))
			{
				block = _level.GetBlock(coord);
				_blockCache[coord] = block;
			}

			return block;
		}

		public void SetBlock(Block block, bool broadcast = true, bool applyPhysics = true, bool calculateLight = true)
		{
			_blockCache.Remove(block.Coordinates);
			_level.SetBlock(block, broadcast, applyPhysics, calculateLight);
		}
	}
}