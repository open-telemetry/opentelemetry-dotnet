// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;

namespace OpenTelemetry.Context.Tests;

[Collection(RuntimeContextCollectionDefinition.Name)]
public sealed class RuntimeContextTests : IDisposable
{
    public RuntimeContextTests()
    {
        RuntimeContext.Clear();
    }

    [Fact]
    public static void RegisterSlotWithInvalidNameThrows()
    {
        Assert.Throws<ArgumentException>(() => RuntimeContext.RegisterSlot<bool>(string.Empty));
        Assert.ThrowsAny<ArgumentException>(() => RuntimeContext.RegisterSlot<bool>(null!));
    }

    [Fact]
    public static void RegisterSlotWithSameName()
    {
        var slot = RuntimeContext.RegisterSlot<bool>("testslot");
        Assert.NotNull(slot);
        Assert.Throws<InvalidOperationException>(() => RuntimeContext.RegisterSlot<bool>("testslot"));
    }

    [Fact]
    public static void GetSlotWithInvalidNameThrows()
    {
        Assert.Throws<ArgumentException>(() => RuntimeContext.GetSlot<bool>(string.Empty));
        Assert.ThrowsAny<ArgumentException>(() => RuntimeContext.GetSlot<bool>(null!));
    }

    [Fact]
    public void GetSlotReturnsNullForNonExistingSlot()
    {
        Assert.Throws<ArgumentException>(() => RuntimeContext.GetSlot<bool>("testslot"));
    }

    [Fact]
    public void GetSlotReturnsNullWhenTypeNotMatchingExistingSlot()
    {
        RuntimeContext.RegisterSlot<bool>("testslot");
        Assert.Throws<InvalidCastException>(() => RuntimeContext.GetSlot<int>("testslot"));
    }

    [Fact]
    public void RegisterAndGetSlot()
    {
        var expectedSlot = RuntimeContext.RegisterSlot<int>("testslot");
        Assert.NotNull(expectedSlot);
        expectedSlot.Set(100);
        var actualSlot = RuntimeContext.GetSlot<int>("testslot");
        Assert.Same(expectedSlot, actualSlot);
        Assert.Equal(100, expectedSlot.Get());
    }

    [Fact]
    public void ValueTypeSlotNullableTests()
    {
        var expectedSlot = RuntimeContext.RegisterSlot<int>("testslot_valuetype");
        Assert.NotNull(expectedSlot);

        var slotValueAccessor = expectedSlot as IRuntimeContextSlotValueAccessor;
        Assert.NotNull(slotValueAccessor);

        Assert.Equal(0, expectedSlot.Get());
        Assert.Equal(0, slotValueAccessor.Value);

        slotValueAccessor.Value = 100;

        Assert.Equal(100, expectedSlot.Get());
        Assert.Equal(100, slotValueAccessor.Value);

        slotValueAccessor.Value = null;

        Assert.Equal(0, expectedSlot.Get());
        Assert.Equal(0, slotValueAccessor.Value);

        Assert.Throws<InvalidCastException>(() => slotValueAccessor.Value = false);
    }

    [Fact]
    public void NullableValueTypeSlotNullableTests()
    {
        var expectedSlot = RuntimeContext.RegisterSlot<int?>("testslot_nullablevaluetype");
        Assert.NotNull(expectedSlot);

        var slotValueAccessor = expectedSlot as IRuntimeContextSlotValueAccessor;
        Assert.NotNull(slotValueAccessor);

        Assert.Null(expectedSlot.Get());
        Assert.Null(slotValueAccessor.Value);

        slotValueAccessor.Value = 100;

        Assert.Equal(100, expectedSlot.Get());
        Assert.Equal(100, slotValueAccessor.Value);

        slotValueAccessor.Value = null;

        Assert.Null(expectedSlot.Get());
        Assert.Null(slotValueAccessor.Value);

        Assert.Throws<InvalidCastException>(() => slotValueAccessor.Value = false);
    }

    [Fact]
    public void ReferenceTypeSlotNullableTests()
    {
        var expectedSlot = RuntimeContext.RegisterSlot<RuntimeContextTests>("testslot_referencetype");
        Assert.NotNull(expectedSlot);

        var slotValueAccessor = expectedSlot as IRuntimeContextSlotValueAccessor;
        Assert.NotNull(slotValueAccessor);

        Assert.Null(expectedSlot.Get());
        Assert.Null(slotValueAccessor.Value);

        slotValueAccessor.Value = this;

        Assert.Equal(this, expectedSlot.Get());
        Assert.Equal(this, slotValueAccessor.Value);

        slotValueAccessor.Value = null;

        Assert.Null(expectedSlot.Get());
        Assert.Null(slotValueAccessor.Value);

        Assert.Throws<InvalidCastException>(() => slotValueAccessor.Value = new object());
    }

