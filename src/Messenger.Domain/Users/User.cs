using Messenger.Domain.Common;

namespace Messenger.Domain.Users;

public class User : BaseEntity
{
    public string DisplayName { get; private set; }

    public string UserName { get; private set; }

    public bool IsActive { get; private set; } = true;
    public bool IsAdmin { get; private set; }



    private User()
    {
    }

    public User(string displayName, string userName)
    {
        DisplayName = displayName;
        UserName = userName;
    }

    public void MakeAdmin()
{
    IsAdmin = true;
}
    
}