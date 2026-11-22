using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Umad.P1TeleTrouncing;
using static AnoMech.Core.Game.Party.PartyRole;
using static AnoMech.Scenarios.Umad.P1TeleTrouncing.TelePortentDirection;

namespace AnoMech.Tests;

public class UmadP1TeleTrouncingYanAiTests
{
    private const int YanStratIndex = 1;

    [TestCase(false, true, false)]
    [TestCase(false, false, false)]
    [TestCase(true, true, false)]
    [TestCase(true, false, true)]
    public void YanPlacesQuadrantArrowsWestEastConfettiFixedTethersAndMysterySpots(bool dpsGetsDifferent, bool dpsGetsConfused, bool fireIsStack)
    {
        Dictionary<PartyRole, Vector3>? confetti = null;
        Dictionary<PartyRole, Vector3>? tethers = null;
        Dictionary<PartyRole, Vector3>? mystery = null;

        var run = ScenarioRun.Execute(typeof(UmadP1TeleTrouncingScenario), YanStratIndex,
            400 + (dpsGetsDifferent ? 4 : 0) + (dpsGetsConfused ? 2 : 0) + (fireIsStack ? 1 : 0),
            new ScenarioRunOptions
            {
                PlayerRole = MainTank,
                Overrides = settings =>
                {
                    var o = (UmadP1TeleTrouncingStateOverrides)settings;
                    UmadP1TeleTrouncingScenarioTests.Pin(o);
                    o.DpsGetsDifferent = dpsGetsDifferent;
                    o.DifferentCycleRoles = dpsGetsDifferent ? [MeleeDpsA, MeleeDpsB, PhysRangedDps, CasterDps] : [MainTank, OffTank, RegenHealer, ShieldHealer];
                    o.DpsGetsConfused = dpsGetsConfused;
                    o.FireIsStack = fireIsStack;
                },
                Probe = probe =>
                {
                    if (probe.Crossed(22.6f))
                        confetti = Enum.GetValues<PartyRole>().ToDictionary(r => r, r => probe.Member(r)!.Position);
                    if (probe.Crossed(28.3f))
                        tethers = Enum.GetValues<PartyRole>().ToDictionary(r => r, r => probe.Member(r)!.Position);
                    if (probe.Crossed(40.8f))
                        mystery = Enum.GetValues<PartyRole>().ToDictionary(r => r, r => probe.Member(r)!.Position);
                },
            });

        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.Multiple(() =>
        {
            Assert.That(confetti![MainTank].X, Is.LessThan(-2f));
            Assert.That(confetti[OffTank].X, Is.LessThan(-2f));
            Assert.That(confetti[MeleeDpsA].X, Is.GreaterThan(2f));
            Assert.That(confetti[MeleeDpsB].X, Is.GreaterThan(2f));

            Assert.That(Vector2.Distance(Flat(tethers![MainTank]), new(-4.3f, -4.3f)), Is.LessThan(0.5f));
            Assert.That(Vector2.Distance(Flat(tethers[OffTank]), new(4.3f, -4.3f)), Is.LessThan(0.5f));
            Assert.That(Vector2.Distance(Flat(tethers[MeleeDpsA]), new(-4.3f, 4.3f)), Is.LessThan(0.5f));
            Assert.That(Vector2.Distance(Flat(tethers[MeleeDpsB]), new(4.3f, 4.3f)), Is.LessThan(0.5f));
            Assert.That(Vector2.Distance(Flat(tethers[PhysRangedDps]), new(-9f, -9f)), Is.LessThan(0.5f));
            Assert.That(Vector2.Distance(Flat(tethers[CasterDps]), new(9f, -9f)), Is.LessThan(0.5f));
            Assert.That(Vector2.Distance(Flat(tethers[RegenHealer]), new(-9f, 9f)), Is.LessThan(0.5f));
            Assert.That(Vector2.Distance(Flat(tethers[ShieldHealer]), new(9f, 9f)), Is.LessThan(0.5f));

            if (fireIsStack)
            {
                Assert.That(mystery![MeleeDpsA].X, Is.LessThan(-2f));
                Assert.That(mystery[MeleeDpsA].Z, Is.LessThan(-2f));
                Assert.That(mystery[MainTank].X, Is.GreaterThan(2f));
                Assert.That(mystery[MainTank].Z, Is.GreaterThan(2f));
            }
            else
            {
                Assert.That(mystery![CasterDps].Z, Is.LessThan(-10f));
                Assert.That(mystery[ShieldHealer].X, Is.GreaterThan(10f));
                Assert.That(mystery[RegenHealer].Z, Is.GreaterThan(10f));
                Assert.That(mystery[PhysRangedDps].X, Is.LessThan(-10f));
            }
        });
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
