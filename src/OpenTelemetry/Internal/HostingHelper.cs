// <copyright file="HostingHelper.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Internal;

internal static class HostingHelper
{
    private static readonly object LockObject = new();
    private static bool initialized;
    private static Type? hostedServiceImplementation;

    public static void AddOpenTelemetryHostedServiceIntoServiceCollection(IServiceCollection services)
    {
        if (TryAddOpenTelemetryHostedServiceIntoServiceCollection(services, out var reason))
        {
            OpenTelemetrySdkEventSource.Log.HostedServiceRegistered();
        }
        else
        {
            OpenTelemetrySdkEventSource.Log.HostedServiceRegistrationSkipped(reason);
        }
    }

    private static bool TryAddOpenTelemetryHostedServiceIntoServiceCollection(IServiceCollection services, out string? reason)
    {
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
        bool isDynamicCodeSupported = RuntimeFeature.IsDynamicCodeSupported;
#else
        // Note: This is for .NET Framework and/or .NET Standard 2.0 targets.
        bool isDynamicCodeSupported = true;
#endif
        if (!isDynamicCodeSupported)
        {
            reason = "Dynamic code not supported";
            return false;
        }

        var iHostedServiceType = Type.GetType(
            "Microsoft.Extensions.Hosting.IHostedService, Microsoft.Extensions.Hosting.Abstractions", throwOnError: false);

        if (iHostedServiceType == null)
        {
            reason = "Microsoft.Extensions.Hosting.IHostedService not found";
            return false;
        }

        lock (LockObject)
        {
            if (!initialized)
            {
                try
                {
                    hostedServiceImplementation = CreateHostedServiceImplementation(iHostedServiceType);
                }
                catch (Exception ex)
                {
                    OpenTelemetrySdkEventSource.Log.HostedServiceRegistrationFailure(ex);
                }
                finally
                {
                    initialized = true;
                }
            }
        }

        if (hostedServiceImplementation == null)
        {
            reason = "Initialization failure";
            return false;
        }

        services.TryAddSingleton<TelemetryHostedServiceHelper>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton(iHostedServiceType, hostedServiceImplementation));

