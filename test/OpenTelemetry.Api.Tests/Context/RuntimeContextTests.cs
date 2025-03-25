// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Context.Tests;

public class RuntimeContextTests : IDisposable
{
    public RuntimeContextTests()
    {
        RuntimeContext.Clear();
    }

    [Fact]
    public static void RegisterSlotWithInvalidNameThrows()
    {
        Assert.Throws<ArgumentException>(() => RuntimeContext.RegisterSlot<bool>(string.Empty));
        Assert.Throws<ArgumentException>(() => RuntimeContext.RegisterSlot<bool>(null!));
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
        Assert.Throws<ArgumentException>(() => RuntimeContext.GetSlot<bool>(null!));
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

#if NETFRAMEWORK
    private class RemoteObject : ContextBoundObject
    {
        public int GetValueFromContextSlot(string slotName)
        {
            // Slot is not propagated across AppDomains, attempting to get
            // an existing slot here should throw an ArgumentException.
            try
            {
                RuntimeContext.GetSlot<int>(slotName);

                throw new Exception("Should not have found an existing slot: " + slotName);
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
