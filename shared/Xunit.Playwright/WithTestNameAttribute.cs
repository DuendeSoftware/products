// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Xunit.v3;

namespace Duende.Xunit.Playwright;

public class WithTestNameAttribute : Attribute, IBeforeAfterTestAttribute
{
    public static string CurrentTestName = string.Empty;
    public static string CurrentClassName = string.Empty;

    public void Before(MethodInfo methodInfo, IXunitTest _)
    {
        CurrentTestName = methodInfo.Name;
        CurrentClassName = methodInfo.DeclaringType!.Name;
    }

    public void After(MethodInfo methodInfo, IXunitTest _)
    { }
}
