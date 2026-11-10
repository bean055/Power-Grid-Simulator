using System;
using System.Collections.Generic;

namespace PowerGridSim.Models
{
    public readonly record struct LineImpedance(double LengthKm, double RPu, double XPu);

    public static class LineParameters
    {
        public static LineImpedance[] Calculate(PowerGridBlueprint bp)
        {
            var nodes = bp.BuildNodeIndex();
            double baseMva = bp.BaseMVA > 0 ? bp.BaseMVA : 100.0;
            var result = new LineImpedance[bp.Cables.Count];

            for (int i = 0; i < bp.Cables.Count; i++)
            {
                var cable = bp.Cables[i];
                if (!nodes.TryGetValue(cable.FromNodeId, out int a) ||
                    !nodes.TryGetValue(cable.ToNodeId, out int b)) continue;

                var from = bp.Nodes[a];
                var to = bp.Nodes[b];

                double lengthKm = cable.LengthKm > 0
                    ? cable.LengthKm
                    : Math.Max(0.05, Math.Sqrt(Math.Pow(from.X - to.X, 2) + Math.Pow(from.Y - to.Y, 2)) / 400.0 * 1.609);

                bool transmission = cable.Type == NodeCableType.TransmissionLine;
                double dropPerKm = transmission ? 0.03 / 100.0 : 0.04 / 10.0;
                double xOverR = transmission ? 10.0 : 3.0;

                double rating = cable.Rating.ThermalLimitMVA > 0 ? cable.Rating.ThermalLimitMVA : baseMva;
                double zPu = dropPerKm * lengthKm * (baseMva / rating);

                double r = zPu / Math.Sqrt(1.0 + xOverR * xOverR);
                result[i] = new LineImpedance(lengthKm, r, r * xOverR);
            }
            return result;
        }
    }
}