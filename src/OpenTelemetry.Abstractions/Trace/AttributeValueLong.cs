﻿// <copyright file="AttributeValueLong.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Trace
{
    public abstract class AttributeValueLong : AttributeValue
    {
        public AttributeValueLong()
        {
        }

        public static AttributeValue Create(long longValue)
        {
            return new AttributeValue<long>(longValue);
        }

        public override Type GetType()
        {
            return AttributeValue.Type.Long;
        }

        public new abstract long GetLongValue();
    }
}
