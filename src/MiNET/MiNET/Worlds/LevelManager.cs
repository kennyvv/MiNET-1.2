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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using MiNET.Utils;
using MiNET.Worlds;
using MiNET.Worlds.Generators;
using MiNET.Worlds.Generators.Survival;

namespace MiNET
{
	public class LevelManager
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof (LevelManager));

		public List<Level> Levels { get; set; } = new List<Level>();

		public EntityManager EntityManager { get; set; } = new EntityManager();

		private readonly GameMode _gameMode = Config.GetProperty("GameMode", GameMode.Survival);
		private readonly Difficulty _difficulty = Config.GetProperty("Difficulty", Difficulty.Normal);
		private readonly int _viewDistance = Config.GetProperty("ViewDistance", 11);
		private readonly bool _enableBlockTicking = Config.GetProperty("EnableBlockTicking", false);
		private readonly bool _enableChunkTicking = Config.GetProperty("EnableChunkTicking", false);
		private readonly bool _isWorldTimeStarted = Config.GetProperty("IsWorldTimeStarted", false);
		public LevelManager()
		{
		}

		public virtual Level GetLevel(Player player, string name)
		{
			Level level = Levels.FirstOrDefault(l => l.LevelId.Equals(name, StringComparison.InvariantCultureIgnoreCase));
			if (level == null)
			{
				AnvilWorldProvider worldProvider = new AnvilWorldProvider
				{
					MissingChunkProvider = new OverworldGenerator(),
					ReadSkyLight = !Config.GetProperty("CalculateLights", false),
					ReadBlockLight = !Config.GetProperty("CalculateLights", false),
				};

				level = new Level(this, name, worldProvider, EntityManager, _gameMode, _difficulty, _viewDistance)
				{
					EnableBlockTicking = _enableBlockTicking,
					EnableChunkTicking = _enableChunkTicking,
					IsWorldTimeStarted = _isWorldTimeStarted
				};
				level.Initialize();

				Levels.Add(level);

				OnLevelCreated(new LevelEventArgs(null, level));
			}

			return level;
		}

		public static void RecalculateBlockLight(Level level, AnvilWorldProvider wp)
		{
			var sources = wp.LightSources.ToArray();
			Parallel.ForEach(sources, block => { BlockLightCalculations.Calculate(level, block.Coordinates); });
		}

		public void RemoveLevel(Level level)
		{
			if (Levels.Contains(level))
			{
				Levels.Remove(level);
			}

			level.Close();
		}

		public event EventHandler<LevelEventArgs> LevelCreated;

		protected virtual void OnLevelCreated(LevelEventArgs e)
		{
			LevelCreated?.Invoke(this, e);
		}

		public virtual Level GetDimension(Level level, Dimension dimension)
		{
			if (dimension == Dimension.Overworld) throw new Exception($"Can not get level for '{dimension}' from the LevelManager");
			if (dimension == Dimension.Nether && !level.WorldProvider.HaveNether()) return null;
			if (dimension == Dimension.TheEnd && !level.WorldProvider.HaveTheEnd()) return null;

			if (!(level.WorldProvider is AnvilWorldProvider overworld)) return null;

			var worldProvider = new AnvilWorldProvider(overworld.BasePath)
			{
				ReadBlockLight = overworld.ReadBlockLight,
				ReadSkyLight = overworld.ReadSkyLight,
				Dimension = dimension,
				MissingChunkProvider = new AirWorldGenerator(),
			};

			Level newLevel = new Level(level.LevelManager, level.LevelId + "_" + dimension, worldProvider, EntityManager, level.GameMode, level.Difficulty, level.ViewDistance)
			{
				OverworldLevel = level,
				Dimension = dimension,
				EnableBlockTicking = level.EnableBlockTicking,
				EnableChunkTicking = level.EnableChunkTicking,
				IsWorldTimeStarted = level.IsWorldTimeStarted
			};

			newLevel.Initialize();

			return newLevel;
		}
	}
}