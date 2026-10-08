// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.ConformanceReport.Services;
using Xunit.Sdk;
using AlgorithmRule = Duende.ConformanceReport.Services.Fapi2SecurityAssessorTests.ServerAssessments.AlgorithmRule;

[assembly: RegisterXunitSerializer(typeof(AlgorithmRuleSerializer), typeof(AlgorithmRule))]

namespace Duende.ConformanceReport.Services;

/// <summary>
/// Lets xUnit serialize <see cref="AlgorithmRule"/> theory arguments by rule ID. Without this,
/// xUnit can't enumerate the theory rows at discovery time, so IDE test explorers show a single
/// test per method instead of one per rule.
/// </summary>
public sealed class AlgorithmRuleSerializer : IXunitSerializer
{
    public object Deserialize(Type type, string serializedValue) =>
        Fapi2SecurityAssessorTests.ServerAssessments.AllAlgorithmRules.Single(r => r.RuleId == serializedValue);

    public bool IsSerializable(Type type, object? value, [NotNullWhen(false)] out string? failureReason)
    {
        failureReason = null;
        return true;
    }

    public string Serialize(object value) => ((AlgorithmRule)value).RuleId;
}
