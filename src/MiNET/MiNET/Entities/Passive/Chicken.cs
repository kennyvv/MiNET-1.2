using System;
using System.Numerics;
using MiNET.Entities.Behaviors;
using MiNET.Items;
using MiNET.Worlds;

namespace MiNET.Entities.Passive
{
	public class Chicken : PassiveMob
	{
		private int _timeUntilLayEgg = 0;

		public Chicken(Level level, Random rnd = null) : base(EntityType.Chicken, level)
		{
			EntityTypeId = 0x130a;
			Width = Length = 0.4;
			Height = 0.7;
			HealthManager.MaxHealth = 40;
			HealthManager.ResetHealth();
			Speed = 0.25f;
			CanClimb = true;
			IsAffectedByGravity = true;

			var random = rnd ?? new Random((int)DateTime.UtcNow.Ticks);
			_timeUntilLayEgg = 6000 + random.Next(6000);

			Behaviors.Add(new PanicBehavior(this, 60, Speed, 1.4));
			Behaviors.Add(new TemptedBehavior(this, typeof(ItemWheatSeeds), 10, 1.0)); //TODO: Add other seeds
			Behaviors.Add(new WanderBehavior(this, Speed, 1.0));
			Behaviors.Add(new LookAtPlayerBehavior(this));
			Behaviors.Add(new RandomLookaroundBehavior(this));
		}

		public override void OnTick(Entity[] entities)
		{
			base.OnTick(entities);

			if (!IsOnGround && Velocity.Y < 0.0D)
			{
				Velocity *= new Vector3(1, 0.6f, 1);
			}

			if (_timeUntilLayEgg-- <= 0)
			{
				Level.DropItem(KnownPosition, new ItemEgg());
				_timeUntilLayEgg = 6000 + Level.Random.Next(6000);
			}
		}

		public override Item[] GetDrops()
		{
			Random random = new Random();
			return new[]
			{
				ItemFactory.GetItem(365),
				ItemFactory.GetItem(288, 0, random.Next(1, 3)),
			};
		}
	}
}