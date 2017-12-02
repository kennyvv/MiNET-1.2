using MiNET.Blocks;
using MiNET.Utils;
using MiNET.Worlds;

namespace MiNET.Entities.Projectiles
{
	public class EnderPearl : Projectile
	{
		public EnderPearl(Player shooter, Level level) : base(shooter, 87, level, 5)
		{
			Width = 0.25;
			Length = 0.25;
			Height = 0.25;

			Gravity = 0.03;
			Drag = 0.01;

			HealthManager.IsInvulnerable = true;
		}

		protected override bool OnBlockColission(Block block)
		{
			DoTeleport();
			return true;
		}

		protected override bool OnEntityColission(Entity hitEntity)
		{
			DoTeleport();
			return true;
		}

		private void DoTeleport()
		{
			if (Shooter is Player shooter)
			{
				if (KnownPosition.Y > 0)
				{
					shooter.Teleport((PlayerLocation) KnownPosition.Clone());
					shooter.HealthManager.TakeHit(this, CalculateDamage(shooter), DamageCause.Magic);
				}
			}
		}

		private int CalculateDamage(Player shooter)
		{
			int epf = 0;
			foreach (var armor in shooter.Inventory.GetArmor())
			{
				epf += armor.GetEnchantingLevel(EnchantingType.Protection);
				epf += (3 * armor.GetEnchantingLevel(EnchantingType.FeatherFalling));
				if (epf >= 20)
				{
					epf = 20;
					break;
				}
			}

			return Damage*(1 - epf/25);
		}
	}
}