    [Fact]
    public void ContextSlotTypeDefaultsToAsyncLocal()
    {
        Assert.Equal(typeof(AsyncLocalRuntimeContextSlot<>), RuntimeContext.ContextSlotType);
    }

    [Fact]
    public void ContextSlotTypeRejectsInvalidValues()
    {
        Assert.Throws<ArgumentNullException>(() => RuntimeContext.ContextSlotType = null!);
        Assert.Throws<NotSupportedException>(() => RuntimeContext.ContextSlotType = typeof(List<>));
        Assert.Equal(typeof(AsyncLocalRuntimeContextSlot<>), RuntimeContext.ContextSlotType);
    }

    [Fact]
    public void ThreadLocalSlotRoundTripsValues()
    {
        RunWithContextSlotType(
            typeof(ThreadLocalRuntimeContextSlot<>),
            () =>
            {
                var slot = RuntimeContext.RegisterSlot<int>("threadlocal_valuetype");
                Assert.IsType<ThreadLocalRuntimeContextSlot<int>>(slot);
                Assert.Equal("threadlocal_valuetype", slot.Name);

                var accessor = Assert.IsType<IRuntimeContextSlotValueAccessor>(slot, exactMatch: false);

                Assert.Equal(0, slot.Get());

                slot.Set(100);
                Assert.Equal(100, slot.Get());
                Assert.Equal(100, accessor.Value);

                accessor.Value = 200;
                Assert.Equal(200, slot.Get());

                accessor.Value = null;
                Assert.Equal(0, slot.Get());
                Assert.Equal(0, accessor.Value);

                Assert.Throws<InvalidCastException>(() => accessor.Value = false);

                // The value is local to the thread that set it.
                slot.Set(300);
                var otherThreadValue = -1;
                var thread = new Thread(() => otherThreadValue = slot.Get());
                thread.Start();
                thread.Join();
                Assert.Equal(0, otherThreadValue);
                Assert.Equal(300, slot.Get());

                // Disposing more than once is safe.
                slot.Dispose();
                slot.Dispose();
            });
    }

    [Fact]
    public void ThreadLocalSlotRoundTripsReferenceValues()
    {
        RunWithContextSlotType(
            typeof(ThreadLocalRuntimeContextSlot<>),
            () =>
            {
                var slot = RuntimeContext.RegisterSlot<RuntimeContextTests>("threadlocal_referencetype");
                var accessor = Assert.IsType<IRuntimeContextSlotValueAccessor>(slot, exactMatch: false);

                Assert.Null(slot.Get());
                Assert.Null(accessor.Value);

                accessor.Value = this;
                Assert.Same(this, slot.Get());

                accessor.Value = null;
                Assert.Null(slot.Get());

                Assert.Throws<InvalidCastException>(() => accessor.Value = new object());
            });
    }

    [Fact]
    public void ValueAccessorsByNameGetAndSetSlotValues()
    {
        RuntimeContext.RegisterSlot<int>("named_slot");

        Assert.Equal(0, RuntimeContext.GetValue<int>("named_slot"));
        Assert.Equal(0, RuntimeContext.GetValue("named_slot"));

        RuntimeContext.SetValue("named_slot", 5);
        Assert.Equal(5, RuntimeContext.GetValue<int>("named_slot"));
        Assert.Equal(5, RuntimeContext.GetValue("named_slot"));

        RuntimeContext.SetValue<int>("named_slot", 6);
        Assert.Equal(6, RuntimeContext.GetValue<int>("named_slot"));

        RuntimeContext.SetValue("named_slot", (object?)7);
        Assert.Equal(7, RuntimeContext.GetValue("named_slot"));
    }

