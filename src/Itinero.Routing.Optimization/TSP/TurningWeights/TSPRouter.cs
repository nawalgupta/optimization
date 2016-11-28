﻿// Itinero.Optimization - Route optimization for .NET
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

using System.Linq;
using Itinero.Algorithms;
using Itinero.Profiles;
using Itinero.Routing.Optimization.TurningWeights;
using Itinero.Optimization.TSP.TurningWeights;
using Itinero.Optimization.Algorithms.TurningWeights;
using Itinero.Data.Network;
using Itinero.Algorithms.Weights;
using System.Collections.Generic;

namespace Itinero.Routing.Optimization.TSP.TurningWeights
{
    /// <summary>
    /// An algorithm to calculate u-turn aware TSP solutions.
    /// </summary>
    public class TSPRouter : AlgorithmBase
    {
        private readonly Profile _profile;
        private readonly RouterPoint[] _locations;
        private readonly int _first;
        private readonly int? _last;
        private readonly Router _router;
        private readonly float _turnPenalty;
        private readonly Itinero.Optimization.Algorithms.Solvers.ISolver<float, TSProblem, TSPObjective, Itinero.Optimization.Routes.Route, float> _solver;

        /// <summary>
        /// Creates a new TSP router.
        /// </summary>
        public TSPRouter(Router router, Profile profile, RouterPoint[] locations, float turnPenalty)
        {
            _router = router;
            _locations = locations;
            _profile = profile;
            _turnPenalty = turnPenalty;

            _first = 0;
            _last = null;
        }

        private Itinero.Optimization.Routes.Route _route = null;
        private Itinero.Optimization.Routes.Route _originalRoute = null;
        private TurningWeightBidirectionalDykstra _turnWeightMatrix;

        /// <summary>
        /// Executes the actual algorithm.
        /// </summary>
        protected override void DoRun()
        {
            // calculates weight matrix.
            _turnWeightMatrix = new TurningWeightBidirectionalDykstra(_router, _profile, _locations);
            _turnWeightMatrix.Run();
            if (!_turnWeightMatrix.HasSucceeded)
            { // algorithm has not succeeded.
                this.ErrorMessage = string.Format("Could not calculate weight matrix: {0}",
                    _turnWeightMatrix.ErrorMessage);
                return;
            }

            LocationError error;
            if (_turnWeightMatrix.Errors.TryGetValue(_first, out error))
            { // if the first location could not be resolved everything fails.
                this.ErrorMessage = string.Format("Could resolve first location: {0}",
                    error);
                return;
            }

            // build problem.
            var first = _first;
            TSProblem problem = null;
            if (_last.HasValue)
            { // the last customer was set.
                if (_turnWeightMatrix.Errors.TryGetValue(_last.Value, out error))
                { // if the last location is set and it could not be resolved everything fails.
                    this.ErrorMessage = string.Format("Could resolve last location: {0}",
                        error);
                    return;
                }

                problem = new TSProblem(_turnWeightMatrix.IndexOf(first), _turnWeightMatrix.IndexOf(_last.Value),
                    _turnWeightMatrix.Weights, _turnPenalty);
            }
            else
            { // the last customer was not set.
                problem = new TSProblem(_turnWeightMatrix.IndexOf(first), _turnWeightMatrix.Weights, _turnPenalty);
            }

            // execute the solver.
            if (_solver == null)
            {
                _originalRoute = problem.Solve();
            }
            else
            {
                _originalRoute = problem.Solve(_solver);
            }
            
            // convert route to a route with the original location indices.
            if (_originalRoute.Last.HasValue)
            {
                _route = new Itinero.Optimization.Routes.Route(_originalRoute.Select(x => _turnWeightMatrix.DirectedLocationIndexOf(x)),
                    _turnWeightMatrix.DirectedLocationIndexOf(
                        _originalRoute.Last.Value));
            }
            else
            {
                _route = new Itinero.Optimization.Routes.Route(_originalRoute.Select(x => _turnWeightMatrix.DirectedLocationIndexOf(x)));
            }

            this.HasSucceeded = true;
        }

        /// <summary>
        /// Gets the raw route representing the order of the locations.
        /// </summary>
        public Itinero.Optimization.Routes.IRoute RawRoute
        {
            get
            {
                return _route;
            }
        }

