// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Configuration.Models;

/// <summary>
/// Represents the result of a step in dynamic client registration validation or
/// processing.
/// </summary>
public interface IStepResult
{
}

/// <summary>
/// Static helper class for creating instances of IStepResult implementations,
/// wrapped in tasks.
/// </summary>
public static class StepResult
{
    /// <summary>
    /// Creates a step result that represents failure, wrapped in a task.
    /// </summary>
    /// <param name="errorDescription"></param>
    /// <param name="error"></param>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    public static Task<IStepResult> Failure(string errorDescription,
        string error = DynamicClientRegistrationErrors.InvalidClientMetadata) =>
            Task.FromResult<IStepResult>(new DynamicClientRegistrationError(error, errorDescription));

    /// <summary>
    /// Creates a step result that represents success, wrapped in a task.
    /// </summary>
    /// <returns>A task that returns an <see cref="IStepResult"/>, which either
    /// represents that this step succeeded or failed.</returns>
    public static Task<IStepResult> Success() =>
        Task.FromResult<IStepResult>(new SuccessfulStep());
}
