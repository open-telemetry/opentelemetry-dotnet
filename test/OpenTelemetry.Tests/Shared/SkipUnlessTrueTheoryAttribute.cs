// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using OpenTelemetry.Internal;
using Xunit;

namespace OpenTelemetry.Tests;

internal sealed class SkipUnlessTrueTheoryAttribute : TheoryAttribute
{
    public SkipUnlessTrueTheoryAttribute(Type typeContainingTest, string testFieldName, string skipMessage)
    {
        Guard.ThrowIfNull(typeContainingTest);
        Guard.ThrowIfNullOrEmpty(testFieldName);
        Guard.ThrowIfNullOrEmpty(skipMessage);

        var field = typeContainingTest.GetField(testFieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Static field '{testFieldName}' could not be found on '{typeContainingTest}' type.");

        if (field.FieldType != typeof(Func<bool>))
        {
            throw new InvalidOperationException($"Field '{testFieldName}' on '{typeContainingTest}' type should be defined as '{typeof(Func<bool>)}'.");
        }

        var testFunc = (Func<bool>)field.GetValue(null)!;

        if (!testFunc())
        {
            this.Skip = skipMessage;
        }
    }
}