    [Fact]
    public void ValueAccessorsByNameValidateTheSlotName()
    {
        Assert.Throws<ArgumentException>(() => RuntimeContext.GetValue("missing"));
        Assert.Throws<ArgumentException>(() => RuntimeContext.SetValue("missing", 1));
        Assert.Throws<ArgumentException>(() => RuntimeContext.GetValue<int>("missing"));
        Assert.Throws<ArgumentException>(() => RuntimeContext.SetValue<int>("missing", 1));

        Assert.Throws<ArgumentException>(() => RuntimeContext.GetValue(string.Empty));
        Assert.Throws<ArgumentException>(() => RuntimeContext.SetValue(string.Empty, 1));
        Assert.ThrowsAny<ArgumentException>(() => RuntimeContext.GetValue(null!));
        Assert.ThrowsAny<ArgumentException>(() => RuntimeContext.SetValue(null!, 1));
    }

#if NETFRAMEWORK
    [Fact]
    public void RemotingSlotRoundTripsValues()
    {
        RunWithContextSlotType(
            typeof(RemotingRuntimeContextSlot<>),
            () =>
            {
                var valueSlot = RuntimeContext.RegisterSlot<int>("remoting_valuetype");
                Assert.IsType<RemotingRuntimeContextSlot<int>>(valueSlot);

                var valueAccessor = Assert.IsType<IRuntimeContextSlotValueAccessor>(valueSlot, exactMatch: false);

                Assert.Equal(0, valueSlot.Get());

                valueSlot.Set(100);
                Assert.Equal(100, valueSlot.Get());
                Assert.Equal(100, valueAccessor.Value);

                valueAccessor.Value = null;
                Assert.Equal(0, valueSlot.Get());

                Assert.Throws<InvalidCastException>(() => valueAccessor.Value = false);

                var referenceSlot = RuntimeContext.RegisterSlot<RuntimeContextTests>("remoting_referencetype");
                var referenceAccessor = Assert.IsType<IRuntimeContextSlotValueAccessor>(referenceSlot, exactMatch: false);

                Assert.Null(referenceSlot.Get());

                referenceAccessor.Value = this;
                Assert.Same(this, referenceSlot.Get());

                referenceAccessor.Value = null;
                Assert.Null(referenceSlot.Get());
            });
    }
#endif

#if NETFRAMEWORK
    [Fact]
    public void NetFrameworkGetSlotInAnotherAppDomain()
    {
        const string slotName = "testSlot";
        var slot = RuntimeContext.RegisterSlot<int>(slotName);
        Assert.NotNull(slot);
        slot.Set(100);

        // Create an object in another AppDomain and try to access the slot
        // value from it.

        var domainSetup = AppDomain.CurrentDomain.SetupInformation;
        var ad = AppDomain.CreateDomain("other-domain", null, domainSetup);

        var remoteObjectTypeName = typeof(RemoteObject).FullName;
        Assert.NotNull(remoteObjectTypeName);

        var obj = (RemoteObject)ad.CreateInstanceAndUnwrap(
            typeof(RemoteObject).Assembly.FullName,
            remoteObjectTypeName);

        // Value we previously put into the slot ("100") would not be propagated
        // across AppDomains. We should get default(int) = 0 back.
        Assert.Equal(0, obj.GetValueFromContextSlot(slotName));
    }
#endif

    public void Dispose()
    {
        RuntimeContext.Clear();
        GC.SuppressFinalize(this);
    }

    private static void RunWithContextSlotType(Type slotType, Action action)
    {
        // Baggage registers its slot in a static initializer, so make sure that has
        // happened with the default slot type before the slot type is changed.
        _ = Baggage.Current;

        var original = RuntimeContext.ContextSlotType;
        try
        {
            RuntimeContext.ContextSlotType = slotType;
            action();
        }
        finally
        {
            RuntimeContext.ContextSlotType = original;
        }
    }

#if NETFRAMEWORK
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    private sealed class RemoteObject : ContextBoundObject
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
    {
#pragma warning disable CA1822 // Mark members as static
        public int GetValueFromContextSlot(string slotName)
#pragma warning restore CA1822 // Mark members as static
        {
            // Slot is not propagated across AppDomains, attempting to get
            // an existing slot here should throw an ArgumentException.
            try
            {
                RuntimeContext.GetSlot<int>(slotName);

                throw new InvalidOperationException("Should not have found an existing slot: " + slotName);
            }
            catch (ArgumentException)
            {
                // This is ok.
            }

            // Now re-register this slot.
            RuntimeContext.RegisterSlot<int>(slotName);

            var slot = RuntimeContext.GetSlot<int>(slotName);

            // We didn't put any value into this slot, so default(int) should be returned.
            // Previously an exception was thrown at this point.
            return slot.Get();
        }
    }
#endif

}
