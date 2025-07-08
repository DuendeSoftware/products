// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using Perfolizer.Horology;
using Perfolizer.Metrology;

namespace Bff.Benchmarks;

[Config(typeof(BenchmarkConfig))]
public abstract class BenchmarkBase
{
    public abstract Task InitializeAsync();
    public abstract Task DisposeAsync();
}

public class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        var exporter = new CsvExporter(
            CsvSeparator.CurrentCulture,
            new SummaryStyle(
                cultureInfo: System.Globalization.CultureInfo.InvariantCulture,
                printUnitsInHeader: false,
                printUnitsInContent: false,
                timeUnit: TimeUnit.Microsecond,
                sizeUnit: SizeUnit.KB
            ));

        AddJob(Job.MediumRun);

        AddExporter(exporter);
    }
}
