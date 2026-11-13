using System.Runtime.CompilerServices;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Messenger.ArchitectureTests;

/// <summary>
/// Integration events travel through the outbox and MassTransit. MassTransit rejects message types without a
/// namespace; from June to November 2026 <c>MessageCreatedIntegrationEvent</c> had none, so every such event
/// ended in the dead-letter table and new messages were never indexed for search or notified.
/// </summary>
public sealed class IntegrationEventTests
{
    private static readonly Type[] ContractTypes =
        typeof(Messenger.Contracts.Users.CreateUserRequest).Assembly.GetExportedTypes();

    public static TheoryData<Type> IntegrationEventTypes()
    {
        var data = new TheoryData<Type>();

        foreach (var type in ContractTypes.Where(x =>
                     x.Name.EndsWith("IntegrationEvent", StringComparison.Ordinal) && !x.IsAbstract))
            data.Add(type);

        return data;
    }

    [Fact]
    public void Every_Contract_Type_Has_A_Namespace()
    {
        ContractTypes
            .Where(x => string.IsNullOrEmpty(x.Namespace))
            .Select(x => x.Name)
            .Should().BeEmpty("MassTransit cannot publish a message type without a namespace");
    }

    [Fact]
    public void Integration_Events_Are_Found()
    {
        ((IEnumerable<object[]>)IntegrationEventTypes()).Should().NotBeEmpty();
    }

    [Theory]
    [MemberData(nameof(IntegrationEventTypes))]
    public async Task Integration_Event_Can_Be_Published(Type eventType)
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        // Published the way OutboxPublisherWorker does it: an object plus its runtime type.
        var integrationEvent = RuntimeHelpers.GetUninitializedObject(eventType);
        await harness.Bus.Publish(integrationEvent, eventType);

        (await harness.Published.Any(x => x.MessageType == eventType)).Should().BeTrue();
    }
}
