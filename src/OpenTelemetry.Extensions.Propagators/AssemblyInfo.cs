// <copyright file="AssemblyInfo.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;

#if SIGNED
[assembly: InternalsVisibleTo("OpenTelemetry.Extensions.Propagators.Tests, PublicKey=002400000480000094000000060200000024000052534131000400000100010051C1562A090FB0C9F391012A32198B5E5D9A60E9B80FA2D7B434C9E5CCB7259BD606E66F9660676AFC6692B8CDC6793D190904551D2103B7B22FA636DCBB8208839785BA402EA08FC00C8F1500CCEF28BBF599AA64FFB1E1D5DC1BF3420A3777BADFE697856E9D52070A50C3EA5821C80BEF17CA3ACFFA28F89DD413F096F898")]
#else
[assembly: InternalsVisibleTo("OpenTelemetry.Extensions.Propagators.Tests")]
#endif
