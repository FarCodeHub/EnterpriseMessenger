using Messenger.Domain.Common;

namespace Messenger.Domain.Users;

public class User : BaseEntity
{
    public string DisplayName { get; private set; }

    public string UserName { get; private set; }

    public bool IsActive { get; private set; } = true;
    public bool IsAdmin { get; private set; }

    /// <summary>
    /// The identity provider's subject id (Keycloak "sub") once the user has signed in through it.
    /// </summary>
    public string? ExternalSubject { get; private set; }



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

public void Deactivate()
{
    IsActive = false;
}

public void Activate()
{
    IsActive = true;
}

    public void LinkExternalSubject(string externalSubject)
    {
        if (string.IsNullOrWhiteSpace(externalSubject))
            throw new ArgumentException("External subject is required.", nameof(externalSubject));

        ExternalSubject = externalSubject;
    }

    public void UpdateDisplayName(string displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            DisplayName = displayName;
    }
    
}