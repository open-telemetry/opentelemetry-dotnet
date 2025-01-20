// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace;

internal sealed class TracerProviderSdk : TracerProvider
{
    internal const string TracesSamplerConfigKey = "OTEL_TRACES_SAMPLER";
    internal const string TracesSamplerArgConfigKey = "OTEL_TRACES_SAMPLER_ARG";

    internal readonly IServiceProvider ServiceProvider;
    internal readonly IDisposable? OwnedServiceProvider;
    internal int ShutdownCount;
    internal bool Disposed;

    private readonly List<object> instrumentations = new();
    private readonly ActivityListener listener;
    private readonly Sampler sampler;
    private readonly Action<Activity> getRequestedDataAction;
    private readonly bool supportLegacyActivity;
    private BaseProcessor<Activity>? processor;

    internal TracerProviderSdk(
        IServiceProvider serviceProvider,
        bool ownsServiceProvider)
    {
        Debug.Assert(serviceProvider != null, "serviceProvider was null");

        var state = serviceProvider!.GetRequiredService<TracerProviderBuilderSdk>();
        state.RegisterProvider(this);

        this.ServiceProvider = serviceProvider!;

        if (ownsServiceProvider)
        {
            this.OwnedServiceProvider = serviceProvider as IDisposable;
            Debug.Assert(this.OwnedServiceProvider != null, "serviceProvider was not IDisposable");
        }

        OpenTelemetrySdkEventSource.Log.TracerProviderSdkEvent("Building TracerProvider.");

        var configureProviderBuilders = serviceProvider!.GetServices<IConfigureTracerProviderBuilder>();
        foreach (var configureProviderBuilder in configureProviderBuilders)
        {
            configureProviderBuilder.ConfigureBuilder(serviceProvider!, state);
        }

        StringBuilder processorsAdded = new StringBuilder();
        StringBuilder instrumentationFactoriesAdded = new StringBuilder();

        var resourceBuilder = state.ResourceBuilder ?? ResourceBuilder.CreateDefault();
        resourceBuilder.ServiceProvider = serviceProvider;
        this.Resource = resourceBuilder.Build();

        this.sampler = GetSampler(serviceProvider!.GetRequiredService<IConfiguration>(), state.Sampler);
        OpenTelemetrySdkEventSource.Log.TracerProviderSdkEvent($"Sampler added = \"{this.sampler.GetType()}\".");

        this.supportLegacyActivity = state.LegacyActivityOperationNames.Count > 0;

        Regex? legacyActivityWildcardModeRegex = null;
        foreach (var legacyName in state.LegacyActivityOperationNames)
        {
            if (WildcardHelper.ContainsWildcard(legacyName))
            {
                legacyActivityWildcardModeRegex = WildcardHelper.GetWildcardRegex(state.LegacyActivityOperationNames);
                break;
            }
        }

        // Note: Linq OrderBy performs a stable sort, which is a requirement here
        IEnumerable<BaseProcessor<Activity>> processors = state.Processors.OrderBy(p => p.PipelineWeight);

        state.AddExceptionProcessorIfEnabled(ref processors);

        foreach (var processor in processors)
        {
            this.AddProcessor(processor);
            processorsAdded.Append(processor.GetType());
            processorsAdded.Append(';');
        }

        foreach (var instrumentation in state.Instrumentation)
        {
            if (instrumentation.Instance is not null)
            {
                this.instrumentations.Add(instrumentation.Instance);
            }

            instrumentationFactoriesAdded.Append(instrumentation.Name);
            instrumentationFactoriesAdded.Append(';');
        }

        if (processorsAdded.Length != 0)
        {
            processorsAdded.Remove(processorsAdded.Length - 1, 1);
            OpenTelemetrySdkEventSource.Log.TracerProviderSdkEvent($"Processors added = \"{processorsAdded}\".");
        }

        if (instrumentationFactoriesAdded.Length != 0)
        {
            instrumentationFactoriesAdded.Remove(instrumentationFactoriesAdded.Length - 1, 1);
            OpenTelemetrySdkEventSource.Log.TracerProviderSdkEvent($"Instrumentations added = \"{instrumentationFactoriesAdded}\".");
        }

        var activityListener = new ActivityListener();

        if (this.supportLegacyActivity)
        {
            Func<Activity, bool>? legacyActivityPredicate = null;
            if (legacyActivityWildcardModeRegex != null)
            {
                legacyActivityPredicate = activity => legacyActivityWildcardModeRegex.IsMatch(activity.OperationName);
            }
            else
            {
                legacyActivityPredicate = activity => state.LegacyActivityOperationNames.Contains(activity.OperationName);
            }

            activityListener.ActivityStarted = activity =>
            {
                OpenTelemetrySdkEventSource.Log.ActivityStarted(activity);

                if (string.IsNullOrEmpty(activity.Source.Name))
                {
                    if (legacyActivityPredicate(activity))
                    {
                        // Legacy activity matches the user configured list.
                        // Call sampler for the legacy activity
                        // unless suppressed.
                        if (!Sdk.SuppressInstrumentation)
                        {
                            this.getRequestedDataAction!(activity);
                        }
                        else
                        {
                            activity.IsAllDataRequested = false;
                        }
                    }
                    else
                    {
                        // Legacy activity doesn't match the user configured list. No need to proceed further.
                        return;
                    }
                }

                if (!activity.IsAllDataRequested)
                {
                    return;
                }

                if (SuppressInstrumentationScope.IncrementIfTriggered() == 0)
                {
                    this.processor?.OnStart(activity);
                }
            };

            activityListener.ActivityStopped = activity =>
            {
                OpenTelemetrySdkEventSource.Log.ActivityStopped(activity);

                if (string.IsNullOrEmpty(activity.Source.Name) && !legacyActivityPredicate(activity))
                {
                    // Legacy activity doesn't match the user configured list. No need to proceed further.
                    return;
                }

                if (!activity.IsAllDataRequested)
                {
                    return;
                }

                // Spec says IsRecording must be false once span ends.
                // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#isrecording
                // However, Activity has slightly different semantic
                // than Span and we don't have strong reason to do this
                // now, as Activity anyway allows read/write always.
                // Intentionally commenting the following line.
                // activity.IsAllDataRequested = false;

                if (SuppressInstrumentationScope.DecrementIfTriggered() == 0)
                {
                    this.processor?.OnEnd(activity);
                }
            };
        }
        else
        {
            activityListener.ActivityStarted = activity =>
            {
                OpenTelemetrySdkEventSource.Log.ActivityStarted(activity);

                if (activity.IsAllDataRequested && SuppressInstrumentationScope.IncrementIfTriggered() == 0)
                {
                    this.processor?.OnStart(activity);
                }
            };

            activityListener.ActivityStopped = activity =>
            {
                OpenTelemetrySdkEventSource.Log.ActivityStopped(activity);

                if (!activity.IsAllDataRequested)
                {
                    return;
                }

                // Spec says IsRecording must be false once span ends.
                // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#isrecording
                // However, Activity has slightly different semantic
                // than Span and we don't have strong reason to do this
                // now, as Activity anyway allows read/write always.
                // Intentionally commenting the following line.
                // activity.IsAllDataRequested = false;

                if (SuppressInstrumentationScope.DecrementIfTriggered() == 0)
                {
                    this.processor?.OnEnd(activity);
                }
            };
        }

        if (this.sampler is AlwaysOnSampler)
        {
            activityListener.Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                !Sdk.SuppressInstrumentation ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None;
            this.getRequestedDataAction = this.RunGetRequestedDataAlwaysOnSampler;
        }
        else if (this.sampler is AlwaysOffSampler)
        {
            activityListener.Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                !Sdk.SuppressInstrumentation ? PropagateOrIgnoreData(ref options) : ActivitySamplingResult.None;
            this.getRequestedDataAction = this.RunGetRequestedDataAlwaysOffSampler;
        }
        else
        {
            // This delegate informs ActivitySource about sampling decision when the parent context is an ActivityContext.
            activityListener.Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                !Sdk.SuppressInstrumentation ? ComputeActivitySamplingResult(ref options, this.sampler) : ActivitySamplingResult.None;
            this.getRequestedDataAction = this.RunGetRequestedDataOtherSampler;
        }

        // Sources can be null. This happens when user
        // is only interested in InstrumentationLibraries
        // which do not depend on ActivitySources.
        if (state.Sources.Any())
        {
            // Validation of source name is already done in builder.
            if (state.Sources.Any(s => WildcardHelper.ContainsWildcard(s)))
            {
                var regex = WildcardHelper.GetWildcardRegex(state.Sources);

                // Function which takes ActivitySource and returns true/false to indicate if it should be subscribed to
                // or not.
                activityListener.ShouldListenTo = activitySource =>
                    this.supportLegacyActivity ?
                    string.IsNullOrEmpty(activitySource.Name) || regex.IsMatch(activitySource.Name) :
                    regex.IsMatch(activitySource.Name);
            }
            else
            {
                var activitySources = new HashSet<string>(state.Sources, StringComparer.OrdinalIgnoreCase);

                if (this.supportLegacyActivity)
                {
                    activitySources.Add(string.Empty);
                }

                // Function which takes ActivitySource and returns true/false to indicate if it should be subscribed to
                // or not.
                activityListener.ShouldListenTo = activitySource => activitySources.Contains(activitySource.Name);
            }
        }
        else
        {
            if (this.supportLegacyActivity)
            {
                activityListener.ShouldListenTo = activitySource => string.IsNullOrEmpty(activitySource.Name);
            }
        }

        ActivitySource.AddActivityListener(activityListener);
        this.listener = activityListener;
        OpenTelemetrySdkEventSource.Log.TracerProviderSdkEvent("TracerProvider built successfully.");
    }

