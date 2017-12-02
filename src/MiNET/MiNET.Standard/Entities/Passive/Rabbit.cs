using MiNET.Entities.Behaviors;
using MiNET.Items;
using MiNET.Worlds;

namespace MiNET.Entities.Passive
{
	public class Rabbit : PassiveMob
	{
		public Rabbit(Level level) : base(EntityType.Rabbit, level)
		{
			Width = Length = 0.6;
			Height = 0.7;
			HealthManager.MaxHealth = 100;
			HealthManager.ResetHealth();
			Speed = 0.3;

			Behaviors.Add(new PanicBehavior(this, 60, Speed, 2.2));
			Behaviors.Add(new TemptedBehavior(this, typeof(ItemCarrot), 10, 1.0));
			Behaviors.Add(new WanderBehavior(this, Speed, 0.6));
			Behaviors.Add(new LookAtPlayerBehavior(this));
		}

		public override Item[] GetDrops()
		{
			return new[]
			{
				ItemFactory.GetItem(411)
			};
		}
	}
}