using System;
using NUnit.Framework;
using Titan.Core;

namespace Titan.Tests
{
    /// <summary>FEEL-1: hitstop budget, the danger guard, reduced effects, the hit streak, haptics, frame stats.</summary>
    public class FeelTests
    {
        static readonly FeelConfig Cfg = new FeelConfig();

        [Test]
        public void HitStopNeverFreezesMoreThanTenPercentOfAnySecond()
        {
            var rng = new SeededRandom(20261015);
            var warp = new TimeWarp(Cfg);
            const float dt = 0.001f;
            // 120 s of random requests: bursts of pieces (0.08), heads (0.06 + slow-mo) and defeats (0.12).
            var frozen = new bool[120000];
            float t = 0f;
            for (int k = 0; k < frozen.Length; k++, t += dt)
            {
                if (rng.Value() < 0.004f)
                {
                    float seconds = rng.Range(0, 3) == 0 ? 0.12f : rng.Range(0, 2) == 0 ? 0.08f : 0.06f;
                    float granted = warp.HitStop(t, seconds, 0.05f, false, 0.3f);
                    Assert.LessOrEqual(granted, 0.12f + 1e-4f, "never more than the largest request");
                    if (rng.Value() < 0.3f) warp.SlowMo(t, 0.15f, 0.35f, false, 0.3f);
                }
                frozen[k] = warp.Scale(t) <= Cfg.FreezeScale;
                Assert.LessOrEqual(warp.FrozenIn(t - Cfg.HitStopWindow, t), Cfg.HitStopMaxFraction * Cfg.HitStopWindow + 1e-4f, "granted freeze in the second ending at " + t);
            }
            int window = (int)(Cfg.HitStopWindow / dt), inWindow = 0, worst = 0;
            for (int k = 0; k < frozen.Length; k++)
            {
                if (frozen[k]) inWindow++;
                if (k >= window && frozen[k - window]) inWindow--;
                worst = Math.Max(worst, inWindow);
            }
            Assert.LessOrEqual(worst * dt, Cfg.HitStopMaxFraction * Cfg.HitStopWindow + 5 * dt, "worst rolling second, sampled every 1 ms (one sample of rounding per stop)");
            Assert.Greater(worst * dt, 0.05f, "the test does reach the cap region");
        }

        [Test]
        public void HitStopCallsNeverStack()
        {
            var warp = new TimeWarp(Cfg);
            Assert.AreEqual(0.08f, warp.HitStop(10f, 0.08f, 0.05f, false, 0f), 1e-5f);
            // A second call 0.01 s later: the max remaining wins (0.07 left vs 0.06 asked), nothing is added.
            Assert.AreEqual(0.07f, warp.HitStop(10.01f, 0.06f, 0.05f, false, 0f), 1e-5f);
            Assert.AreEqual(0.08f, warp.FrozenIn(9f, 11f), 1e-5f);
            Assert.AreEqual(0.05f, warp.Scale(10.079f));
            Assert.AreEqual(1f, warp.Scale(10.081f));
        }

        [Test]
        public void TheDefeatStopIsCappedAtTheBudget()
        {
            var warp = new TimeWarp(Cfg);
            Assert.AreEqual(0.10f, warp.HitStop(5f, Cfg.DefeatStop, Cfg.StopScale, false, 0f), 1e-4f, "0.12 asked, 0.10 allowed");
            // Right after, a piece asks again: nothing is left in this second.
            Assert.AreEqual(0f, warp.HitStop(5.2f, 0.08f, 0.05f, false, 0f), 1e-4f);
            // A second later the budget is back.
            Assert.AreEqual(0.08f, warp.HitStop(6.2f, 0.08f, 0.05f, false, 0f), 1e-4f);
        }

        [Test]
        public void ReducedEffectsReturnEarly()
        {
            var warp = new TimeWarp(Cfg);
            Assert.AreEqual(0f, warp.HitStop(1f, 0.08f, 0.05f, true, 0f));
            Assert.IsFalse(warp.SlowMo(1f, 0.15f, 0.35f, true, 0f));
            Assert.AreEqual(1f, warp.Scale(1.01f));
            Assert.AreEqual(0f, warp.FrozenIn(0f, 2f));
        }

        [Test]
        public void NoSlowMoOfAnyKindInTheLastFifteenPercent()
        {
            var warp = new TimeWarp(Cfg);
            Assert.AreEqual(0f, warp.HitStop(1f, 0.08f, 0.05f, false, 0.85f));
            Assert.IsFalse(warp.SlowMo(1f, 0.15f, 0.35f, false, 0.85f));
            Assert.IsFalse(warp.SlowMo(1f, 0.15f, 0.35f, false, 0.97f));
            Assert.AreEqual(1f, warp.Scale(1.05f));
            Assert.Greater(warp.HitStop(1f, 0.08f, 0.05f, false, 0.849f), 0f, "just before the line it is allowed");
        }

        [Test]
        public void SlowMoFollowsTheHitStopAndLastsAtMostFifteenHundredths()
        {
            var warp = new TimeWarp(Cfg);
            warp.HitStop(2f, 0.06f, 0.05f, false, 0.2f);
            Assert.IsTrue(warp.SlowMo(2f, 1.0f, 0.35f, false, 0.2f));
            Assert.AreEqual(0.05f, warp.Scale(2.03f));
            Assert.AreEqual(0.35f, warp.Scale(2.07f));
            Assert.IsTrue(warp.SlowMoActive(2.2f));
            Assert.AreEqual(1f, warp.Scale(2.06f + 0.15f + 0.001f), "capped at 0.15 s unscaled");
            warp.HitStop(3f, 0.08f, 0.05f, false, 0.2f);
            warp.SlowMo(3f, 0.15f, 0.35f, false, 0.2f);
            warp.Cancel(3.01f);
            Assert.AreEqual(1f, warp.Scale(3.02f), "pause and game over return the scale to 1");
            Assert.AreEqual(0.01f, warp.FrozenIn(2.5f, 4f), 1e-4f, "a cancelled stop only counts what ran");
        }

