// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0.

public class GitHubContexts
{
    public static  GitHubContexts Instance { get; } = new();
    public virtual GitHubContext  GitHub   { get; } = new();
    public virtual SecretsContext Secrets  { get; } = new();
    public virtual EventContext   Event    { get; } = new();

    public abstract class Context(string name)
    {
        protected string Name => name;

        protected string Expression(string s) => "${{ " + s + " }}";
    }

    public class GitHubContext() : Context("github")
    {
    }

    public class SecretsContext() : Context("secrets")
    {
        public string GitHubToken => Expression($"{Name}.GITHUB_TOKEN");
    }

    public class EventContext() : Context("github.event")
    {
        public EventsInputContext Input { get; } = new();
    }

    public class EventsInputContext() : Context("github.event.inputs")
    {
        public string Version => Expression($"{Name}.version");
    }
}