    internal Resource Resource { get; }

    internal List<object> Instrumentations => this.instrumentations;

    internal BaseProcessor<Activity>? Processor => this.processor;

    internal Sampler Sampler => this.sampler;

    internal TracerProviderSdk AddProcessor(BaseProcessor<Activity> processor)
    {
        Guard.ThrowIfNull(processor);

        processor.SetParentProvider(this);

        if (this.processor == null)
        {
            this.processor = processor;
        }
        else if (this.processor is CompositeProcessor<Activity> compositeProcessor)
        {
            compositeProcessor.AddProcessor(processor);
        }
        else
        {
            var newCompositeProcessor = new CompositeProcessor<Activity>(new[]
            {
                this.processor,
            });
            newCompositeProcessor.SetParentProvider(this);
            newCompositeProcessor.AddProcessor(processor);
            this.processor = newCompositeProcessor;
        }

        return this;
    }

    internal bool OnForceFlush(int timeoutMilliseconds)
    {
        return this.processor?.ForceFlush(timeoutMilliseconds) ?? true;
    }

    /// <summary>
    /// Called by <c>Shutdown</c>. This function should block the current
    /// thread until shutdown completed or timed out.
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <c>Timeout.Infinite</c> to wait indefinitely.
    /// </param>
    /// <returns>
    /// Returns <c>true</c> when shutdown succeeded; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This function is called synchronously on the thread which made the
    /// first call to <c>Shutdown</c>. This function should not throw
    /// exceptions.
    /// </remarks>
    internal bool OnShutdown(int timeoutMilliseconds)
    {
        // TO DO Put OnShutdown logic in a task to run within the user provider timeOutMilliseconds
        bool? result;
        if (this.instrumentations != null)
        {
            foreach (var item in this.instrumentations)
            {
                (item as IDisposable)?.Dispose();
            }

            this.instrumentations.Clear();
        }

        result = this.processor?.Shutdown(timeoutMilliseconds);
        this.listener?.Dispose();
        return result ?? true;
    }