        [Test]
        public void TriggerTable()
        {
            var small = Juice.ForPiece(Cfg, 11, Monster.LeftArm, true);
            Assert.AreEqual(0f, small.StopSeconds);
            Assert.IsFalse(small.FovPunch);
            var big = Juice.ForPiece(Cfg, 12, Monster.LeftArm, false);
            Assert.AreEqual(0.08f, big.StopSeconds);
            Assert.IsTrue(big.FovPunch);
            Assert.AreEqual(0f, big.SlowMoSeconds);
            Assert.IsFalse(big.Haptic);
            var hugeBroken = Juice.ForPiece(Cfg, 45, Monster.Body, false);
            Assert.AreEqual(0f, hugeBroken.SlowMoSeconds, "a broken body chunk gets no slow-mo");
            Assert.IsTrue(hugeBroken.Haptic, "but a 40+ piece vibrates");
            var head = Juice.ForPiece(Cfg, 40, Monster.Head, false);
            Assert.AreEqual(0.08f, head.StopSeconds, "the larger stop wins");
            Assert.AreEqual(0.15f, head.SlowMoSeconds);
            var clean = Juice.ForPiece(Cfg, 52, Monster.RightArm, true);
            Assert.AreEqual(0.15f, clean.SlowMoSeconds);
            Assert.IsTrue(Juice.DefeatHaptic(TitanRole.Boss, false));
            Assert.IsTrue(Juice.DefeatHaptic(TitanRole.Normal, true));
            Assert.IsFalse(Juice.DefeatHaptic(TitanRole.Sprint, false));
        }

        [Test]
        public void HitPitchRisesPerSlotAndResets()
        {
            var s = new HitStreak(Cfg, 6);
            Assert.AreEqual(1f, s.Hit(0, 0f));
            Assert.AreEqual(1.04f, s.Hit(0, 0.3f), 1e-5f);
            Assert.AreEqual(1.08f, s.Hit(0, 0.6f), 1e-5f);
            Assert.AreEqual(1f, s.Hit(1, 0.61f), "slots are independent");
            Assert.AreEqual(1f, s.Hit(0, 1.2f), "0.5 s without a hit resets the streak");
            float t = 2f, p = 0f;
            for (int i = 0; i < 40; i++, t += 0.1f) p = s.Hit(2, t);
            Assert.AreEqual(1.6f, p, 1e-5f, "capped at 1.6");
            Assert.AreEqual(1.6f, Juice.HitPitch(Cfg, 15), 1e-5f);
            Assert.AreEqual(1.56f, Juice.HitPitch(Cfg, 14), 1e-5f);
        }

        [Test]
        public void HapticsAtMostOncePerIntervalAndNeverWhenOff()
        {
            var gate = new HapticGate(Cfg);
            var rng = new SeededRandom(7);
            float t = 0f, last = -10f;
            for (int i = 0; i < 5000; i++)
            {
                t += rng.Value() * 0.3f;
                if (gate.TryFire(t, true))
                {
                    Assert.GreaterOrEqual(t - last, Cfg.HapticMinInterval - 1e-5f);
                    last = t;
                }
            }
            Assert.Greater(gate.Fired, 100);
            var off = new HapticGate(Cfg);
            for (int i = 0; i < 100; i++) Assert.IsFalse(off.TryFire(i * 10f, false));
            Assert.AreEqual(0, off.Fired);
        }

        [Test]
        public void FrameStatsP95OverTheLast300Frames()
        {
            var f = new FrameStats(300);
            for (int i = 0; i < 1000; i++) f.Add(100f); // old slow frames roll out
            for (int i = 0; i < 285; i++) f.Add(16f);
            for (int i = 0; i < 15; i++) f.Add(40f);
            Assert.AreEqual(300, f.Count);
            Assert.AreEqual(16f, f.P95, "15 of 300 frames at 40 ms is exactly the 5% tail");
            f.Add(40f);
            Assert.AreEqual(40f, f.P95);
            Assert.AreEqual(1000f / ((284 * 16f + 16 * 40f) / 300f), f.Fps, 0.01f);
        }

        [Test]
        public void AbandonedRunCountsAndKeepsTheCheckpoint()
        {
            var profile = new Profile { bestScore = 10 };
            profile.SaveCheckpoint(6, 1234, 99);
            var telemetry = new Telemetry(() => profile);
            var s = new RunSession(profile, telemetry, new TitanConfig(), RunStart.Fresh(3));
            for (int i = 0; i < 400 && s.Game.Score <= 10; i++)
            {
                if (i % 10 == 0)
                    for (int k = 0; k < s.Game.Tray.Length; k++)
                        if (s.Game.CanDeploy(k)) { s.Game.Deploy(k); break; }
                s.Game.Tick(0.05f);
                foreach (var e in s.Game.Events) s.Process(e);
                s.Game.Events.Clear();
            }
            int score = s.Game.Score;
            Assert.Greater(score, 10);
            var signal = s.Abandon();
            Assert.AreEqual(SessionSignal.Save | SessionSignal.RunEnded, signal);
            Assert.AreEqual(1, profile.totalRuns);
            Assert.AreEqual(score, profile.bestScore, "the score of an abandoned run still counts");
            Assert.IsTrue(profile.HasCheckpoint, "the checkpoint stays");
            var end = profile.log.FindLast(l => l.e == Telemetry.RunEnd);
            Assert.AreEqual(Telemetry.ReasonAbandoned, end.b);
        }
    }
}
