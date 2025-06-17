// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

// full login / logout flow

// Api calls:

// plain yarp call
// Local api
// multi-frontend index.html response
// remote api user token
//  - yarp
//  - plain
//  - single multi-frontend
//  - 100 multi-frontends
//  - server side sessions
//  - large response payload

// remote api client credentials token
//  - yarp
//  - plain
//  - single multi-frontend
//  - 100 multi-frontends
//  - large response payload

// with distributed cache + short lifecycle

using Bff.Performance.Scenarios.Bff;
using NBomber.Contracts.Stats;
using NBomber.CSharp;
using NBomber.Http;

NBomberRunner
    .RegisterScenarios(new BffScenarios(new Uri("https://localhost:6001")).Scenarios)
    .WithWorkerPlugins(new HttpMetricsPlugin())
    .WithReportingInterval(TimeSpan.FromSeconds(5))
    .WithReportFormats(
        ReportFormat.Csv, ReportFormat.Html
    )
    .Run();
