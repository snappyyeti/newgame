using System;

namespace Titan.Core
{
    /// <summary>
    /// A scripted titan (FTUE-1): the script may build titan n itself and put fixed cannons in its starting tray.
    /// Everything else (the deck of the other colors, the draws, the clock) stays the normal game.
    /// </summary>
    public interface ITitanScript
    {
        /// <summary>Titan n built by the script, or null for the normal builder.</summary>
        Monster BuildTitan(int n);

        /// <summary>
        /// Fixed cannons for the starting tray of titan n (index = tray slot, null = drawn normally), or null for none.
        /// Their ammo is taken out of the titan's deck, so the ammo still equals the cubes.
        /// </summary>
        Cannon[] StartingTray(int n, Monster titan, TitanConfig config);

        /// <summary>True when titan n waits for the first tap before it starts walking (<see cref="TitanGame.HoldApproach"/>).</summary>
        bool HoldApproach(int n);
    }

    /// <summary>
    /// How a run differs from the endless game: a titan script (the FTUE titan), a fixed number of titans (Titanul
    /// Zilei), and per-titan build levels, roles and clock numbers. Null fields mean "as in the endless game".
    /// Titan k of a plan is still titan number k of the run (seed, score bonus, events).
    /// </summary>
    public sealed class RunPlan
    {
        /// <summary>0 = endless; otherwise defeating titan #MaxTitans ends the run with <see cref="LoseReason.Completed"/>.</summary>
        public int MaxTitans;
        /// <summary>Build level of titan k (index k − 1); null or out of range: TitanConfig.BuildLevel(k).</summary>
        public int[] BuildLevels;
        /// <summary>Role of titan k (index k − 1); null or out of range: TitanConfig.RoleOf(k).</summary>
        public TitanRole[] Roles;
        /// <summary>The n the titan clock uses for titan k (speedup exponent and tutorial pace); null: k itself.</summary>
        public int[] ClockN;
        /// <summary>Titan 1 waits for the first tap (the daily attempt starts at the first deploy).</summary>
        public bool HoldFirstTitan;
        public ITitanScript Script;

        public int BuildLevelOf(int k, TitanConfig cfg) => BuildLevels != null && k >= 1 && k <= BuildLevels.Length ? BuildLevels[k - 1] : cfg.BuildLevel(k);
        public TitanRole RoleOf(int k, TitanConfig cfg) => Roles != null && k >= 1 && k <= Roles.Length ? Roles[k - 1] : cfg.RoleOf(k);
        public int ClockNOf(int k) => ClockN != null && k >= 1 && k <= ClockN.Length ? ClockN[k - 1] : k;
        public bool HoldsApproach(int k) => (HoldFirstTitan && k == 1) || (Script != null && Script.HoldApproach(k));

        /// <summary>The first runs of an install: titan 1 is the scripted tutorial titan (FTUE-1).</summary>
        public static RunPlan Tutorial() => new RunPlan { Script = new TutorialScript() };
    }

    /// <summary>
    /// The FTUE titan (DESIGN.md 8, FTUE-1): a level-1 titan with a fixed palette — yellow legs, red torso front and
    /// belt, yellow core, yellow shoulders and neck, blue arms and head, yellow eyes — and a yellow cannon in tray slot
    /// 0 whose ammo is exactly what the real targeting rule needs to sever both shoulders and the neck (simulated on a
    /// copy at build time, capped at the yellow cube count). The titan waits for the first tap.
    /// </summary>
    public sealed class TutorialScript : ITitanScript
    {
        public const int Red = 0, Blue = 1, Yellow = 2;

        public static readonly TitanColors Colors = new TitanColors
        {
            Boot = Yellow, Leg = Yellow, Torso = Red, Belt = Red, Core = Yellow,
            Joint = Yellow, Neck = Yellow, Arm = Blue, Head = Blue, Eye = Yellow,
        };

        public static Monster BuildTutorialTitan() => MonsterBuilder.Build(1, Colors);

        public Monster BuildTitan(int n) => n == 1 ? BuildTutorialTitan() : null;

        public bool HoldApproach(int n) => n == 1 && Ftue.HoldApproachUntilFirstDeploy;

        public Cannon[] StartingTray(int n, Monster titan, TitanConfig config)
        {
            if (n != 1) return null;
            var tray = new Cannon[config.TraySize];
            tray[0] = new Cannon(Yellow, ShotsToSeverAll(titan, Yellow, config.SlotX(0), config.TargetOrder));
            return tray;
        }

        /// <summary>
        /// Shots a cannon of <paramref name="color"/> fired alone from <paramref name="slotX"/> needs until both arms and
        /// the head have fallen, with the real targeting rule (simulated on a copy); capped at the color's cube count.
        /// </summary>
        public static int ShotsToSeverAll(Monster titan, int color, float slotX, TargetOrder order)
        {
            var m = titan.Clone();
            m.Order = order;
            int cap = m.CountOfColor(color), shots = 0;
            while (shots < cap && (m.RemainingPartSize(Monster.LeftArm) > 0 || m.RemainingPartSize(Monster.RightArm) > 0 || m.RemainingPartSize(Monster.Head) > 0))
            {
                if (!m.PickTarget(color, slotX, out var target)) break;
                shots++;
                m.Remove(target);
            }
            return Math.Max(1, shots);
        }
    }
}
