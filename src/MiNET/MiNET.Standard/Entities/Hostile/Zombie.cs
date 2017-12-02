using MiNET.Blocks;
using MiNET.Entities.Behaviors;
using MiNET.Utils;
using MiNET.Worlds;

namespace MiNET.Entities.Hostile
{
	public class Zombie : HostileMob, IAgeable
	{
		public override double Height
		{
			get => IsBaby ? base.Height * 0.5f : base.Height;
			set => base.Height = value;
		}

		public override double Speed
		{
			get => IsBaby ? base.Speed * 1.5f : base.Speed;
			set => base.Speed = value;
		}

		public Zombie(Level level) : base((int)EntityType.Zombie, level)
		{
			Width = Length = 0.6;
			base.Height = 1.95;
			NoAi = true;
			Speed = 0.23;

			AttackDamage = 3;

			Behaviors.Add(new MeeleAttackBehavior(this, 1.0, 35));
			Behaviors.Add(new FindAttackableTargetBehavior(this, 35));
			Behaviors.Add(new WanderBehavior(this, Speed, 1.0));
			Behaviors.Add(new LookAtPlayerBehavior(this, 8.0));
			Behaviors.Add(new RandomLookaroundBehavior(this));
		}

		public override MetadataDictionary GetMetadata()
		{
			var metadata = base.GetMetadata();
			metadata[(int)MetadataFlags.Scale] = new MetadataFloat(IsBaby ? 0.5f : 1.0);
			return metadata;
		}

		public override void OnTick(Entity[] entities)
		{
			base.OnTick(entities);

			Block block = Level.GetBlock(KnownPosition);
			if (!(block is StationaryWater) && !(block is FlowingWater) && block.SkyLight > 7 && (Level.CurrentWorldTime < 12566 || Level.CurrentWorldTime > 23450))
			{
				if (!HealthManager.IsOnFire) HealthManager.Ignite(80);
			}
			else
			{
				if (HealthManager.IsOnFire) HealthManager.Ignite(80); // last kick in the butt
			}
		}
	}
}