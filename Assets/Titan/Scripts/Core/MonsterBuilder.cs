using System;
using System.Collections.Generic;

namespace Titan.Core
{
    public interface IRandom
    {
        /// <summary>Integer in [min, maxExclusive).</summary>
        int Range(int min, int maxExclusive);
        /// <summary>Float in [0, 1).</summary>
        float Value();
    }

    public sealed class SystemRandom : IRandom
    {
        readonly Random random;
        public SystemRandom() { random = new Random(); }
        public SystemRandom(int seed) { random = new Random(seed); }
        public int Range(int min, int maxExclusive) => random.Next(min, maxExclusive);
        public float Value() => (float)random.NextDouble();
    }

    /// <summary>
    /// Builds a humanoid titan out of boxes: boots, legs, a torso with a hidden core, shoulders, arms, a neck and a head.
    /// Legs and torso are the anchored body. Shoulders and neck are thin joints in their own color:
    /// shoot them away and the arm or the head breaks off. Every cube carries a part tag (body, left arm, right arm, head),
    /// joints are tagged as body, and the arm color never matches the joints or the head.
    /// Every level is bigger, deeper and uses more colors.
    /// </summary>
    /// <summary>The colors of every body region of a titan (palette indices 0-5).</summary>
    public struct TitanColors
    {
        public int Boot, Leg, Torso, Core, Belt, Joint, Arm, Neck, Head, Eye;
    }

    public static class MonsterBuilder
    {
        public const int MaxColors = 6;

        public static int ColorsForLevel(int level) => Math.Min(3 + (level - 1) / 2, MaxColors);

        /// <param name="extraColors">Colors on top of <see cref="ColorsForLevel"/> (capped at MaxColors), e.g. TitanConfig.BossExtraColor.</param>
        public static Monster Build(int level, IRandom rng, int extraColors = 0)
        {
            level = Math.Max(1, level);
            int colorCount = Math.Min(ColorsForLevel(level) + Math.Max(0, extraColors), MaxColors);
            var palette = new List<int>();
            for (int c = 0; c < MaxColors; c++) palette.Add(c);
            Shuffle(palette, rng);
            palette = palette.GetRange(0, colorCount);

            // The arm color never matches a joint (shoulder, neck) or the head: an arm cannon eats its arm from the
            // hand up and never cuts anything by itself (DESIGN.md 4.2.3). The hand has the arm color.
            int boot = Pick(palette, rng);
            int leg = Pick(palette, rng, boot);
            int torso = Pick(palette, rng, leg);
            int core = Pick(palette, rng, torso);
            int belt = Pick(palette, rng, torso);
            int joint = Pick(palette, rng, torso);
            int arm = Pick(palette, rng, joint);
            int neck = Pick(palette, rng, torso, arm);
            int head = Pick(palette, rng, neck, arm);
            int eye = Pick(palette, rng, head);

            return Build(level, new TitanColors { Boot = boot, Leg = leg, Torso = torso, Core = core, Belt = belt, Joint = joint, Arm = arm, Neck = neck, Head = head, Eye = eye });
        }

        /// <summary>
        /// The titan of <paramref name="level"/> with a fixed palette (scripted titans, FTUE-1). Uses no random numbers:
        /// the shape depends on the level only.
        /// </summary>
        public static Monster Build(int level, TitanColors colors)
        {
            level = Math.Max(1, level);
            int boot = colors.Boot, leg = colors.Leg, torso = colors.Torso, core = colors.Core, belt = colors.Belt;
            int joint = colors.Joint, arm = colors.Arm, neck = colors.Neck, head = colors.Head, eye = colors.Eye;
            int depth = Math.Min(2 + (level - 1) / 3, 4);
            int legH = 3 + (level - 1) / 3;
            int legW = level >= 4 ? 3 : 2;
            int gap = level >= 3 ? 3 : 1;              // odd, so the titan stays symmetric
            int half = Math.Max(legW + gap / 2, 1 + (level + 1) / 2);
            half = Math.Min(half, 7);
            int torsoH = Math.Min(4 + level, 11);
            int armW = 2;
            int armH = Math.Min(torsoH - 1 + (level - 1) / 3, torsoH + legH - 2);
            int headHalf = Math.Min(2 + (level - 1) / 3, half);
            int headH = Math.Min(3 + (level - 1) / 3, 6);
            int neckHalf = level >= 6 ? 1 : 0;

            var m = new Monster();

            // Legs, with boots on the ground row.
            int legInner = gap / 2 + 1;
            for (int y = 0; y < legH; y++)
                for (int i = 0; i < legW; i++)
                    for (int z = 0; z < depth; z++)
                    {
                        int color = y == 0 ? boot : leg;
                        m.Set(new Int3(-(legInner + i), y, z), color, true);
                        m.Set(new Int3(legInner + i, y, z), color, true);
                    }

            // Torso: a colored front with a belt, and a different color hidden inside.
            int torsoBottom = legH;
            int torsoTop = legH + torsoH - 1;
            int beltY = torsoBottom + torsoH / 3;
            for (int y = torsoBottom; y <= torsoTop; y++)
                for (int x = -half; x <= half; x++)
                    for (int z = 0; z < depth; z++)
                        m.Set(new Int3(x, y, z), z == 0 ? (y == beltY ? belt : torso) : core, true);

            // Shoulders (the only link between torso and arm, tagged as body joints) and arms hanging down.
            // The bottom row of each arm is the hand: same color, marked as a glove for the view's shape cue.
            for (int side = -1; side <= 1; side += 2)
            {
                int armPart = side < 0 ? Monster.LeftArm : Monster.RightArm;
                for (int z = 0; z < depth; z++) m.Set(new Int3(side * (half + 1), torsoTop, z), joint, false, Monster.Body, true);
                int armBottom = torsoTop - armH + 1;
                for (int y = armBottom; y <= torsoTop; y++)
                    for (int i = 0; i < armW; i++)
                        for (int z = 0; z < depth; z++)
                        {
                            var c = new Int3(side * (half + 2 + i), y, z);
                            m.Set(c, arm, false, armPart);
                            if (y == armBottom) m.MarkGlove(c);
                        }
            }

            // Neck (the only link to the head, tagged as body joints) and the head with two eyes on its face.
            int neckY = torsoTop + 1;
            for (int x = -neckHalf; x <= neckHalf; x++)
                for (int z = 0; z < depth; z++)
                    m.Set(new Int3(x, neckY, z), neck, false, Monster.Body, true);
            int headBottom = neckY + 1;
            int headTop = headBottom + headH - 1;
            for (int y = headBottom; y <= headTop; y++)
                for (int x = -headHalf; x <= headHalf; x++)
                    for (int z = 0; z < depth; z++)
                    {
                        bool isEye = z == 0 && y == headTop - 1 && Math.Abs(x) == Math.Max(1, headHalf - 1);
                        m.Set(new Int3(x, y, z), isEye ? eye : head, false, Monster.Head);
                    }

            return m;
        }

        /// <summary>A random palette color that is none of <paramref name="avoid"/>; the last avoid is dropped first if that is impossible.</summary>
        static int Pick(List<int> palette, IRandom rng, params int[] avoid)
        {
            for (int keep = avoid.Length; keep >= 0; keep--)
            {
                var options = new List<int>();
                foreach (var c in palette)
                    if (Array.IndexOf(avoid, c, 0, keep) < 0) options.Add(c);
                if (options.Count == 0) continue;
                return options.Count == 1 ? options[0] : options[rng.Range(0, options.Count)];
            }
            return palette[0];
        }

        static void Shuffle<T>(List<T> list, IRandom rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                var t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }
    }
}
