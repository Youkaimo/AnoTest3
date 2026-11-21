using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Fru.P2DiamondDust;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Fru.P2DiamondDust.FruP2DiamondDustState;

namespace AnoMech.Tests;

public class FruP2DiamondDustManaAiTests
{
    [TestCase(0, true, 1, 5, 7, 3, 6, 4, 0, 2)]
    [TestCase(0, false, 0, 4, 6, 2, 5, 3, 7, 1)]
    [TestCase(1, true, 0, 4, 6, 2, 5, 3, 7, 1)]
    [TestCase(1, false, 1, 5, 7, 3, 6, 4, 0, 2)]
    public void WholePartyRotatesClockwiseOnlyWhenMarkedMatchesFirstIcicleParity(
        int firstIcicle, bool supportsMarked, int mt, int st, int h1, int h2, int d1, int d2, int d3, int d4)
    {
        Dictionary<PartyRole, Vector3>? kick = null;
        var run = ScenarioRun.Execute(typeof(FruP2DiamondDustScenario), 1, 10 + firstIcicle * 2 + (supportsMarked ? 1 : 0),
            new ScenarioRunOptions
            {
                PlayerRole = MainTank,
                Overrides = settings =>
                {
                    var o = (FruP2DiamondDustStateOverrides)settings;
                    o.Axe = true;
                    o.FirstIcicle = firstIcicle;
                    o.SupportsMarked = supportsMarked;
                    o.MirrorOctant = 2;
                    o.Stillness = true;
                    o.GazeOctant = 6;
                },
                Probe = probe =>
                {
                    if (probe.Crossed(20.6f))
                        kick = Enum.GetValues<PartyRole>().ToDictionary(role => role, role => probe.Member(role)!.Position);
                },
            });

        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.Multiple(() =>
        {
            AssertOnOctant(kick![MainTank], mt);
            AssertOnOctant(kick[OffTank], st);
            AssertOnOctant(kick[RegenHealer], h1);
            AssertOnOctant(kick[ShieldHealer], h2);
            AssertOnOctant(kick[MeleeDpsA], d1);
            AssertOnOctant(kick[MeleeDpsB], d2);
            AssertOnOctant(kick[PhysRangedDps], d3);
            AssertOnOctant(kick[CasterDps], d4);
        });
    }

    [TestCase(0, 0, 4)]
    [TestCase(1, 1, 5)]
    [TestCase(2, 6, 2)]
    [TestCase(3, 7, 3)]
    public void KnockbackSplitsWToNeForGroupOneAndSwToEForGroupTwo(int firstIcicle, int groupOneOctant, int groupTwoOctant)
    {
        Dictionary<PartyRole, Vector3>? knockback = null;
        var run = ScenarioRun.Execute(typeof(FruP2DiamondDustScenario), 1, 30 + firstIcicle, new ScenarioRunOptions
        {
            PlayerRole = MainTank,
            Overrides = settings =>
            {
                var o = (FruP2DiamondDustStateOverrides)settings;
                o.Axe = false;
                o.FirstIcicle = firstIcicle;
                o.SupportsMarked = true;
                o.MirrorOctant = (firstIcicle + 2) % 8;
                o.Stillness = false;
                o.GazeOctant = 0;
            },
            Probe = probe =>
            {
                if (probe.Crossed(27.4f))
                    knockback = Enum.GetValues<PartyRole>().ToDictionary(role => role, role => probe.Member(role)!.Position);
            },
        });

        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.Multiple(() =>
        {
            foreach (var role in new[] { MainTank, RegenHealer, MeleeDpsA, PhysRangedDps })
                AssertOnOctant(knockback![role], groupOneOctant, toleranceDegrees: 28f);
            foreach (var role in new[] { OffTank, ShieldHealer, MeleeDpsB, CasterDps })
                AssertOnOctant(knockback![role], groupTwoOctant, toleranceDegrees: 28f);
        });
    }

    [TestCase(0, true, 3.25f, 7.25f)]
    [TestCase(0, false, 3.25f, 7.25f)]
    [TestCase(2, true, 2f, 6f)]
    [TestCase(2, false, 2f, 6f)]
    [TestCase(7, true, 3f, 7f)]
    [TestCase(7, false, 3f, 7f)]
    [TestCase(1, true, 6f, 6f)]
    [TestCase(1, false, 6f, 6f)]
    public void SinboundHolyMatchesAllFourManaCasesForStillnessAndSilence(
        int mirrorOctant, bool stillness, float groupOneIceOctant, float groupTwoIceOctant)
    {
        Dictionary<PartyRole, Vector3>? ice = null;
        var run = ScenarioRun.Execute(typeof(FruP2DiamondDustScenario), 1, 50 + mirrorOctant * 2 + (stillness ? 1 : 0),
            new ScenarioRunOptions
            {
                PlayerRole = ShieldHealer,
                Overrides = settings =>
                {
                    var o = (FruP2DiamondDustStateOverrides)settings;
                    o.Axe = true;
                    o.FirstIcicle = 0;
                    o.SupportsMarked = true;
                    o.MirrorOctant = mirrorOctant;
                    o.Stillness = stillness;
                    o.GazeOctant = 4;
                },
                Probe = probe =>
                {
                    if (probe.Crossed(39.9f))
                        ice = Enum.GetValues<PartyRole>().ToDictionary(role => role, role => probe.Member(role)!.Position);
                },
            });

        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.Multiple(() =>
        {
            AssertNear(ice![MainTank], Point(groupOneIceOctant, 17f));
            AssertNear(ice[RegenHealer], Point(groupOneIceOctant, 17f));
            AssertNear(ice[OffTank], Point(groupTwoIceOctant, 17f));
            AssertNear(ice[ShieldHealer], Point(groupTwoIceOctant, 17f));
        });
    }

    private static void AssertOnOctant(Vector3 actual, int octant, float toleranceDegrees = 8f)
    {
        var expected = Vector3.Normalize(Point(octant, 1f));
        var dir = Vector3.Normalize(actual);
        var angle = MathF.Acos(Math.Clamp(Vector3.Dot(expected, dir), -1f, 1f)) * 180f / MathF.PI;
        Assert.That(angle, Is.LessThan(toleranceDegrees), $"Expected octant {octant}, got {actual} (off by {angle:F1}°)");
    }

    private static void AssertNear(Vector3 actual, Vector3 expected)
        => Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1f), $"Expected {expected}, got {actual}");
}
