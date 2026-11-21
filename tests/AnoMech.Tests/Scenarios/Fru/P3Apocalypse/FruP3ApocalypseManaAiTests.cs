using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Fru.P3Apocalypse;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.P3Apocalypse.FruP3ApocalypseState;

namespace AnoMech.Tests;

public class FruP3ApocalypseManaAiTests
{
    [Test]
    public void PermaswapUsesZOrderAndColoredApocalypsePositions()
    {
        var sampleTimes = new[] { 35.9f, 43.95f, WaterAt(3) - 0.1f };
        var samples = new Dictionary<float, Dictionary<PartyRole, Vector3>>();

        var run = ScenarioRun.Execute(typeof(FruP3ApocalypseScenario), 1, 77, new ScenarioRunOptions
        {
            PlayerRole = ShieldHealer,
            Overrides = settings =>
            {
                var o = (FruP3ApocalypseStateOverrides)settings;
                o.Rotation = 2;
                o.Clockwise = true;
                o.SpiritTakerTarget = MeleeDpsB;
                o.WaterDurations = [3, 2, 1, 1, 0, 0, 2, 3];
            },
            Probe = probe =>
            {
                foreach (var time in sampleTimes)
                    if (probe.Crossed(time))
                        samples[time] = Enum.GetValues<PartyRole>()
                            .ToDictionary(role => role, role => probe.Member(role)!.Position);
            },
        });

        Assert.That(run.Passed, Is.True, run.ToString());

        var spread = samples[35.9f];
        var bait = samples[43.95f];
        var water3 = samples[WaterAt(3) - 0.1f];

        Assert.Multiple(() =>
        {
            AssertNear(spread[MeleeDpsA], Spoke(5, 9.7f));
            AssertNear(spread[MainTank], Spoke(4, 9.7f));
            AssertNear(spread[OffTank], Polar(5 * 45f + 10.5f, 18.85f));
            AssertNear(spread[ShieldHealer], Polar(5 * 45f - 10.5f, 18.85f));

            AssertNear(spread[MeleeDpsB], Spoke(1, 9.7f));
            AssertNear(spread[PhysRangedDps], Spoke(0, 9.7f));
            AssertNear(spread[RegenHealer], Polar(1 * 45f + 10.5f, 18.85f));
            AssertNear(spread[CasterDps], Polar(1 * 45f - 10.5f, 18.85f));

            Assert.That(bait[OffTank].Z, Is.GreaterThan(15f));
            Assert.That(MathF.Abs(bait[OffTank].X), Is.LessThan(1f));

            foreach (var role in new[] { MeleeDpsA, MainTank, OffTank, ShieldHealer })
                Assert.That(water3[role].X, Is.GreaterThan(2f));
            foreach (var role in new[] { MeleeDpsB, PhysRangedDps, RegenHealer, CasterDps })
                Assert.That(water3[role].X, Is.LessThan(-2f));
        });
    }

    [TestCase(0, false, 1, 2, 5, 6)]
    [TestCase(0, true, 7, 6, 3, 2)]
    [TestCase(1, false, 2, 3, 6, 7)]
    [TestCase(1, true, 0, 7, 4, 3)]
    [TestCase(2, false, 7, 0, 3, 4)]
    [TestCase(2, true, 5, 4, 1, 0)]
    [TestCase(3, false, 0, 1, 4, 5)]
    [TestCase(3, true, 6, 5, 2, 1)]
    public void AllEightLightConfigurationsMatchTuuflessSpreadAndPass(
        int rotation, bool clockwise, int thSafe, int thSecond, int dpsSafe, int dpsSecond)
    {
        Dictionary<PartyRole, Vector3>? spread = null;
        var run = ScenarioRun.Execute(typeof(FruP3ApocalypseScenario), 1, 100 + rotation * 2 + (clockwise ? 1 : 0),
            new ScenarioRunOptions
            {
                PlayerRole = MainTank,
                Overrides = settings =>
                {
                    var o = (FruP3ApocalypseStateOverrides)settings;
                    o.Rotation = rotation;
                    o.Clockwise = clockwise;
                    o.SpiritTakerTarget = MainTank;
                    o.WaterDurations = [0, 1, 2, 3, 0, 1, 2, 3];
                },
                Probe = probe =>
                {
                    if (probe.Crossed(35.9f))
                        spread = Enum.GetValues<PartyRole>()
                            .ToDictionary(role => role, role => probe.Member(role)!.Position);
                },
            });

        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.Multiple(() =>
        {
            AssertNear(spread![MainTank], Spoke(clockwise ? thSafe : thSecond, 9.7f));
            AssertNear(spread[OffTank], Spoke(clockwise ? thSecond : thSafe, 9.7f));
            AssertNear(spread[RegenHealer], Polar(thSafe * 45f + 10.5f, 18.85f));
            AssertNear(spread[ShieldHealer], Polar(thSafe * 45f - 10.5f, 18.85f));

            AssertNear(spread[MeleeDpsA], Spoke(clockwise ? dpsSafe : dpsSecond, 9.7f));
            AssertNear(spread[MeleeDpsB], Spoke(clockwise ? dpsSecond : dpsSafe, 9.7f));
            AssertNear(spread[PhysRangedDps], Polar(dpsSafe * 45f + 10.5f, 18.85f));
            AssertNear(spread[CasterDps], Polar(dpsSafe * 45f - 10.5f, 18.85f));
        });
    }

    [Test]
    public void TwoPairSwapAndSwappedOffTankBaitPass()
    {
        var twoPairRun = ScenarioRun.Execute(typeof(FruP3ApocalypseScenario), 1, 55, new ScenarioRunOptions
        {
            PlayerRole = MeleeDpsA,
            Overrides = settings =>
            {
                var o = (FruP3ApocalypseStateOverrides)settings;
                o.Rotation = 1;
                o.Clockwise = false;
                o.WaterDurations = [0, 0, 1, 1, 2, 3, 2, 3];
            },
        });

        Vector3? swappedOffTankBait = null;
        var swappedOtRun = ScenarioRun.Execute(typeof(FruP3ApocalypseScenario), 1, 56, new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            Overrides = settings =>
            {
                var o = (FruP3ApocalypseStateOverrides)settings;
                o.Rotation = 2;
                o.Clockwise = true;
                o.WaterDurations = [3, 1, 1, 2, 0, 0, 2, 3];
            },
            Probe = probe =>
            {
                if (probe.Crossed(43.95f)) swappedOffTankBait = probe.Member(OffTank)!.Position;
            },
        });

        Assert.Multiple(() =>
        {
            Assert.That(twoPairRun.Passed, Is.True, twoPairRun.ToString());
            Assert.That(swappedOtRun.Passed, Is.True, swappedOtRun.ToString());
            Assert.That(swappedOffTankBait!.Value.Z, Is.LessThan(-15f));
        });
    }

    private static Vector3 Spoke(int spoke, float radius) => Polar(spoke * 45f, radius);

    private static Vector3 Polar(float degrees, float radius)
    {
        var rad = degrees * MathF.PI / 180f;
        return new Vector3(MathF.Sin(rad), 0f, -MathF.Cos(rad)) * radius;
    }

    private static void AssertNear(Vector3 actual, Vector3 expected)
        => Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.75f), $"Expected {expected}, got {actual}");
}
