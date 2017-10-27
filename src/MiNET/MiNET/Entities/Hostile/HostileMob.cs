﻿using MiNET.Worlds;

namespace MiNET.Entities.Hostile
{
	public abstract class HostileMob : Mob
	{
		protected HostileMob(int entityTypeId, Level level)
			: base(entityTypeId, level)
		{
			NoAi = true;
		}

		protected HostileMob(EntityType type, Level level)
			: base(type, level)
		{
			NoAi = true;
		}
	}
}