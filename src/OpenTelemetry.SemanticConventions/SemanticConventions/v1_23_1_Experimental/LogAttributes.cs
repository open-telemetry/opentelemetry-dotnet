// <copyright file="LogAttributes.cs" company="OpenTelemetry Authors">
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

// <auto-generated> This file has been auto generated from buildscripts/semantic-conventions/templates/Attributes.cs.j2</auto-generated>

using System;

namespace OpenTelemetry.SemanticConventions.v1_23_1_Experimental
{
    /// <summary>
    /// Describes semantic conventions for attributes in the <c>log</c> namespace.
    /// </summary>
    public static class LogAttributes
    {
        /// <summary>
        /// The basename of the file.
        /// </summary>
        public const string LogFileName = "log.file.name";

        /// <summary>
        /// The basename of the file, with symlinks resolved.
        /// </summary>
        public const string LogFileNameResolved = "log.file.name_resolved";

        /// <summary>
        /// The full path to the file.
        /// </summary>
        public const string LogFilePath = "log.file.path";

        /// <summary>
        /// The full path to the file, with symlinks resolved.
        /// </summary>
        public const string LogFilePathResolved = "log.file.path_resolved";

        /// <summary>
        /// The stream associated with the log. See below for a list of well-known values.
        /// </summary>
        public const string LogIostream = "log.iostream";

        /// <summary>
        /// A unique identifier for the Log Record.
        /// </summary>
        /// <remarks>
        /// If an id is provided, other log records with the same id will be considered duplicates and can be removed safely. This means, that two distinguishable log records MUST have different values.
        /// The id MAY be an <a href="https://github.com/ulid/spec">Universally Unique Lexicographically Sortable Identifier (ULID)</a>, but other identifiers (e.g. UUID) may be used as needed.
        /// </remarks>
        public const string LogRecordUid = "log.record.uid";

        /// <summary>
        /// The stream associated with the log. See below for a list of well-known values.
        /// </summary>
        public static class LogIostreamValues
        {
            /// <summary>
            /// Logs from stdout stream.
            /// </summary>
            public const string Stdout = "stdout";
            /// <summary>
            /// Events from stderr stream.
            /// </summary>
            public const string Stderr = "stderr";
        }
    }
}