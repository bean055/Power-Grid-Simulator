using System;
using System.Collections.Generic;
using System.Linq;
using static PowerGridSim.Models.Record;

namespace PowerGridSim.Models
{
    public static class Simulator
    {
        public static SimulationTimeline Run(PowerGridBlueprint bp, IEnumerable<SimEvent> events, int totalHours = 168)
        {
            var timeline = new SimulationTimeline { TownName = bp.TownName };
            var engine = new PowerGridEngine();

            var eventsByHour = events.GroupBy(e => e.TargetHour).ToDictionary(g => g.Key, g => g.ToList());
            var brokenCables = new HashSet<string>();
            var blackedOut = new HashSet<string>();

            for (int h = 0; h < totalHours; h++)
            {
                var time = new SimTime(h);

                if (eventsByHour.TryGetValue(h, out var hourEvents))
                    foreach (var ev in hourEvents)
                        ApplyEvent(ev, brokenCables, blackedOut);

                var openCables = new HashSet<string>(brokenCables);
                foreach (var c in bp.Cables)
                    if (blackedOut.Contains(c.FromNodeId) || blackedOut.Contains(c.ToNodeId))
                        openCables.Add(c.Id);

                var (p, q) = RequestedInjections(bp, time, blackedOut);
                var result = engine.Solve(bp, p, q, openCables);

                timeline.Frames.Add(BuildFrame(bp, time, p, result, brokenCables, blackedOut));
            }
            return timeline;
        }

        private static (double[] P, double[] Q) RequestedInjections(
            PowerGridBlueprint bp, SimTime time, HashSet<string> blackedOut)
        {
            int n = bp.Nodes.Count;
            var p = new double[n];
            var q = new double[n];
            double demand = 0, renewable = 0, plantCapacity = 0;

            for (int i = 0; i < n; i++)
            {
                if (blackedOut.Contains(bp.Nodes[i].Id)) continue;
                switch (bp.Nodes[i])
                {
                    case ConsumerNode c:
                        p[i] = -c.DemandMWAt(time);
                        q[i] = -c.DemandMvarAt(time);
                        demand -= p[i];
                        break;
                    case GeneratorNode g when g.GeneratorType == GeneratorType.PowerPlant:
                        plantCapacity += g.Rating.NominalMVA;
                        break;
                    case GeneratorNode g:
                        p[i] = g.AvailableMWAt(time);
                        renewable += p[i];
                        break;
                }
            }

            if (renewable > demand && renewable > 0)
            {
                double scale = demand / renewable;
                for (int i = 0; i < n; i++)
                    if (bp.Nodes[i] is GeneratorNode) p[i] *= scale;
                renewable = demand;
            }

            double gap = Math.Max(0, demand - renewable);
            if (plantCapacity > 0)
            {
                double share = Math.Min(gap, plantCapacity) / plantCapacity;
                for (int i = 0; i < n; i++)
                    if (bp.Nodes[i] is GeneratorNode g && g.GeneratorType == GeneratorType.PowerPlant
                        && !blackedOut.Contains(g.Id))
                        p[i] = g.Rating.NominalMVA * share;
            }
            return (p, q);
        }

        private static SimFrame BuildFrame(PowerGridBlueprint bp, SimTime time, double[] requestedP,
            AcResult r, HashSet<string> broken, HashSet<string> blackedOut)
        {
            var frame = new SimFrame
            {
                TotalHours = time.TotalHours,
                TimeLabel = time.ToString(),
                ConnectionCount = r.ConnectionCount
            };

            for (int i = 0; i < bp.Nodes.Count; i++)
            {
                var node = bp.Nodes[i];
                bool isOut = blackedOut.Contains(node.Id);
                bool violation = !isOut && (r.VoltagePu[i] < bp.MinVoltagePu || r.VoltagePu[i] > bp.MaxVoltagePu);

                double unmet = node is ConsumerNode && !isOut
                    ? Math.Max(0, (-requestedP[i]) - (-r.P[i]))
                    : 0;

                if (isOut) frame.BlackedOutNodesCount++;
                if (violation) frame.VoltageViolationCount++;
                if (r.P[i] > 0) frame.TotalGenerationMW += r.P[i];
                else frame.TotalDemandMW -= r.P[i];
                frame.TotalReactiveMvar += r.Q[i];
                frame.DemandUnmetMW += unmet;

                frame.NodeStates[node.Id] = new NodeState
                {
                    ActivePowerMW = r.P[i],
                    ReactivePowerMvar = r.Q[i],
                    VoltagePu = r.VoltagePu[i],
                    VoltageKV = r.VoltagePu[i] * node.EffectiveKV,
                    VoltagePhaseAngle = r.AngleDeg[i],
                    BusType = r.BusTypes[i],
                    IsBlackedOut = isOut,
                    IsVoltageViolation = violation,
                    DemandUnmetMW = unmet
                };
            }

            for (int i = 0; i < bp.Cables.Count; i++)
            {
                var cable = bp.Cables[i];
                double apparent = PowerRating.Apparent(r.FlowMW[i], r.FlowMvar[i]);
                frame.TotalLineLossesMW += r.LossMW[i];
                frame.CableStates[cable.Id] = new CableState
                {
                    CurrentFlowMW = r.FlowMW[i],
                    ReactiveFlowMvar = r.FlowMvar[i],
                    ApparentMVA = apparent,
                    LoadPercentage = cable.Rating.ThermalLimitMVA > 0
                        ? apparent / cable.Rating.ThermalLimitMVA * 100 : 0,
                    LossMW = r.LossMW[i],
                    IsBroken = broken.Contains(cable.Id)
                };
            }
            return frame;
        }

        private static void ApplyEvent(SimEvent ev, HashSet<string> broken, HashSet<string> blackedOut)
        {
            switch (ev.Event)
            {
                case SimEventType.BreakCable: broken.Add(ev.TargetId); break;
                case SimEventType.RepairCable: broken.Remove(ev.TargetId); break;
                case SimEventType.Blackout: blackedOut.Add(ev.TargetId); break;
                case SimEventType.RestoreNode: blackedOut.Remove(ev.TargetId); break;
            }
        }
    }
}