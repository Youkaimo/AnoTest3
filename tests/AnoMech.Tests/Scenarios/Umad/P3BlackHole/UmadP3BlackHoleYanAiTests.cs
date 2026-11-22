using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Umad.P3BlackHole;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

public class UmadP3BlackHoleYanAiTests
{
    private const int YanStratIndex = 2;

    [TestCase(1, 0)]
    [TestCase(-1, 1)]
    public void YanPullsFirstTetherCounterClockwiseAndBaitsStompCenter(int chirality, int initialAngle)
    {
        Vector3? firstTetherPullPos = null;
        Dictionary<PartyRole, Vector3>? stompBait = null;

        var run = ScenarioRun.Execute(typeof(UmadP3BlackHoleScenario), YanStratIndex,
            600 + (chirality > 0 ? 2 : 0) + initialAngle,
            new ScenarioRunOptions
            {
                PlayerRole = MainTank,
                Overrides = settings =>
                {
                    var o = (UmadP3BlackHoleStateOverrides)settings;
                    UmadP3BlackHoleScenarioTests.Pin(o);
                    o.MiniBlackHoleChirality = chirality;
                    o.MiniBlackHoleInitialAngle = initialAngle;
                },
                Probe = probe =>
                {
                    if (probe.Crossed(31.8f))
                        firstTetherPullPos = probe.Member(MainTank)!.Position;
                    if (probe.Crossed(146.5f))
                        stompBait = Enum.GetValues<PartyRole>().ToDictionary(r => r, r => probe.Member(r)!.Position);
                },
            });

        Assert.That(run.Passed, Is.True, run.ToString());
        Assert.Multiple(() =>
        {
            Assert.That(firstTetherPullPos!.Value.X, Is.LessThan(-2f));
            Assert.That(firstTetherPullPos.Value.Z, Is.GreaterThan(2f));
            foreach (var role in Enum.GetValues<PartyRole>())
                Assert.That(new Vector2(stompBait![role].X, stompBait[role].Z).Length(), Is.LessThan(2.5f), $"{role} should bait Blizzard 1 at center");
        });
    }
}