    protected override void Dispose(bool disposing)
    {
        if (!this.Disposed)
        {
            if (disposing)
            {
                if (this.instrumentations != null)
                {
                    foreach (var item in this.instrumentations)
                    {
                        (item as IDisposable)?.Dispose();
                    }

                    this.instrumentations.Clear();
                }

                (this.sampler as IDisposable)?.Dispose();

                // Wait for up to 5 seconds grace period
                this.processor?.Shutdown(5000);
                this.processor?.Dispose();

                // Shutdown the listener last so that anything created while instrumentation cleans up will still be processed.
                // Redis instrumentation, for example, flushes during dispose which creates Activity objects for any profiling
                // sessions that were open.
                this.listener?.Dispose();

                this.OwnedServiceProvider?.Dispose();
            }

            this.Disposed = true;
            OpenTelemetrySdkEventSource.Log.ProviderDisposed(nameof(TracerProvider));
        }

        base.Dispose(disposing);
    }

    private static Sampler GetSampler(IConfiguration configuration, Sampler? stateSampler)
    {
        var sampler = stateSampler;

        if (configuration.TryGetStringValue(TracesSamplerConfigKey, out var configValue))
        {
            if (sampler != null)
            {
                OpenTelemetrySdkEventSource.Log.TracerProviderSdkEvent(
                    $"Trace sampler configuration value '{configValue}' has been ignored because a value '{sampler.GetType().FullName}' was set programmatically.");
                return sampler;
            }

            switch (configValue)
            {
                case var _ when string.Equals(configValue, "always_on", StringComparison.OrdinalIgnoreCase):
                    sampler = new AlwaysOnSampler();
                    break;
                case var _ when string.Equals(configValue, "always_off", StringComparison.OrdinalIgnoreCase):
                    sampler = new AlwaysOffSampler();
                    break;
                case var _ when string.Equals(configValue, "traceidratio", StringComparison.OrdinalIgnoreCase):
                    {
                        var traceIdRatio = ReadTraceIdRatio(configuration);
                        sampler = new TraceIdRatioBasedSampler(traceIdRatio);
                        break;
                    }

                case var _ when string.Equals(configValue, "parentbased_always_on", StringComparison.OrdinalIgnoreCase):
                    sampler = new ParentBasedSampler(new AlwaysOnSampler());
                    break;
                case var _ when string.Equals(configValue, "parentbased_always_off", StringComparison.OrdinalIgnoreCase):
                    sampler = new ParentBasedSampler(new AlwaysOffSampler());
                    break;
                case var _ when string.Equals(configValue, "parentbased_traceidratio", StringComparison.OrdinalIgnoreCase):
                    {
                        var traceIdRatio = ReadTraceIdRatio(configuration);
                        sampler = new ParentBasedSampler(new TraceIdRatioBasedSampler(traceIdRatio));
                        break;
                    }

                default:
                    OpenTelemetrySdkEventSource.Log.TracesSamplerConfigInvalid(configValue);
                    break;
            }

            if (sampler != null)
            {
                OpenTelemetrySdkEventSource.Log.TracerProviderSdkEvent($"Trace sampler set to '{sampler.GetType().FullName}' from configuration.");
            }
        }

        return sampler ?? new ParentBasedSampler(new AlwaysOnSampler());
    }

