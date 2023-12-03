// <copyright file="MockSpan.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using OpenTracing;
using OpenTracing.Tag;

namespace OpenTelemetry.Shims.OpenTracing.Tests.Mock;

internal class MockSpan : ISpan
{
    public ISpanContext Context => throw new NotImplementedException();

    public void Finish()
    {
        throw new NotImplementedException();
    }

    public void Finish(DateTimeOffset finishTimestamp)
    {
        throw new NotImplementedException();
    }

    public string GetBaggageItem(string key)
    {
        throw new NotImplementedException();
    }

    public ISpan Log(IEnumerable<KeyValuePair<string, object>> fields)
    {
        throw new NotImplementedException();
    }

    public ISpan Log(DateTimeOffset timestamp, IEnumerable<KeyValuePair<string, object>> fields)
    {
        throw new NotImplementedException();
    }

    public ISpan Log(string @event)
    {
        throw new NotImplementedException();
    }

    public ISpan Log(DateTimeOffset timestamp, string @event)
    {
        throw new NotImplementedException();
    }

    public ISpan SetBaggageItem(string key, string value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetOperationName(string operationName)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(string key, string value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(string key, bool value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(string key, int value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(string key, double value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(BooleanTag tag, bool value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(IntOrStringTag tag, string value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(IntTag tag, int value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(StringTag tag, string value)
    {
        throw new NotImplementedException();
    }
}
