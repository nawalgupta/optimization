﻿/*
 *  Licensed to SharpSoftware under one or more contributor
 *  license agreements. See the NOTICE file distributed with this work for 
 *  additional information regarding copyright ownership.
 * 
 *  SharpSoftware licenses this file to you under the Apache License, 
 *  Version 2.0 (the "License"); you may not use this file except in 
 *  compliance with the License. You may obtain a copy of the License at
 * 
 *       http://www.apache.org/licenses/LICENSE-2.0
 * 
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using System;
using System.Collections.Generic;
using Itinero.Logging;
using Itinero.Optimization.Models.Mapping;
using Itinero.Optimization.Solvers.Shared;
using Itinero.Optimization.Solvers.TSP_TW.EAX;

namespace Itinero.Optimization.Solvers.TSP_TW
{
    /// <summary>
    /// Hooks the TSP-TW solvers into the solver registry.
    /// </summary>
    internal static class TSPTWSolverHook
    {
        /// <summary>
        /// The default solver hook for the TSP.
        /// </summary>
        public static readonly SolverRegistry.SolverHook Default = new SolverRegistry.SolverHook()
        {
            Name = "TSP-TW",
            TrySolve = TSPTWSolverHook.Solve
        };

        private static Result<IEnumerable<(int vehicle, IEnumerable<int> tour)>> Solve(MappedModel model, 
            Action<IEnumerable<(int vehicle, IEnumerable<int>)>> intermediateResult)
        {
            var tsptw = model.TryToTSPTW();
            if (tsptw.IsError)
            {
                return tsptw.ConvertError<IEnumerable<(int vehicle, IEnumerable<int> tour)>>();
            }
            
            // use a default solver to solve the TSP-TW here.
            try
            {
                var solution = EAXSolver.Default.Search(tsptw.Value);
                return new Result<IEnumerable<(int vehicle, IEnumerable<int> tour)>>(
                    (new (int vehicle, IEnumerable<int> tour) [] { (0, solution.Solution) }));
            }
            catch (Exception ex)
            {
                Logger.Log($"{nameof(TSPTWSolverHook)}.{nameof(Solve)}", TraceEventType.Critical, $"Unhandled exception: {ex.ToString()}.");
                return new Result<IEnumerable<(int vehicle, IEnumerable<int> tour)>>($"Unhandled exception: {ex.ToString()}.");
            }
        }

        /// <summary>
        /// Converts the given abstract model to a TSP-TW.
        /// </summary>
        internal static Result<TSPTWProblem> TryToTSPTW(this MappedModel model)
        {
            if (!model.IsTSPTW(out var reasonWhenFailed))
            {
                return new Result<TSPTWProblem>($"Model is not a TSP-TW: {reasonWhenFailed}");
            }

            var vehicle = model.VehiclePool.Vehicles[0];
            var metric = vehicle.Metric;
            if (!model.TryGetTravelCostsForMetric(metric, out var weights))
            {
                throw new Exception("Travel costs not found but model was declared valid.");
            }
            var first = 0;
            int? last = null;
            if (vehicle.Departure.HasValue)
            {
                first = vehicle.Departure.Value;
            }
            if (vehicle.Arrival.HasValue)
            {
                last = vehicle.Arrival.Value;
            }

            var windows = new TimeWindow[model.Visits.Length];
            for (var v = 0; v < windows.Length; v++)
            {
                var visit = model.Visits[v];
                
                windows[v] = new TimeWindow();
                if (visit.TimeWindow != null &&
                    !visit.TimeWindow.IsEmpty)
                {
                    windows[v] = new TimeWindow()
                    {
                        Min = visit.TimeWindow.Min,
                        Max = visit.TimeWindow.Max
                    };
                }
            }
            return new Result<TSPTWProblem>(new TSPTWProblem(first, last, weights.Costs, windows));
        }

        private static bool IsTSPTW(this MappedModel model, out string reasonIfNot)
        {
            if (model.VehiclePool.Reusable ||
                model.VehiclePool.Vehicles.Length > 1)
            {
                reasonIfNot = "More than one vehicle or vehicle reusable.";
                return false;
            }
            var vehicle = model.VehiclePool.Vehicles[0];
            if (vehicle.TurnPentalty != 0)
            {
                reasonIfNot = "Turning penalty, this is a directed problem.";
                return false;
            }
            if (vehicle.CapacityConstraints != null &&
                vehicle.CapacityConstraints.Length > 0)
            {
                reasonIfNot = "At least one capacity constraint was found.";
                return false;
            }

            var timeWindow = false;
            foreach (var visit in model.Visits)
            {
                if (visit.TimeWindow == null || visit.TimeWindow.IsEmpty) continue;
                timeWindow = true;
            }
            if (!timeWindow)
            {
                reasonIfNot = "No timewindows detected.";
                return false;
            }

            reasonIfNot = string.Empty;
            return true;
        }
    }
}