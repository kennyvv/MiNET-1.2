using MiNET.Entities.Behaviors;
using MiNET.Worlds;
using MiNET.Items;

namespace MiNET.Entities.Hostile
{
	public class Enderman : HostileMob
	{
		public Enderman(Level level) : base(EntityType.Enderman, level)
		{
			Width = Length = 0.6;
			Height = 2.9;
			HealthManager.MaxHealth = 400;
			HealthManager.ResetHealth();
			NoAi = true;
			Speed = 0.3f;

			AttackDamage = 7;

			Behaviors.Add(new MeeleAttackBehavior(this, 1.0, 16));
			Behaviors.Add(new FindAttackableTargetBehavior(this, 16));
			Behaviors.Add(new WanderBehavior(this, Speed, 1.0));
			Behaviors.Add(new LookAtPlayerBehavior(this, 8.0));
			Behaviors.Add(new RandomLookaroundBehavior(this));
		}
	}
}