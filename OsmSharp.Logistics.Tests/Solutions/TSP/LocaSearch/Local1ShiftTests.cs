﻿// OsmSharp - OpenStreetMap (OSM) SDK
// Copyright (C) 2015 Abelshausen Ben
// 
// This file is part of OsmSharp.
// 
// OsmSharp is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// OsmSharp is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with OsmSharp. If not, see <http://www.gnu.org/licenses/>.

using NUnit.Framework;
using OsmSharp.Logistics.Routes;
using OsmSharp.Logistics.Solutions.TSP.LocalSearch;
using System.Linq;

namespace OsmSharp.Logistics.Tests.Solutions.TSP.LocaSearch
{
    /// <summary>
    /// Contains tests for the Local1Shift local search operator.
    /// </summary>
    [TestFixture]
    public class Local1ShiftTests
    {
        /// <summary>
        /// Tests a route where only one shift is possible/needed.
        /// </summary>
        [Test]
        public void Test1OneShift()
        {
            // create the problem and make sure 0->1->2->3->4 is the solution.
            var problem = new TSPProblemMock(0, 5, 10, true);
            problem.Weights[0][1] = 1;
            problem.Weights[1][2] = 1;
            problem.Weights[2][3] = 1;
            problem.Weights[3][4] = 1;
            problem.Weights[4][0] = 1;

            // create a route with one shift.
            var route = Route.CreateFrom(new int[] { 0, 2, 3, 1, 4 }, true);

            // apply the 1-shift local search, it should find the customer to replocate.
            var localSearch = new Local1Shift();
            var delta = 0.0;
            localSearch.Apply(problem, route, out delta);

            // test result.
            Assert.AreEqual(-27, delta);
            Assert.AreEqual(new int[] { 0, 1, 2, 3, 4 }, route.ToArray());
        }

        /// <summary>
        /// Tests a route where two shifts are possible/needed.
        /// </summary>
        [Test]
        public void Test2TwoShifts()
        {
            // create the problem and make sure 0->1->2->3->4 is the solution.
            var problem = new TSPProblemMock(0, 5, 10, true);
            problem.Weights[0][1] = 1;
            problem.Weights[1][2] = 1;
            problem.Weights[2][3] = 1;
            problem.Weights[3][4] = 1;
            problem.Weights[4][0] = 1;

            // create a route with one shift.
            var route = Route.CreateFrom(new int[] { 0, 2, 4, 1, 3 }, true);

            // apply the 1-shift local search, it should find the customer to replocate.
            var localSearch = new Local1Shift();
            var delta = 0.0;
            localSearch.Apply(problem, route, out delta);

            // test result.
            Assert.AreEqual(-45, delta);
            Assert.AreEqual(new int[] { 0, 1, 2, 3, 4 }, route.ToArray());
        }
    }
}
