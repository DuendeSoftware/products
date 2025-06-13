// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using Perfolizer.Metrology;


public class Program
{


    static void Main(string[] args)
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

        var manualConfig = ManualConfig.CreateEmpty()
            .AddExporter(exporter);
        BenchmarkRunner.Run(typeof(Program).Assembly, manualConfig);
    }
}
