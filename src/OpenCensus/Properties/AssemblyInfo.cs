// <copyright file="AssemblyInfo.cs" company="OpenCensus Authors">
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

using System.Runtime.CompilerServices;

[assembly: System.CLSCompliant(true)]

[assembly: InternalsVisibleTo("OpenCensus.Tests" + AssemblyInfo.PublicKey)]
[assembly: InternalsVisibleTo("OpenCensus.Collector.Dependencies.Tests" + AssemblyInfo.PublicKey)]
[assembly: InternalsVisibleTo("OpenCensus.Collector.AspNetCore.Tests" + AssemblyInfo.PublicKey)]
[assembly: InternalsVisibleTo("OpenCensus.Collector.StackExchangeRedis.Tests" + AssemblyInfo.PublicKey)]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2" + AssemblyInfo.MoqPublicKey)]

#if SIGNED
internal static class AssemblyInfo
{
    public const string PublicKey = ", PublicKey=002400000480000094000000060200000024000052534131000400000100010051C1562A090FB0C9F391012A32198B5E5D9A60E9B80FA2D7B434C9E5CCB7259BD606E66F9660676AFC6692B8CDC6793D190904551D2103B7B22FA636DCBB8208839785BA402EA08FC00C8F1500CCEF28BBF599AA64FFB1E1D5DC1BF3420A3777BADFE697856E9D52070A50C3EA5821C80BEF17CA3ACFFA28F89DD413F096F898";
    public const string MoqPublicKey = ", PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7";
}
#else
internal static class AssemblyInfo
{
    public const string PublicKey = "";
    public const string MoqPublicKey = "";
}
#endif