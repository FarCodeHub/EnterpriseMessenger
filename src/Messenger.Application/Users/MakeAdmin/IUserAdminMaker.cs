namespace Messenger.Application.Users.MakeAdmin;

public interface IUserAdminMaker
{
    Task<MakeUserAdminResult> MakeAdminAsync(
        MakeUserAdminCommand command,
        CancellationToken cancellationToken);
}