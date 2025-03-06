// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Events;

/// <summary>
/// Event for successful user logout
/// </summary>
/// <seealso cref="Event" />
public class UserLogoutSuccessEvent : Event
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserLogoutSuccessEvent"/> class.
    /// </summary>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="name">The name.</param>
    public UserLogoutSuccessEvent(string subjectId, string name)
        : base(EventCategories.Authentication,
            "User Logout Success",
            EventTypes.Success,
            EventIds.UserLogoutSuccess)
    {
        SubjectId = subjectId;
        DisplayName = name;
    }

    /// <summary>
    /// Gets or sets the subject identifier.
    /// </summary>
    /// <value>
    /// The subject identifier.
    /// </value>
    public string SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    /// <value>
    /// The display name.
    /// </value>
    public string DisplayName { get; set; }
}