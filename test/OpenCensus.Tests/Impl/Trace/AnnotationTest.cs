// <copyright file="AnnotationTest.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace.Test
{
    using System;
    using System.Collections.Generic;
    using OpenCensus.Utils;
    using Xunit;

    public class AnnotationTest
    {
        [Fact]
        public void FromDescription_NullDescription()
        {
            Assert.Throws<ArgumentNullException>(() => Annotation.FromDescription(null));
        }

        [Fact]
        public void FromDescription()
        {
            IAnnotation annotation = Annotation.FromDescription("MyAnnotationText");
            Assert.Equal("MyAnnotationText", annotation.Description);
            Assert.Equal(0, annotation.Attributes.Count);
        }

        [Fact]
        public void FromDescriptionAndAttributes_NullDescription()
        {
            Assert.Throws<ArgumentNullException>(() => Annotation.FromDescriptionAndAttributes(null, new Dictionary<string, IAttributeValue>()));
        }

        [Fact]
        public void FromDescriptionAndAttributes_NullAttributes()
        {
            Assert.Throws<ArgumentNullException>(() => Annotation.FromDescriptionAndAttributes("", null));
        }

        [Fact]
        public void FromDescriptionAndAttributes()
        {
            Dictionary<string, IAttributeValue> attributes = new Dictionary<string, IAttributeValue>();
            attributes.Add(
                "MyStringAttributeKey", AttributeValue<string>.Create("MyStringAttributeValue"));
            IAnnotation annotation = Annotation.FromDescriptionAndAttributes("MyAnnotationText", attributes);
            Assert.Equal("MyAnnotationText", annotation.Description);
            Assert.Equal(attributes, annotation.Attributes);
        }

        [Fact]
        public void FromDescriptionAndAttributes_EmptyAttributes()
        {
            IAnnotation annotation =
                Annotation.FromDescriptionAndAttributes(
                    "MyAnnotationText", new Dictionary<string, IAttributeValue>());
            Assert.Equal("MyAnnotationText", annotation.Description);
            Assert.Equal(0, annotation.Attributes.Count);
        }

        [Fact]
        public void Annotation_EqualsAndHashCode()
        {
            // EqualsTester tester = new EqualsTester();
            // Map<String, AttributeValue> attributes = new HashMap<String, AttributeValue>();
            // attributes.put(
            //    "MyStringAttributeKey", AttributeValue.stringAttributeValue("MyStringAttributeValue"));
            // tester
            //    .addEqualityGroup(
            //        Annotation.fromDescription("MyAnnotationText"),
            //        Annotation.fromDescriptionAndAttributes(
            //            "MyAnnotationText", Collections.< String, AttributeValue > emptyMap()))
            //    .addEqualityGroup(
            //        Annotation.fromDescriptionAndAttributes("MyAnnotationText", attributes),
            //        Annotation.fromDescriptionAndAttributes("MyAnnotationText", attributes))
            //    .addEqualityGroup(Annotation.fromDescription("MyAnnotationText2"));
            // tester.testEquals();
        }

        [Fact]
        public void Annotation_ToString()
        {
            IAnnotation annotation = Annotation.FromDescription("MyAnnotationText");
            Assert.Contains("MyAnnotationText", annotation.ToString());
            Dictionary<string, IAttributeValue> attributes = new Dictionary<string, IAttributeValue>();
            attributes.Add(
                "MyStringAttributeKey", AttributeValue<string>.Create("MyStringAttributeValue"));
            annotation = Annotation.FromDescriptionAndAttributes("MyAnnotationText2", attributes);
            Assert.Contains("MyAnnotationText2", annotation.ToString());
            Assert.Contains(Collections.ToString(attributes), annotation.ToString());
        }
    }
}
