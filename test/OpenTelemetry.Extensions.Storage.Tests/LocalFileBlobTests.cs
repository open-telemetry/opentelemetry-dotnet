// <copyright file="LocalFileBlobTests.cs" company="OpenTelemetry Authors">
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

using System.IO;
using System.Text;
using Xunit;

namespace OpenTelemetry.Extensions.Storage.Tests
{
    public class LocalFileBlobTests
    {
        [Fact]
        public void LocalFileBlobTests_E2E_Test()
        {
            var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            IPersistentBlob blob = new LocalFileBlob(testFile.FullName);

            var data = Encoding.UTF8.GetBytes("Hello, World!");
            IPersistentBlob blob1 = blob.Write(data);
            var blobContent = blob.Read();

            Assert.Equal(testFile.FullName, ((LocalFileBlob)blob1).FullPath);
            Assert.Equal(data, blobContent);

            blob1.Delete();
            Assert.False(testFile.Exists);
        }

        [Fact]
        public void LocalFileBlobTests_Lease()
        {
            var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            IPersistentBlob blob = new LocalFileBlob(testFile.FullName);

            var data = Encoding.UTF8.GetBytes("Hello, World!");
            IPersistentBlob blob1 = blob.Write(data);
            IPersistentBlob leasedBlob = blob1.Lease(1000);

            Assert.Contains(".lock", ((LocalFileBlob)leasedBlob).FullPath);

            blob1.Delete();
            Assert.False(testFile.Exists);
        }
    }
}
