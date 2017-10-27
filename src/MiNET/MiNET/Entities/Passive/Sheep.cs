﻿using System;
using MiNET.Entities.Behaviors;
using MiNET.Items;
using MiNET.Utils;
using MiNET.Worlds;

namespace MiNET.Entities.Passive
{
	public class Sheep : PassiveMob, IAgeable
	{
		private byte _color = 0;

		public Sheep(Level level, Random rnd = null) : base(EntityType.Sheep, level)
		{
			Width = Length = 0.9;
			Height = 1.3;
			HealthManager.MaxHealth = 80;
			HealthManager.ResetHealth();
			Speed = 0.23f;

			Random random = rnd ?? new Random();
			var d = random.NextDouble();
			_color = (byte)(d < 0.81 ? 0 : d < 0.86 ? 8 : d < 0.91 ? 7 : d < 0.96 ? 15 : d < 0.99 ? 12 : 6);

			Behaviors.Add(new PanicBehavior(this, 60, Speed, 1.25));
			Behaviors.Add(new TemptedBehavior(this, typeof(ItemWheat), 10, 1.1));
			Behaviors.Add(new EatBlockBehavior(this));
			Behaviors.Add(new WanderBehavior(this, Speed, 1.0));
			Behaviors.Add(new LookAtPlayerBehavior(this));
			Behaviors.Add(new RandomLookaroundBehavior(this));
		}

		public override MetadataDictionary GetMetadata()
		{
			var metadata = base.GetMetadata();
			metadata[3] = new MetadataByte(_color);
			metadata[16] = new MetadataInt(32);
			return metadata;
		}

		public override Item[] GetDrops()
		{
			Random random = new Random();
			return new[]
			{
				ItemFactory.GetItem(35, _color, 1),
				ItemFactory.GetItem(423, 0, random.Next(1, 3)),
			};
		}
	}
}