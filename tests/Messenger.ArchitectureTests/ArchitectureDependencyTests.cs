using FluentAssertions;
using NetArchTest.Rules;

namespace Messenger.ArchitectureTests;

public sealed class ArchitectureDependencyTests
{
    private const string DomainNamespace = "Messenger.Domain";
    private const string ApplicationNamespace = "Messenger.Application";
    private const string InfrastructureNamespace = "Messenger.Infrastructure";
    private const string ApiNamespace = "Messenger.Api";
    private const string ContractsNamespace = "Messenger.Contracts";

    [Fact]
    public void Domain_Should_Not_Depend_On_Other_Projects()
    {
        var result = Types
            .InAssembly(typeof(Messenger.Domain.Common.BaseEntity).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                ApplicationNamespace,
                InfrastructureNamespace,
                ApiNamespace,
                ContractsNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain must be pure. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_Api()
    {
        var result = Types
            .InAssembly(typeof(Messenger.Application.Messages.SendMessage.IMessageSender).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                InfrastructureNamespace,
                ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Application must not depend on Infrastructure or Api. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Api()
    {
        var result = Types
            .InAssembly(typeof(Messenger.Infrastructure.Persistence.MessengerDbContext).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Infrastructure must not depend on Api. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Contracts_Should_Not_Depend_On_Other_Projects()
    {
        var result = Types
            .InAssembly(typeof(Messenger.Contracts.Users.CreateUserRequest).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                DomainNamespace,
                ApplicationNamespace,
                InfrastructureNamespace,
                ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Contracts must stay dependency-free. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}