using System.Numerics;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Umad.P4KefkaSays;
using static AnoMech.Core.Game.Party.PartyRole;

namespace AnoMech.Tests;

public class UmadP4KefkaSaysYanAiTests
{
    private const int YanStratIndex = 2;

    [TestCase(0, true, false)]
    [TestCase(2, false, true)]
    [TestCase(4, true, true)]
    [TestCase(6, false, false)]
    public void YanSplitsFloodNorthSouthAndResolvesWave1RelativeToNeoExdeath(int neoStepsCw, bool wave1Real, bool wave2Real)
    {
        var neoDir = Direction.N.Rotate(neoStepsCw);
        Dictionary<PartyRole, Vector3>? flood = null;
        Dictionary<PartyRole, Vector3>? wave1 = null;

        var run = ScenarioRun.Execute(typeof(UmadP4KefkaSaysScenario), YanStratIndex,
            700 + neoStepsCw * 4 + (wave1Real ? 2 : 0) + (wave2Real ? 1 : 0),
            new ScenarioRunOptions
            {
                PlayerRole = MainTank,
                Overrides = settings =>
                {
                    var o = (UmadP4KefkaSaysStateOverrides)settings;
                    UmadP4KefkaSaysScenarioTests.Pin(o);
                    o.NeoExdeathDirection = neoDir;
                    o.ExdeathCast1Real = wave1Real;
                    o.ExdeathCast2 = wave2Real ? ExdeathCast2Mode.Real : ExdeathCast2Mode.Fake;
                },
                Probe = probe =>
                {
                    if (probe.Crossed(62.5f))
                        flood = Enum.GetValues<PartyRole>().ToDictionary(r => r, r => probe.Member(r)!.Position);
                    if (probe.Crossed(71.0f))
                        wave1 = Enum.GetValues<PartyRole>().ToDictionary(r => r, r => probe.Member(r)!.Position);
                },
            });

        Assert.That(run.Passed, Is.True, run.ToString());
        var towardNeo = neoDir.Apply(new Vector3(0f, 0f, -1f));
        var rightOfNeo = neoDir.Apply(new Vector3(1f, 0f, 0f));
        var supportSolo = wave1Real ? RegenHealer : ShieldHealer;
        var dpsSolo = wave1Real ? PhysRangedDps : CasterDps;

        Assert.Multiple(() =>
        {
            Assert.That(Vector3.Dot(flood![MainTank], towardNeo), Is.GreaterThan(3f));
            Assert.That(Vector3.Dot(flood[MeleeDpsA], towardNeo), Is.LessThan(-3f));

            Assert.That(Vector3.Dot(wave1![MainTank], towardNeo), Is.GreaterThan(6f));
            Assert.That(Vector3.Dot(wave1[MeleeDpsA], towardNeo), Is.LessThan(-6f));
            Assert.That(Vector3.Dot(wave1[supportSolo], rightOfNeo), Is.GreaterThan(8f));
            Assert.That(Vector3.Dot(wave1[dpsSolo], rightOfNeo), Is.GreaterThan(8f));
        });
    }
}
