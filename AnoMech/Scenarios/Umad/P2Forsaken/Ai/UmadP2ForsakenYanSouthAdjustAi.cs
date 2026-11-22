using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Umad.P2Forsaken.Ai;

public sealed class UmadP2ForsakenYanSouthAdjustAi : IScenarioAi<UmadP2ForsakenState>
{
    public string Name => "Yan (South Adjust)";
    public string? Group => "JP";

    public void Run(UmadP2ForsakenState state, SimWorld world)
        => new UmadP2ForsakenYanAiHelper(
               UmadP2ForsakenYanAiHelper.YanOdd,
               UmadP2ForsakenYanAiHelper.YanEven,
               UmadP2ForsakenYanAiHelper.SouthAdjustReorder).Run(state, world);
}
