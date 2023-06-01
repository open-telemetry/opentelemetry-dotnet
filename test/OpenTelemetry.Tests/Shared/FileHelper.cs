// <copyright file="FileHelper.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Tests.Shared;

/// <summary>
/// This class contains methods to help with common file I/O tasks.
/// </summary>
internal static class FileHelper
{
    /// <summary>
    /// Checks if a directory exists and then attempts to delete that directory.
    /// By default, this method will throw an exception if the delete fails.
    /// </summary>
    /// <param name="path">Path of directory to be deleted.</param>
    /// <param name="fail">Indicates if this method should throw exceptions.</param>
    public static void DeleteDirectory(string path, bool fail = true)
    {
        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch
            {
                if (fail)
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Checks if a file exists and then attempts to delete that file.
    /// By default, this method will throw an exception if the delete fails.
    /// </summary>
    /// <param name="path">Path of file to be deleted.</param>
    /// <param name="fail">Indicates if this method should throw exceptions.</param>
    public static void DeleteFile(string path, bool fail = true)
    {
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                if (fail)
                {
                    throw;
                }
            }
        }
    }
}