        reason = null;
        return true;
    }

    private static Type CreateHostedServiceImplementation(Type iHostedServiceType)
    {
        /*
         * Note: This code builds a class dynamically that does this...
         *
         * namespace OpenTelemetry.Extensions.Hosting.Implementation;
         *
         * class TelemetryHostedService : IHostedService
         * {
         *   private readonly TelemetryHostedServiceHelper telemetryHostedServiceHelper;
         *
         *   public TelemetryHostedService(TelemetryHostedServiceHelper telemetryHostedServiceHelper)
         *   {
         *      this.telemetryHostedServiceHelper = telemetryHostedServiceHelper;
         *   }
         *
         *   public Task StartAsync(CancellationToken cancellationToken)
         *   {
         *      this.telemetryHostedServiceHelper.Start();
         *      return Task.CompletedTask;
         *   }
         *
         *   public Task StopAsync(CancellationToken cancellationToken)
         *   {
         *      return Task.CompletedTask;
         *   }
         * }
         */

        var getCompletedTaskMethod = typeof(Task).GetProperty(nameof(Task.CompletedTask), BindingFlags.Static | BindingFlags.Public)?.GetMethod
            ?? throw new InvalidOperationException("Task.CompletedTask could not be found reflectively.");

        // Note: It is important that the assembly is named
        // OpenTelemetry.Extensions.Hosting and the type is named
        // OpenTelemetry.Extensions.Hosting.Implementation.TelemetryHostedService
        // to preserve compatibility with Azure Functions:
        // https://github.com/Azure/azure-functions-host/blob/d4655cc4fbb34fc14e6861731991118a9acd02ed/src/WebJobs.Script.WebHost/DependencyInjection/DependencyValidator/DependencyValidator.cs#L57.
        var assemblyName = new AssemblyName("OpenTelemetry.Extensions.Hosting");

        assemblyName.SetPublicKey(typeof(HostingHelper).Assembly.GetName().GetPublicKey());

        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

        // Note: We use IgnoresAccessChecksToAttribute to allow the dynamic
        // assembly to call TelemetryHostedService which is internal to
        // OpenTelemetry.dll.
        var ignoresAccessChecksTo = new CustomAttributeBuilder(
            typeof(IgnoresAccessChecksToAttribute).GetConstructor(new Type[] { typeof(string) }) ?? throw new InvalidOperationException("IgnoresAccessChecksToAttribute constructor could not be found reflectively."),
            new object[] { typeof(TelemetryHostedServiceHelper).Assembly.GetName().Name! });

        assemblyBuilder.SetCustomAttribute(ignoresAccessChecksTo);

        var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name!);

        var typeBuilder = moduleBuilder.DefineType("OpenTelemetry.Extensions.Hosting.Implementation.TelemetryHostedService", TypeAttributes.NotPublic);

        typeBuilder.AddInterfaceImplementation(iHostedServiceType);

        var hostedServiceImplementationField = typeBuilder.DefineField(
            "telemetryHostedServiceHelper",
            typeof(TelemetryHostedServiceHelper),
            FieldAttributes.Private | FieldAttributes.InitOnly);

        var constructor = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            new Type[] { typeof(TelemetryHostedServiceHelper) });

        var constructorGenerator = constructor.GetILGenerator();

        constructorGenerator.Emit(OpCodes.Ldarg_0);
        constructorGenerator.Emit(
            OpCodes.Call,
            typeof(object).GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException("Object constructor could not be found reflectively."));
        constructorGenerator.Emit(OpCodes.Ldarg_0);
        constructorGenerator.Emit(OpCodes.Ldarg_1);
        constructorGenerator.Emit(OpCodes.Stfld, hostedServiceImplementationField);
        constructorGenerator.Emit(OpCodes.Ret);

        var startAsyncMethodBuilder = typeBuilder.DefineMethod(
            "StartAsync",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(Task),
            new Type[] { typeof(CancellationToken) });

        var startAsyncMethodGenerator = startAsyncMethodBuilder.GetILGenerator();

        startAsyncMethodGenerator.Emit(OpCodes.Ldarg_0);
        startAsyncMethodGenerator.Emit(OpCodes.Ldfld, hostedServiceImplementationField);
        startAsyncMethodGenerator.Emit(
            OpCodes.Call,
            typeof(TelemetryHostedServiceHelper).GetMethod(nameof(TelemetryHostedServiceHelper.Start)) ?? throw new InvalidOperationException($"{nameof(TelemetryHostedServiceHelper)}.{nameof(TelemetryHostedServiceHelper.Start)} could not be found reflectively."));
        startAsyncMethodGenerator.Emit(OpCodes.Call, getCompletedTaskMethod);
        startAsyncMethodGenerator.Emit(OpCodes.Ret);

        typeBuilder.DefineMethodOverride(
            startAsyncMethodBuilder,
            iHostedServiceType.GetMethod("StartAsync") ?? throw new InvalidOperationException("IHostedService.StartAsync could not be found reflectively."));

        var stopAsyncMethodBuilder = typeBuilder.DefineMethod(
            "StopAsync",
            MethodAttributes.Public | MethodAttributes.Virtual,
            typeof(Task),
            new Type[] { typeof(CancellationToken) });

        var stopAsyncMethodGenerator = stopAsyncMethodBuilder.GetILGenerator();

        stopAsyncMethodGenerator.Emit(OpCodes.Call, getCompletedTaskMethod);
        stopAsyncMethodGenerator.Emit(OpCodes.Ret);

        typeBuilder.DefineMethodOverride(
            stopAsyncMethodBuilder,
            iHostedServiceType.GetMethod("StopAsync") ?? throw new InvalidOperationException("IHostedService.StopAsync could not be found reflectively."));

#if !NETSTANDARD2_0
        return typeBuilder.CreateType()
#else
        return typeBuilder.CreateTypeInfo()
#endif
             ?? throw new InvalidOperationException("IHostedService implementation could not be created dynamically.");
    }

    private sealed class TelemetryHostedServiceHelper
    {
        private readonly IServiceProvider serviceProvider;

        public TelemetryHostedServiceHelper(IServiceProvider serviceProvider)
        {
            Debug.Assert(serviceProvider != null, "serviceProvider was null");

            this.serviceProvider = serviceProvider!;
        }

        public void Start()
        {
            var serviceProvider = this.serviceProvider;

            var meterProvider = serviceProvider.GetService<MeterProvider>();
            if (meterProvider == null)
            {
                OpenTelemetrySdkEventSource.Log.MeterProviderNotRegistered();
            }

            var tracerProvider = serviceProvider.GetService<TracerProvider>();
            if (tracerProvider == null)
            {
                OpenTelemetrySdkEventSource.Log.TracerProviderNotRegistered();
            }
        }
    }
}