        /// <summary>
        /// Builds the resulting route.
        /// </summary>
        /// <returns></returns>
        public Route BuildRoute()
        {
            this.CheckHasRunAndHasSucceeded();

            Route route = null;
            // TODO: check what to do here, use the cached version or not?
            var weightHandler = _profile.DefaultWeightHandler(_router);
            foreach (var pair in _originalRoute.Pairs())
            {
                var pairDirectedFrom = _turnWeightMatrix.SourcePaths[CustomerHelper.ExtractDirectedSource(pair.From)];
                var pairDirectedTo = _turnWeightMatrix.TargetPaths[CustomerHelper.ExtractDirectedTarget(pair.To)];

                var pairDirectedFromEdge = _router.Db.Network.GetEdges(pairDirectedFrom.From.Vertex).First(x => x.To == pairDirectedFrom.Vertex).IdDirected();
                var pairDirectedToEdge = _router.Db.Network.GetEdges(pairDirectedTo.Vertex).First(x => x.To == pairDirectedTo.From.Vertex).IdDirected();

                var fromPoi = CustomerHelper.ExtractCustomer(pair.From);
                var toPoi = CustomerHelper.ExtractCustomer(pair.To);

                var fromRouterPoint = _locations[fromPoi];
                var toRouterPoint = _locations[toPoi];

                var localRouteRaw = _router.TryCalculateRaw(_profile, weightHandler, pairDirectedFromEdge, pairDirectedToEdge, null).Value;
                localRouteRaw.StripSource();
                localRouteRaw.StripTarget();

                var localRoute = _router.BuildRoute(_profile, weightHandler, fromRouterPoint, toRouterPoint, localRouteRaw).Value;
                if (route == null)
                {
                    route = localRoute;
                }
                else
                {
                    route = route.Concatenate(localRoute);
                }
            }
            return route;
        }

        /// <summary>
        /// Builds the result route in segments divided by routes between customers.
        /// </summary>
        /// <returns></returns>
        public List<Result<Route>> TryBuildRoutes()
        {
            this.CheckHasRunAndHasSucceeded();

            var routes = new List<Result<Route>>();
            // TODO: check what to do here, use the cached version or not?
            var weightHandler = _profile.DefaultWeightHandler(_router);
            foreach (var pair in _originalRoute.Pairs())
            {
                var pairDirectedFrom = _turnWeightMatrix.SourcePaths[CustomerHelper.ExtractDirectedSource(pair.From)];
                var pairDirectedTo = _turnWeightMatrix.TargetPaths[CustomerHelper.ExtractDirectedTarget(pair.To)];

                var pairDirectedFromEdge = _router.Db.Network.GetEdges(pairDirectedFrom.From.Vertex).First(x => x.To == pairDirectedFrom.Vertex).IdDirected();
                var pairDirectedToEdge = _router.Db.Network.GetEdges(pairDirectedTo.Vertex).First(x => x.To == pairDirectedTo.From.Vertex).IdDirected();

                var fromPoi = CustomerHelper.ExtractCustomer(pair.From);
                var toPoi = CustomerHelper.ExtractCustomer(pair.To);

                var fromRouterPoint = _locations[fromPoi];
                var toRouterPoint = _locations[toPoi];

                var localRouteRaw = _router.TryCalculateRaw(_profile, weightHandler, pairDirectedFromEdge, pairDirectedToEdge, null).Value;
                localRouteRaw.StripSource();
                localRouteRaw.StripTarget();

                var localRoute = _router.BuildRoute(_profile, weightHandler, fromRouterPoint, toRouterPoint, localRouteRaw);
                routes.Add(localRoute);
            }
            return routes;
        }

        /// <summary>
        /// Builds the result route in segments divided by routes between customers.
        /// </summary>
        /// <returns></returns>
        public List<Route> BuildRoutes()
        {
            this.CheckHasRunAndHasSucceeded();

            var routes = new List<Route>();
            // TODO: check what to do here, use the cached version or not?
            var weightHandler = _profile.DefaultWeightHandler(_router);
            foreach (var pair in _originalRoute.Pairs())
            {
                var pairDirectedFrom = _turnWeightMatrix.SourcePaths[CustomerHelper.ExtractDirectedSource(pair.From)];
                var pairDirectedTo = _turnWeightMatrix.TargetPaths[CustomerHelper.ExtractDirectedTarget(pair.To)];

                var pairDirectedFromEdge = _router.Db.Network.GetEdges(pairDirectedFrom.From.Vertex).First(x => x.To == pairDirectedFrom.Vertex).IdDirected();
                var pairDirectedToEdge = _router.Db.Network.GetEdges(pairDirectedTo.Vertex).First(x => x.To == pairDirectedTo.From.Vertex).IdDirected();

                var fromPoi = CustomerHelper.ExtractCustomer(pair.From);
                var toPoi = CustomerHelper.ExtractCustomer(pair.To);

                var fromRouterPoint = _locations[fromPoi];
                var toRouterPoint = _locations[toPoi];

                var localRouteRaw = _router.TryCalculateRaw(_profile, weightHandler, pairDirectedFromEdge, pairDirectedToEdge, null).Value;
                localRouteRaw.StripSource();
                localRouteRaw.StripTarget();

                var localRoute = _router.BuildRoute(_profile, weightHandler, fromRouterPoint, toRouterPoint, localRouteRaw);
                if (localRoute.IsError)
                {
                    throw new Itinero.Exceptions.RouteNotFoundException(
                        string.Format("Part of the TSP-route was not found: {0}[{1}] -> {2}[{3}] - {4}.",
                            pair.From, fromPoi, pair.To, toPoi, localRoute.ErrorMessage));
                }
                routes.Add(localRoute.Value);
            }
            return routes;
        }
    }
}