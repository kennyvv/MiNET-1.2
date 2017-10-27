using MiNET.Entities.Behaviors;
using MiNET.Utils;
using MiNET.Worlds;

namespace MiNET.Entities.Hostile
{
	public class Creeper : HostileMob
	{
		public int Fuse { get; set; } = -1;
		public bool DamageWorld { get; set; } = true;
		public Creeper(Level level) : base(EntityType.Creeper, level)
		{
			Width = Length = 0.6;
			Height = 1.8;
			NoAi = true;

			Behaviors.Add(new CreepBehavior(this, 0.7, 16, DamageWorld));
			Behaviors.Add(new FindAttackableTargetBehavior(this, 35));
			Behaviors.Add(new WanderBehavior(this, Speed, 0.7));
			Behaviors.Add(new LookAtPlayerBehavior(this, 8.0));
			Behaviors.Add(new RandomLookaroundBehavior(this));
		}

		public override MetadataDictionary GetMetadata()
		{
			var r = base.GetMetadata();
			r[56] = new MetadataInt(Fuse);
			
			return r;
		}
	}
}