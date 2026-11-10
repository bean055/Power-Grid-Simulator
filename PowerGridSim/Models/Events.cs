using System;
using System.Collections.Generic;
using System.Text;

namespace PowerGridSim.Models
{
    public enum SimEventType
    {
        BreakCable,
        RepairCable,
        Blackout,
        RestoreNode,
        AdjustGeneration
    }
    public record SimEvent(
        int TargetHour,
        SimEventType Event,
        string TargetId
        );
}
