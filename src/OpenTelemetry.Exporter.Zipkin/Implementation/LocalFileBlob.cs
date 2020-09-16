// <copyright file="LocalFileBlob.cs" company="OpenTelemetry Authors">
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

using System;
using System.IO;

namespace OpenTelemetry.Exporter.Zipkin.Implementation
{
    internal class LocalFileBlob
    {
        internal LocalFileBlob(string fullPath)
        {
            this.FullPath = fullPath;
        }

        internal string FullPath { get; private set; }

        internal string[] Read()
        {
            try
            {
                return File.ReadAllLines(this.FullPath);
            }
            catch (Exception)
            {
                // TODO: Log Exception
            }

            return null;
        }

        internal void Write(string[] data, int leasePeriod = 0)
        {
            string path = this.FullPath + ".tmp";

            try
            {
                using (StreamWriter sw = new StreamWriter(path))
                {
                    foreach (string line in data)
                    {
                        sw.WriteLine(line);
                    }
                }

                if (leasePeriod > 0)
                {
                    var timestamp = DateTime.Now.ToUniversalTime() + TimeSpan.FromSeconds(leasePeriod);
                    this.FullPath += $@"{timestamp:yyy-MM-ddTHHmmss.ffffff}.lock";
                }

                File.Move(path, this.FullPath);
            }
            catch (Exception)
            {
                // TODO: Log Exception
            }
        }

        internal void Lease(int seconds)
        {
            var path = this.FullPath;
            var leaseTimestamp = DateTime.Now.ToUniversalTime() + TimeSpan.FromSeconds(seconds);
            if (path.EndsWith(".lock"))
            {
                path = path.Substring(0, path.LastIndexOf('@'));
            }

            path += $"@{leaseTimestamp:yyy-MM-ddTHHmmss.ffffff}.lock";

            try
            {
                File.Move(this.FullPath, path);
            }
            catch (Exception)
            {
                // TODO: Log Exception
            }

            this.FullPath = path;
        }

        internal void Delete()
        {
            try
            {
                if (File.Exists(this.FullPath))
                {
                    File.Delete(this.FullPath);
                }
            }
            catch (Exception)
            {
                // Log Exception
            }
        }
    }
}
