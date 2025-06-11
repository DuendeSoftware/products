// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Reports;
using Perfolizer.Metrology;

namespace Bff.Benchmarks;

[ShortRunJob]
[Config(typeof(Config))]
public class BenchmarkBase
{
}

public class Config : ManualConfig
{
    public Config()
    {
        var exporter = new CsvExporter(
            CsvSeparator.CurrentCulture,
            new SummaryStyle(
                cultureInfo: System.Globalization.CultureInfo.CurrentCulture,
                printUnitsInHeader: false,
                printUnitsInContent: false,
                timeUnit: Perfolizer.Horology.TimeUnit.Microsecond,
                sizeUnit: SizeUnit.KB
            ));
        AddExporter(exporter);
    }
}