    private static double ReadTraceIdRatio(IConfiguration configuration)
    {
        if (configuration.TryGetStringValue(TracesSamplerArgConfigKey, out var configValue) &&
                double.TryParse(configValue, out var traceIdRatio))
        {
            return traceIdRatio;
        }
        else
        {
            OpenTelemetrySdkEventSource.Log.TracesSamplerArgConfigInvalid(configValue ?? string.Empty);
        }

        return 1.0;
    }

    private static ActivitySamplingResult ComputeActivitySamplingResult(
        ref ActivityCreationOptions<ActivityContext> options,
        Sampler sampler)
    {
        var samplingParameters = new SamplingParameters(
            options.Parent,
            options.TraceId,
            options.Name,
            options.Kind,
            options.Tags,
            options.Links);

        var samplingResult = sampler.ShouldSample(samplingParameters);

        var activitySamplingResult = samplingResult.Decision switch
        {
            SamplingDecision.RecordAndSample => ActivitySamplingResult.AllDataAndRecorded,
            SamplingDecision.RecordOnly => ActivitySamplingResult.AllData,
            _ => PropagateOrIgnoreData(ref options),
        };

        if (activitySamplingResult > ActivitySamplingResult.PropagationData)
        {
            foreach (var att in samplingResult.Attributes)
            {
                options.SamplingTags.Add(att.Key, att.Value);
            }
        }

        if (activitySamplingResult != ActivitySamplingResult.None
            && samplingResult.TraceStateString != null)
        {
            // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#sampler
            // Spec requires clearing Tracestate if empty Tracestate is returned.
            // Since .NET did not have this capability, it'll break
            // existing samplers if we did that. So the following is
            // adopted to remain spec-compliant and backward compat.
            // The behavior is:
            // if sampler returns null, its treated as if it has not intended
            // to change Tracestate. Existing SamplingResult ctors will put null as default TraceStateString,
            // so all existing samplers will get this behavior.
            // if sampler returns non-null, then it'll be used as the
            // new value for Tracestate
            // A sampler can return string.Empty if it intends to clear the state.
            options = options with { TraceState = samplingResult.TraceStateString };
        }

        return activitySamplingResult;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ActivitySamplingResult PropagateOrIgnoreData(ref ActivityCreationOptions<ActivityContext> options)
    {
        var isRootSpan = options.Parent.TraceId == default;

        // If it is the root span or the parent is remote select PropagationData so the trace ID is preserved
        // even if no activity of the trace is recorded (sampled per OpenTelemetry parlance).
        return (isRootSpan || options.Parent.IsRemote)
            ? ActivitySamplingResult.PropagationData
            : ActivitySamplingResult.None;
    }

    private void RunGetRequestedDataAlwaysOnSampler(Activity activity)
    {
        activity.IsAllDataRequested = true;
        activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
    }

    private void RunGetRequestedDataAlwaysOffSampler(Activity activity)
    {
        activity.IsAllDataRequested = false;
        activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
    }

    private void RunGetRequestedDataOtherSampler(Activity activity)
    {
        ActivityContext parentContext;

        // Check activity.ParentId alone is sufficient to normally determine if a activity is root or not. But if one uses activity.SetParentId to override the TraceId (without intending to set an actual parent), then additional check of parentspanid being empty is required to confirm if an activity is root or not.
        // This checker can be removed, once Activity exposes an API to customize ID Generation (https://github.com/dotnet/runtime/issues/46704) or issue https://github.com/dotnet/runtime/issues/46706 is addressed.
        if (string.IsNullOrEmpty(activity.ParentId) || activity.ParentSpanId.ToHexString() == "0000000000000000")
        {
            parentContext = default;
        }
        else if (activity.Parent != null)
        {
            parentContext = activity.Parent.Context;
        }
        else
        {
            parentContext = new ActivityContext(
                activity.TraceId,
                activity.ParentSpanId,
                activity.ActivityTraceFlags,
                activity.TraceStateString,
                isRemote: true);
        }

        var samplingParameters = new SamplingParameters(
            parentContext,
            activity.TraceId,
            activity.DisplayName,
            activity.Kind,
            activity.TagObjects,
            activity.Links);

        var samplingResult = this.sampler.ShouldSample(samplingParameters);

        switch (samplingResult.Decision)
        {
            case SamplingDecision.Drop:
                activity.IsAllDataRequested = false;
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                break;
            case SamplingDecision.RecordOnly:
                activity.IsAllDataRequested = true;
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                break;
            case SamplingDecision.RecordAndSample:
                activity.IsAllDataRequested = true;
                activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
                break;
        }

        if (samplingResult.Decision != SamplingDecision.Drop)
        {
            foreach (var att in samplingResult.Attributes)
            {
                activity.SetTag(att.Key, att.Value);
            }
        }

        if (samplingResult.TraceStateString != null)
        {
            activity.TraceStateString = samplingResult.TraceStateString;
        }
    }
}
