using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Umad.P2Forsaken;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

public class UmadP2ForsakenYanSouthAdjustAiTests
{
    private const int YanSouthAdjustStratIndex = 6;

    [TestCase(1, true)]
    [TestCase(-1, true)]
    [TestCase(1, false)]
    [TestCase(-1, false)]
    public void YanSouthAdjustClearsAndBaitsFinalEndAtNorthMarker(int rotation, bool finalIsPast)
    {
        Vector3? finalBaitPos = null;

        var run = ScenarioRun.Execute(typeof(UmadP2ForsakenScenario), YanSouthAdjustStratIndex,
            500 + (rotation > 0 ? 2 : 0) + (finalIsPast ? 1 : 0),
            new ScenarioRunOptions
            {
                PlayerRole = MainTank,
                Overrides = settings =>
                {
                    var o = (UmadP2ForsakenStateOverrides)settings;
                    UmadP2ForsakenScenarioTests.Pin(o);
                    o.Rotation = rotation;
                    o.EndAttacks[3] = finalIsPast ? EndAttack.PastsEnd : EndAttack.FuturesEnd;
                },
                Probe = probe =>
                {
                    if (probe.Crossed(99.5f))
                        finalBaitPos = probe.Member(MainTank)!.Position;
                },
            });

        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.That(finalBaitPos!.Value.X, Is.InRange(-2f, 2f));
        Assert.That(finalBaitPos.Value.Z, Is.LessThan(-7f));
    }
}
