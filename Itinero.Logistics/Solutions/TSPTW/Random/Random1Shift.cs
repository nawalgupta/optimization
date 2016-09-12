﻿// Itinero.Logistics - Route optimization for .NET
// Copyright (C) 2016 Abelshausen Ben
// 
// This file is part of Itinero.
// 
// Itinero is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// Itinero is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Itinero. If not, see <http://www.gnu.org/licenses/>.

using Itinero.Logistics.Routes;
using Itinero.Logistics.Solutions.TSPTW.Objectives;
using Itinero.Logistics.Solvers;
using Itinero.Logistics.Algorithms;
using System;

namespace Itinero.Logistics.Solutions.TSPTW.Random
{
    /// <summary>
    /// An operator to execute n random 1-shift* relocations.
    /// </summary>
    /// <remarks>* 1-shift: Remove a customer and relocate it somewhere.</remarks>
    public class Random1Shift<T> : IPerturber<T, ITSPTW<T>, TSPTWObjective<T>, IRoute, float>
        where T : struct
    {
        private IRandomGenerator _random = RandomGeneratorExtensions.GetRandom();

        /// <summary>
        /// Returns the name of the operator.
        /// </summary>
        public string Name
        {
            get { return "RAN_1SHFT"; }
        }

        /// <summary>
        /// Returns true if the given objective is supported.
        /// </summary>
        /// <returns></returns>
        public bool Supports(TSPTWObjective<T>  objective)
        {
            return objective.Name == MinimumWeightObjective<T>.MinimumWeightObjectiveName;
        }

        /// <summary>
        /// Returns true if there was an improvement, false otherwise.
        /// </summary>
        /// <returns></returns>
        public bool Apply(ITSPTW<T> problem, TSPTWObjective<T>  objective, IRoute route, out float difference)
        {
            return this.Apply(problem, objective, route, 1, out difference);
        }

        /// <summary>
        /// Returns true if there was an improvement, false otherwise.
        /// </summary>
        /// <returns></returns>
        public bool Apply(ITSPTW<T> problem, TSPTWObjective<T>  objective, IRoute solution, int level, out float difference)
        {
            if(problem.Weights.Length == 1)
            { // there is only one customer, cannot shift randomly.
                difference = 0;
                return false;
            }

            difference = 0;
            while (level > 0)
            {
                // remove random customer after another random customer.
                var customer = _random.Generate(problem.Weights.Length);
                var before = _random.Generate(problem.Weights.Length - 1);
                if (before >= customer)
                { // customer is the same of after.
                    before++;
                }

                // calculate new total min diff.
                var previous = Constants.NOT_SET;
                var time = 0.0f;
                var enumerator = solution.GetEnumerator();
                var feasible = true;
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    if (current != customer)
                    { // ignore invalid, add it after 'before'.
                        if (previous != Constants.NOT_SET)
                        { // keep track if time.
                            time += problem.WeightHandler.GetTime(problem.Weights[previous][current]);
                        }
                        var window = problem.Windows[enumerator.Current];
                        if (window.Max < time)
                        { // ok, unfeasible.
                            feasible = false;
                            break;
                        }
                        if (window.Min > time)
                        { // wait here!
                            time = window.Min;
                        }
                        previous = current;
                        if (current == before)
                        { // also add the before->invalid.
                            time += problem.WeightHandler.GetTime(problem.Weights[current][customer]);
                            window = problem.Windows[customer];
                            if (window.Max < time)
                            { // ok, unfeasible.
                                feasible = false;
                                break;
                            }
                            if (window.Min > time)
                            { // wait here!
                                time = window.Min;
                            }
                            previous = customer;
                        }
                    }
                }

                if (feasible)
                { // move is feasible, do it.
                    float shiftDiff;
                    if (!objective.ShiftAfter(problem, solution, customer, before, out shiftDiff))
                    {
                        throw new Exception(
                            string.Format("Failed to shift customer {0} after {1} in route {2}.", customer, before, solution.ToInvariantString()));
                    }
                    difference += shiftDiff;
                }

                // decrease level.
                level--;
            }
            return difference > 0;
        }
    }
}